"use strict";

const focusAnalysis={
  general:"consistency",
  completion:"consistency",
  fact_correction:"consistency",
  knowledge_permissions:"permissions",
  hierarchy:"permissions",
  expression_level:"permissions",
  metadata:"metadata",
  world_style:"prose",
  npc_voice:"prose",
  domain_style:"prose"
};
const analysisDefaultFocus={
  consistency:"general",
  permissions:"knowledge_permissions",
  metadata:"metadata",
  prose:"world_style"
};
const focusHelp={
  general:"同时检查格式、内容、知识权限和表达；适合第一次审阅档案。",
  completion:"寻找标题、摘要、事实、表达或权限中的明显空缺，并给出可继续编辑的候选内容。",
  fact_correction:"检查档案内部矛盾、时期冲突、绝对化表述和不稳妥的事实口径；不会替你决定正典。",
  knowledge_permissions:"检查身份授权、禁止项和推荐询问对象，避免把贵族秘密分配给普通平民。",
  hierarchy:"按普通平民、头人、商人、贵族、士兵等身份检查知识范围是否越级。",
  expression_level:"检查摘要、详细表达和秘密表达是否与身份允许的最低详细度一致。",
  metadata:"补充标题、知识分类、时代、确定程度、摘要和状态等作者可见字段。",
  world_style:"检查是否保持客观、中立、详实的世界知识档案口径，减少现代解释和无依据渲染。",
  npc_voice:"根据身份、阶层、地域和知识程度，提出 NPC 更自然、更符合身份的说法。",
  domain_style:"按政治、经济、文化、战争或地理领域检查术语、二级主题、叙述重点和事实表达方式。"
};
const suggestionKindLabels={
  completion:"内容补全",
  correction:"内容纠偏",
  consistency:"一致性",
  permissions:"知识权限",
  hierarchy:"阶层身份",
  expression_level:"表达层级",
  metadata:"档案字段",
  prose:"表达与文风",
  npc_voice:"NPC 语气",
  world_style:"世界观文风",
  domain_style:"领域表达"
};
const severityLabels={error:"需要优先处理",warning:"建议复核",info:"可选优化"};
function friendlyPatchPath(path){
  const value=String(path||"");
  if(value==="/title/zh-CN")return"档案标题";
  if(value==="/summary/zh-CN")return"档案摘要";
  if(value==="/domain")return"知识分类";
  if(value==="/era"||value.startsWith("/era/"))return"适用时期";
  if(value==="/certainty")return"确定程度";
  if(value==="/status")return"内容状态";
  if(/^\/assertions\/\d+\/expressions\/\d+\/text/.test(value))return"NPC 表达";
  if(/^\/assertions\/\d+\/expressions\/\d+\/(grants|denies)/.test(value))return"知识权限";
  if(/^\/assertions\/\d+\/text/.test(value))return"客观事实";
  if(/^\/assertions\/\d+\//.test(value))return"客观事实设置";
  return"档案内容";
}
const originalRequestJson=requestJson;
const originalRenderAi=renderAi;
state.aiFocus=state.aiFocus||"general";
state.aiLocalFindings=state.aiLocalFindings||[];
state.providerSettings=null;
state.aiStaleNotice="";
state.aiContextPath="";

function renderAiStaleNotice(){
  const anchor=$("aiSuggestions");
  if(!anchor?.parentElement)return;
  let node=$("aiStaleNotice");
  if(!node){
    node=document.createElement("div");
    node.id="aiStaleNotice";
    node.className="diag warning";
    anchor.parentElement.insertBefore(node,anchor);
  }
  const visible=Boolean(state.aiStaleNotice&&state.aiContextPath===state.currentPath);
  node.hidden=!visible;
  node.textContent=visible?state.aiStaleNotice:"";
}

function invalidateAiAfterEditorEdit(){
  if(!state.aiConsentToken&&!state.aiBufferId&&!state.aiSuggestions.length)return;
  state.aiConsentToken="";
  state.aiBufferId="";
  state.aiScope=null;
  state.aiSuggestions=[];
  state.aiLocalFindings=[];
  state.aiStaleNotice="你修改了档案，之前的 AI 建议已清除。请保存后重新获取建议。";
  Promise.resolve().then(()=>renderAi());
}

document.addEventListener("input",event=>{
  if(event.target.closest?.("#authorView,#advancedEditor"))invalidateAiAfterEditorEdit();
},true);
document.addEventListener("change",event=>{
  if(event.target.closest?.("#authorView"))invalidateAiAfterEditorEdit();
},true);

requestJson=async function(url,init={}){
  const options=Object.assign({},init);
  const headers=Object.assign({"Content-Type":"application/json"},init.headers||{});
  if(state.aiCsrf)headers["X-AWAKE-CSRF"]=state.aiCsrf;
  options.headers=headers;
  if(url.startsWith("/api/ai/consent-preview")&&typeof init.body==="string"){
    state.aiStaleNotice="";
    state.aiContextPath=state.currentPath||"";
    try{
      const body=JSON.parse(init.body);
      body.focus=$("aiFocus")?.value||state.aiFocus||"general";
      options.body=JSON.stringify(body);
    }catch{}
  }
  return originalRequestJson(url,options);
};

function renderProviderSettingsSummary(){
  const node=$("providerSettingsSummary");
  const data=state.providerSettings;
  if(!node)return;
  if(!data){node.textContent="AI 来源配置状态尚未读取。";return}
  const local=data.localWorkerConfigured
    ? `本机 AI Worker 可用（来源：${data.localWorkerSource||"未配置"}，地址：${data.localWorkerUrl||"未知"}）。`
    : "本机 AI Worker 未配置——AI 生成会用不了，请打开“AI 来源设置”填写本机 Worker 地址和凭据。";
  const cloud=data.source==="本机加密保存"
    ? `云端 Provider 已配置（${data.keyHint||"Key 已隐藏"}）。`
    : data.source==="环境变量"
      ? "云端 Provider 由环境变量提供。"
      : "云端 Provider 未配置（不使用云端可以忽略）。";
  node.textContent=local+" "+cloud;
}

function renderProviderDialogStatus(){
  const node=$("providerStoredStatus");
  const data=state.providerSettings;
  if(!node)return;
  if(!data){node.innerHTML="<strong>尚未读取配置状态。</strong>";return}
  const label=data.source==="本机加密保存"?"本机加密保存":data.source||"未配置";
  const detail=data.hasApiKey?`API Key：${h(data.keyHint||"已配置")}`:"API Key：未配置";
  node.innerHTML=`<strong>当前来源：${h(label)}</strong><p>${h(detail)}。保存后只写入本机加密配置，不会写入世界书。</p>`;
  renderLocalWorkerDialogStatus(data);
}

function renderLocalWorkerDialogStatus(data){
  const node=$("localWorkerStoredStatus");
  if(!node)return;
  const source=data.localWorkerSource||"未配置";
  const stateText=data.localWorkerConfigured?"已配置":"未配置";
  const secret=data.hasLocalWorkerSecret?"凭据：已保存":"凭据：未配置";
  node.innerHTML=`<strong>本机 Worker 状态：${h(stateText)}（来源：${h(source)}）</strong><p>${h(data.localWorkerMessage||"")}${data.localWorkerMessage?"；":""}${h(secret)}。</p><p>AI 生成默认使用本机 Worker；未配置时生成按钮会被拒绝。</p>`;
}

function renderLocalFindings(){
  const node=$("aiLocalFindings");
  if(!node)return;
  if(!state.aiLocalFindings.length){node.innerHTML="";return}
  node.innerHTML=`<div class="scope"><strong>本地检查结果</strong>${state.aiLocalFindings.map(item=>`<p>· ${h(item.message||"需要检查的内容")}</p>`).join("")}</div>`;
}

function renderEnhancedAi(){
  originalRenderAi();
  if(state.aiConsentToken||state.aiBufferId||state.aiSuggestions.length)state.aiContextPath=state.currentPath||"";
  const focus=$("aiFocus");
  if(focus){
    focus.value=state.aiFocus||"general";
    state.aiFocus=focus.value||"general";
    $("aiFocusHelp").textContent=focusHelp[state.aiFocus]||"AI 只会提出候选建议，不会自动覆盖原文。";
    const analysis=$("aiAnalysis");
    if(analysis)analysis.value=focusAnalysis[state.aiFocus]||"consistency";
  }
  renderProviderSettingsSummary();
  renderLocalFindings();
  renderAiStaleNotice();
  renderEnhancedSuggestions();
}

function renderAuthorTarget(item){
  const target=item?.authorTarget;
  if(!item?.canApplyToAuthorMode||!target)return"只能放入高级缓冲区：这条建议包含作者模式暂不覆盖的字段。";
  const count=Number(target.operationCount||1);
  return count>1?`可放入作者表单：${h(target.stepTitle||"作者内容")}（${h(target.label||"多个字段")}，${count} 项）`:`可放入作者表单：${h(target.stepTitle||"作者内容")} · ${h(target.label||"当前字段")}`;
}

function renderEnhancedSuggestions(){
  const node=$("aiSuggestions");
  if(!node)return;
  if(!state.aiSuggestions.length){node.innerHTML="";return}
  node.innerHTML=state.aiSuggestions.map(item=>{
    const kind=suggestionKindLabels[item.kind]||item.kind||"AI 建议";
    const severity=severityLabels[item.severity]||item.severity||"待复核";
    const confidence=typeof item.confidence==="number"?Math.round(item.confidence*100)+"%":"未提供";
    const paths=(item.patch?.operations||[]).map(operation=>operation.path).filter(Boolean);
    const friendlyPaths=[...new Set(paths.map(friendlyPatchPath))];
    const pathText=paths.length?`可能影响：${friendlyPaths.join("、")}`:"仅供作者参考，不会直接修改字段";
    const candidate=item.candidateText?`<div class="technical"><strong>候选表达</strong><div>${h(item.candidateText)}</div></div>`:"";
    const targetText=renderAuthorTarget(item);
    const applyText=item.canApplyToAuthorMode?"放入作者表单":"放入高级缓冲区";
    return `<article class="suggestion"><div class="card-head"><div><strong>${h(item.title||"AI 建议")}</strong><div class="doc-meta"><span class="chip">${h(kind)}</span><span class="chip">${h(severity)}</span><span class="chip">置信度 ${h(confidence)}</span></div></div></div><p>${h(item.reason||"请结合世界观和作者意图复核。")}</p><p class="muted">${h(pathText)}</p><p class="ai-target-note">${targetText}</p>${candidate}<div class="card-actions"><button class="mini primary" data-ai-action="apply" data-suggestion="${h(item.id)}">${applyText}</button><button class="mini" data-ai-action="reject" data-suggestion="${h(item.id)}">撤销建议</button></div><small class="muted">尚未写入正典文件；应用后仍需人工检查并保存。</small></article>`;
  }).join("");
}

applySuggestion=async function(item){
  try{
    const data=await requestJson("/api/ai/authoring/apply",{method:"POST",body:JSON.stringify({path:state.currentPath,bufferId:state.aiBufferId,suggestionId:item.id,applyNonce:item.applyNonce})});
    state.advancedContent=data.content||state.advancedContent;
    if(data.canApplyToAuthorMode&&data.editorDocument?.model){
      state.authorModel=clone(data.editorDocument.model);
      state.step=Number(data.authorTarget?.step??item.authorTarget?.step??0);
      state.mode="author";
      state.aiConsentToken="";
      state.aiBufferId="";
      state.aiScope=null;
      state.aiStaleNotice="";
      state.aiSuggestions=[];
      state.aiContextPath="";
      renderAll();
      requestAnimationFrame(()=>{
        const focusId=data.authorTarget?.focusId||item.authorTarget?.focusId;
        const target=focusId?$(focusId):null;
        if(target)target.focus();
      });
      toast("候选已放入作者表单。其余建议已清空，请检查后保存。","good");
      return;
    }
    state.aiSuggestions=state.aiSuggestions.filter(x=>x.id!==item.id);
    state.aiConsentToken="";
    state.aiBufferId="";
    state.aiScope=null;
    state.aiSuggestions=[];
    state.aiStaleNotice="";
    state.aiContextPath="";
    state.mode="advanced";
    renderAll();
    refreshDirty();
    toast("建议已放入高级编辑器缓冲区，尚未写入文件。","good");
  }catch(error){handleError(error)}
};

renderAi=renderEnhancedAi;

async function loadProviderSettings(){
  try{
    state.providerSettings=await requestJson("/api/ai/provider-settings");
    renderProviderDialogStatus();
    renderAi();
  }catch(error){
    state.providerSettings=null;
    renderProviderDialogStatus();
    if(error.code!=="WB-AI-CSRF-403")toast(safeAiErrorMessage(error,"无法读取云端配置状态。"),"warn");
  }
}

async function openProviderSettings(){
  await loadProviderSettings();
  const data=state.providerSettings||{};
  $("providerBaseUrl").value=data.baseUrl||"";
  $("providerModel").value=data.model||"";
  $("providerApiKey").value="";
  $("localWorkerUrl").value=data.localWorkerUrl||"";
  $("localWorkerSecret").value="";
  renderProviderDialogStatus();
  $("providerDialog").showModal();
}

async function saveProviderSettings(event){
  event.preventDefault();
  const button=$("providerForm").querySelector("button[type=submit]");
  button.disabled=true;
  try{
    const data=await requestJson("/api/ai/provider-settings",{method:"PUT",body:JSON.stringify({baseUrl:$("providerBaseUrl").value.trim(),model:$("providerModel").value.trim(),apiKey:$("providerApiKey").value,clearSavedConfiguration:false,localWorkerUrl:$("localWorkerUrl").value.trim(),localWorkerSecret:$("localWorkerSecret").value,clearSavedLocalWorkerConfiguration:false})});
    state.providerSettings=data;
    $("providerApiKey").value="";
    $("localWorkerSecret").value="";
    renderProviderDialogStatus();
    renderAi();
    await loadProviders();
    $("providerDialog").close();
    toast("AI 来源配置已保存到本机加密存储。","good");
  }catch(error){handleError(error)}finally{button.disabled=false}
}

async function clearProviderSettings(){
  if(!confirm("确定删除本机保存的云端 Provider 配置吗？环境变量配置不会被删除。"))return;
  try{
    const data=await requestJson("/api/ai/provider-settings",{method:"PUT",body:JSON.stringify({clearSavedConfiguration:true})});
    state.providerSettings=data;
    $("providerBaseUrl").value=data.baseUrl||"";
    $("providerModel").value=data.model||"";
    $("providerApiKey").value="";
    renderProviderDialogStatus();
    renderAi();
    await loadProviders();
    toast("本机保存的云端配置已删除。","good");
  }catch(error){handleError(error)}
}

async function clearLocalWorkerSettings(){
  if(!confirm("确定删除本机保存的本机 AI Worker 配置吗？删除后 AI 生成会用不了，除非改回用环境变量提供。"))return;
  try{
    const data=await requestJson("/api/ai/provider-settings",{method:"PUT",body:JSON.stringify({clearSavedLocalWorkerConfiguration:true})});
    state.providerSettings=data;
    $("localWorkerUrl").value=data.localWorkerUrl||"";
    $("localWorkerSecret").value="";
    renderProviderDialogStatus();
    renderAi();
    await loadProviders();
    toast("本机 Worker 配置已删除。","good");
  }catch(error){handleError(error)}
}

function runLocalAiCheck(){
  const findings=typeof localChecks==="function"?localChecks():[];
  state.aiLocalFindings=findings;
  renderAi();
  toast(findings.length?`本地检查发现 ${findings.length} 项需要处理。`:"本地检查通过，未发现明显填写问题。",findings.length?"warn":"good");
}

$("providerSettingsButton").addEventListener("click",openProviderSettings);
$("providerForm").addEventListener("submit",saveProviderSettings);
$("cancelProviderButton").addEventListener("click",()=>$("providerDialog").close());
$("clearProviderButton").addEventListener("click",clearProviderSettings);
$("clearLocalWorkerButton").addEventListener("click",clearLocalWorkerSettings);
$("aiLocalCheck").addEventListener("click",runLocalAiCheck);
$("aiAnalysis").addEventListener("change",event=>{state.aiFocus=analysisDefaultFocus[event.target.value]||"general";state.aiConsentToken="";renderAi()});
$("aiFocus").addEventListener("change",event=>{state.aiFocus=event.target.value;$("aiFocusHelp").textContent=focusHelp[state.aiFocus]||"AI 只会提出候选建议，不会自动覆盖原文。";state.aiConsentToken="";renderAi()});
async function waitForAiSession(){
  for(let index=0;index<80;index++){
    if(state.aiCsrf)return true;
    await new Promise(resolve=>setTimeout(resolve,50));
  }
  return false;
}

(async()=>{
  if(await waitForAiSession()){
    await loadProviderSettings();
    await loadProviders();
  }
})();
