const assert=require("node:assert/strict");
const fs=require("node:fs");
const path=require("node:path");

const studioRoot=path.resolve(__dirname,"..","..");
const webRoot=path.join(studioRoot,"src","Awake.WorldbookStudio.Web");
const index=fs.readFileSync(path.join(webRoot,"wwwroot","index.html"),"utf8");
const safety=fs.readFileSync(path.join(webRoot,"wwwroot","studio-editor-safety.js"),"utf8");
const ai=fs.readFileSync(path.join(webRoot,"wwwroot","studio-ai.js"),"utf8");
const draft=fs.readFileSync(path.join(webRoot,"wwwroot","studio-draft.js"),"utf8");
const batch=fs.readFileSync(path.join(webRoot,"wwwroot","studio-batch.js"),"utf8");
const program=fs.readFileSync(path.join(webRoot,"Program.cs"),"utf8");
const batchEndpoints=fs.readFileSync(path.join(webRoot,"BatchEndpoints.cs"),"utf8");
const draftEndpoints=fs.readFileSync(path.join(webRoot,"AuthoringDraftEndpoints.cs"),"utf8");

assert.equal((index.match(/id="authorizeCompileButton"/g)||[]).length,1,"compile authorization button must have one stable DOM id");
for(const legacy of ["/api/save-authoring","/api/save-authoring/check","/api/save-editor-document","/api/document/new","/api/ai/apply","/api/ai/reject","/api/compile","/api/export"]){
  assert.equal(safety.includes(legacy)||ai.includes(legacy)||index.includes(legacy),false,`customer UI must not call retired route ${legacy}`);
}
assert(safety.includes("/api/authoring/save-authoring"),"advanced save must use the authoring route");
assert(safety.includes("/api/authoring/save-editor-document")||index.includes("/api/authoring/save-editor-document"),"author save must use the authoring route");
assert(safety.includes("/api/authoring/compile"),"compile must use the authority-backed route");
assert(safety.includes("/api/authoring/export-staging"),"export must use the authority-backed staging route");
assert(/async function validateSafe\(\)[\s\S]*const tier=state\.authorModel\?\.contentTier\|\|\"base\"/.test(safety),"validate must resolve the content tier locally");
assert(ai.includes("/api/ai/authoring/apply"),"ordinary AI apply must use a non-retired route");
assert(ai.includes("/api/ai/authoring/reject")||index.includes("/api/ai/authoring/reject"),"ordinary AI reject must use a non-retired route");
assert(draft.includes("/api/ai/authoring/draft/"),"reference draft workflow must use a non-retired route");
assert(batch.includes("/api/ai/authoring/batch"),"batch workflow must use a non-retired route");
assert(program.includes("/api/authoring/save-authoring"),"web must expose the authoring save route");
assert(program.includes("/api/authoring/document/new"),"web must expose the authoring create route");
assert(program.includes("/api/authoring/compile"),"web must expose the authority-backed compile route");
assert(program.includes("/api/authoring/export-staging"),"web must expose the authority-backed export route");
assert(program.includes("/api/ai/authoring/apply"),"web must expose the authoring AI apply route");
assert(program.includes("/api/ai/authoring/reject"),"web must expose the authoring AI reject route");
assert(batchEndpoints.includes("string routePrefix"),"batch endpoints must be reusable under the authoring prefix");
assert(draftEndpoints.includes("string routePrefix"),"draft endpoints must be reusable under the authoring prefix");
assert(index.includes("Provider"),"customer UI must expose provider configuration/status");
assert(batch.includes("重新确认读取范围"),"batch recovery must require explicit consent again");
assert(safety.includes("保存结果暂时无法确认"),"save unknown-result recovery must remain visible");
process.stdout.write("PASS: customer closure route harness\n");
