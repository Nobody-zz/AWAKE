"use strict";

const assert=require("node:assert/strict");
const fs=require("node:fs");
const path=require("node:path");
const vm=require("node:vm");

const studioRoot=path.resolve(__dirname,"..","..");
const webRoot=path.join(studioRoot,"src","Awake.WorldbookStudio.Web","wwwroot");
const context={console,AbortController,window:null};
context.window=context;
vm.runInNewContext(fs.readFileSync(path.join(webRoot,"studio-editor-session.js"),"utf8"),context,{filename:"studio-editor-session.js"});
vm.runInNewContext(fs.readFileSync(path.join(webRoot,"studio-local-drafts.js"),"utf8"),context,{filename:"studio-local-drafts.js"});
const Session=context.AwakeEditorSession;
const LocalDrafts=context.AwakeLocalDrafts;
let passed=0;
let total=0;

function run(name,action){total++;action();passed++;process.stdout.write("PASS "+name+"\n")}
function makeController(){let sequence=0;return new Session.Controller({tokenFactory:()=>String(++sequence)})}
function makeBaseline(){return {sourceHash:"hash-old",revision:1}}

run("raw save captures source hash and revision",()=>{
  const controller=makeController();
  controller.startDocument("authoring/a.yaml","advanced",makeBaseline());
  const operation=controller.beginRawSave("authoring/a.yaml","new: content",makeBaseline(),{path:"authoring/a.yaml",content:"new: content",sourceHash:"hash-old",revision:1});
  assert.ok(operation);
  assert.equal(operation.context.capturedSourceHash,"hash-old");
  assert.equal(operation.context.capturedRevision,1);
  assert.equal(controller.rawSaveStatus,"saving");
});

run("raw pending accepts only the returned raw content hash",()=>{
  const controller=makeController();
  controller.startDocument("authoring/a.yaml","advanced",makeBaseline());
  const operation=controller.beginRawSave("authoring/a.yaml","new: content",makeBaseline(),{});
  controller.markRawPending(operation);
  const check=controller.beginRawPendingCheck();
  const decision=controller.resolveRawPending(check,{path:"authoring/a.yaml",content:"new: content",hash:"hash-new",revision:2,expectedContentHash:"hash-new"});
  assert.equal(decision.status,"confirmed");
  assert.equal(controller.rawSaveStatus,"idle");
});

run("raw pending rejects a different returned raw hash",()=>{
  const controller=makeController();
  controller.startDocument("authoring/a.yaml","advanced",makeBaseline());
  const operation=controller.beginRawSave("authoring/a.yaml","new: content",makeBaseline(),{});
  controller.markRawPending(operation);
  const check=controller.beginRawPendingCheck();
  const decision=controller.resolveRawPending(check,{path:"authoring/a.yaml",content:"other: content",hash:"hash-other",revision:2,expectedContentHash:"hash-new"});
  assert.equal(decision.status,"conflict");
  assert.equal(controller.rawSaveStatus,"conflict");
});

run("local drafts isolate paths and remove secrets",()=>{
  const values=new Map();
  const storage={getItem:key=>values.get(key)||null,setItem:(key,value)=>values.set(key,value),removeItem:key=>values.delete(key)};
  const store=new LocalDrafts.LocalDraftStore({storage,prefix:"test",maxAgeMs:100000});
  const first={workspaceHash:"workspace-a",documentPath:"authoring/a.yaml",kind:"editor",sourceHash:"hash-old",revision:1};
  const second={workspaceHash:"workspace-a",documentPath:"authoring/b.yaml",kind:"editor",sourceHash:"hash-old",revision:1};
  const parallel={workspaceHash:"workspace-a",documentPath:"authoring/a.yaml",kind:"editor",taskId:"task-other",sourceHash:"hash-old",revision:1};
  store.save(first,{advancedContent:"draft",apiKey:"should-not-persist",nested:{csrfToken:"also-hidden"}},3);
  assert.notEqual(store.key(first),store.key(second));
  assert.notEqual(store.key(first),store.key(parallel));
  const raw=values.get(store.key(first));
  assert.equal(raw.includes("should-not-persist"),false);
  assert.equal(raw.includes("also-hidden"),false);
  assert.equal(store.load(first,{sourceHash:"hash-old",revision:1}).status,"available");
  assert.equal(store.load(first,{sourceHash:"hash-new",revision:2}).status,"stale");
  store.clearAfterSave(first,3,4);
  assert.equal(store.load(first,{sourceHash:"hash-old",revision:1}).status,"available");
  store.clearAfterSave(first,4,4);
  assert.equal(store.load(first,{sourceHash:"hash-old",revision:1}).status,"missing");
});

const html=fs.readFileSync(path.join(webRoot,"index.html"),"utf8");
const safety=fs.readFileSync(path.join(webRoot,"studio-editor-safety.js"),"utf8");
const draft=fs.readFileSync(path.join(webRoot,"studio-draft.js"),"utf8");
assert(html.includes('<script src="studio-local-drafts.js"></script>'),"page must load local drafts before the inline app");
assert(html.includes('<script src="studio-editor-safety.js"></script>'),"page must load the safety coordinator after the inline app");
assert(html.includes("window.awakeStudioState=state"),"page must expose the app state to the safety coordinator");
assert(html.includes("error.diagnostics=Array.isArray(data.diagnostics)?data.diagnostics:[]"),"request errors must retain server diagnostics");
assert(safety.includes("/api/authoring/save-authoring/check"),"safety coordinator must expose raw save result checks");
assert(safety.includes("editorReadbackValid"),"raw saves must validate the author projection against the saved raw document");
assert(safety.includes('button?.dataset.action==="save-author"'),"author review save action must use the safety coordinator");
assert(safety.includes("root.awakeEnsureSaved=ensureSaved"),"reference creation must be able to use the shared save guard");
assert(safety.includes("refreshButton:()=>refreshSafe()"),"refresh must use the shared save guard");
assert(safety.includes("root.renderSaveStatus"),"raw status reset must restore the shared save status UI");
assert(safety.includes("previousBaseline"),"canceled or failed same-document opens must not trigger draft restoration");
assert(safety.includes("sourceHash=${encodeURIComponent(current.sourceHash)}"),"validate operation must bind the saved source hash");
assert(draft.includes("draftState.generation"),"reference drafts must fence late responses by generation");
assert(draft.includes("function draftMarkChanged"),"reference draft edits must advance the response generation");
assert(draft.includes("function draftMarkChanged(){\n  draftState.generation+=1;\n}"),"reference draft edits must not cancel an in-flight create request");
assert(draft.includes("draftState.activeRequest===request"),"reference request cleanup must not leave the busy state after edits");
assert(draft.includes("草稿在请求期间发生了变化，未自动切换"),"stale reference creation must explain that the created document was not switched");
assert(draft.includes("draftLocalStore"),"reference workflow must persist a local draft");
assert(draft.includes('window.awakeEnsureSaved("从参考资料创建新档案")'),"reference creation must guard the current document before creating");
assert(!draft.includes("openDocument(data.document.path,true)"),"reference creation must not force-discard the current document");

process.stdout.write("PASS: editor safety harness ("+passed+"/"+total+")\n");
