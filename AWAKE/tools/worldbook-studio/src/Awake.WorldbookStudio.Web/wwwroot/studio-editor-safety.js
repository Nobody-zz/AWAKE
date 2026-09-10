"use strict";
(function(root){
  const state=root.awakeStudioState;
  if(!state||!root.AwakeEditorSession||!root.AwakeLocalDrafts)return;
  const $=id=>document.getElementById(id);
  const draftStore=new root.AwakeLocalDrafts.LocalDraftStore();
  let restoredDraftKey="";
  let openRestorePromise=null;
  let compileProof=null;

  function clone(value){return value==null?value:JSON.parse(JSON.stringify(value))}
  function authorDirty(){return Boolean(state.authorModel&&state.session.savedBaseline)&&!root.AwakeEditorSession.projectionsEqual(state.authorModel,state.session.savedBaseline.editorProjection)}
  function advancedDirty(){return state.advancedContent!==state.savedAdvanced}
  function currentDirty(){return state.mode==="advanced"?advancedDirty():authorDirty()}
  function baseline(){
    if(state.mode==="advanced"&&state.savedAdvancedBaseline)return state.savedAdvancedBaseline;
    return state.session.savedBaseline;
  }
  function version(){
    const current=baseline();
    if(!state.currentPath||!current||typeof current.sourceHash!=="string"||!Number.isInteger(Number(current.revision)))return null;
    return {path:state.currentPath,sourceHash:current.sourceHash,revision:Number(current.revision)};
  }
  function draftContext(){
    const current=state.session.savedBaseline||state.savedAdvancedBaseline||{};
    return {workspaceHash:state.workspaceHash||"local",documentPath:state.currentPath,kind:"editor",sourceHash:current.sourceHash,revision:Number(current.revision)};
  }
  function draftPayload(){
    return {mode:state.mode,authorModel:clone(state.authorModel),advancedContent:state.advancedContent};
  }
  function scheduleDraft(){
    if(!state.currentPath||!currentDirty())return;
    const result=draftStore.schedule(draftContext(),draftPayload(),state.session.editGeneration,450);
    if(result.status==="too-large")toast("临时草稿超过大小限制，请先保存正式档案。","warn");
  }
  function flushDraft(){
    if(!state.currentPath||!currentDirty())return;
    draftStore.flush(draftContext(),draftPayload(),state.session.editGeneration);
  }
  function clearDraft(savedGeneration){
    if(!state.currentPath)return;
    draftStore.clearAfterSave(draftContext(),savedGeneration,state.session.editGeneration);
  }
  function editorBaseline(editorDocument){
    const revision=Number(editorDocument?.revision);
    if(!editorDocument?.path||!editorDocument?.model||typeof editorDocument?.sourceHash!=="string"||!Number.isInteger(revision))return null;
    return {editorProjection:root.AwakeEditorSession.normalizeAuthorProjection(editorDocument.model),sourceHash:editorDocument.sourceHash,revision};
  }
  function editorReadbackValid(editorDocument,readback){
    return Boolean(editorBaseline(editorDocument)&&root.AwakeEditorSession.readbackMatchesEditorDocument(editorDocument,readback));
  }
  function rawResponse(result){
    const fields=root.AwakeEditorSession.rawReadbackFields(result);
    return {path:fields.path,content:fields.content,sourceHash:fields.sourceHash,revision:fields.revision,expectedContentHash:result?.expectedContentHash??result?.ExpectedContentHash??fields.expectedContentHash,document:result?.document??result?.Document,report:result?.report??result?.Report};
  }
  function rawReadbackValid(operation,readback){
    const fields=root.AwakeEditorSession.rawReadbackFields(readback);
    return fields.path===operation.context.documentKey&&typeof fields.content==="string"&&fields.sourceHash&&Number.isInteger(fields.revision)&&fields.revision>operation.context.capturedRevision&&fields.sourceHash!==String(operation.context.capturedSourceHash).toLowerCase()&&(!fields.expectedContentHash||fields.sourceHash===fields.expectedContentHash);
  }
  function rawStatusUi(){
    const status=state.session.rawSaveStatus;
    const statusNode=$("dirtyStatus");
    const detailNode=$("saveStatusMessage");
    const reloadNode=$("reloadFromDiskButton");
    const checkNode=$("checkSaveButton");
    if(status==="idle"){
      if(typeof root.renderSaveStatus==="function"){root.renderSaveStatus();return}
      if(reloadNode)reloadNode.hidden=true;
      if(checkNode)checkNode.hidden=true;
      return;
    }
    const message=state.session.rawStatusMessage(currentDirty());
    statusNode.textContent=status==="saving"?"正在保存原始文件…":status==="pending-confirmation"?"原始文件结果待确认":status==="failed"?"原始文件保存失败":"原始文件发生冲突";
    statusNode.className="save-"+status.replace("-","");
    detailNode.textContent=message;
    detailNode.className="save-status-message save-"+status.replace("-","");
    reloadNode.hidden=status!=="conflict";
    checkNode.hidden=status!=="saving"&&status!=="pending-confirmation";
    checkNode.textContent=state.session.rawCheckLabel();
    checkNode.disabled=status==="pending-confirmation"&&state.session.rawActiveOperation?.kind==="raw-save-check";
  }
  function updateBaselineFromRaw(editorDocument,readback){
    const fields=root.AwakeEditorSession.rawReadbackFields(readback);
    if(fields.path&&typeof fields.content==="string"&&fields.sourceHash&&Number.isInteger(fields.revision))state.savedAdvancedBaseline={sourceHash:fields.sourceHash,revision:fields.revision,content:fields.content};
    const editor=editorBaseline(editorDocument);
    if(editor){state.snapshot=editorDocument;state.authorModel=clone(editorDocument.model);state.session.savedBaseline=editor}
    return fields;
  }
  function applyRawSave(editorDocument,readback,commit){
    const fields=updateBaselineFromRaw(editorDocument,readback);
    invalidateCompileProof();
    if(!commit.keepCurrentContent)state.advancedContent=fields.content;
    state.savedAdvanced=fields.content;
    if(editorDocument)setDiagnostics(editorDocument.diagnostics||[]);
    clearDraft(commit.capturedGeneration??state.session.rawSaveContext?.capturedGeneration??state.session.editGeneration);
    renderAll();
    rawStatusUi();
    toast(commit.keepCurrentContent?"已保存刚才版本；高级编辑器还有新修改。":"原始世界书文件已保存，版本信息已刷新。","good");
    action(commit.keepCurrentContent?"已保存，仍有新修改":"高级文件保存完成");
  }
  function errorForUnknown(){const error=new Error("原始文件保存结果暂时无法确认，当前修改仍保留。请点击“检查原始文件保存结果”。");error.code="WB-AUTHORING-UNKNOWN-503";error.status=503;return error}
  async function saveAdvancedSafe(){
    if(!state.currentPath){toast("还没有打开档案。","warn");return false}
    if(!advancedDirty()){toast("高级文件没有需要保存的修改。","good");return true}
    const current=version();
    if(!current){toast("当前档案缺少可用的版本信息，请重新读取档案。","warn");return false}
    if(state.session.mode!=="advanced")state.session.mode="advanced";
    const savedGeneration=state.session.editGeneration;
    const operation=state.session.beginRawSave(state.currentPath,state.advancedContent,current,{path:state.currentPath,content:state.advancedContent,sourceHash:current.sourceHash,revision:current.revision});
    if(!operation){toast(state.session.rawStatusMessage(true),"warn");rawStatusUi();return false}
    state.busy=true;rawStatusUi();action("正在解析、校验并保存原始文件…");
    try{
      const result=await requestJson("/api/authoring/save-authoring",{method:"POST",body:JSON.stringify(operation.context.request),signal:operation.controller?.signal});
      if(!state.session.isCurrentRawSave(operation))return false;
      const readback=rawResponse(result);
      if(!rawReadbackValid(operation,readback))throw errorForUnknown();
      const editorDocument=result.editorDocument||result.editor_document||(await requestJson("/api/editor-document?path="+encodeURIComponent(operation.context.documentKey),{signal:operation.controller?.signal}));
      if(!editorReadbackValid(editorDocument,readback))throw errorForUnknown();
      const commit=state.session.commitRawSave(operation,readback);
      if(!commit)throw errorForUnknown();
      commit.capturedGeneration=savedGeneration;
      state.busy=false;
      applyRawSave(editorDocument,readback,commit);
      return true;
    }catch(error){
      const status=state.session.handleRawSaveFailure(operation,error);
      if(status===null)return false;
      state.busy=false;
      rawStatusUi();
      if(status==="pending-confirmation"){action("原始文件保存结果待确认");toast("保存结果暂时无法确认。当前修改仍保留，请点击“检查原始文件保存结果”。","warn");}
      else if(status==="conflict"){action("原始文件保存冲突，需要重新读取");toast("文件已经发生变化，程序没有覆盖它。请重新读取磁盘版本。","error");}
      else handleError(error);
      return false;
    }finally{
      if(state.session.rawActiveOperation===operation&&state.session.rawSaveStatus==="saving"){state.busy=false;state.session.rawSaveStatus="pending-confirmation";rawStatusUi()}
    }
  }
  async function checkRawSaveResult(){
    const operation=state.session.beginRawPendingCheck();
    if(!operation){toast("当前没有需要检查的原始文件保存结果，或检查正在进行中。","good");rawStatusUi();return false}
    state.busy=true;rawStatusUi();action("正在检查原始文件保存结果…");
    try{
      const response=await requestJson("/api/authoring/save-authoring/check",{method:"POST",body:JSON.stringify(operation.context.request),signal:operation.controller?.signal});
      if(!state.session.isCurrentRawCheck(operation))return false;
      if(response.status==="pending"){
        state.session.finishRawPendingCheck(operation);state.busy=false;rawStatusUi();toast("暂时无法确认保存结果，请稍后再次检查。","warn");return false;
      }
      let readback=rawResponse(response);
      if(typeof readback.content!=="string"){
        const fallback=rawResponse(await requestJson("/api/document?path="+encodeURIComponent(operation.context.documentKey),{signal:operation.controller?.signal}));
        readback={...fallback,expectedContentHash:readback.expectedContentHash};
      }
      if(!readback||typeof root.AwakeEditorSession.rawReadbackFields(readback).content!=="string")throw errorForUnknown();
      let editorDocument=null;
      if(response.status==="confirmed"){
        editorDocument=response.editorDocument||response.editor_document||(await requestJson("/api/editor-document?path="+encodeURIComponent(operation.context.documentKey),{signal:operation.controller?.signal}));
        if(!editorReadbackValid(editorDocument,readback))throw errorForUnknown();
      }
      const decision=state.session.resolveRawPending(operation,readback);
      if(decision.status==="pending"){state.busy=false;rawStatusUi();toast("暂时无法确认保存结果，请稍后再次检查。","warn");return false}
      if(decision.status==="not-submitted"){state.busy=false;rawStatusUi();action("确认原始文件未写入磁盘");toast("已确认这次保存没有写入磁盘。当前修改仍保留，可以再次保存。","warn");return false}
      if(decision.status==="conflict"){state.busy=false;rawStatusUi();action("原始文件结果与本次保存不一致");toast("磁盘版本与本次原始文件保存无法对应，请重新读取磁盘版本。","error");return false}
      state.busy=false;
      applyRawSave(editorDocument,readback,{...decision,capturedGeneration:operation.context.capturedGeneration});
      return true;
    }catch(error){
      if(!state.session.isCurrentRawCheck(operation))return false;
      state.session.finishRawPendingCheck(operation);state.busy=false;rawStatusUi();action("原始文件保存结果仍待确认");toast("检查保存结果时遇到问题。当前修改仍保留，请稍后再次检查。","warn");return false;
    }
  }
  async function saveCurrent(){
    if(state.mode==="advanced")return saveAdvancedSafe();
    const savedGeneration=state.session.editGeneration;
    await root.saveAuthor();
    const success=state.session.saveStatus==="idle"&&!authorDirty();
    if(success){
      const current=state.session.savedBaseline;
      if(current)state.savedAdvancedBaseline={sourceHash:current.sourceHash,revision:Number(current.revision),content:state.savedAdvanced};
      clearDraft(savedGeneration);
      invalidateCompileProof();
    }
    return success;
  }
  async function ensureSaved(label){
    if(state.session.isLocked()){toast(state.session.statusMessage(currentDirty()),"warn");return false}
    if(!currentDirty())return true;
    if(!confirm(`执行“${label}”前需要先保存当前修改。现在保存吗？`))return false;
    const saved=await saveCurrent();
    if(!saved||currentDirty()||state.session.saveStatus!=="idle"||state.session.rawSaveStatus!=="idle"){
      toast("当前修改尚未安全保存，已取消后续操作。","warn");return false;
    }
    return true;
  }
  let refreshPromise=null;
  async function refreshSafe(){
    if(refreshPromise){toast("正在刷新工作室，请稍候。","warn");return refreshPromise}
    refreshPromise=(async()=>{
      if(!await ensureSaved("刷新工作室"))return false;
      if(typeof root.bootstrap!=="function"){toast("刷新模块尚未加载，请稍后重试。","error");return false}
      invalidateCompileProof();
      await root.bootstrap();
      return true;
    })().finally(()=>{refreshPromise=null});
    return refreshPromise;
  }
  function operationVersion(){
    const current=state.session.savedBaseline||state.savedAdvancedBaseline;
    if(!state.currentPath||!current||typeof current.sourceHash!=="string"||!Number.isInteger(Number(current.revision))){toast("当前档案缺少保存版本信息，请重新读取档案。","warn");return null}
    return {path:state.currentPath,sourceHash:current.sourceHash,revision:Number(current.revision)};
  }
  function proofValue(value){
    if(typeof value==="string"){const id=value.trim();return id?{id}:null}
    if(!value||typeof value!=="object")return null;
    const id=value.compile_proof_id??value.compileProofId??value.proof_id??value.id;
    if(typeof id!=="string"||!id.trim())return null;
    const revision=value.revision??value.document_revision??value.documentRevision;
    return {id:id.trim(),path:value.path??value.document_path??value.documentPath,sourceHash:value.source_hash??value.content_hash??value.sourceHash??value.contentHash,revision:revision==null?null:Number(revision)};
  }
  function activeCompileProof(){
    const proof=proofValue(compileProof||state.compileProof||state.compileProofId);
    const current=baseline();
    if(!proof||currentDirty()||!state.currentPath||!current)return null;
    if(proof.path&&proof.path!==state.currentPath)return null;
    if(proof.sourceHash&&typeof current.sourceHash==="string"&&proof.sourceHash.toLowerCase()!==current.sourceHash.toLowerCase())return null;
    if(proof.revision!=null&&Number.isInteger(proof.revision)&&proof.revision!==Number(current.revision))return null;
    return proof;
  }
  function updateCompileControls(){
    const enabled=Boolean(activeCompileProof())&&!state.busy;
    for(const id of ["compileButton","exportButton"]){const button=$(id);if(button)button.disabled=!enabled}
  }
  function invalidateCompileProof(){
    compileProof=null;
    state.compileProof=null;
    state.compileProofId="";
    state.compileProofState="stale";
    updateCompileControls();
  }
  function setCompileProof(value){
    const proof=proofValue(value);
    if(!proof){invalidateCompileProof();return false}
    compileProof=proof;
    state.compileProof=proof;
    state.compileProofId=proof.id;
    state.compileProofState="ready";
    updateCompileControls();
    return true;
  }
  function requiredCompileProof(){
    const proof=activeCompileProof();
    if(!proof){updateCompileControls();toast("当前没有与已保存档案匹配的有效 CompileProof，请先完成显式批准。","warn");return null}
    return proof.id;
  }
  function shouldInvalidateProof(error){
    return error?.status===404||error?.status===409||String(error?.code||"").includes("PROOF")||String(error?.code||"").includes("CAS");
  }
  async function authorizeCompileSafe(){
    if(!await ensureSaved("申请编译授权"))return false;
    if(!state.currentPath||!state.authorModel){toast("请先打开一份已保存档案。","warn");return false}
    const documentId=state.authorModel.documentId||state.snapshot?.documentId;
    if(!documentId){toast("当前档案缺少稳定编号，请重新读取档案。","error");return false}
    state.busy=true;updateCompileControls();action("正在建立显式编译授权…");
    try{
      const suffix=()=>Date.now().toString(36)+"."+Math.random().toString(36).slice(2,8);
      const registered=await requestJson("/api/ai/authoring/documents/register",{method:"POST",body:JSON.stringify({operationId:"customer.register."+suffix(),path:state.currentPath})});
      state.compileProofState="registered";
      const selected=await requestJson("/api/ai/authoring/selections",{method:"POST",body:JSON.stringify({operationId:"customer.selection."+suffix(),documentIds:[registered.document_revision.documentId||documentId]})});
      state.compileProofState="selected";
      const approved=await requestJson("/api/ai/authoring/selections/"+encodeURIComponent(selected.selection.selectionId)+"/approve",{method:"POST",body:JSON.stringify({operationId:"customer.approval."+suffix()})});
      state.compileProofState="approved";
      const tier=state.authorModel.contentTier||"base";
      const issued=await requestJson("/api/ai/authoring/compile-proof",{method:"POST",body:JSON.stringify({operationId:"customer.proof."+suffix(),approvalId:approved.approval_proof.approvalId,contentTier:tier})});
      const proof=issued.compile_proof||issued.compileProof||issued;
      if(!setCompileProof({...proof,path:state.currentPath,sourceHash:state.snapshot?.sourceHash,revision:Number(state.snapshot?.revision),approvalId:approved.approval_proof.approvalId,selectionId:selected.selection.selectionId,contentTier:tier}))throw new Error("CompileProof 返回内容无效");
      toast("显式编译授权已签发，现在可以编译或导出。","good");action("CompileProof 已就绪");return true;
    }catch(error){invalidateCompileProof();handleError(error);return false}finally{state.busy=false;updateCompileControls()}
  }
  async function validateSafe(){
    if(!await ensureSaved("工作区检查"))return;
    const current=operationVersion();if(!current)return;
    try{const tier=state.authorModel?.contentTier||"base";let token="";if(tier==="adult_optional"){const data=await requestJson(`/api/confirmation-token?contentTier=${tier}`);token=data.confirmationToken}const report=await requestJson(`/api/validate?path=${encodeURIComponent(current.path)}&sourceHash=${encodeURIComponent(current.sourceHash)}&revision=${current.revision}`,{method:"POST",body:JSON.stringify({path:current.path,sourceHash:current.sourceHash,revision:current.revision,contentTier:tier,confirmationToken:token})});setDiagnostics(report.diagnostics||[]);toast(report.valid?"整个工作区检查通过。":"工作区存在需要处理的问题。",report.valid?"good":"warn")}catch(error){handleError(error)}
  }
  async function previewSafe(){
    if(!await ensureSaved("身份预览"))return;
    const current=operationVersion();if(!current)return;
    try{const data=await requestJson(`/api/preview?profile=${encodeURIComponent(state.profile)}&fixture=studio-preview&path=${encodeURIComponent(current.path)}&sourceHash=${encodeURIComponent(current.sourceHash)}&revision=${current.revision}`);const results=data.npc_preview?.results||data.results||[];$("previewOutput").innerHTML=results.length?results.map(item=>`<div class="preview-item"><strong>${h(item.status||"知识结果")}</strong><p>${h(item.text||item.message||"没有可显示的内容")}</p></div>`).join(""):"<div class=\"muted\">这个身份当前没有可预览的知识结果。</div>";toast("已按当前身份生成预览。","good")}catch(error){handleError(error)}
  }
  async function compileSafe(){
    if(!await ensureSaved("编译运行包"))return;
    const compileProofId=requiredCompileProof();if(!compileProofId)return false;
    state.busy=true;updateCompileControls();
    try{const tier=state.compileProof?.contentTier||state.authorModel?.contentTier||"base";let token="";if(tier==="adult_optional"){const data=await requestJson("/api/confirmation-token?contentTier="+encodeURIComponent(tier));token=data.confirmationToken}const body={compile_proof_id:compileProofId};if(token)body.confirmation_token=token;const result=await requestJson("/api/authoring/compile",{method:"POST",body:JSON.stringify(body)});setDiagnostics(result.validation?.diagnostics||[]);toast("运行包编译完成。","good");return true}catch(error){if(shouldInvalidateProof(error))invalidateCompileProof();handleError(error);return false}finally{state.busy=false;updateCompileControls()}
  }
  async function exportSafe(){
    if(!await ensureSaved("导出候选包"))return;
    const compileProofId=requiredCompileProof();if(!compileProofId)return false;
    state.busy=true;updateCompileControls();
    try{const tier=state.compileProof?.contentTier||state.authorModel?.contentTier||"base";let token="";if(tier==="adult_optional"){const data=await requestJson("/api/confirmation-token?contentTier="+encodeURIComponent(tier));token=data.confirmationToken}const body={compile_proof_id:compileProofId};if(token)body.confirmation_token=token;const result=await requestJson("/api/authoring/export-staging",{method:"POST",body:JSON.stringify(body)});toast("候选包已导出到 Studio 工作区。","good");action(result.staging||"导出完成");return true}catch(error){if(shouldInvalidateProof(error))invalidateCompileProof();handleError(error);return false}finally{state.busy=false;updateCompileControls()}
  }
  async function restoreDraft(){
    if(openRestorePromise)return openRestorePromise;
    if(!state.currentPath)return;
    const context=draftContext();const key=draftStore.key(context);if(restoredDraftKey===key)return;
    openRestorePromise=(async()=>{
      const loaded=draftStore.load(context,baseline());
      if(!["available","stale"].includes(loaded.status)){restoredDraftKey=key;return}
      const stale=loaded.status==="stale";
      const question=stale?"发现一份基于旧磁盘版本的临时草稿。恢复它不会自动覆盖磁盘，之后仍需正常保存。要恢复吗？":"发现上次未保存的临时草稿。要恢复吗？";
      if(!confirm(question)){draftStore.discard(context);restoredDraftKey=key;return}
      const payload=loaded.payload||{};
      if(payload.authorModel)state.authorModel=clone(payload.authorModel);
      if(typeof payload.advancedContent==="string")state.advancedContent=payload.advancedContent;
      const mode=payload.mode==="advanced"?"advanced":"author";
      state.mode=mode;state.session.startDocument(state.currentPath,mode,state.session.savedBaseline||baseline());state.session.markEdit();renderAll();rawStatusUi();restoredDraftKey=key;toast(stale?"已恢复旧版本临时草稿，请保存前检查冲突。":"已恢复临时草稿，请继续编辑并保存。","warn");
    })().finally(()=>{openRestorePromise=null});
    return openRestorePromise;
  }
  function afterOpen(){
    invalidateCompileProof();
    const current=state.session.savedBaseline;
    if(current&&typeof current.sourceHash==="string"&&Number.isInteger(Number(current.revision)))state.savedAdvancedBaseline={sourceHash:current.sourceHash,revision:Number(current.revision),content:state.advancedContent};
    rawStatusUi();
    return restoreDraft();
  }
  function installOpenWrapper(){
    if(typeof root.openDocument!=="function")return;
    const originalOpen=root.openDocument;
    root.openDocument=async function(path,force=false){const previousBaseline=state.session.savedBaseline;const result=await originalOpen(path,force);const opened=state.session.documentKey===path&&state.session.savedBaseline!==previousBaseline&&!state.session.loading;if(opened)await afterOpen();return result};
  }
  function installClickGuards(){
    const guarded={
      saveButton:()=>saveCurrent(),
      stepSave:()=>state.mode==="author"?saveCurrent():null,
      advancedSave:()=>saveAdvancedSafe(),
      authorizeCompileButton:()=>authorizeCompileSafe(),
      checkSaveButton:()=>state.session.rawSaveStatus!=="idle"?checkRawSaveResult():root.checkSaveResult(),
      reloadFromDiskButton:()=>state.session.rawSaveStatus==="conflict"?root.openDocument(state.currentPath,true):root.reloadFromDisk(),
      refreshButton:()=>refreshSafe(),
      validateButton:()=>validateSafe(),
      previewButton:()=>previewSafe(),
      compileButton:()=>compileSafe(),
      exportButton:()=>exportSafe()
    };
    document.addEventListener("click",event=>{
      const button=event.target.closest?.("button[id]");
      const operation=button&&guarded[button.id]||(button?.dataset.action==="save-author"?()=>saveCurrent():null);
      if(!operation)return;
      event.preventDefault();event.stopImmediatePropagation();Promise.resolve(operation()).catch(error=>handleError(error));
    },true);
  }
  function installInputDrafts(){
    document.addEventListener("input",event=>{if(event.target.closest?.("#authorView,#advancedEditor")){invalidateCompileProof();if(event.target.closest?.("#advancedEditor"))state.session.markEdit();scheduleDraft();rawStatusUi()}});
    document.addEventListener("change",event=>{if(event.target.closest?.("#authorView,#advancedEditor")){invalidateCompileProof();scheduleDraft();rawStatusUi()}});
    document.addEventListener("click",()=>{setTimeout(()=>{if(currentDirty())scheduleDraft()},0)});
    root.addEventListener("beforeunload",flushDraft);
  }
  installOpenWrapper();
  installClickGuards();
  installInputDrafts();
  root.awakeSetCompileProof=setCompileProof;
  root.awakeClearCompileProof=invalidateCompileProof;
  root.awakeSaveAdvancedSafe=saveAdvancedSafe;
  root.awakeCheckRawSaveResult=checkRawSaveResult;
  root.awakeEnsureSaved=ensureSaved;
  root.awakeRefreshSafe=refreshSafe;
  root.awakeValidateSafe=validateSafe;
  root.awakePreviewSafe=previewSafe;
  root.awakeCompileSafe=compileSafe;
  root.awakeExportSafe=exportSafe;
  root.awakeAuthorizeCompileSafe=authorizeCompileSafe;
  updateCompileControls();
  const restoreTimer=setInterval(()=>{if(state.currentPath){clearInterval(restoreTimer);afterOpen()}},150);
})(typeof window!=="undefined"?window:globalThis);
