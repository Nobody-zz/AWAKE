"use strict";

const assert=require("node:assert/strict");
const fs=require("node:fs");
const path=require("node:path");

const studioRoot=path.resolve(__dirname,"..","..");
const webRoot=path.join(studioRoot,"src","Awake.WorldbookStudio.Web","wwwroot");
const html=fs.readFileSync(path.join(webRoot,"index.html"),"utf8");
const ai=fs.readFileSync(path.join(webRoot,"studio-ai.js"),"utf8");
const authoringUx=fs.readFileSync(path.join(webRoot,"studio-authoring-ux.js"),"utf8");
const taxonomyPath=path.resolve(studioRoot,"..","..","docs","worldbook-studio-plan","knowledge-taxonomy.v1.json");
const taxonomy=JSON.parse(fs.readFileSync(taxonomyPath,"utf8"));

function count(source,pattern){return (source.match(pattern)||[]).length}

assert.equal(count(ai,/function renderEnhancedSuggestions\(\)/g),1,"AI suggestions should have one authoritative renderer");
assert.equal(ai.includes("baseApplySuggestion"),false,"obsolete apply wrapper must not remain");
assert(ai.includes("data.canApplyToAuthorMode&&data.editorDocument?.model"),"AI apply must recognize author-mode projections");
assert(ai.includes("state.mode=\"author\""),"supported suggestions must switch back to author mode");
assert(ai.includes("state.aiSuggestions=[]"),"applying a buffered candidate must clear stale suggestions");
assert(ai.includes("state.aiBufferId=\"\""),"applying a buffered candidate must invalidate the AI buffer");
assert(ai.includes("state.aiScope=null"),"applying a buffered candidate must clear the old scope");
const draft=fs.readFileSync(path.join(webRoot,"studio-draft.js"),"utf8");
assert(draft.includes("segmentation_reason_codes"),"draft candidates must preserve segmentation reasons");
assert(draft.includes("sourceSpans"),"draft candidates must preserve source spans");
assert(draft.includes("targetSpans"),"draft candidates must preserve first-class target spans");
assert(draft.includes("draftAuthoringGoal"),"Quick Authoring must expose an authoring goal field");
assert(draft.includes("draftUserInstruction"),"Quick Authoring must expose a simple instruction field");
assert(draft.includes("mustNotInvent"),"Quick Authoring must expose must-not-invent constraints");
assert(draft.includes("requestedContentTier"),"Quick Authoring must expose an explicit content tier");
assert(draft.includes("draftEntryKind"),"Quick Authoring must expose a user-facing entry kind");
assert(draft.includes("draftFacadeSummary"),"Quick Authoring must expose a user-facing workflow summary");
assert(draft.includes("draftFlowStatus"),"Quick Authoring must expose a user-facing generation status");
assert(draft.includes("draftAdvancedOptions"),"Quick Authoring must keep advanced options collapsible");
assert(draft.includes("整理资料成世界书草稿"),"Quick Authoring must use a user-facing entry title");
assert(draft.includes("查看来源"),"Quick Authoring must expose source traceability in user-facing results");
assert(draft.includes("需要你确认"),"Quick Authoring must expose actionable review wording");
assert(draft.includes("draftRenderDiagnostics"),"draft UI must render warnings and unresolved diagnostics");
assert(draft.includes("draftMigratePayload"),"draft UI must migrate legacy local payloads");
assert(draft.includes("draftGenerateLegacyFacts"),"legacy staged facts must use a dedicated handler");
assert(draft.includes("requestedPerspectives:[]"),"Quick Authoring must default to no NPC perspectives");
assert(draft.includes("draftGenerateQuickCandidate"),"Quick Authoring must use a distinct complete-generation handler");
assert(draft.includes("高级候选审查"),"candidate merge/split actions must be placed behind advanced review wording");
assert(draft.includes("function draftQuickMarkup"),"Quick Authoring must have an independent view template");
assert(draft.includes("function draftLegacyMarkup"),"legacy staged authoring must retain an independent view template");
assert(draft.includes("function draftEnsureLegacyFields"),"legacy staged authoring must provide its own compatibility fields");
assert(draft.includes('data-draft-view="quick"'),"Quick Authoring view must identify its own UI state");
assert(draft.includes("draftRestoreServer"),"draft UI must reconnect persisted server Drafts");
assert(draft.includes("serverSourceContentHash"),"draft UI must bind server continuation to source hash");
assert(draft.includes("draftLocalTaskId"),"draft UI must isolate local tasks per browser tab");
assert(draft.includes("draftPersistNow"),"draft UI must flush local state on page leave");
assert(draft.includes("分割理由"),"draft UI must display segmentation reasons");
assert(draft.includes("reviewProjection"),"draft UI must consume risk review projection");
assert(draft.includes("去重后证据"),"draft UI must show deduplicated evidence counts");
assert(draft.includes("draftReviewDecision"),"draft UI must expose review decision operations");
assert(draft.includes("data-draft-candidate-check"),"draft UI must support multi-candidate selection");
assert(ai.includes("aiStaleNotice"),"manual edits must explain why old AI suggestions disappeared");
assert(ai.includes("document.addEventListener(\"input\""),"manual text edits must invalidate stale AI suggestions");
assert(ai.includes("document.addEventListener(\"change\""),"manual choice edits must invalidate stale AI suggestions");
assert(ai.includes("requestAnimationFrame"),"applying a candidate must focus the target after rerender");

assert(authoringUx.includes("domain.examples"),"taxonomy guidance must include domain examples");
assert(authoringUx.includes("sub?.examples"),"taxonomy guidance must include subdomain examples");
assert(authoringUx.includes("domain.conflictHints"),"taxonomy guidance must include common mistakes");
assert(authoringUx.includes("这份档案应该写什么"),"taxonomy guidance must explain the writing purpose in Chinese");
assert(authoringUx.includes("baseRenderBasicContent"),"basic information must retain the original author renderer");
assert(authoringUx.includes("baseRenderFactsContent"),"facts must retain the original author renderer");
assert(authoringUx.includes("请先说明希望整理什么内容"),"AI goal errors must have a clear user message");
assert(authoringUx.includes("身份视角最多填写 3 个"),"AI perspective errors must have a clear user message");

assert.equal(taxonomy.domains.length,5,"the author catalog must keep the five main knowledge domains");
for(const domain of taxonomy.domains){
  assert(domain.label?.["zh-CN"],`domain ${domain.id} must have a Chinese label`);
  assert(domain.help?.["zh-CN"],`domain ${domain.id} must explain what to write`);
  assert(domain.examples?.["zh-CN"]?.length,`domain ${domain.id} must have writing examples`);
  assert(domain.conflict_hints?.["zh-CN"]?.length,`domain ${domain.id} must have boundary reminders`);
  assert(domain.subdomains?.length,`domain ${domain.id} must have subdomains`);
  for(const subdomain of domain.subdomains){
    assert(subdomain.label?.["zh-CN"],`subdomain ${domain.id}.${subdomain.id} must have a Chinese label`);
    assert(subdomain.help?.["zh-CN"],`subdomain ${domain.id}.${subdomain.id} must explain what to write`);
    assert(subdomain.examples?.["zh-CN"]?.length,`subdomain ${domain.id}.${subdomain.id} must have writing examples`);
  }
}

assert(html.includes('id="doc-title" data-doc-field="title"'),"author mode must expose the title field");
assert(html.includes('id="doc-summary" data-doc-field="summary"'),"author mode must expose the summary field");
assert(html.includes('id="fact-${i}-text"'),"fact cards must expose stable focus targets");
assert(html.includes("从资料生成待审核档案"),"the primary AI entry must describe Quick Authoring output");
assert(html.includes("检查当前档案"),"the assistant panel must describe inspection rather than creation");
assert(html.includes("批量生成多个待审核档案"),"the batch entry must describe batch output");

process.stdout.write("PASS: editor content static checks\n");
