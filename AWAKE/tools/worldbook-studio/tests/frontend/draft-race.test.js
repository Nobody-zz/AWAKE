"use strict";

const assert=require("node:assert/strict");
const fs=require("node:fs");
const path=require("node:path");
const vm=require("node:vm");

const studioRoot=path.resolve(__dirname,"..","..");
const webRoot=path.join(studioRoot,"src","Awake.WorldbookStudio.Web","wwwroot");
const localDraftsSource=fs.readFileSync(path.join(webRoot,"studio-local-drafts.js"),"utf8");
const draftSource=fs.readFileSync(path.join(webRoot,"studio-draft.js"),"utf8");

function makeHarness(){
  const elements=new Map();
  const toasts=[];
  const requests=[];
  const calls={loadDocuments:0,openDocument:0,aborts:0};
  const state={workspaceHash:"workspace",currentPath:"authoring/current.yaml",catalog:{profiles:[]},authorModel:{},step:1};

  class Element{
    constructor(){
      this.value="";
      this.disabled=false;
      this.open=true;
      this.dataset={};
      this.classList={toggle(){},remove(){}};
    }
    addEventListener(){}
    close(){this.open=false}
    querySelector(){return null}
    closest(){return null}
  }

  const getElement=id=>{
    if(!elements.has(id))elements.set(id,new Element());
    return elements.get(id);
  };

  class TestAbortController{
    constructor(){this.signal={};}
    abort(){calls.aborts+=1}
  }

  const context={
    console,
    AbortController:TestAbortController,
    state,
    window:null,
    document:{
      body:{insertAdjacentHTML(){}},
      querySelectorAll(){return []}
    },
    $(id){return getElement(id)},
    clone(value){return JSON.parse(JSON.stringify(value))},
    key(){return "generated-key"},
    domainEntry(){return true},
    subdomainEntry(){return true},
    toast(message){toasts.push(String(message))},
    handleError(error){throw error},
    requestJson(url,options){
      requests.push({url,options});
      return new Promise((resolve,reject)=>{
        requests[requests.length-1].resolve=resolve;
        requests[requests.length-1].reject=reject;
      });
    },
    async loadDocuments(){calls.loadDocuments+=1},
    async openDocument(){calls.openDocument+=1},
    applyDraftToAuthorModel(){},
    refreshDirty(){},
    renderAll(){},
    confirm(){return true}
    ,h(value){return String(value??"").replaceAll("&","&amp;").replaceAll("<","&lt;").replaceAll(">","&gt;").replaceAll('"',"&quot;")}
    ,setTimeout
    ,clearTimeout
  };
  context.window=context;
  context.awakeEnsureSaved=async()=>true;
  vm.runInNewContext(localDraftsSource,context,{filename:"studio-local-drafts.js"});
  vm.runInNewContext(draftSource+"\n globalThis.__draftTest={draftState,draftCreateDocument,draftMarkChanged,draftRestoreServer,draftSourceEdited,draftPayload};",context,{filename:"studio-draft.js"});

  return {context,elements,getElement,toasts,requests,calls,api:context.__draftTest};
}

function prepareCreate(harness){
  const {api,getElement}=harness;
  getElement("draftTitleCandidate").value="新档案";
  getElement("draftSummaryCandidate").value="档案摘要";
  getElement("draftDomainCandidate").value="politics";
  getElement("draftSourceName").value="参考资料";
  getElement("draftSourceText").value="原始资料";
  getElement("draftProvider").value="local";
  getElement("draftPerspectives").value="普通平民";
  api.draftState.draftId="draft-1";
  api.draftState.facts=[{id:"fact-1",text:"事实",reviewStatus:"accepted"}];
  api.draftState.expressions=[];
  api.draftState.metadata={title:"新档案",summary:"档案摘要",domain:"politics"};
}

async function waitForRequest(harness){
  for(let attempt=0;attempt<20&&harness.requests.length===0;attempt++)await new Promise(resolve=>setImmediate(resolve));
  assert.equal(harness.requests.length,1,"create-document request should be issued");
}

async function run(name,action){await action();process.stdout.write("PASS "+name+"\n")}

(async()=>{
  await run("late create response does not abort or switch",async()=>{
    const harness=makeHarness();
    prepareCreate(harness);
    const operation=harness.api.draftCreateDocument();
    await waitForRequest(harness);
    harness.getElement("draftSourceText").value="用户修改后的资料";
    harness.api.draftMarkChanged();
    harness.requests[0].resolve({document:{path:"authoring/created.yaml"}});
    await operation;
    assert.equal(harness.calls.aborts,0);
    assert.equal(harness.calls.loadDocuments,0);
    assert.equal(harness.calls.openDocument,0);
    assert.equal(harness.api.draftState.busy,false);
    assert.ok(harness.toasts.some(message=>message.includes("草稿在请求期间发生了变化，未自动切换")));
  });

  await run("create response rechecks after document refresh",async()=>{
    const harness=makeHarness();
    prepareCreate(harness);
    harness.context.loadDocuments=async()=>{
      harness.calls.loadDocuments+=1;
      harness.api.draftMarkChanged();
    };
    const operation=harness.api.draftCreateDocument();
    await waitForRequest(harness);
    harness.requests[0].resolve({document:{path:"authoring/created.yaml"}});
    await operation;
    assert.equal(harness.calls.loadDocuments,1);
    assert.equal(harness.calls.openDocument,0);
    assert.equal(harness.api.draftState.busy,false);
    assert.ok(harness.toasts.some(message=>message.includes("草稿在请求期间发生了变化，未自动切换")));
  });

  await run("server draft resume applies only matching source hash",async()=>{
    const harness=makeHarness();
    harness.api.draftState.draftId="draft-resume";
    harness.api.draftState.serverSourceContentHash="hash-resume";
    harness.getElement("draftSourceName").value="参考资料";
    harness.getElement("draftSourceText").value="原始资料";
    const operation=harness.api.draftRestoreServer();
    await waitForRequest(harness);
    assert.equal(harness.requests[0].url,"/api/ai/authoring/draft/draft-resume");
    harness.requests[0].resolve({ok:true,sourceContentHash:"hash-resume",result:{
      stage:"facts",
      reviewOnly:true,
      facts:[{id:"fact-resume",text:"恢复事实",reviewStatus:"pending",inferred:false,evidence:null}],
      metadata:null,
      expressions:[],
      warnings:["恢复警告"],
      unresolved:[],
      coverage:null,
      targetSpans:[],
      propositions:[],
      claims:[],
      candidateSet:null
    }});
    await operation;
    assert.equal(harness.api.draftState.facts[0].id,"fact-resume");
    assert.ok(harness.toasts.some(message=>message.includes("已接回服务端 Draft")));
  });

  await run("editing source clears server draft binding",async()=>{
    const harness=makeHarness();
    harness.api.draftState.draftId="draft-old";
    harness.api.draftState.serverSourceContentHash="hash-old";
    harness.api.draftSourceEdited();
    assert.equal(harness.api.draftState.draftId,"");
    assert.equal(harness.api.draftState.serverSourceContentHash,"");
  });
})().catch(error=>{console.error(error);process.exitCode=1});
