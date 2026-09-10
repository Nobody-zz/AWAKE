"use strict";

const assert=require("node:assert/strict");
const fs=require("node:fs");
const path=require("node:path");
const vm=require("node:vm");

const studioRoot=path.resolve(__dirname,"..","..");
const webRoot=path.join(studioRoot,"src","Awake.WorldbookStudio.Web","wwwroot");
const sessionSource=fs.readFileSync(path.join(webRoot,"studio-editor-session.js"),"utf8");
const html=fs.readFileSync(path.join(webRoot,"index.html"),"utf8");
const context={console,AbortController,window:null};
context.window=context;
vm.runInNewContext(sessionSource,context,{filename:"studio-editor-session.js"});
const Session=context.AwakeEditorSession;
let passed=0;
let total=0;

function run(name,action){
  total++;
  action();
  passed++;
  process.stdout.write("PASS "+name+"\n");
}

function assertProjectionEqual(left,right,message){
  assert.equal(Session.stableStringify(Session.normalizeAuthorProjection(left)),Session.stableStringify(Session.normalizeAuthorProjection(right)),message);
}

function rawReadback(editorDocument,content="yaml"){
  return {path:editorDocument.path,content,report:{inputHash:editorDocument.sourceHash},document:{revision:editorDocument.revision}};
}

function makeModel(overrides={}){
  return {
    title:"档案",
    domain:"politics",
    subdomain:"council",
    relatedDomains:["culture"],
    status:"needs_review",
    contentTier:"base",
    summary:"摘要",
    era:{preset:"current",certainty:"exact",startYear:null,endYear:null},
    assertions:[{
      kind:"fact",
      text:"事实",
      expressions:[{
        layer:"summary",
        text:"表达",
        grants:[{profileId:"profile.commoner",scope:"local",minDetail:"summary",conditions:{}}],
        denies:[],
        fallbackReferrals:["referral.tavernkeeper"]
      }]
    }],
    ...overrides
  };
}

function makeController(){
  let sequence=0;
  return new Session.Controller({tokenFactory:()=>String(++sequence)});
}

function baseline(model=makeModel(),sourceHash="hash-old",revision=1){
  return {editorProjection:Session.normalizeAuthorProjection(model),sourceHash,revision};
}

run("D1-D3 ignore projection noise and equivalent empty values",()=>{
  const base=makeModel({subdomain:"",relatedDomains:[]});
  const noisy=JSON.parse(JSON.stringify(base));
  noisy.documentId="doc.internal";
  noisy.revision=9;
  noisy.clientKey="client-noise";
  noisy.label="噪声";
  noisy.assertions[0].expressions[0].grants[0].profileLabel="普通平民";
  noisy.assertions[0].expressions[0].grants[0].valid=true;
  noisy.assertions[0].expressions[0].grants[0].key="rule-noise";
  noisy.assertions[0].expressions[0].fallbackReferrals=[{id:"referral.tavernkeeper",label:"酒馆老板",valid:true}];
  delete noisy.relatedDomains;
  delete noisy.assertions[0].expressions[0].grants[0].conditions;
  noisy.subdomain="";
  noisy.era={preset:"current",certainty:"exact",startYear:"",endYear:null};
  assertProjectionEqual(base,noisy,"UI-only fields and equivalent defaults must not become dirty");
});

run("D4-D6 preserve meaningful content, order and conditions",()=>{
  const base=makeModel();
  const changed=JSON.parse(JSON.stringify(base));
  changed.assertions[0].text="事实已改";
  assert.notEqual(Session.projectionKey(base),Session.projectionKey(changed),"fact text must become dirty");
  const reordered=JSON.parse(JSON.stringify(base));
  reordered.assertions.push({kind:"fact",text:"第二条",expressions:[]});
  const swapped=JSON.parse(JSON.stringify(reordered));
  swapped.assertions.reverse();
  assert.notEqual(Session.projectionKey(reordered),Session.projectionKey(swapped),"array order must remain meaningful");
  const conditional=JSON.parse(JSON.stringify(base));
  conditional.assertions[0].expressions[0].grants[0].conditions={min_age:30};
  assert.notEqual(Session.projectionKey(base),Session.projectionKey(conditional),"permission conditions must remain meaningful");
});

run("old open responses cannot write after A to B to A",()=>{
  const controller=makeController();
  const firstA=controller.beginOpen("authoring/a.yaml");
  const b=controller.beginOpen("authoring/b.yaml");
  const secondA=controller.beginOpen("authoring/a.yaml");
  assert.equal(controller.isCurrentOpen(firstA),false,"first A response must be stale");
  assert.equal(controller.isCurrentOpen(b),false,"B response must be stale after second A");
  assert.equal(controller.isCurrentOpen(secondA),true,"second A response must be current");
  assert.equal(controller.completeOpen(secondA,"authoring/a.yaml",baseline()),true,"current A should commit");
});

run("saving rejects duplicate saves and captures a deep snapshot",()=>{
  const controller=makeController();
  const model=makeModel();
  controller.startDocument("authoring/a.yaml","author",baseline(model));
  const operation=controller.beginSave("authoring/a.yaml",model,controller.savedBaseline,{path:"authoring/a.yaml",model});
  assert.ok(operation,"first save should start");
  assert.equal(controller.beginSave("authoring/a.yaml",model,controller.savedBaseline,{}),null,"second save must not start");
  model.title="changed after capture";
  assert.equal(operation.context.capturedModel.title,"档案","save request must keep a deep snapshot");
});

run("readback must match path, hash and revision",()=>{
  const editorDocument={path:"authoring/a.yaml",sourceHash:"HASH-A",revision:2};
  assert.equal(Session.readbackMatchesEditorDocument(editorDocument,rawReadback(editorDocument)),true,"matching readback should be accepted");
  assert.equal(Session.readbackMatchesEditorDocument(editorDocument,rawReadback({...editorDocument,sourceHash:"HASH-B"})),false,"hash mismatch must be rejected");
  assert.equal(Session.readbackMatchesEditorDocument(editorDocument,rawReadback({...editorDocument,revision:3})),false,"revision mismatch must be rejected");
  assert.equal(Session.readbackMatchesEditorDocument(editorDocument,{...rawReadback(editorDocument),path:"authoring/b.yaml"}),false,"path mismatch must be rejected");
});

run("saving while editing updates only the saved baseline",()=>{
  const controller=makeController();
  const model=makeModel();
  controller.startDocument("authoring/a.yaml","author",baseline(model));
  const operation=controller.beginSave("authoring/a.yaml",model,controller.savedBaseline,{path:"authoring/a.yaml",model});
  controller.markEdit();
  const savedModel=makeModel({title:"已保存版本"});
  const result=controller.commitSave(operation,{path:"authoring/a.yaml",model:savedModel,sourceHash:"hash-new",revision:2});
  assert.equal(result.keepCurrentModel,true,"newer input must remain in the form");
  assert.equal(controller.savedBaseline.revision,2,"revision must update atomically");
  assert.equal(controller.savedBaseline.sourceHash,"hash-new","hash must update atomically");
  assert.equal(controller.saveStatus,"idle","successful save must unlock the session");
  const staleResult=controller.beginSave("authoring/a.yaml",makeModel({title:"再次修改"}),controller.savedBaseline,{});
  assert.equal(controller.commitSave(staleResult,{path:"authoring/a.yaml",model:makeModel({title:"再次修改"}),sourceHash:"hash-new",revision:2}),null,"a save result that does not advance the version must not be accepted");
});

run("failure mapping preserves the form and locks only conflicts",()=>{
  const controller=makeController();
  const model=makeModel();
  const oldBaseline=baseline(model);
  controller.startDocument("authoring/a.yaml","author",oldBaseline);
  const failed=controller.beginSave("authoring/a.yaml",model,oldBaseline,{});
  assert.equal(controller.handleSaveFailure(failed,{status:422}),"failed","400/422 should be retryable failures");
  assert.equal(controller.savedBaseline.revision,1,"failed save must keep the baseline");
  const conflict=controller.beginSave("authoring/a.yaml",model,oldBaseline,{});
  assert.ok(conflict,"failed state should allow a deliberate retry");
  assert.equal(controller.handleSaveFailure(conflict,{status:409}),"conflict","CAS conflict should be explicit");
  assert.equal(controller.beginOpen("authoring/b.yaml").accepted,false,"conflict must block switching");
});

run("pending result rejects a different projection",()=>{
  const controller=makeController();
  const oldModel=makeModel();
  controller.startDocument("authoring/a.yaml","author",baseline(oldModel));
  const operation=controller.beginSave("authoring/a.yaml",oldModel,controller.savedBaseline,{});
  controller.markPending(operation);
  const check=controller.beginPendingCheck();
  assert.ok(check,"pending state should allow one result check");
  const differentModel=makeModel({title:"外部版本"});
  const decision=controller.resolvePending(check,{path:"authoring/a.yaml",model:differentModel,sourceHash:"hash-new",revision:2});
  assert.equal(decision.status,"conflict","a result that differs from the captured POST must not be guessed as success");
});

run("pending result confirms only the exact captured POST projection",()=>{
  const controller=makeController();
  const oldModel=makeModel();
  controller.startDocument("authoring/a.yaml","author",baseline(oldModel));
  const newModel=makeModel({title:"已提交"});
  const operation=controller.beginSave("authoring/a.yaml",newModel,controller.savedBaseline,{});
  controller.markPending(operation);
  const check=controller.beginPendingCheck();
  const decision=controller.resolvePending(check,{path:"authoring/a.yaml",model:newModel,sourceHash:"hash-new",revision:2});
  assert.equal(decision.status,"confirmed","exact new projection/hash/revision should confirm submission");
  assert.equal(controller.savedBaseline.revision,2,"confirmed pending result must update the complete baseline");
});

run("pending result identifies exact old baseline as not submitted",()=>{
  const controller=makeController();
  const oldModel=makeModel();
  const oldBaseline=baseline(oldModel);
  controller.startDocument("authoring/a.yaml","author",oldBaseline);
  const operation=controller.beginSave("authoring/a.yaml",makeModel({title:"本次修改"}),oldBaseline,{});
  controller.markPending(operation);
  const check=controller.beginPendingCheck();
  const decision=controller.resolvePending(check,{path:"authoring/a.yaml",model:oldModel,sourceHash:"hash-old",revision:1});
  assert.equal(decision.status,"not-submitted","exact old triple must be reported as not submitted");
  assert.equal(controller.saveStatus,"failed","not submitted should allow a deliberate retry");
});

run("pending partial change becomes conflict and can only be cleared by forced reload",()=>{
  const controller=makeController();
  const oldModel=makeModel();
  controller.startDocument("authoring/a.yaml","author",baseline(oldModel));
  const operation=controller.beginSave("authoring/a.yaml",makeModel({title:"本次修改"}),controller.savedBaseline,{});
  controller.markPending(operation);
  const check=controller.beginPendingCheck();
  const decision=controller.resolvePending(check,{path:"authoring/a.yaml",model:makeModel({title:"外部修改"}),sourceHash:"hash-external",revision:2});
  assert.equal(decision.status,"conflict","partial result must not be adopted");
  assert.equal(controller.beginOpen("authoring/b.yaml").accepted,false,"conflict must block normal switching");
  const reload=controller.beginOpen("authoring/a.yaml",{force:true});
  assert.ok(reload&&reload.requestToken,"forced reload should be the explicit conflict escape hatch");
});

run("late save responses cannot unlock a newer pending check",()=>{
  const controller=makeController();
  const model=makeModel();
  controller.startDocument("authoring/a.yaml","author",baseline(model));
  const save=controller.beginSave("authoring/a.yaml",model,controller.savedBaseline,{});
  controller.markPending(save);
  const check=controller.beginPendingCheck();
  assert.equal(controller.handleSaveFailure(save,{status:409}),null,"late POST failure must be stale");
  assert.equal(controller.isCurrentCheck(check),true,"late POST response must not replace the result check");
});

assert(html.includes('<script src="studio-editor-session.js"></script>'),"page must load the session coordinator before inline code");
assert(html.includes('id="checkSaveButton"'),"page must expose the pending result check action");
assert(html.includes('id="reloadFromDiskButton"'),"page must expose the conflict reload action");
assert(!html.includes('JSON.stringify(state.authorModel)!==state.savedAuthor'),"raw author JSON must not remain the dirty authority");
assert(html.includes("state.dirty&&!confirm(\"当前档案有未保存修改。继续新建会放弃这些修改，确定继续吗？\")"),"new document must confirm before discarding dirty edits");
assert(html.includes("!AwakeEditorSession.readbackMatchesEditorDocument(snapshot,raw)"),"open must reject mixed-version readback");
assert(html.includes("!AwakeEditorSession.readbackMatchesEditorDocument(editorDocument,raw)"),"save and pending check must reject mixed-version readback");

process.stdout.write("PASS: editor session harness ("+passed+"/"+total+")\n");
