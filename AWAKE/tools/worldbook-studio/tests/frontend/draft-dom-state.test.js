"use strict";

const assert=require("node:assert/strict");
const fs=require("node:fs");
const path=require("node:path");
const vm=require("node:vm");

const studioRoot=path.resolve(__dirname,"..","..");
const webRoot=path.join(studioRoot,"src","Awake.WorldbookStudio.Web","wwwroot");
const localDraftsSource=fs.readFileSync(path.join(webRoot,"studio-local-drafts.js"),"utf8");
const draftSource=fs.readFileSync(path.join(webRoot,"studio-draft.js"),"utf8");

class Element{
  constructor(){
    this.value="";
    this.innerHTML="";
    this.textContent="";
    this.hidden=false;
    this.disabled=false;
    this.open=false;
    this.dataset={};
    this.parentNode={insertBefore(){}};
    this.classList={toggle(){},remove(){}};
  }
  addEventListener(){}
  showModal(){this.open=true}
  close(){this.open=false}
}

function makeHarness(){
  const elements=new Map();
  const storageValues=new Map();
  const toasts=[];
  const storage={
    getItem:key=>storageValues.get(key)||null,
    setItem:(key,value)=>storageValues.set(key,value),
    removeItem:key=>storageValues.delete(key)
  };
  const state={workspaceHash:"workspace",currentPath:"authoring/current.yaml",authorModel:{},step:1};
  const getElement=id=>{
    if(!elements.has(id))elements.set(id,new Element());
    return elements.get(id);
  };
  const context={
    console,
    Date,
    TextEncoder,
    AbortController,
    localStorage:storage,
    state,
    window:null,
    document:{
      body:{insertAdjacentHTML(){}},
      querySelectorAll(){return []},
      createElement(){return new Element()}
    },
    $(id){return getElement(id)},
    clone(value){return JSON.parse(JSON.stringify(value))},
    key(){return "test-key"},
    h(value){return String(value??"").replaceAll("&","&amp;").replaceAll("<","&lt;").replaceAll(">","&gt;").replaceAll('"',"&quot;")},
    domainEntry(){return true},
    subdomainEntry(){return true},
    toast(message){toasts.push(String(message))},
    confirm(){return true},
    renderAll(){},
    refreshDirty(){},
    renderDraftBusy(){},
    handleError(error){throw error},
    requestJson(){throw new Error("requestJson must not be called by this test")},
    loadDocuments:async()=>{},
    openDocument:async()=>{},
    applyDraftToAuthorModel(){}
  };
  context.window=context;
  vm.runInNewContext(localDraftsSource,context,{filename:"studio-local-drafts.js"});
  context.draftStorage=storage;
  vm.runInNewContext(draftSource+"\n globalThis.__draftTest={draftState,draftPayload,draftPersist,draftRestoreLocal,draftFilteredCandidates,renderDraftResults,draftSelectCandidate,draftMigratePayload,draftResetVisibleFields,draftApplyAdultGate,draftQuickFields,draftSourceEdited};",context,{filename:"studio-draft.js"});
  return {context,elements,getElement,storage,toasts,api:context.__draftTest};
}

function candidate(id,title,fact){
  return {
    candidate_id:id,
    metadata:{title,summary:`摘要 ${id}`,domain:"politics"},
    facts:[{id:`fact-${id}`,text:fact,reviewStatus:"pending"}],
    expressions:[],
    reasonCodes:["topic_boundary"],
    sourceSpans:[{locator:`source/${id}`,quote:fact,verification_status:"verified"}],
    evidenceCount:1,
    risk:"green"
  };
}

function savePayload(harness,payload){
  const store=new harness.context.AwakeLocalDrafts.LocalDraftStore({
    storage:harness.storage,
    prefix:"awake.worldbook.studio.reference-draft.v1"
  });
  const context={workspaceHash:"workspace",documentPath:"authoring/current.yaml",kind:"reference"};
  assert.equal(store.save(context,payload,7).status,"saved");
}

function run(name,action){
  action();
  process.stdout.write(`PASS ${name}\n`);
}

run("reload restores draft content, multi-selection, filter and detail",()=>{
  const harness=makeHarness();
  const candidates=[candidate("candidate-0001","第一条","第一条事实"),candidate("candidate-0999","目标条目","目标事实")];
  savePayload(harness,{
    sourceName:"参考资料",
    sourceText:"原始资料",
    stage:"metadata",
    providerId:"local",
    perspectives:"普通平民",
    facts:[],
    metadata:{title:"恢复标题",summary:"恢复摘要",domain:"politics"},
    expressions:[],
    candidates,
    candidateFilter:"目标条目",
    candidateDetailId:"candidate-0999",
    selectedCandidateId:"candidate-0999",
    selectedCandidateIds:["candidate-0001","candidate-0999"]
  });
  harness.api.draftRestoreLocal();
  assert.equal(harness.api.draftState.sourceText,"原始资料");
  assert.equal(harness.api.draftState.stage,"metadata");
  assert.deepEqual(Array.from(harness.api.draftState.selectedCandidateIds),["candidate-0001","candidate-0999"]);
  assert.equal(harness.api.draftState.candidateFilter,"目标条目");
  assert.equal(harness.api.draftState.candidateDetailId,"candidate-0999");
  assert.equal(harness.getElement("draftCandidateFilter").value,"目标条目");
  assert.match(harness.getElement("draftCandidatesResults").innerHTML,/candidate-0999/);
  assert.match(harness.getElement("draftCandidatesResults").innerHTML,/checked/);
  assert.match(harness.getElement("draftCandidateDetail").innerHTML,/目标条目/);
  assert.ok(harness.toasts.some(message=>message.includes("已恢复参考资料草稿")));
});

run("1000 candidates filter, detail and stable selection survive rerender",()=>{
  const harness=makeHarness();
  harness.api.draftState.candidates=Array.from({length:1000},(_,index)=>{
    const id=`candidate-${String(index).padStart(4,"0")}`;
    return candidate(id,index===999?"边界目标 0999":`知识条目 ${String(index).padStart(4,"0")}`,index===999?"边界目标事实":"普通事实");
  });
  harness.api.draftState.selectedCandidateIds=["candidate-0001","candidate-0999"];
  harness.api.draftState.selectedCandidateId="candidate-0999";
  harness.api.draftState.candidateDetailId="candidate-0999";
  harness.api.draftState.candidateFilter="边界目标 0999";
  assert.equal(harness.api.draftFilteredCandidates().length,1);
  harness.api.renderDraftResults();
  const filteredHtml=harness.getElement("draftCandidatesResults").innerHTML;
  assert.match(filteredHtml,/显示 1\/1000 条/);
  assert.match(filteredHtml,/data-draft-candidate-check="candidate-0999" checked/);
  assert.match(harness.getElement("draftCandidateDetail").innerHTML,/边界目标事实/);
  assert.match(harness.getElement("draftCandidateDetail").innerHTML,/source\/candidate-0999/);
  harness.api.draftState.candidateFilter="";
  harness.api.renderDraftResults();
  const allHtml=harness.getElement("draftCandidatesResults").innerHTML;
  assert.match(allHtml,/显示 1000\/1000 条/);
  assert.match(allHtml,/data-draft-candidate-check="candidate-0001" checked/);
  assert.match(allHtml,/data-draft-candidate-check="candidate-0999" checked/);
  assert.ok(allHtml.indexOf("candidate-0001")<allHtml.indexOf("candidate-0999"),"candidate order must remain stable");
assert.match(harness.getElement("draftCandidateDetail").innerHTML,/边界目标 0999/);
 });

{
  const harness=makeHarness();
  const migrated=harness.api.draftMigratePayload({sourceText:"旧资料",perspectives:"普通平民\n商人",facts:[]});
  assert.equal(migrated.schemaVersion,"awake.worldbook.studio.reference-draft.v2");
  assert.deepEqual(Array.from(migrated.quickAuthoring.requestedAudience),[]);
assert.deepEqual(Array.from(migrated.quickAuthoring.mustNotInvent),[]);
  assert.equal(migrated.quickAuthoring.adultConfirmed,false);
  assert.equal(harness.api.draftMigratePayload({schemaVersion:"future.v9"}),null);
 }

run("new draft reset clears stale mode, fields, candidates and adult confirmation",()=>{
  const harness=makeHarness();
  harness.api.draftState.mode="legacy_staged";
  harness.getElement("draftMode").value="legacy_staged";
  harness.getElement("draftSourceText").value="旧资料";
  harness.getElement("draftAuthoringGoal").value="旧目标";
  harness.getElement("draftCandidatesResults").innerHTML="旧候选";
  harness.getElement("draftContentTier").value="adult_optional";
  harness.getElement("draftAdultConfirmed").checked=true;
  harness.api.draftResetVisibleFields();
  assert.equal(harness.getElement("draftMode").value,"quick_authoring");
  assert.equal(harness.getElement("draftSourceText").value,"");
  assert.equal(harness.getElement("draftAuthoringGoal").value,"");
  assert.equal(harness.getElement("draftCandidatesResults").innerHTML,"");
  assert.equal(harness.getElement("draftContentTier").value,"");
  assert.equal(harness.getElement("draftAdultConfirmed").checked,false);
});

run("adult gate state follows selected content tier",()=>{
  const harness=makeHarness();
  harness.getElement("draftContentTier").value="adult_optional";
  harness.getElement("draftAdultConfirmed").checked=true;
  harness.api.draftApplyAdultGate();
  assert.equal(harness.api.draftState.adultConfirmed,true);
  assert.equal(harness.api.draftQuickFields().adultConfirmed,true);
  harness.getElement("draftContentTier").value="base";
  harness.api.draftApplyAdultGate();
  assert.equal(harness.api.draftState.adultConfirmed,false);
  assert.equal(harness.getElement("draftAdultConfirmed").checked,false);
});

run("editing the source clears the previous adult confirmation",()=>{
  const harness=makeHarness();
  harness.api.draftState.adultConfirmed=true;
  harness.getElement("draftAdultConfirmed").checked=true;
  harness.api.draftSourceEdited();
  assert.equal(harness.api.draftState.adultConfirmed,false);
  assert.equal(harness.getElement("draftAdultConfirmed").checked,false);
});

const source=fs.readFileSync(path.join(webRoot,"studio-draft.js"),"utf8");
assert(source.includes("draftEnsureCandidatePanel();\n    wireDraftDialog()"),"Quick candidate panel must exist before candidate event binding");
assert(source.includes("draftEnsureCandidatePanel();\n  wireDraftDialog()"),"legacy candidate panel must exist before candidate event binding");
assert(source.includes("selectedCandidateIds:clone(draftState.selectedCandidateIds)"),"local draft payload must persist multi-selection");
assert(source.includes("draftState.candidateFilter"),"candidate filter must be part of draft state");
assert(source.includes("draftCandidatesResults").valueOf()&&source.includes("draftResetVisibleFields"),"opening a draft must clear stale candidate results");
assert(source.includes("adultConfirmed"),"draft state must persist adult confirmation");
assert(source.includes("provider.hidden=false"),"Quick Authoring must keep provider choice available in advanced settings");
process.stdout.write("PASS: draft DOM/state harness (5/5)\n");
