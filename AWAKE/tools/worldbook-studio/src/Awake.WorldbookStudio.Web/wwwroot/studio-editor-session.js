"use strict";
(function(root){
  const ruleConditionKeys=["culture_ids","kingdom_ids","settlement_ids","role_ids","is_female","is_clan_leader","min_age","max_age","min_management","min_steward","min_skill"];
  const lockedStatuses=new Set(["saving","pending-confirmation","conflict"]);

  function clone(value){return value==null?value:JSON.parse(JSON.stringify(value))}
  function token(prefix,factory){return prefix+"-"+factory()}
  function defaultToken(){
    if(root.crypto?.randomUUID)return root.crypto.randomUUID();
    return Date.now().toString(36)+"-"+Math.random().toString(36).slice(2,10);
  }
  function text(value){return value==null?"":String(value)}
  function blankToNull(value){const result=text(value);return result===""?null:result}
  function enumValue(value,fallback=""){const result=text(value);return result||fallback}
  function nullableInteger(value){
    if(value==null||value==="")return null;
    const result=Number(value);
    return Number.isInteger(result)?result:null;
  }
  function stringArray(value){return (Array.isArray(value)?value:[]).map(item=>text(item)).filter(Boolean)}
  function referralId(value){return typeof value==="object"&&value!==null?text(value.id||value.referralId):text(value)}
  function normalizeConditions(value){
    const conditions=value&&typeof value==="object"&&!Array.isArray(value)?value:{};
    const result={};
    ruleConditionKeys.forEach(key=>{
      if(!Object.prototype.hasOwnProperty.call(conditions,key)||conditions[key]==null||conditions[key]==="")return;
      const item=conditions[key];
      if(Array.isArray(item)){
        const values=item.map(value=>text(value)).filter(Boolean);
        if(values.length)result[key]=values;
      }else if(typeof item==="boolean")result[key]=item;
      else if(["min_age","max_age","min_management","min_steward","min_skill"].includes(key)){
        const number=nullableInteger(item);
        if(number!==null)result[key]=number;
      }else result[key]=text(item);
    });
    return result;
  }
  function normalizeRules(value){
    return (Array.isArray(value)?value:[]).map(rule=>({
      profileId:text(rule?.profileId??rule?.profile_id),
      scope:text(rule?.scope),
      minDetail:text(rule?.minDetail??rule?.min_detail),
      conditions:normalizeConditions(rule?.conditions)
    }));
  }
  function normalizeAuthorProjection(model){
    const value=model||{};
    const era=value.era||{};
    return {
      title:text(value.title),
      domain:enumValue(value.domain),
      subdomain:blankToNull(value.subdomain),
      relatedDomains:stringArray(value.relatedDomains),
      status:enumValue(value.status),
      contentTier:enumValue(value.contentTier,"base"),
      summary:text(value.summary),
      era:{
        preset:enumValue(era.preset??era.key,"unknown"),
        certainty:enumValue(era.certainty,"unknown"),
        startYear:nullableInteger(era.startYear),
        endYear:nullableInteger(era.endYear)
      },
      assertions:(Array.isArray(value.assertions)?value.assertions:[]).map(assertion=>({
        kind:enumValue(assertion?.kind,"fact"),
        text:text(assertion?.text),
        expressions:(Array.isArray(assertion?.expressions)?assertion.expressions:[]).map(expression=>({
          layer:enumValue(expression?.layer,"summary"),
          text:text(expression?.text),
          grants:normalizeRules(expression?.grants),
          denies:normalizeRules(expression?.denies),
          fallbackReferrals:(Array.isArray(expression?.fallbackReferrals)?expression.fallbackReferrals:[]).map(referralId).filter(Boolean)
        }))
      }))
    };
  }
  function stableValue(value){
    if(Array.isArray(value))return value.map(stableValue);
    if(value&&typeof value==="object")return Object.keys(value).sort().reduce((result,key)=>{result[key]=stableValue(value[key]);return result},{});
    return value;
  }
  function stableStringify(value){return JSON.stringify(stableValue(value))}
  function projectionKey(value){return stableStringify(normalizeAuthorProjection(value))}
  function projectionsEqual(left,right){return projectionKey(left)===projectionKey(right)}
  function readbackMatchesEditorDocument(editorDocument,raw){
    if(!editorDocument||!raw||typeof raw.content!=="string")return false;
    const expectedPath=text(editorDocument.path);
    const actualPath=text(raw.path??raw.Path);
    const expectedHash=text(editorDocument.sourceHash).toLowerCase();
    const rawReport=raw.report??raw.Report;
    const actualHash=text(rawReport?.inputHash??rawReport?.InputHash??raw.sourceHash??raw.SourceHash).toLowerCase();
    const expectedRevision=Number(editorDocument.revision);
    const rawDocument=raw.document??raw.Document;
    const actualRevision=Number(rawDocument?.revision??rawDocument?.Revision??raw.revision??raw.Revision);
    return Boolean(expectedPath&&actualPath===expectedPath&&expectedHash&&actualHash===expectedHash&&Number.isInteger(expectedRevision)&&expectedRevision>=1&&Number.isInteger(actualRevision)&&actualRevision===expectedRevision);
  }
  function rawReadbackFields(readback){
    const document=readback?.document??readback?.Document??readback;
    const report=readback?.report??readback?.Report??document?.report??document?.Report;
    return {
      path:text(readback?.path??readback?.Path??document?.path??document?.Path),
      content:typeof readback?.content==="string"?readback.content:typeof readback?.Content==="string"?readback.Content:typeof document?.content==="string"?document.content:typeof document?.Content==="string"?document.Content:null,
      sourceHash:text(readback?.hash??readback?.sourceHash??readback?.SourceHash??report?.inputHash??report?.InputHash).toLowerCase(),
      expectedContentHash:text(readback?.expectedContentHash??readback?.ExpectedContentHash).toLowerCase()||null,
      revision:Number(readback?.revision??readback?.Revision??document?.revision??document?.Revision)
    };
  }
  function readbackMatchesRawDocument(expected,readback){
    const actual=rawReadbackFields(readback);
    return Boolean(expected?.path&&actual.path===expected.path&&expected?.sourceHash&&actual.sourceHash===String(expected.sourceHash).toLowerCase()&&Number.isInteger(expected.revision)&&expected.revision>=1&&Number.isInteger(actual.revision)&&actual.revision===expected.revision&&typeof actual.content==="string");
  }

  class Controller{
    constructor(options={}){
      this.tokenFactory=options.tokenFactory||defaultToken;
      this.abortControllerFactory=options.abortControllerFactory||(()=>typeof AbortController==="function"?new AbortController():{abort(){},signal:undefined});
      this.sessionToken=null;
      this.documentKey=null;
      this.mode="author";
      this.editGeneration=0;
      this.savedBaseline=null;
      this.saveStatus="idle";
      this.saveContext=null;
      this.activeOperation=null;
      this.rawSaveStatus="idle";
      this.rawSaveContext=null;
      this.rawActiveOperation=null;
      this.loading=false;
      this.listRequestToken=null;
    }
    nextToken(prefix){return token(prefix,this.tokenFactory)}
    beginOpen(path,options={}){
      if(!path)return {accepted:false,reason:"missing-path"};
      const conflictReload=options.force===true&&(this.saveStatus==="conflict"||this.rawSaveStatus==="conflict");
      if(this.isLocked()&&!conflictReload)return {accepted:false,reason:"locked",status:this.saveStatus};
      if(conflictReload){this.saveStatus="idle";this.saveContext=null;this.activeOperation=null;this.rawSaveStatus="idle";this.rawSaveContext=null;this.rawActiveOperation=null;}
      const operation={accepted:true,kind:"open",path,conflictReload,sessionToken:this.nextToken("session"),requestToken:this.nextToken("request")};
      this.sessionToken=operation.sessionToken;
      this.loading=true;
      this.activeOperation=operation;
      return operation;
    }
    isCurrentOpen(operation){return Boolean(operation&&operation.kind==="open"&&this.activeOperation===operation&&this.sessionToken===operation.sessionToken&&this.loading)}
    completeOpen(operation,documentKey,baseline){
      if(!this.isCurrentOpen(operation))return false;
      this.documentKey=documentKey;
      this.mode="author";
      this.editGeneration=0;
      this.savedBaseline=clone(baseline);
      this.saveStatus="idle";
      this.saveContext=null;
      this.loading=false;
      this.activeOperation=null;
      this.rawSaveStatus="idle";
      this.rawSaveContext=null;
      this.rawActiveOperation=null;
      return true;
    }
    failOpen(operation){
      if(!this.isCurrentOpen(operation))return false;
      if(operation.conflictReload)this.saveStatus="conflict";
      this.loading=false;
      this.activeOperation=null;
      return true;
    }
    beginListRequest(){
      const operation={kind:"list",requestToken:this.nextToken("request")};
      this.listRequestToken=operation.requestToken;
      return operation;
    }
    isCurrentListRequest(operation){return Boolean(operation&&operation.kind==="list"&&this.listRequestToken===operation.requestToken)}
    startDocument(documentKey,mode,baseline){
      this.sessionToken=this.nextToken("session");
      this.documentKey=documentKey;
      this.mode=mode||"author";
      this.editGeneration=0;
      this.savedBaseline=clone(baseline);
      this.saveStatus="idle";
      this.saveContext=null;
      this.loading=false;
      this.activeOperation=null;
      this.rawSaveStatus="idle";
      this.rawSaveContext=null;
      this.rawActiveOperation=null;
      return this.sessionToken;
    }
    switchMode(mode){
      if(this.isLocked()||this.loading)return false;
      this.mode=mode;
      this.editGeneration=0;
      this.sessionToken=this.nextToken("session");
      this.activeOperation=null;
      this.rawSaveStatus="idle";
      this.rawSaveContext=null;
      this.rawActiveOperation=null;
      return true;
    }
    markEdit(){this.editGeneration+=1;return this.editGeneration}
    isLocked(){return lockedStatuses.has(this.saveStatus)||lockedStatuses.has(this.rawSaveStatus)}
    canStartSave(){return this.mode==="author"&&!this.loading&&(this.saveStatus==="idle"||this.saveStatus==="failed")&&!this.activeOperation}
    beginSave(documentKey,model,baseline,request){
      if(!this.canStartSave()||!documentKey||!baseline)return null;
      const context={
        documentKey,
        mode:this.mode,
        capturedGeneration:this.editGeneration,
        capturedModel:clone(model),
        capturedModelProjection:normalizeAuthorProjection(model),
        capturedBaselineProjection:clone(baseline.editorProjection),
        capturedSourceHash:baseline.sourceHash,
        capturedRevision:baseline.revision,
        request:clone(request)
      };
      const operation={kind:"save",sessionToken:this.sessionToken,requestToken:this.nextToken("request"),context,controller:this.abortControllerFactory()};
      this.saveContext=context;
      this.saveStatus="saving";
      this.activeOperation=operation;
      return operation;
    }
    isCurrentSave(operation){return Boolean(operation&&operation.kind==="save"&&this.activeOperation===operation&&this.sessionToken===operation.sessionToken&&(this.saveStatus==="saving"||this.saveStatus==="pending-confirmation"))}
    handleSaveFailure(operation,error){
      if(!this.isCurrentSave(operation))return null;
      const httpStatus=Number(error?.status||0);
      const status=httpStatus===409?"conflict":httpStatus>=400&&httpStatus<500&&httpStatus!==408&&httpStatus!==429?"failed":"pending-confirmation";
      this.saveStatus=status;
      if(status!=="pending-confirmation")this.activeOperation=null;
      return status;
    }
    markPending(operation){
      if(!this.isCurrentSave(operation))return false;
      this.saveStatus="pending-confirmation";
      return true;
    }
    beginPendingCheck(){
      if(this.saveStatus==="pending-confirmation"&&this.activeOperation?.kind==="save-check")return null;
      if(this.saveStatus==="saving"&&this.activeOperation?.kind==="save"){
        const save=this.activeOperation;
        this.saveContext=save.context;
        this.saveStatus="pending-confirmation";
        try{save.controller?.abort()}catch{}
      }
      if(this.saveStatus!=="pending-confirmation"||!this.saveContext||!this.documentKey)return null;
      const operation={kind:"save-check",sessionToken:this.sessionToken,requestToken:this.nextToken("request"),context:clone(this.saveContext),controller:this.abortControllerFactory()};
      this.activeOperation=operation;
      return operation;
    }
    isCurrentCheck(operation){return Boolean(operation&&operation.kind==="save-check"&&this.activeOperation===operation&&this.sessionToken===operation.sessionToken&&this.saveStatus==="pending-confirmation")}
    finishPendingCheck(operation){
      if(!this.isCurrentCheck(operation))return false;
      this.activeOperation=null;
      return true;
    }
    resolvePending(operation,editorDocument){
      if(!this.isCurrentCheck(operation))return {status:"stale"};
      const context=operation.context;
      const path=editorDocument?.path;
      const model=editorDocument?.model;
      const sourceHash=editorDocument?.sourceHash;
      const revision=Number(editorDocument?.revision);
      if(!path||!model||typeof sourceHash!=="string"||!Number.isInteger(revision)){
        this.activeOperation=null;
        return {status:"pending"};
      }
      if(path!==context.documentKey){this.saveStatus="conflict";this.saveContext=null;this.activeOperation=null;return {status:"conflict"};}
      const newMatch=projectionsEqual(model,context.capturedModelProjection)&&revision>context.capturedRevision&&sourceHash!==context.capturedSourceHash;
      if(newMatch){
        this.savedBaseline={editorProjection:normalizeAuthorProjection(model),sourceHash,revision};
        this.saveStatus="idle";
        this.saveContext=null;
        this.activeOperation=null;
        return {status:"confirmed",baseline:clone(this.savedBaseline),keepCurrentModel:this.editGeneration!==context.capturedGeneration};
      }
      const oldMatch=projectionsEqual(model,context.capturedBaselineProjection)&&revision===context.capturedRevision&&sourceHash===context.capturedSourceHash;
      if(oldMatch){this.saveStatus="failed";this.saveContext=null;this.activeOperation=null;return {status:"not-submitted"};}
      this.saveStatus="conflict";
      this.saveContext=null;
      this.activeOperation=null;
      return {status:"conflict"};
    }
    commitSave(operation,editorDocument){
      if(!this.isCurrentSave(operation))return null;
      const context=operation.context;
      const revision=Number(editorDocument?.revision);
      if(editorDocument?.path!==context.documentKey||!editorDocument?.model||typeof editorDocument?.sourceHash!=="string"||!Number.isInteger(revision)||revision<=context.capturedRevision||editorDocument.sourceHash.toLowerCase()===String(context.capturedSourceHash).toLowerCase())return null;
      this.savedBaseline={editorProjection:normalizeAuthorProjection(editorDocument.model),sourceHash:editorDocument.sourceHash,revision};
      const result={baseline:clone(this.savedBaseline),keepCurrentModel:this.editGeneration!==context.capturedGeneration};
      this.saveStatus="idle";
      this.saveContext=null;
      this.activeOperation=null;
      return result;
    }
    statusMessage(dirty){
      if(this.rawSaveStatus==="saving")return "正在保存原始文件…修改仍可继续编辑";
      if(this.rawSaveStatus==="pending-confirmation")return "原始文件保存结果待确认，请检查磁盘结果";
      if(this.rawSaveStatus==="failed")return "原始文件保存失败，当前修改仍保留";
      if(this.rawSaveStatus==="conflict")return "原始文件发生冲突，请重新读取磁盘版本";
      if(this.saveStatus==="saving")return "正在保存…修改仍可继续编辑";
      if(this.saveStatus==="pending-confirmation")return "保存结果待确认，请检查磁盘结果";
      if(this.saveStatus==="failed")return "保存失败，当前修改仍保留";
      if(this.saveStatus==="conflict")return "文件发生冲突，请重新读取磁盘版本";
      return dirty?"有未保存修改":"未修改";
    }
    checkLabel(){return this.saveStatus==="saving"?"停止等待并检查结果":"检查保存结果"}
    canStartRawSave(){return this.mode==="advanced"&&!this.loading&&(this.rawSaveStatus==="idle"||this.rawSaveStatus==="failed")&&!this.rawActiveOperation}
    beginRawSave(documentKey,rawContent,baseline,request){
      if(!this.canStartRawSave()||!documentKey||typeof rawContent!=="string"||!baseline)return null;
      const context={documentKey,mode:this.mode,capturedGeneration:this.editGeneration,capturedContent:rawContent,capturedSourceHash:baseline.sourceHash,capturedRevision:baseline.revision,request:clone(request)};
      const operation={kind:"raw-save",sessionToken:this.sessionToken,requestToken:this.nextToken("request"),context,controller:this.abortControllerFactory()};
      this.rawSaveContext=context;
      this.rawSaveStatus="saving";
      this.rawActiveOperation=operation;
      return operation;
    }
    isCurrentRawSave(operation){return Boolean(operation&&operation.kind==="raw-save"&&this.rawActiveOperation===operation&&this.sessionToken===operation.sessionToken&&(this.rawSaveStatus==="saving"||this.rawSaveStatus==="pending-confirmation"))}
    handleRawSaveFailure(operation,error){
      if(!this.isCurrentRawSave(operation))return null;
      this.rawSaveStatus=rawFailureStatus(error);
      if(this.rawSaveStatus!=="pending-confirmation")this.rawActiveOperation=null;
      return this.rawSaveStatus;
    }
    markRawPending(operation){if(!this.isCurrentRawSave(operation))return false;this.rawSaveStatus="pending-confirmation";return true}
    beginRawPendingCheck(){
      if(this.rawSaveStatus==="pending-confirmation"&&this.rawActiveOperation?.kind==="raw-save-check")return null;
      if(this.rawSaveStatus==="saving"&&this.rawActiveOperation?.kind==="raw-save"){
        const save=this.rawActiveOperation;
        this.rawSaveContext=save.context;
        this.rawSaveStatus="pending-confirmation";
        try{save.controller?.abort()}catch{}
      }
      if(this.rawSaveStatus!=="pending-confirmation"||!this.rawSaveContext||!this.documentKey)return null;
      const operation={kind:"raw-save-check",sessionToken:this.sessionToken,requestToken:this.nextToken("request"),context:clone(this.rawSaveContext),controller:this.abortControllerFactory()};
      this.rawActiveOperation=operation;
      return operation;
    }
    isCurrentRawCheck(operation){return Boolean(operation&&operation.kind==="raw-save-check"&&this.rawActiveOperation===operation&&this.sessionToken===operation.sessionToken&&this.rawSaveStatus==="pending-confirmation")}
    finishRawPendingCheck(operation){if(!this.isCurrentRawCheck(operation))return false;this.rawActiveOperation=null;return true}
    resolveRawPending(operation,readback){
      if(!this.isCurrentRawCheck(operation))return {status:"stale"};
      const context=operation.context;
      const fields=rawReadbackFields(readback);
      if(!fields.path||typeof fields.content!=="string"||!fields.sourceHash||!Number.isInteger(fields.revision))return {status:"pending"};
      if(fields.path!==context.documentKey){this.rawSaveStatus="conflict";this.rawSaveContext=null;this.rawActiveOperation=null;return {status:"conflict"};}
      const expectedContentHash=fields.expectedContentHash;
      const isNew=fields.revision>context.capturedRevision&&fields.sourceHash!==String(context.capturedSourceHash).toLowerCase()&&(!expectedContentHash||fields.sourceHash===expectedContentHash);
      if(isNew){
        this.rawSaveStatus="idle";
        this.rawSaveContext=null;
        this.rawActiveOperation=null;
        return {status:"confirmed",readback:clone(readback),keepCurrentContent:this.editGeneration!==context.capturedGeneration};
      }
      const isOld=fields.revision===context.capturedRevision&&fields.sourceHash===String(context.capturedSourceHash).toLowerCase()&&fields.content===context.capturedContent;
      if(isOld){this.rawSaveStatus="failed";this.rawSaveContext=null;this.rawActiveOperation=null;return {status:"not-submitted"};}
      this.rawSaveStatus="conflict";
      this.rawSaveContext=null;
      this.rawActiveOperation=null;
      return {status:"conflict"};
    }
    commitRawSave(operation,readback){
      if(!this.isCurrentRawSave(operation))return null;
      const context=operation.context;
      const fields=rawReadbackFields(readback);
      if(fields.path!==context.documentKey||typeof fields.content!=="string"||!fields.sourceHash||!Number.isInteger(fields.revision)||fields.revision<=context.capturedRevision||fields.sourceHash===String(context.capturedSourceHash).toLowerCase()||(fields.expectedContentHash&&fields.sourceHash!==fields.expectedContentHash))return null;
      const result={baseline:{sourceHash:fields.sourceHash,revision:fields.revision,content:fields.content},keepCurrentContent:this.editGeneration!==context.capturedGeneration};
      this.rawSaveStatus="idle";
      this.rawSaveContext=null;
      this.rawActiveOperation=null;
      return result;
    }
    rawStatusMessage(dirty){
      if(this.rawSaveStatus==="saving")return "正在保存原始文件…修改仍可继续编辑";
      if(this.rawSaveStatus==="pending-confirmation")return "原始文件保存结果待确认，请检查磁盘结果";
      if(this.rawSaveStatus==="failed")return "原始文件保存失败，当前修改仍保留";
      if(this.rawSaveStatus==="conflict")return "原始文件发生冲突，请重新读取磁盘版本";
      return dirty?"有未保存修改":"未修改";
    }
    rawCheckLabel(){return this.rawSaveStatus==="saving"?"停止等待并检查原始文件":"检查原始文件保存结果"}
  }

  function rawFailureStatus(error){
    const httpStatus=Number(error?.status||0);
    return httpStatus===409?"conflict":httpStatus>=400&&httpStatus<500&&httpStatus!==408&&httpStatus!==429?"failed":"pending-confirmation";
  }

  root.AwakeEditorSession={Controller,clone,normalizeAuthorProjection,stableStringify,projectionKey,projectionsEqual,readbackMatchesEditorDocument,readbackMatchesRawDocument,rawReadbackFields,rawFailureStatus,ruleConditionKeys:[...ruleConditionKeys]};
})(typeof window!=="undefined"?window:globalThis);
