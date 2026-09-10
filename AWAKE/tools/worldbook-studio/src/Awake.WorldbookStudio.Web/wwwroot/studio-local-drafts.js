"use strict";
(function(root){
  const maxBytes=1.5*1024*1024;
  const maxAgeMs=30*24*60*60*1000;
  const blockedKeys=new Set(["apikey","api_key","csrf","csrftoken","consenttoken","providertoken","sessiontoken","authorization","secret","password","access_token","refresh_token"]);

  function text(value){return value==null?"":String(value)}
  function stable(value){
    if(Array.isArray(value))return value.map(stable);
    if(value&&typeof value==="object")return Object.keys(value).sort().reduce((result,key)=>{result[key]=stable(value[key]);return result},{});
    return value;
  }
  function hash(value){
    let result=2166136261;
    for(let index=0;index<value.length;index++){result^=value.charCodeAt(index);result=Math.imul(result,16777619)}
    return (result>>>0).toString(16).padStart(8,"0");
  }
  function sanitize(value,key=""){
    if(blockedKeys.has(key.toLowerCase().replace(/[-_]/g,"")))return undefined;
    if(Array.isArray(value))return value.map(item=>sanitize(item)).filter(item=>item!==undefined);
    if(value&&typeof value==="object"){
      const result={};
      Object.keys(value).forEach(name=>{const item=sanitize(value[name],name);if(item!==undefined)result[name]=item});
      return result;
    }
    return value;
  }
  function bytes(value){
    try{return new TextEncoder().encode(value).length}catch{return value.length*2}
  }
  function contextValue(context){
    const value=context||{};
    return {workspace:text(value.workspaceHash||value.workspaceKey||"local"),path:text(value.documentPath||value.path),kind:text(value.kind||value.mode||"editor"),task:text(value.taskId||value.draftTaskId),sourceHash:text(value.sourceHash).toLowerCase()||null,revision:Number.isInteger(value.revision)?value.revision:null};
  }

  class LocalDraftStore{
    constructor(options={}){
      this.storage=options.storage??(typeof root.localStorage!=="undefined"?root.localStorage:null);
      this.prefix=options.prefix||"awake.worldbook.studio.draft.v1";
      this.maxAgeMs=Number.isFinite(options.maxAgeMs)?options.maxAgeMs:maxAgeMs;
      this.maxBytes=Number.isFinite(options.maxBytes)?options.maxBytes:maxBytes;
      this.timers=new Map();
      this.onResult=typeof options.onResult==="function"?options.onResult:null;
    }
    key(context){
      const value=contextValue(context);
      return `${this.prefix}.${hash(JSON.stringify(stable({workspace:value.workspace,path:value.path,kind:value.kind,task:value.task})))}`;
    }
    cancel(context){
      const key=this.key(context);
      const timer=this.timers.get(key);
      if(timer!==undefined)clearTimeout(timer);
      this.timers.delete(key);
    }
    save(context,payload,generation){
      const key=this.key(context);
      if(!this.storage)return {ok:false,status:"unavailable"};
      const record={version:1,savedAt:Date.now(),generation:Number.isInteger(generation)?generation:null,context:contextValue(context),payload:sanitize(payload)};
      let serialized;
      try{serialized=JSON.stringify(record)}catch{return {ok:false,status:"unavailable"}}
      if(bytes(serialized)>this.maxBytes)return {ok:false,status:"too-large"};
      try{this.storage.setItem(key,serialized);return {ok:true,status:"saved",key}}catch{return {ok:false,status:"unavailable",key}}
    }
    flush(context,payload,generation){this.cancel(context);return this.save(context,payload,generation)}
    schedule(context,payload,generation,delay=450){
      const key=this.key(context);
      this.cancel(context);
      if(!this.storage)return {ok:false,status:"unavailable",key};
      const timer=setTimeout(()=>{this.timers.delete(key);const result=this.save(context,payload,generation);if(this.onResult)this.onResult(result)},delay);
      this.timers.set(key,timer);
      return {ok:true,status:"scheduled",key};
    }
    load(context,currentBaseline){
      const key=this.key(context);
      if(!this.storage)return {status:"unavailable",key};
      let raw;
      try{raw=this.storage.getItem(key)}catch{return {status:"unavailable",key}}
      if(!raw)return {status:"missing",key};
      let record;
      try{record=JSON.parse(raw)}catch{this.discard(context);return {status:"invalid",key}}
      if(!record||record.version!==1||!record.payload||!record.context){this.discard(context);return {status:"invalid",key}}
      if(!Number.isFinite(record.savedAt)||Date.now()-record.savedAt>this.maxAgeMs){this.discard(context);return {status:"expired",key}}
      const baseline=currentBaseline||{};
      const draftHash=text(record.context.sourceHash||record.payload.sourceHash).toLowerCase();
      const draftRevision=Number(record.context.revision??record.payload.revision);
      const currentHash=text(baseline.sourceHash).toLowerCase();
      const currentRevision=Number(baseline.revision);
      const stale=Boolean(draftHash&&currentHash&&draftHash!==currentHash)||Number.isInteger(draftRevision)&&Number.isInteger(currentRevision)&&draftRevision!==currentRevision;
      return {status:stale?"stale":"available",key,payload:record.payload,generation:record.generation,savedAt:record.savedAt,context:record.context,baseline:{sourceHash:draftHash||null,revision:Number.isInteger(draftRevision)?draftRevision:null}};
    }
    discard(context){
      const key=this.key(context);
      this.cancel(context);
      try{this.storage?.removeItem(key)}catch{}
      return {ok:true,key};
    }
    clearAfterSave(context,savedGeneration,currentGeneration){
      if(savedGeneration!==currentGeneration)return {ok:true,status:"kept",key:this.key(context)};
      return {...this.discard(context),status:"cleared"};
    }
  }

  root.AwakeLocalDrafts={LocalDraftStore,contextValue,sanitize};
})(typeof window!=="undefined"?window:globalThis);
