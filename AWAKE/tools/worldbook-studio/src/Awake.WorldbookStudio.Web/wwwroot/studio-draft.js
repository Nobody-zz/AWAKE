"use strict";

const draftState={token:"",draftId:"",serverSourceContentHash:"",attemptId:"",stage:"facts",uiStep:"input",mode:"quick_authoring",sourceName:"",sourceText:"",authoringGoal:"",userInstruction:"",requestedEntryKind:"general",requestedDomain:"",requestedSubdomain:"",requestedAudience:[],styleConstraints:[],mustPreserve:[],mustNotInvent:[],requestedContentTier:"",adultConfirmed:false,facts:[],metadata:null,expressions:[],candidates:[],warnings:[],unresolved:[],coverage:null,targetSpans:[],propositions:[],claims:[],candidateSetGenerationId:"",candidateSetSourceContentHash:"",candidateSetPacketHash:"",candidateFilter:"",candidateDetailId:"",selectedCandidateId:"",selectedCandidateIds:[],busy:false,generation:0,activeRequest:null};
const draftPayloadSchemaVersion="awake.worldbook.studio.reference-draft.v2";
const draftStagePanels={facts:"draftFactsPanel",metadata:"draftMetadataPanel",expressions:"draftExpressionsPanel"};
const draftLocalStore=new AwakeLocalDrafts.LocalDraftStore({prefix:"awake.worldbook.studio.reference-draft.v1",onResult:result=>draftReportLocalSave(result)});
const draftLocalTaskId=(()=>{
  try{
    const key="awake.worldbook.studio.reference-draft.task.v1";
    let value=window.sessionStorage?.getItem(key);
    if(!value){value="task-"+Date.now().toString(36)+"-"+Math.random().toString(36).slice(2,10);window.sessionStorage?.setItem(key,value)}
    return value;
  }catch{return "task-local-fallback"}
})();
const draftProfileFallbackLabels={"profile.villager":"普通村民","profile.townsfolk":"城镇居民","profile.commoner":"普通平民","profile.headman":"头人","profile.notable":"地方重要人物","profile.merchant":"商人","profile.tavernkeeper":"酒馆老板","profile.ransom_broker":"赎金经纪人","profile.soldier":"士兵","profile.noble":"贵族","profile.noble_high_steward":"高管理贵族","profile.anonymous":"身份未知"};
const draftDomainLabels={politics:"政治",economy:"经济",culture:"文化",war:"战争",geography:"地理"};

function draftIsPresetId(value){return typeof value==="string"&&/^(?:profile\.|awake:identity:)/i.test(value.trim())}

function draftProfileDisplay(profileId,strictPreset=false){
  const value=typeof profileId==="string"?profileId.trim():"";
  if(!value)return {label:"未指定身份",known:false,warning:"身份预设缺少登记值，请人工选择或补充自定义视角。"};
  const profile=(state.catalog?.profiles||[]).find(item=>item.value===value||item.id===value);
  const label=profile?.label||profile?.display?.["zh-CN"]||draftProfileFallbackLabels[value];
  if(label&&!draftIsPresetId(label))return {label,known:true,warning:""};
  if(strictPreset||draftIsPresetId(value))return {label:"未登记身份（需人工选择）",known:false,warning:"AI 返回了未登记的身份预设，请人工选择已登记身份或保留自定义视角。"};
  return {label:value,known:false,warning:""};
}

function draftExpressionDisplay(item){
  const perspective=draftProfileDisplay(item.perspective);
  const hasUnknownProfile=(Array.isArray(item.profileIds)?item.profileIds:[]).some(profileId=>draftProfileDisplay(profileId,true).warning);
  const warning=perspective.warning||hasUnknownProfile?"身份预设需要人工复核，请人工选择已登记身份或保留自定义视角。":"";
  return {label:perspective.label,warning};
}

function draftLegacyMarkup(){
  return `<dialog id="draftDialog" class="draft-dialog"><form id="draftForm" class="draft-body"><div class="head-row"><div><h2>从参考资料开始创建</h2><p class="muted">先把资料交给 AI 整理成草稿，再由你逐条检查。不会自动修改现有档案。</p></div><button id="draftCloseButton" type="button" class="ghost">关闭</button></div><div class="draft-note">参考资料只在本次工作室会话中使用；只有你最后保存档案时，确认过的内容才会进入作者表单。</div><div class="field full"><label for="draftSourceName">资料名称<span>可选，例如：卡拉迪亚编年史·西帝国章节</span></label><input id="draftSourceName" placeholder="给这份参考资料起一个容易认出的名字"></div><div class="field full"><label for="draftSourceText">粘贴参考资料<span>必填；也可以用下方按钮导入 UTF-8 文本文件</span></label><textarea id="draftSourceText" class="draft-source" placeholder="把游戏资料、模组文本、开发者笔记或自己的客观材料粘贴到这里……"></textarea><div class="draft-toolbar"><input id="draftSourceFile" type="file" accept=".txt,.md,.yaml,.yml,.json,text/plain,text/markdown,application/json,text/yaml"><span id="draftSourceStats" class="muted">尚未输入资料</span></div></div><div class="field"><label for="draftProvider">AI 来源<span>云端或本机 Worker 均可</span></label><select id="draftProvider"><option value="local">本机 AI Worker</option><option value="cloud">云端 AI Provider</option></select></div><div class="draft-stage"><button type="button" data-draft-stage="facts" class="active"><strong>1. 提取客观事实</strong><small>只整理资料中明确写出的内容</small></button><button type="button" data-draft-stage="metadata"><strong>2. 生成档案简介</strong><small>根据已确认事实生成标题和摘要</small></button><button type="button" data-draft-stage="expressions"><strong>3. 生成身份表达</strong><small>按人物身份生成 NPC 能说的话</small></button></div><section id="draftFactsPanel"><div class="head-row"><h3>客观事实草稿</h3><div class="draft-toolbar"><button id="draftGenerateFacts" type="button" class="primary">提取事实</button><button id="draftAcceptAllFacts" type="button" class="ghost">全部采纳</button></div></div><p class="help">每条事实都保留资料依据。带有“AI 推断”的内容不要直接当成正典，先看证据再决定。修改卡片文字后，需要再次点击“采纳”。</p><div id="draftFactsResults" class="draft-results"></div></section><section id="draftMetadataPanel" hidden><div class="head-row"><h3>档案简介草稿</h3><button id="draftGenerateMetadata" type="button" class="primary">生成简介</button></div><div class="field full"><label for="draftTitleCandidate">标题候选</label><input id="draftTitleCandidate" placeholder="生成后可以直接修改"></div><div class="field full"><label for="draftSummaryCandidate">摘要候选</label><textarea id="draftSummaryCandidate" class="draft-summary" placeholder="生成后可以直接修改"></textarea></div><div class="field"><label for="draftDomainCandidate">知识分类候选<span>请选择最合适的一类；进入作者表单后还可以继续修改二级主题</span></label><select id="draftDomainCandidate"><option value="">待选择</option><option value="politics">政治</option><option value="economy">经济</option><option value="culture">文化</option><option value="war">战争</option><option value="geography">地理</option></select></div><div id="draftMetadataInfo" class="help"></div></section><section id="draftExpressionsPanel" hidden><div class="head-row"><h3>身份视角表达草稿</h3><button id="draftGenerateExpressions" type="button" class="primary">生成身份表达</button></div><div class="field full"><label for="draftPerspectives">想生成哪些视角<span>一行一个；留空表示不限定视角，由 AI 根据资料提出。</span></label><textarea id="draftPerspectives" class="draft-perspectives"></textarea></div><div id="draftExpressionsResults" class="draft-results"></div></section><div class="dialog-actions"><button id="draftCreateDocument" type="button" class="primary" disabled>采纳草稿并进入作者表单</button></div></form></dialog>`;
}

function draftQuickMarkup(){
  return `<dialog id="draftDialog" class="draft-dialog" data-draft-view="quick"><form id="draftForm" class="draft-body"><div class="head-row"><div><h2>从资料生成待审核档案</h2><p class="muted">提供资料和整理目标，AI 会生成一份待审核草稿；不会自动修改现有档案。</p></div><button id="draftCloseButton" type="button" class="ghost">关闭</button></div><div id="draftFacadeSummary" class="draft-note"><strong>先说明边界：</strong>AI 只依据你提供的资料整理，不会替你确认正典，也不会把资料之外的内容自动补进世界书。</div><div class="draft-quick-input"><div class="field full"><label for="draftSourceName">资料名称<span>可选，方便你在本次工作区中识别资料</span></label><input id="draftSourceName" placeholder="例如：卡拉迪亚编年史·西帝国章节"></div><div class="field full"><label for="draftSourceText">参考资料<span>必填；可以粘贴文字或导入 UTF-8 文件</span></label><textarea id="draftSourceText" class="draft-source" placeholder="粘贴你希望整理的资料……"></textarea><div class="draft-toolbar"><input id="draftSourceFile" type="file" accept=".txt,.md,.yaml,.yml,.json,text/plain,text/markdown,application/json,text/yaml"><span id="draftSourceStats" class="muted">尚未输入资料</span></div></div><div class="field full"><label for="draftAuthoringGoal">我想整理什么<span>请描述最终想得到的世界知识档案，必填</span></label><input id="draftAuthoringGoal" placeholder="例如：整理西帝国继承冲突的参与方、立场和已知事实"></div><div class="field full"><label for="draftUserInstruction">还有什么特别要求<span>可选；只影响整理重点和表达方式，不能改变事实证据</span></label><textarea id="draftUserInstruction" class="draft-quick-text" placeholder="例如：保留各方不同看法，不要把流言写成确定事实。"></textarea></div><div class="field"><label for="draftContentTier">内容范围<span>必须选择；unknown 不会自动变成基础内容</span></label><select id="draftContentTier"><option value="">请选择</option><option value="base">基础内容</option><option value="adult_optional">成人拓展</option></select><div id="draftAdultGate" class="draft-adult-gate" hidden><label><input id="draftAdultConfirmed" type="checkbox">我已确认这是 18+ 成人拓展内容，并愿意在生成前承担人工审核责任。</label><p>未确认时不会生成或建档；年龄不明确的内容不能通过确认。</p></div></div></div><select id="draftMode" hidden><option value="quick_authoring">quick_authoring</option></select><select id="draftEntryKind" hidden><option value="general">general</option></select><div id="draftFlowStatus" class="draft-flow-status" aria-live="polite">准备好后点击“开始生成草稿”。</div><details id="draftAdvancedOptions" class="draft-advanced-options"><summary>进一步限定结果（可选）</summary><div id="draftAdvancedContent" class="draft-advanced-content"><p class="help">这些设置只会缩小范围或调整表达方式，不能授权 AI 增加资料中没有的事实。</p><div class="field"><label for="draftRequestedDomain">期望主分类</label><select id="draftRequestedDomain"><option value="">由 AI 提出候选</option><option value="politics">政治</option><option value="economy">经济</option><option value="culture">文化</option><option value="war">战争</option><option value="geography">地理</option></select></div><div class="field"><label for="draftRequestedSubdomain">期望二级主题<span>可选，不代表自动采纳</span></label><input id="draftRequestedSubdomain" placeholder="例如：继承与合法性"></div><div class="field full"><label for="draftRequestedAudience">目标受众<span>一行一个</span></label><textarea id="draftRequestedAudience" class="draft-quick-text" placeholder="普通玩家"></textarea></div><div class="field full"><label for="draftStyleConstraints">文风与格式约束<span>一行一个</span></label><textarea id="draftStyleConstraints" class="draft-quick-text" placeholder="简洁的中世纪编年史体"></textarea></div><div class="field"><label for="draftMustPreserve">必须保留<span>一行一个；未满足会进入待处理项</span></label><textarea id="draftMustPreserve" class="draft-quick-text" placeholder="各方对继承的不同看法"></textarea></div><div class="field"><label for="draftMustNotInvent">禁止新增<span>一行一个；例如新人物、新年份、正式 ID</span></label><textarea id="draftMustNotInvent" class="draft-quick-text" placeholder="新人物&#10;新年份&#10;正式 entity ID"></textarea></div><div class="field full"><label for="draftPerspectives">需要生成哪些 NPC 身份表达（可选）<span>不填写就不生成身份表达；最多 3 个视角，一行一个</span></label><textarea id="draftPerspectives" class="draft-perspectives" placeholder="留空表示只生成世界知识内容"></textarea></div><div class="field"><label for="draftProvider">AI 来源<span>默认使用本机 AI Worker；云端选项只在高级设置中出现</span></label><select id="draftProvider"><option value="local">本机 AI Worker</option><option value="cloud">云端 AI Provider</option></select></div><button id="draftOpenLegacyButton" type="button" class="ghost">打开兼容分阶段流程</button></div></details><section id="draftFactsPanel"><div class="head-row"><h3>生成结果</h3><button id="draftStartQuickButton" type="button" class="primary">开始生成草稿</button></div><p class="help">AI 会先整理资料、绑定来源，再生成待审核候选。生成后请查看来源、警告和待处理项。</p><div id="draftFactsResults" class="draft-results"></div></section><section id="draftMetadataPanel" hidden><div class="head-row"><h3>标题、摘要和分类建议</h3></div><div class="field full"><label for="draftTitleCandidate">标题候选</label><input id="draftTitleCandidate" placeholder="生成后可以直接修改"></div><div class="field full"><label for="draftSummaryCandidate">摘要候选</label><textarea id="draftSummaryCandidate" class="draft-summary" placeholder="生成后可以直接修改"></textarea></div><div class="field"><label for="draftDomainCandidate">知识分类候选</label><select id="draftDomainCandidate"><option value="">待选择</option><option value="politics">政治</option><option value="economy">经济</option><option value="culture">文化</option><option value="war">战争</option><option value="geography">地理</option></select></div><div id="draftMetadataInfo" class="help"></div></section><section id="draftExpressionsPanel" hidden><div class="head-row"><h3>NPC 身份表达建议</h3></div><div id="draftExpressionsResults" class="draft-results"></div></section><div class="dialog-actions"><button id="draftCreateDocument" type="button" class="primary" disabled>创建待审核档案</button></div></form></dialog>`;
}

function draftEnsureLegacyFields(){
  const form=document.querySelector("#draftForm");
  if(!form||$("draftMode"))return;
  const mode=document.createElement("select");
  mode.id="draftMode";
  mode.hidden=true;
  mode.innerHTML='<option value="legacy_staged">legacy_staged</option>';
  form.append(mode);
  const entryKind=document.createElement("select");
  entryKind.id="draftEntryKind";
  entryKind.hidden=true;
  entryKind.innerHTML='<option value="general">general</option>';
  form.append(entryKind);
  const tier=document.createElement("div");
  tier.className="field";
  tier.innerHTML='<label for="draftContentTier">内容范围<span>必须选择；成人拓展需要额外确认</span></label><select id="draftContentTier"><option value="">请选择</option><option value="base">基础内容</option><option value="adult_optional">成人拓展</option></select><div id="draftAdultGate" class="draft-adult-gate" hidden><label><input id="draftAdultConfirmed" type="checkbox">我已确认这是 18+ 成人拓展内容，并愿意在生成前承担人工审核责任。</label><p>未确认时不会建档。</p></div>';
  const stage=form.querySelector(".draft-stage");
  if(stage)stage.insertAdjacentElement("beforebegin",tier);
  else form.append(tier);
}

function draftEnsureQuickFields(){
  if($("draftMode"))return;
  const note=document.querySelector("#draftDialog .draft-note");
  if(!note)return;
  const panel=document.createElement("section");
  panel.id="draftQuickAuthoringPanel";
  panel.className="draft-quick-panel";
  panel.innerHTML='<div class="head-row"><div><h3>Quick Authoring 快速创作</h3><p class="help">简单目标会进入本次请求，并由 AI 生成完整的待审核候选；它不是高保真 Semantic Migration。</p></div><span class="chip good">仅生成 review_only 草稿</span></div><div class="draft-quick-grid"><div class="field"><label for="draftMode">创作模式</label><select id="draftMode"><option value="quick_authoring">Quick Authoring：简单提示词 → 完整候选</option><option value="legacy_staged">兼容分阶段流程</option></select></div><div class="field"><label for="draftContentTier">内容层级<span>必须明确选择，unknown 不会自动变成 base</span></label><select id="draftContentTier"><option value="">待选择</option><option value="base">base：基础内容</option><option value="adult_optional">adult_optional：成人拓展</option></select><div id="draftAdultGate" class="draft-adult-gate" hidden><label><input id="draftAdultConfirmed" type="checkbox">我已确认这是 18+ 成人拓展内容，并愿意在生成前承担人工审核责任。</label><p>未勾选时不会生成或建档；年龄不明确的内容不能通过此确认。</p></div></div><div class="field full"><label for="draftAuthoringGoal">创作目标<span>例如：整理一份关于西帝国继承冲突的政治档案</span></label><input id="draftAuthoringGoal" placeholder="你希望这份世界知识档案解决什么问题？"></div><div class="field full"><label for="draftUserInstruction">简单提示词<span>只作为不可信作者输入，不能改写系统规则、证据或审核状态</span></label><textarea id="draftUserInstruction" class="draft-quick-text" placeholder="例如：保留继承冲突和各方立场，不要把流言写成确定事实。"></textarea></div><div class="field"><label for="draftRequestedDomain">期望主分类</label><select id="draftRequestedDomain"><option value="">由 AI 提出候选</option><option value="politics">政治</option><option value="economy">经济</option><option value="culture">文化</option><option value="war">战争</option><option value="geography">地理</option></select></div><div class="field"><label for="draftRequestedSubdomain">期望二级主题<span>可选，不代表自动采纳</span></label><input id="draftRequestedSubdomain" placeholder="例如：继承与合法性"></div><div class="field full"><label for="draftRequestedAudience">目标受众<span>一行一个，例如：普通玩家、世界观作者、NPC 对话系统</span></label><textarea id="draftRequestedAudience" class="draft-quick-text" placeholder="普通玩家"></textarea></div><div class="field full"><label for="draftStyleConstraints">文风与格式约束<span>一行一个</span></label><textarea id="draftStyleConstraints" class="draft-quick-text" placeholder="简洁的中世纪编年史体"></textarea></div><div class="field"><label for="draftMustPreserve">必须保留<span>一行一个；未满足会进入 unresolved</span></label><textarea id="draftMustPreserve" class="draft-quick-text" placeholder="各方对继承的不同看法"></textarea></div><div class="field"><label for="draftMustNotInvent">禁止新增<span>一行一个；不得新增人物、年份、战争或正式 ID</span></label><textarea id="draftMustNotInvent" class="draft-quick-text" placeholder="新人物&#10;新年份&#10;正式 entity ID"></textarea></div></div></section>';
  note.insertAdjacentElement("afterend",panel);
  draftEnsureUserFacade();
}

function draftEnsureUserFacade(){
  const panel=$("draftQuickAuthoringPanel");
  if(!panel||$("draftFacadeSummary"))return;
  panel.classList.add("draft-advanced-shell");
  const dialog=document.querySelector("#draftDialog");
  const heading=dialog?.querySelector(".draft-body > .head-row h2");
  const subtitle=dialog?.querySelector(".draft-body > .head-row p");
  const note=dialog?.querySelector(".draft-note");
  if(heading)heading.textContent="整理资料成世界书草稿";
  if(subtitle)subtitle.textContent="你提供资料和目标，AI 帮你整理成一份待确认草稿；确认后才会进入世界书编辑器。";
  if(note)note.innerHTML="<strong>先说明边界：</strong>AI 只依据你提供的资料整理，不会替你确认正典，也不会把资料之外的内容自动补进世界书。";
  const perspectiveLabel=$("draftPerspectives")?.closest(".field")?.querySelector("label");
  if(perspectiveLabel)perspectiveLabel.innerHTML="需要生成哪些 NPC 身份表达（可选）<span>不填写就不生成身份表达；最多填写 3 个视角，一行一个。</span>";
  const sourceTextField=$("draftSourceText")?.closest(".field");
  const sourceNameField=$("draftSourceName")?.closest(".field");
  const providerField=$("draftProvider")?.closest(".field");
  if(sourceTextField){
    const label=sourceTextField.querySelector("label");
    if(label)label.innerHTML="参考资料<span>必填。AI 只会以这里的内容为依据；传闻、观点和历史记载会单独标记。</span>";
  }
  if(sourceNameField){
    const label=sourceNameField.querySelector("label");
    if(label)label.innerHTML="资料名称<span>可选，用来帮助你之后认出这份资料。</span>";
  }
  if(providerField){
    const label=providerField.querySelector("label");
    if(label)label.innerHTML="AI 来源<span>高级设置；本机 Worker 适合离线测试。</span>";
  }
  const header=panel.querySelector(".head-row");
  if(header){
    const title=header.querySelector("h3");
    const help=header.querySelector(".help");
    if(title)title.textContent="告诉 AI 你想整理什么";
    if(help)help.textContent="下面只需要填写目标和资料；其余限制可以留空，生成后仍会由你确认。";
  }
  const summary=document.createElement("div");
  summary.id="draftFacadeSummary";
  summary.className="draft-facade-summary";
  summary.innerHTML="<div><strong>AI 会做什么</strong><span>从资料中整理主题、事实、摘要和表达建议，并标出需要你判断的地方。</span></div><div><strong>AI 不会做什么</strong><span>不会把未知内容当成事实，不会自动发布，也不会修改已有档案。</span></div><p id=\"draftFlowStatus\" class=\"draft-flow-status\" aria-live=\"polite\">准备好后点击“开始生成草稿”。</p>";
  header?.insertAdjacentElement("afterend",summary);
  const primary=document.createElement("div");
  primary.id="draftFacadePrimary";
  primary.className="draft-facade-primary";
  summary.insertAdjacentElement("afterend",primary);
  const entryField=document.createElement("div");
  entryField.className="field";
  entryField.innerHTML='<label for="draftEntryKind">我要整理的内容</label><select id="draftEntryKind"><option value="general">一般世界知识</option><option value="person">人物</option><option value="place">地点</option><option value="event">事件</option><option value="faction">势力或家族</option><option value="institution">制度、文化或习俗</option></select>';
  primary.append(entryField);
  const startButton=document.createElement("button");
  startButton.id="draftStartQuickButton";
  startButton.type="button";
  startButton.className="primary draft-start-button";
  startButton.textContent="开始生成草稿";
  startButton.addEventListener("click",draftGenerateQuickCandidate);
  primary.append(startButton);
  const moveField=(id,target)=>{
    const field=$(id)?.closest(".field");
    if(field)target.append(field);
  };
  moveField("draftAuthoringGoal",primary);
  moveField("draftUserInstruction",primary);
  moveField("draftContentTier",primary);
  if(sourceTextField)primary.append(sourceTextField);
  const advanced=document.createElement("details");
  advanced.id="draftAdvancedOptions";
  advanced.className="draft-advanced-options";
  advanced.innerHTML='<summary>进一步限定结果（可选）</summary><div id="draftAdvancedContent" class="draft-advanced-content"><p class="help">这些设置只用于缩小范围和调整表达，不会授权 AI 增加资料中没有的内容。</p></div>';
  panel.append(advanced);
  const advancedContent=$("draftAdvancedContent");
  moveField("draftMode",advancedContent);
  moveField("draftRequestedDomain",advancedContent);
  moveField("draftRequestedSubdomain",advancedContent);
  moveField("draftRequestedAudience",advancedContent);
  moveField("draftStyleConstraints",advancedContent);
  moveField("draftMustPreserve",advancedContent);
  moveField("draftMustNotInvent",advancedContent);
  moveField("draftPerspectives",advancedContent);
  if(sourceNameField)advancedContent.append(sourceNameField);
  if(providerField)advancedContent.append(providerField);
  const stage=dialog?.querySelector(".draft-stage");
  if(stage)advancedContent.append(stage);
  const actions=dialog?.querySelector(".dialog-actions");
  if(actions){
    const finalNote=document.createElement("p");
    finalNote.id="draftFacadeFinalNote";
    finalNote.className="draft-facade-final-note";
     finalNote.textContent="下一步只会创建 needs_review 待审核档案；进入编辑器后仍需继续修改并保存，不会自动发布。";
    actions.insertAdjacentElement("beforebegin",finalNote);
    const create=$("draftCreateDocument");
     if(create)create.textContent="创建待审核档案";
  }
  draftApplyFacadeMode();
}

function draftEnsureProviderHint(){
  const anchor=$("draftFacadeSummary")||$("draftProvider")?.closest?.(".field");
  if(!anchor)return;
  let node=$("draftProviderHint");
  if(!node){
    node=document.createElement("div");
    node.id="draftProviderHint";
    node.className="draft-note";
    node.innerHTML="<strong>本机 AI 需要额外的 Worker 服务，随包不含</strong><p>请先确认 Worker 已在本机启动，并由提供方在启动 Studio 之前配置好它的地址与密钥（环境变量）。当前界面没有填写本机 Worker 地址的入口。</p><p>拿不到 Worker 时，可以改用云端 Provider，或不使用 AI——档案编辑、校验、预览、导出和“先做本地检查”仍然可用。详见随包 README_使用说明.txt、新手指引_世界书内容编辑者.md 与 docs 目录下的本地 Worker 配置说明。</p>";
    anchor.insertAdjacentElement("afterend",node);
  }
  draftApplyProviderHint();
}

function draftApplyProviderHint(){
  const hint=$("draftProviderHint");
  if(!hint)return;
  hint.hidden=($("draftProvider")?.value||"local")!=="local";
}

function draftApplyFacadeMode(){
  const mode=$("draftMode")?.value||draftState.mode||"quick_authoring";
  const quick=mode!=="legacy_staged";
  const stage=document.querySelector?.("#draftDialog .draft-stage");
  if(stage)stage.hidden=quick;
  const provider=$("draftProvider")?.closest?.(".field");
  if(provider)provider.hidden=false;
  const status=$("draftFlowStatus");
  if(status)status.textContent=quick?"准备好后点击“开始生成草稿”。":"分阶段整理已开启：先确认资料中的明确内容，再生成简介和身份表达。";
  const generate=$("draftGenerateFacts");
  if(generate){
    generate.textContent=quick?"开始生成草稿":"提取资料中的明确内容";
    generate.hidden=quick;
  }
  const startButton=$("draftStartQuickButton");
  if(startButton){
    startButton.hidden=!quick;
    startButton.textContent=quick?"开始生成草稿":"";
  }
  const factsTitle=document.querySelector?.("#draftFactsPanel h3");
  if(factsTitle)factsTitle.textContent=quick?"资料中明确写出的内容":"资料中的事实草稿";
  const metadataTitle=document.querySelector?.("#draftMetadataPanel h3");
  if(metadataTitle)metadataTitle.textContent=quick?"标题、摘要和分类建议":"档案简介草稿";
  const expressionsTitle=document.querySelector?.("#draftExpressionsPanel h3");
  if(expressionsTitle)expressionsTitle.textContent=quick?"不同身份的表达建议":"身份视角表达草稿";
  const tier=$("draftContentTier")?.closest?.(".field")?.querySelector?.("label");
  if(tier)tier.innerHTML="内容范围<span>请选择这份草稿的内容范围；成人拓展仍需额外确认。</span>";
  draftApplyAdultGate();
  draftEnsureProviderHint();
}

function draftApplyAdultGate(){
  const tier=$("draftContentTier");
  const gate=$("draftAdultGate");
  const checkbox=$("draftAdultConfirmed");
  const adult=tier?.value==="adult_optional";
  if(gate)gate.hidden=!adult;
  if(checkbox){
    checkbox.disabled=!adult;
    if(!adult)checkbox.checked=false;
    draftState.adultConfirmed=adult&&checkbox.checked===true;
  }
}

function draftLines(id){
  return ($(id)?.value||"").split(/\r?\n/).map(value=>value.trim()).filter(Boolean);
}

function draftQuickFields(){
  return {
    mode:$("draftMode")?.value||draftState.mode||"quick_authoring",
    authoringGoal:$("draftAuthoringGoal")?.value.trim()||draftState.authoringGoal||"",
    userInstruction:$("draftUserInstruction")?.value.trim()||draftState.userInstruction||"",
    requestedEntryKind:$("draftEntryKind")?.value||draftState.requestedEntryKind||"general",
    requestedDomain:$("draftRequestedDomain")?.value||draftState.requestedDomain||"",
    requestedSubdomain:$("draftRequestedSubdomain")?.value.trim()||draftState.requestedSubdomain||"",
    requestedAudience:draftLines("draftRequestedAudience"),
    requestedPerspectives:draftLines("draftPerspectives"),
    styleConstraints:draftLines("draftStyleConstraints"),
    mustPreserve:draftLines("draftMustPreserve"),
    mustNotInvent:draftLines("draftMustNotInvent"),
    requestedContentTier:$("draftContentTier")?.value||draftState.requestedContentTier||"",
    adultConfirmed:$("draftAdultConfirmed")?.checked===true||draftState.adultConfirmed===true
  };
}

function draftMigratePayload(payload){
  if(!payload||typeof payload!=="object")return null;
  const version=payload.schemaVersion||"awake.worldbook.studio.reference-draft.v1";
  if(version!=="awake.worldbook.studio.reference-draft.v1"&&version!==draftPayloadSchemaVersion)return null;
  const quick=payload.quickAuthoring&&typeof payload.quickAuthoring==="object"?payload.quickAuthoring:{};
  return {...payload,schemaVersion:draftPayloadSchemaVersion,quickAuthoring:{
     mode:quick.mode||"quick_authoring",
     authoringGoal:quick.authoringGoal||"",
     userInstruction:quick.userInstruction||"",
     requestedEntryKind:quick.requestedEntryKind||"general",
     requestedDomain:quick.requestedDomain||"",
     requestedSubdomain:quick.requestedSubdomain||"",
     requestedAudience:Array.isArray(quick.requestedAudience)?quick.requestedAudience:[],
     requestedPerspectives:Array.isArray(quick.requestedPerspectives)?quick.requestedPerspectives:[],
     styleConstraints:Array.isArray(quick.styleConstraints)?quick.styleConstraints:[],
    mustPreserve:Array.isArray(quick.mustPreserve)?quick.mustPreserve:[],
    mustNotInvent:Array.isArray(quick.mustNotInvent)?quick.mustNotInvent:[],
     requestedContentTier:quick.requestedContentTier||"",
     adultConfirmed:quick.adultConfirmed===true
  },draftId:typeof payload.draftId==="string"?payload.draftId:"",serverSourceContentHash:typeof payload.serverSourceContentHash==="string"?payload.serverSourceContentHash:"",warnings:Array.isArray(payload.warnings)?payload.warnings:[],unresolved:Array.isArray(payload.unresolved)?payload.unresolved:[],targetSpans:Array.isArray(payload.targetSpans)?payload.targetSpans:[],propositions:Array.isArray(payload.propositions)?payload.propositions:[],claims:Array.isArray(payload.claims)?payload.claims:[]};
}

function draftEnsureCandidatePanel(){
  if($("draftCandidatesResults"))return;
  const facts=$("draftFactsPanel");
  if(!facts?.parentNode)return;
  const panel=document.createElement("section");
  panel.id="draftCandidatePanel";
  panel.innerHTML='<div class="head-row"><div><h3>AI 待确认草稿</h3><span class="help">先阅读草稿，再处理需要你确认的问题；确认后才会进入世界书编辑器。</span></div><label class="draft-candidate-filter">查找草稿<input id="draftCandidateFilter" type="search" placeholder="标题、内容或来源"></label></div><div id="draftUserResultSummary" class="draft-user-result-summary"></div><div id="draftCandidateDetail" class="draft-candidate-detail" hidden></div><div id="draftCandidatesResults" class="draft-results"></div>';
  facts.parentNode.insertBefore(panel,facts);
}

function resetDraftState(){
  draftInvalidate();
  draftState.token="";draftState.draftId="";draftState.serverSourceContentHash="";draftState.attemptId="";draftState.stage="facts";draftState.uiStep="input";draftState.mode="quick_authoring";draftState.sourceName="";draftState.sourceText="";draftState.authoringGoal="";draftState.userInstruction="";draftState.requestedEntryKind="general";draftState.requestedDomain="";draftState.requestedSubdomain="";draftState.requestedAudience=[];draftState.requestedPerspectives=[];draftState.styleConstraints=[];draftState.mustPreserve=[];draftState.mustNotInvent=[];draftState.requestedContentTier="";draftState.adultConfirmed=false;draftState.facts=[];draftState.metadata=null;draftState.expressions=[];draftState.candidates=[];draftState.warnings=[];draftState.unresolved=[];draftState.coverage=null;draftState.targetSpans=[];draftState.propositions=[];draftState.claims=[];draftState.candidateSetGenerationId="";draftState.candidateSetSourceContentHash="";draftState.candidateSetPacketHash="";draftState.candidateFilter="";draftState.candidateDetailId="";draftState.selectedCandidateId="";draftState.selectedCandidateIds=[];draftState.busy=false;
}

function draftMarkChanged(){
  draftState.generation+=1;
}

function draftInvalidate(){
  draftMarkChanged();
  try{draftState.activeRequest?.controller?.abort()}catch{}
  draftState.activeRequest=null;
  draftState.token="";
}

function draftContext(){return {workspaceHash:state.workspaceHash||"local",documentPath:state.currentPath||"new-reference",kind:"reference",taskId:draftLocalTaskId}}
function draftLegacyContext(){return {workspaceHash:state.workspaceHash||"local",documentPath:state.currentPath||"new-reference",kind:"reference"}}
function draftPayload(){return {schemaVersion:draftPayloadSchemaVersion,draftId:draftState.draftId,serverSourceContentHash:draftState.serverSourceContentHash,sourceName:$('draftSourceName')?.value||draftState.sourceName,sourceText:$('draftSourceText')?.value||draftState.sourceText,stage:draftState.stage,uiStep:draftState.uiStep,providerId:$('draftProvider')?.value||"local",perspectives:draftQuickFields().requestedPerspectives,quickAuthoring:draftQuickFields(),facts:clone(draftState.facts),metadata:clone(draftState.metadata),expressions:clone(draftState.expressions),candidates:clone(draftState.candidates),warnings:clone(draftState.warnings),unresolved:clone(draftState.unresolved),coverage:clone(draftState.coverage),targetSpans:clone(draftState.targetSpans),propositions:clone(draftState.propositions),claims:clone(draftState.claims),candidateFilter:draftState.candidateFilter,candidateDetailId:draftState.candidateDetailId,selectedCandidateId:draftState.selectedCandidateId,selectedCandidateIds:clone(draftState.selectedCandidateIds)}}
const draftLocalSaveNotice={status:""};

function draftReportLocalSave(result){
  if(result?.ok){draftLocalSaveNotice.status="";return}
  const status=result?.status||"unavailable";
  if(draftLocalSaveNotice.status===status)return;
  draftLocalSaveNotice.status=status;
  if(status==="too-large")toast("草稿太大，已停止自动保存（上限约 1.5 MB）。请手动保存或拆分内容，否则刷新后这份草稿不会恢复。","warn");
  else toast("浏览器本地存储当前不可用，草稿不会自动保存。请手动保存或导出内容，避免刷新后丢失。","warn");
}
function draftPersist(){
  const payload=draftPayload();
  if(!payload.sourceText.trim()&&!payload.facts.length&&!payload.metadata&&!payload.expressions.length)return;
  draftReportLocalSave(draftLocalStore.schedule(draftContext(),payload,draftState.generation,450));
}
function draftPersistNow(){
  const payload=draftPayload();
  if(!payload.sourceText.trim()&&!payload.facts.length&&!payload.metadata&&!payload.expressions.length)return;
  draftReportLocalSave(draftLocalStore.flush(draftContext(),payload,draftState.generation));
}
function draftRestoreLocal(){
  let loaded=draftLocalStore.load(draftContext());
  let restoreContext=draftContext();
  if(loaded.status==="missing"){
    restoreContext=draftLegacyContext();
    loaded=draftLocalStore.load(restoreContext);
  }
  if(!["available","stale"].includes(loaded.status))return;
  const message=loaded.status==="stale"?"发现一份较旧的参考资料草稿。恢复后仍需重新生成受影响的结果，要恢复吗？":"发现上次未完成的参考资料草稿，要恢复吗？";
  if(!confirm(message)){draftLocalStore.discard(restoreContext);return}
  const payload=draftMigratePayload(loaded.payload);
  if(!payload){draftLocalStore.discard(restoreContext);toast("本地草稿版本无法识别，已安全丢弃，请重新开始。","warn");return}
  const quick=payload.quickAuthoring||{};
  draftState.requestedPerspectives=Array.isArray(quick.requestedPerspectives)?quick.requestedPerspectives:(Array.isArray(payload.perspectives)?payload.perspectives:[]);
  draftState.sourceName=payload.sourceName||"";draftState.sourceText=payload.sourceText||"";draftState.stage=payload.stage||"facts";draftState.mode=quick.mode||"quick_authoring";draftState.authoringGoal=quick.authoringGoal||"";draftState.userInstruction=quick.userInstruction||"";draftState.requestedEntryKind=quick.requestedEntryKind||"general";draftState.requestedDomain=quick.requestedDomain||"";draftState.requestedSubdomain=quick.requestedSubdomain||"";draftState.requestedAudience=Array.isArray(quick.requestedAudience)?quick.requestedAudience:[];draftState.styleConstraints=Array.isArray(quick.styleConstraints)?quick.styleConstraints:[];draftState.mustPreserve=Array.isArray(quick.mustPreserve)?quick.mustPreserve:[];draftState.mustNotInvent=Array.isArray(quick.mustNotInvent)?quick.mustNotInvent:[];draftState.requestedContentTier=quick.requestedContentTier||"";draftState.adultConfirmed=quick.adultConfirmed===true;draftState.facts=Array.isArray(payload.facts)?payload.facts:[];draftState.metadata=payload.metadata||null;draftState.expressions=Array.isArray(payload.expressions)?payload.expressions:[];draftState.candidates=Array.isArray(payload.candidates)?payload.candidates.map(draftNormalizeCandidate).filter(Boolean):[];draftState.warnings=Array.isArray(payload.warnings)?payload.warnings:[];draftState.unresolved=Array.isArray(payload.unresolved)?payload.unresolved:[];draftState.coverage=payload.coverage||null;draftState.targetSpans=Array.isArray(payload.targetSpans)?payload.targetSpans:[];draftState.propositions=Array.isArray(payload.propositions)?payload.propositions:[];draftState.claims=Array.isArray(payload.claims)?payload.claims:[];draftState.draftId=typeof payload.draftId==="string"?payload.draftId:"";draftState.serverSourceContentHash=typeof payload.serverSourceContentHash==="string"?payload.serverSourceContentHash:"";const candidateIds=new Set(draftState.candidates.map(item=>item.candidate_id));draftState.selectedCandidateId=candidateIds.has(payload.selectedCandidateId)?payload.selectedCandidateId:"";draftState.selectedCandidateIds=Array.isArray(payload.selectedCandidateIds)?payload.selectedCandidateIds.filter(id=>candidateIds.has(id)):(draftState.selectedCandidateId?[draftState.selectedCandidateId]:[]);draftState.candidateFilter=typeof payload.candidateFilter==="string"?payload.candidateFilter:"";draftState.candidateDetailId=candidateIds.has(payload.candidateDetailId)?payload.candidateDetailId:(draftState.selectedCandidateId||"");draftState.token="";
  if($("draftSourceName"))$("draftSourceName").value=draftState.sourceName;
  if($("draftSourceText"))$("draftSourceText").value=draftState.sourceText;
  if($("draftPerspectives"))$("draftPerspectives").value=Array.isArray(payload.perspectives)?payload.perspectives.join("\n"):payload.perspectives||$("draftPerspectives").value;
  if($("draftProvider"))$("draftProvider").value=payload.providerId||"local";
  if($("draftMode"))$("draftMode").value=draftState.mode;
  if($("draftEntryKind"))$("draftEntryKind").value=draftState.requestedEntryKind;
  if($("draftAuthoringGoal"))$("draftAuthoringGoal").value=draftState.authoringGoal;
  if($("draftUserInstruction"))$("draftUserInstruction").value=draftState.userInstruction;
  if($("draftRequestedDomain"))$("draftRequestedDomain").value=draftState.requestedDomain;
  if($("draftRequestedSubdomain"))$("draftRequestedSubdomain").value=draftState.requestedSubdomain;
  if($("draftRequestedAudience"))$("draftRequestedAudience").value=draftState.requestedAudience.join("\n");
  if($("draftStyleConstraints"))$("draftStyleConstraints").value=draftState.styleConstraints.join("\n");
  if($("draftMustPreserve"))$("draftMustPreserve").value=draftState.mustPreserve.join("\n");
  if($("draftMustNotInvent"))$("draftMustNotInvent").value=draftState.mustNotInvent.join("\n");
  if($("draftContentTier"))$("draftContentTier").value=draftState.requestedContentTier;
  if($("draftAdultConfirmed"))$("draftAdultConfirmed").checked=draftState.adultConfirmed;
  if($("draftTitleCandidate"))$("draftTitleCandidate").value=draftState.metadata?.title||"";
  if($("draftSummaryCandidate"))$("draftSummaryCandidate").value=draftState.metadata?.summary||"";
  if($("draftDomainCandidate"))$("draftDomainCandidate").value=Object.prototype.hasOwnProperty.call(draftDomainLabels,draftState.metadata?.domain)?draftState.metadata.domain:"";
  if($("draftCandidateFilter"))$("draftCandidateFilter").value=draftState.candidateFilter;
  draftSetStage(draftState.stage);draftApplyFacadeMode();draftUpdateStats();renderDraftResults();draftSetFlowStatus("已恢复上次草稿，请检查内容后再继续。");toast("已恢复参考资料草稿，请检查后再生成或采纳。","warn");
}

async function draftRestoreServer(){
  const draftId=draftState.draftId;
  const expectedHash=draftState.serverSourceContentHash;
  if(!draftId||!expectedHash||typeof requestJson!=="function")return;
  const generation=draftState.generation;
  try{
    const data=await requestJson("/api/ai/authoring/draft/"+encodeURIComponent(draftId));
    if(generation!==draftState.generation||draftState.draftId!==draftId)return;
    if(!data?.ok||!data.result)return;
    if(String(data.sourceContentHash||"").toLowerCase()!==expectedHash.toLowerCase()){
      draftState.draftId="";draftState.serverSourceContentHash="";draftState.token="";draftState.attemptId="";
      draftMarkChanged();draftPersist();toast("服务端 Draft 与当前参考资料不一致，已解除旧 Draft 绑定。","warn");return;
    }
    const result=data.result;
    draftState.warnings=Array.isArray(result.warnings)?result.warnings:[];
    draftState.unresolved=Array.isArray(result.unresolved)?result.unresolved:[];
    draftState.coverage=result.coverage||null;
    draftState.targetSpans=Array.isArray(result.targetSpans||result.target_spans)?(result.targetSpans||result.target_spans):[];
    draftState.propositions=Array.isArray(result.propositions)?result.propositions:[];
    draftState.claims=Array.isArray(result.claims)?result.claims:[];
    const reviewItems=Array.isArray(result.reviewProjection?.candidates)?result.reviewProjection.candidates:[];
    const candidates=Array.isArray(result.candidateSet?.candidates)?result.candidateSet.candidates.map(draftNormalizeCandidate).filter(Boolean).map(candidate=>{
      const review=reviewItems.find(item=>(item.candidate_id||item.candidateId)===candidate.candidate_id)||{};
      return {...candidate,risk:review.risk||"yellow",riskReasons:review.risk_reasons||review.riskReasons||[],evidenceCount:Number(review.evidence_count||review.evidenceCount||0)};
    }):[];
    if(candidates.length){
      draftState.candidates=candidates;
      const selected=candidates.find(item=>item.candidate_id===draftState.selectedCandidateId)||candidates[0];
      draftSelectCandidate(selected.candidate_id,false);
    }else{
      draftState.candidates=[];
      draftState.facts=Array.isArray(result.facts)?result.facts:[];
      draftState.metadata=result.metadata||null;
      draftState.expressions=Array.isArray(result.expressions)?result.expressions:[];
      if($("draftTitleCandidate"))$("draftTitleCandidate").value=draftState.metadata?.title||"";
      if($("draftSummaryCandidate"))$("draftSummaryCandidate").value=draftState.metadata?.summary||"";
      if($("draftDomainCandidate"))$("draftDomainCandidate").value=Object.prototype.hasOwnProperty.call(draftDomainLabels,draftState.metadata?.domain)?draftState.metadata.domain:"";
    }
    draftSetStage(result.stage==="complete"?"facts":result.stage||"facts");
    renderDraftResults();draftPersist();toast("已接回服务端 Draft 结果，请继续人工复核。","good");
  }catch(error){if(error?.name!=="AbortError")toast("服务端 Draft 暂时无法续接，仍保留本地草稿。","warn")}
}

function draftSourceEdited(){
  draftState.draftId="";draftState.serverSourceContentHash="";draftState.token="";draftState.attemptId="";draftState.adultConfirmed=false;if($("draftAdultConfirmed"))$("draftAdultConfirmed").checked=false;draftMarkChanged();
}

function draftSnapshot(stage){
  return {generation:draftState.generation,stage,sourceName:$('draftSourceName')?.value.trim()||"未命名参考资料",sourceText:$('draftSourceText')?.value.trim()||"",providerId:$('draftProvider')?.value||"local",perspectives:draftLines("draftPerspectives"),quick:draftQuickFields()};
}

function draftRequestIsCurrent(snapshot){
  const current=draftSnapshot(snapshot.stage);
  return current.generation===snapshot.generation&&current.stage===snapshot.stage&&current.sourceName===snapshot.sourceName&&current.sourceText===snapshot.sourceText&&current.providerId===snapshot.providerId&&JSON.stringify(current.perspectives)===JSON.stringify(snapshot.perspectives)&&JSON.stringify(current.quick)===JSON.stringify(snapshot.quick);
}

function draftNormalizeCandidate(candidate){
  if(!candidate)return null;
  const normalizeEvidence=evidence=>evidence?{...evidence,referenceId:evidence.referenceId||evidence.reference_id,quoteHash:evidence.quoteHash||evidence.quote_hash,locatorObject:evidence.locatorObject||evidence.locator_object||null}:null;
  const facts=(candidate.facts||[]).map(item=>({...item,reviewStatus:item.reviewStatus||item.review_status||"pending",profileIds:item.profileIds||item.profile_ids,factIds:item.factIds||item.fact_ids,evidence:normalizeEvidence(item.evidence),evidenceGroup:(item.evidenceGroup||item.evidence_group||[]).map(normalizeEvidence).filter(Boolean)}));
  const expressions=(candidate.expressions||[]).map(item=>({...item,reviewStatus:item.reviewStatus||item.review_status||"pending",profileIds:item.profileIds||item.profile_ids,factIds:item.factIds||item.fact_ids,evidence:normalizeEvidence(item.evidence)}));
  const metadata=candidate.metadata?{...candidate.metadata,relatedDomains:candidate.metadata.relatedDomains||candidate.metadata.related_domains}:null;
  return {...candidate,facts,expressions,metadata,candidate_id:candidate.candidate_id||candidate.candidateId,reasonCodes:candidate.reasonCodes||candidate.reason_codes||candidate.segmentation_reason_codes||[],sourceSpans:candidate.sourceSpans||candidate.source_spans||[],targetSpans:candidate.targetSpans||candidate.target_spans||[],propositions:candidate.propositions||[],claims:candidate.claims||[],unresolved:candidate.unresolved||[],coverage:candidate.coverage||null};
}

function draftBeginRequest(snapshot){
  const controller=typeof AbortController==="function"?new AbortController():null;
  const request={generation:snapshot.generation,stage:snapshot.stage,controller};
  draftState.activeRequest=request;
  return request;
}

function draftFinishRequest(request){
  if(draftState.activeRequest===request)draftState.activeRequest=null;
}

function draftSetStage(stage){
  draftState.stage=stage;
  document.querySelectorAll("[data-draft-stage]").forEach(button=>button.classList.toggle("active",button.dataset.draftStage===stage));
  Object.entries(draftStagePanels).forEach(([key,id])=>{const panel=$(id);if(panel)panel.hidden=key!==stage});
  if(draftState.mode==="quick_authoring"&&stage==="facts"){
    if($("draftMetadataPanel"))$("draftMetadataPanel").hidden=!draftState.metadata;
    if($("draftExpressionsPanel"))$("draftExpressionsPanel").hidden=draftState.expressions.length===0;
  }
  if($("draftDialog")?.open)draftPersist();
}

const draftSourceLimit=80000;

function draftUpdateStats(){
  const value=$("draftSourceText")?.value||"";
  const stats=$("draftSourceStats");
  if(!stats)return;
  const length=value.length;
  if(!length){stats.textContent="尚未输入资料";stats.className="muted";return}
  const near=length>=draftSourceLimit*0.9;
  stats.textContent=`${length.toLocaleString()} / ${draftSourceLimit.toLocaleString()} 字符`+(length>draftSourceLimit?"（已超过上限，请先分成几份资料）":near?"（接近上限，建议拆分资料）":"");
  stats.className=near?"draft-source-hint":"muted";
}

function draftSourcePayload(){
  const text=$("draftSourceText")?.value.trim()||"";
  if(!text){toast("请先粘贴或导入一份参考资料。","warn");return null}
  if(text.length>draftSourceLimit){toast("参考资料超过 80,000 字符，请先分成几份资料。","warn");return null}
  draftState.sourceName=$("draftSourceName")?.value.trim()||"未命名参考资料";
  draftState.sourceText=text;
  const quick=draftQuickFields();
  draftState.mode=quick.mode;draftState.authoringGoal=quick.authoringGoal;draftState.userInstruction=quick.userInstruction;draftState.requestedEntryKind=quick.requestedEntryKind;draftState.requestedDomain=quick.requestedDomain;draftState.requestedSubdomain=quick.requestedSubdomain;draftState.requestedAudience=quick.requestedAudience;draftState.styleConstraints=quick.styleConstraints;draftState.mustPreserve=quick.mustPreserve;draftState.mustNotInvent=quick.mustNotInvent;draftState.requestedContentTier=quick.requestedContentTier;
  draftState.requestedPerspectives=quick.requestedPerspectives;
  return {sourceName:draftState.sourceName,sourceText:text,providerId:$("draftProvider")?.value||"local",mode:quick.mode,authoringGoal:quick.authoringGoal,userInstruction:quick.userInstruction,requestedEntryKind:quick.requestedEntryKind,requestedDomain:quick.requestedDomain,requestedSubdomain:quick.requestedSubdomain,requestedAudience:quick.requestedAudience,requestedPerspectives:quick.requestedPerspectives,styleConstraints:quick.styleConstraints,mustPreserve:quick.mustPreserve,mustNotInvent:quick.mustNotInvent,requestedContentTier:quick.requestedContentTier,adultConfirmed:quick.adultConfirmed===true};
}

function draftGenerateQuickCandidate(){
  draftState.uiStep="generating";
  draftGenerate("complete");
}

function draftGenerateLegacyFacts(){
  draftGenerate("facts");
}

function draftGenerateFacts(){
  if(draftQuickFields().mode==="legacy_staged")draftGenerateLegacyFacts();
  else draftGenerateQuickCandidate();
}

function draftSetFlowStatus(message){
  const node=$("draftFlowStatus");
  if(node)node.textContent=message;
}

async function draftGenerate(stage){
  const payload=draftSourcePayload();
  if(!payload||draftState.busy)return;
   if(stage!=="facts"&&stage!=="complete"&&!draftState.facts.some(item=>item.reviewStatus==="accepted")){toast("请先至少采纳一条客观事实，再生成后续内容。","warn");draftSetStage("facts");return}
  const snapshot=draftSnapshot(stage);
  const request=draftBeginRequest(snapshot);
  draftState.busy=true;renderDraftBusy(true);
  draftSetFlowStatus(stage==="complete"?"正在读取资料、整理内容并检查来源……":stage==="facts"?"正在整理资料中明确写出的内容……":"正在根据已采纳内容生成建议……");
  try{
    const acceptedFacts=draftState.facts.filter(item=>item.reviewStatus==="accepted");
    const metadata=clone(draftState.metadata);
    const perspectives=snapshot.perspectives;
     const prepared=await requestJson("/api/ai/authoring/draft/prepare",{method:"POST",body:JSON.stringify({...payload,draftId:draftState.draftId||null,stage,acceptedFacts,metadata,perspectives,candidateId:draftState.selectedCandidateId||null,adultConfirmed:payload.adultConfirmed===true}),signal:request.controller?.signal});
    if(!draftRequestIsCurrent(snapshot))return;
     draftState.token=prepared.draftToken;draftState.draftId=prepared.draftId;draftState.serverSourceContentHash=prepared.sourceContentHash||draftState.serverSourceContentHash;draftState.attemptId=prepared.attemptId||"";
     const data=await requestJson("/api/ai/authoring/draft/generate",{method:"POST",body:JSON.stringify({draftToken:draftState.token,attemptId:draftState.attemptId}),signal:request.controller?.signal});
    if(!draftRequestIsCurrent(snapshot))return;
     draftState.token="";draftState.attemptId="";
     draftState.warnings=Array.isArray(data.result.warnings)?data.result.warnings:[];
     draftState.unresolved=Array.isArray(data.result.unresolved)?data.result.unresolved:[];
     draftState.coverage=data.result.coverage||null;
     draftState.targetSpans=Array.isArray(data.result.targetSpans||data.result.target_spans)?(data.result.targetSpans||data.result.target_spans):[];
     draftState.propositions=Array.isArray(data.result.propositions)?data.result.propositions:[];
     draftState.claims=Array.isArray(data.result.claims)?data.result.claims:[];
    const reviewItems=Array.isArray(data.result.reviewProjection?.candidates)?data.result.reviewProjection.candidates:[];
    const candidateList=Array.isArray(data.result.candidateSet?.candidates)?data.result.candidateSet.candidates.map(draftNormalizeCandidate).filter(Boolean).map(candidate=>{const review=reviewItems.find(item=>(item.candidate_id||item.candidateId)===candidate.candidate_id)||{};return{...candidate,risk:review.risk||"yellow",riskReasons:review.risk_reasons||review.riskReasons||[],evidenceCount:Number(review.evidence_count||review.evidenceCount||0)}}):[];
    if(candidateList.length){
      draftState.candidates=candidateList;
      const selected=candidateList.find(item=>item.candidate_id===draftState.selectedCandidateId)||candidateList[0];
      draftSelectCandidate(selected.candidate_id,false);
    }else if(stage==="facts"||stage==="complete")draftState.facts=data.result.facts||[];
     if(stage==="metadata"||stage==="complete"){
      draftState.metadata=data.result.metadata||null;
      $("draftTitleCandidate").value=draftState.metadata?.title||"";
      $("draftSummaryCandidate").value=draftState.metadata?.summary||"";
      $("draftDomainCandidate").value=Object.prototype.hasOwnProperty.call(draftDomainLabels,draftState.metadata?.domain)?draftState.metadata.domain:"";
      $("draftMetadataInfo").textContent=draftState.metadata?.note||"请检查标题、摘要和分类候选。";
    }
     if(stage==="expressions"||stage==="complete")draftState.expressions=data.result.expressions||[];
     if(stage==="complete")draftSetStage("facts");
    renderDraftResults();
    draftPersist();
     draftState.uiStep=stage==="complete"?"review":"input";
     draftSetFlowStatus(stage==="complete"?"已生成待确认草稿，请先查看需要你确认的问题。":stage==="facts"?"已整理资料内容，请逐条确认。":"已生成建议，请继续人工确认。");
     toast(stage==="facts"?"资料内容已整理。":stage==="metadata"?"标题、摘要和分类建议已生成。":"身份表达建议已生成。","good");
  }catch(error){if(error?.name!=="AbortError"&&draftRequestIsCurrent(snapshot)){draftSetFlowStatus("这次没有生成可用草稿，请根据提示处理后重试。");handleError(error)}}finally{if(draftState.activeRequest===request){draftState.busy=false;draftFinishRequest(request);renderDraftBusy(false)}}
}

function renderDraftBusy(busy){
  ["draftStartQuickButton","draftGenerateFacts","draftGenerateMetadata","draftGenerateExpressions","draftCreateDocument"].forEach(id=>{const button=$(id);if(button)button.disabled=busy||id==="draftCreateDocument"&&!draftCanCreate()});
  draftApplyCreateBlockers();
}

function draftCard(item,type,index){
  const accepted=item.reviewStatus==="accepted";
  const label=item.inferred?"AI 推断，必须人工复核":"资料明确内容";
  const text=type==="fact"?item.text:type==="expression"?item.text:item.summary;
  const identity=type==="expression"?draftExpressionDisplay(item):null;
  const title=type==="fact"?`事实 ${index+1}`:type==="expression"?(identity.label||`视角 ${index+1}`):"档案简介候选";
  const evidence=item.evidence?.quote?draftEvidenceDetails([item.evidence]):"<p class=\"evidence\">暂无可定位的原文依据</p>";
  const warning=identity?.warning?`<div class="diag warning"><strong>身份预设需人工复核</strong><p>${h(identity.warning)}</p></div>`:"";
  return `<article class="draft-card${accepted?" accepted":""}"><h4>${h(title)} <span class="chip">${h(label)}</span></h4>${warning}<textarea class="draft-inline-editor" data-draft-text="${type}" data-draft-index="${index}" aria-label="${h(title)}">${h(text||"")}</textarea>${type!=="metadata"?evidence:""}<div class="card-actions"><button type="button" data-draft-review="${type}" data-draft-index="${index}" data-draft-action="${accepted?"reject":"accept"}">${accepted?"取消采纳":"采纳这条"}</button></div></article>`;
}

function draftEvidenceDetails(evidenceList){
  const entries=(evidenceList||[]).filter(item=>item?.quote).map(item=>{
    const locator=item.locator||item.source_locator||"资料原文";
    const verified=item.evidence_verified===true||item.verification_status==="verified";
    return `<li><strong>${h(locator)}</strong><span class="draft-source-status">${verified?"已定位":"需要回看"}</span><p>${h(item.quote)}</p></li>`;
  }).join("");
  return entries?`<details class="draft-source-details"><summary>查看来源（${(evidenceList||[]).filter(item=>item?.quote).length}）</summary><ul>${entries}</ul></details>`:"<p class=\"evidence\">暂无可定位的原文依据</p>";
}

function draftCandidateEvidence(item){
  const values=[];
  (item?.sourceSpans||[]).forEach(span=>values.push(span));
  (item?.facts||[]).forEach(fact=>{
    if(fact?.evidence)values.push(fact.evidence);
    (fact?.evidenceGroup||fact?.evidence_group||[]).forEach(evidence=>values.push(evidence));
  });
  return values.filter((value,index,list)=>value?.quote&&list.findIndex(item=>`${item.locator||""}\u001f${item.quote}`===`${value.locator||""}\u001f${value.quote}`)===index);
}

function draftCandidateSearchText(item){
  const values=[item?.candidate_id,item?.metadata?.title,item?.metadata?.summary,item?.metadata?.domain,item?.risk,...(item?.reasonCodes||[]),...(item?.facts||[]).map(value=>value?.text),...(item?.expressions||[]).map(value=>value?.text),...(item?.sourceSpans||[]).flatMap(value=>[value?.locator,value?.heading_path,value?.source_unit_id,value?.quote])];
  return values.filter(value=>value!=null).join(" ").toLocaleLowerCase();
}

function draftFilteredCandidates(){
  const query=(draftState.candidateFilter||"").trim().toLocaleLowerCase();
  return query?draftState.candidates.filter(item=>draftCandidateSearchText(item).includes(query)):draftState.candidates;
}

const draftRiskReasonLabels={stale_evidence:"来源已过期，需要重新核对",missing_fact:"没有整理出任何事实",unverified_source_span:"来源定位未验证",blocking_unresolved:"有必须先处理的未解决项",duplicate_candidate:"与另一份草稿内容重复",candidate_conflict:"同一句引文得出互相矛盾的说法，需要人工判断",missing_evidence:"缺少来源依据",unverified_evidence:"来源依据未验证",inference_or_uncertainty:"含 AI 推断或不确定内容",inferred_expression:"身份表达含 AI 推断"};
function draftRiskReasonText(item){
  const codes=Array.isArray(item?.riskReasons)?item.riskReasons:[];
  const labels=codes.map(code=>draftRiskReasonLabels[code]).filter(Boolean);
  return labels.length?"需要人工确认的原因："+labels.join("；"):"";
}

// 服务端语义信号：命题覆盖数来自 coverage.*，字段截断来自 coverage.truncations（Core 的 coverage 合同）。
function draftCoverageNumbers(){
  const coverage=draftState.coverage&&typeof draftState.coverage==="object"?draftState.coverage:{};
  const find=names=>{for(const name of names){if(typeof coverage[name]==="number")return coverage[name]}return null};
  const truncations=coverage.truncations&&typeof coverage.truncations==="object"?coverage.truncations:null;
  const fields=truncations&&Array.isArray(truncations.items)?truncations.items.map(item=>item&&item.field).filter(value=>typeof value==="string"):[];
  const warnings=coverage.warnings&&typeof coverage.warnings==="object"?coverage.warnings:null;
  return {
    source:find(["source_proposition_count"]),
    supported:find(["supported_proposition_count"]),
    unsupported:find(["unsupported_proposition_count"]),
    unresolved:find(["unresolved_proposition_count"]),
    truncated:truncations&&typeof truncations.count==="number"?truncations.count:find(["truncated_count","truncated_warning_count","dropped_count"]),
    truncatedFields:fields,
    warningsTotal:warnings&&typeof warnings.count==="number"?warnings.count:find(["warnings_total","warnings_count"]),
    warningsLimit:warnings&&typeof warnings.limit==="number"?warnings.limit:null
  };
}

function draftCoverageGapTexts(){
  const numbers=draftCoverageNumbers(),texts=[];
  if(numbers.source!==null)texts.push("来源命题 "+numbers.source+" 条"+(numbers.supported!==null?"，已覆盖 "+numbers.supported+" 条":""));
  const unsupported=numbers.unsupported!==null?numbers.unsupported:(numbers.source!==null&&numbers.supported!==null?Math.max(0,numbers.source-numbers.supported):null);
  if(unsupported!==null&&unsupported>0)texts.push("未覆盖 "+unsupported+" 条，需要人工确认");
  if(numbers.unresolved!==null&&numbers.unresolved>0)texts.push("未定性的来源命题 "+numbers.unresolved+" 条");
  const shown=Array.isArray(draftState.warnings)?draftState.warnings.length:0;
  if(numbers.truncated!==null&&numbers.truncated>0){
    const fields=numbers.truncatedFields.slice(0,3).join("、");
    texts.push("被截断 "+numbers.truncated+" 处字段"+(fields?"（"+fields+"）":"")+"，需人工核对是否被中途削断");
  }
  if(numbers.warningsTotal!==null&&numbers.warningsTotal>shown)texts.push("警告共 "+numbers.warningsTotal+" 条，本次显示 "+shown+" 条"+(numbers.warningsLimit!==null?"（上限 "+numbers.warningsLimit+" 条）":""));
  return texts;
}

function draftCandidateDetailMarkup(item){
  if(!item)return "";
  const facts=(item.facts||[]).map(value=>`<li>${h(value?.text||"")}</li>`).join("")||"<li>暂无事实</li>";
  const expressions=(item.expressions||[]).map(value=>`<li>${h(value?.text||"")}</li>`).join("");
  const sources=draftEvidenceDetails(draftCandidateEvidence(item));
  const targets=(item.targetSpans||[]).map(value=>`${h(value.text||"未命名目标 span")}（${h(value.operation||"unresolved")}）`).join("；");
  const semantic=`内部追溯：${(item.propositions||[]).length} 个命题 · ${(item.claims||[]).length} 个依据关系 · ${(item.targetSpans||[]).length} 段目标文本`;
  const unresolved=(item.unresolved||[]).map(value=>`<div class="diag ${value.blocking?"error":"warning"}"><strong>${value.blocking?"阻断项":"未解决项"}</strong><p>${h(value.message||"需要人工处理")}</p></div>`).join("");
  const reasons=draftRiskReasonText(item);
  return `<div class="head-row"><strong>待确认草稿：${h(item.metadata?.title||item.candidate_id||"未命名草稿")}</strong><button type="button" data-draft-candidate="${h(item.candidate_id)}">选择这份草稿</button></div><p class="help">${h(item.metadata?.summary||"")}</p><p class="help">当前状态：${h({green:"已找到来源，仍需阅读",yellow:"需要重点确认",red:"需要人工确认（不等于阻断）"}[item.risk]||"需要重点确认")}；可追溯来源：${Number(item.evidenceCount||0)} 条</p>${reasons?`<p class="help">${h(reasons)}</p>`:""}${unresolved||'<p class="help">目前没有自动发现的阻断项，但仍请阅读正文。</p>'}<strong>资料中整理出的内容</strong><ul>${facts}</ul>${expressions?`<strong>不同身份的表达建议</strong><ul>${expressions}</ul>`:""}${sources}<details class="draft-technical-details"><summary>查看详细语义依据</summary><p class="help">${h(semantic)}</p>${targets?`<p class="evidence">目标文本映射：${targets}</p>`:""}</details>`;
}

function draftEnsureDiagnosticsPanel(){
  if($("draftDiagnostics"))return;
  const actions=document.querySelector("#draftDialog .dialog-actions");
  if(!actions)return;
  const panel=document.createElement("section");
  panel.id="draftDiagnostics";
  panel.className="draft-diagnostics";
  actions.insertAdjacentElement("beforebegin",panel);
}

function draftCurrentUnresolved(){
  const selected=draftState.candidates.find(item=>item.candidate_id===draftState.selectedCandidateId);
  const values=[...(draftState.unresolved||[]),...(selected?.unresolved||[])];
  const seen=new Set();
  return values.filter(item=>{const id=item?.id||item?.unresolvedId||JSON.stringify(item);if(seen.has(id))return false;seen.add(id);return true});
}

function draftRenderDiagnostics(){
  const node=$("draftDiagnostics");if(!node)return;
  const warnings=Array.isArray(draftState.warnings)?draftState.warnings:[];
  const unresolved=draftCurrentUnresolved();
  const coverage=draftState.coverage;
  const graph=`语义链路：${draftState.propositions.length} propositions · ${draftState.claims.length} claims · ${draftState.targetSpans.length} target spans`;
  const coverageText=coverage?`覆盖诊断：${h(coverage.status||"未标注")} · ${coverage.heuristic===true?"启发式":"已声明"}${coverage.not_semantic_migration_proof===true?" · 不等同 Semantic Migration":""}`:"覆盖诊断：尚未提供";
  const warningMarkup=warnings.map(item=>`<div class="diag warning"><strong>警告</strong><p>${h(item)}</p></div>`).join("");
  const unresolvedMarkup=unresolved.map(item=>`<div class="diag ${item.blocking?"error":"warning"}"><strong>${item.blocking?"阻断项":"未解决项"}：${h(item.kind||"未分类")}</strong><p>${h(item.message||"需要人工处理")}</p></div>`).join("");
  const blocking=unresolved.filter(item=>item.blocking===true).length;
  const needsReview=unresolved.length-blocking;
  const userStatus=blocking?"当前不能进入编辑器":unresolved.length?"有内容需要你确认":"可以继续查看并确认";
  const userSummary=`<div class="draft-user-summary"><strong>需要你确认</strong><span>${blocking?`有 ${blocking} 项必须先处理。`:needsReview?`有 ${needsReview} 项建议回看来源。`:"当前没有额外阻断项。"}</span></div>`;
  node.innerHTML=`<div class="head-row"><h3>检查结果</h3><span class="chip${blocking?" bad":" good"}">${h(userStatus)}</span></div>${userSummary}${warningMarkup}${unresolvedMarkup||'<p class="help">当前没有未解决项。你仍应阅读草稿后再进入编辑器。</p>'}<details class="draft-technical-details"><summary>查看详细依据</summary><p class="help">${coverageText}</p><p class="help">${graph}</p></details>`;
}

function draftRenderUserResultSummary(){
  const node=$("draftUserResultSummary");if(!node)return;
  const candidates=draftState.candidates.length;
  const facts=draftState.candidates.length
    ?draftState.candidates.reduce((sum,item)=>sum+(item.facts||[]).length,0)
    :draftState.facts.length;
  const expressions=draftState.candidates.length
    ?draftState.candidates.reduce((sum,item)=>sum+(item.expressions||[]).length,0)
    :draftState.expressions.length;
  const unresolved=draftCurrentUnresolved();
  const blocking=unresolved.filter(item=>item.blocking===true).length;
  const sourceCount=draftState.candidates.length
    ?draftState.candidates.reduce((sum,item)=>sum+draftCandidateEvidence(item).length,0)
    :draftState.facts.reduce((sum,item)=>sum+(item.evidence?1:0)+(item.evidenceGroup||[]).length,0);
  const warningCount=(draftState.warnings||[]).length+unresolved.filter(item=>item.blocking!==true).length;
  if(!candidates&&!facts&&!expressions&&!draftState.metadata){
    node.innerHTML='<p class="help">还没有生成草稿。填写目标和参考资料后，点击“开始生成草稿”。</p>';
    return;
  }
  const status=blocking?"暂时不能进入编辑器":unresolved.length?"有内容需要你确认":"可以继续阅读并确认";
  const candidateText=candidates?`候选草稿 ${candidates} 份`:"AI 只给出了顶层内容，没有形成候选草稿";
  const gaps=draftCoverageGapTexts().map(text=>`<span>${h(text)}</span>`).join("");
  node.innerHTML=`<div class="draft-result-status"><strong>${h(status)}</strong><span>这是一份待审核草稿，不是正典，不会自动发布，也不会直接进入游戏运行包。</span></div><div class="draft-result-counts"><span>${h(candidateText)}</span><span>资料内容 ${facts} 条</span><span>可定位来源 ${sourceCount} 条</span><span>警告 ${warningCount} 条</span><span>${blocking?"阻断问题 "+blocking:unresolved.length?"待确认 "+unresolved.length:"暂未发现阻断问题"}</span>${gaps}</div>`;
}

function renderDraftResults(){
  const filteredCandidates=draftFilteredCandidates();
  const candidates=$("draftCandidatesResults");
  if(candidates){
    const cards=filteredCandidates.map((item,index)=>{
      const riskLabel={green:"低风险",yellow:"需复核",red:"需人工确认"}[item.risk]||"需复核";
      const reasons=item.reasonCodes?.length?"<p class=\"help\">分割理由："+item.reasonCodes.map(value=>h(value)).join("、")+"</p>":"";
      const spans=item.sourceSpans?.length?"<p class=\"evidence\">来源定位："+item.sourceSpans.map(value=>h(value.locator||value.heading_path||value.source_unit_id||"已记录 span")+"（"+(value.verification_status==="verified"?"已验证":"未验证，需回看原文")+"）").join("；")+"</p>":"";
      const checked=draftState.selectedCandidateIds.includes(item.candidate_id)?" checked":"";
      const discarded=item.reviewStatus==="discarded"?" <span class=\"chip\">已标记丢弃</span>":"";
      const sourceButton=item.sourceSpans?.length||item.facts?.some(fact=>fact?.evidence||fact?.evidenceGroup?.length)?"<span class=\"draft-source-hint\">可查看来源</span>":"";
      return "<article class=\"draft-card"+(item.candidate_id===draftState.selectedCandidateId?" accepted":"")+"\"><h4><label><input type=\"checkbox\" data-draft-candidate-check=\""+h(item.candidate_id)+"\""+checked+"> "+h(item.metadata?.title||("草稿 "+(index+1)))+"</label> <span class=\"chip\">"+riskLabel+"</span>"+discarded+"</h4><p class=\"muted\">"+h(item.metadata?.summary||item.facts?.[0]?.text||"这份草稿尚未提供摘要。")+"</p><p class=\"help\">资料内容："+(item.facts||[]).length+" 条 · "+sourceButton+"</p>"+reasons+spans+"<button type=\"button\" data-draft-candidate=\""+h(item.candidate_id)+"\">"+(item.candidate_id===draftState.selectedCandidateId?"当前草稿":"查看这份草稿")+"</button></article>";
    }).join("");
     const toolbar="<details class=\"draft-advanced-review\"><summary>高级候选审查</summary><div class=\"draft-review-toolbar\"><button type=\"button\" data-draft-review-decision=\"keep_whole\">保持整体</button><button type=\"button\" data-draft-review-decision=\"merge\">合并选中</button><button type=\"button\" data-draft-review-decision=\"split\">按事实拆分</button><button type=\"button\" data-draft-review-decision=\"discard\">丢弃选中</button><button type=\"button\" data-draft-review-decision=\"reorder\">按当前顺序保存</button><span class=\"help\">显示 "+filteredCandidates.length+"/"+draftState.candidates.length+" 条；已选 "+draftState.selectedCandidateIds.length+" 条</span></div></details>";
     candidates.innerHTML=draftState.candidates.length?(toolbar+(cards||"<p class=\"help\">没有匹配的候选；可清除筛选。</p>")):"<p class=\"help\">本次生成的是一份待审核草稿；请先查看来源和待处理项。</p>";
  }/*
  if(candidates)candidates.innerHTML=draftState.candidates.length?`<div class="draft-review-toolbar"><button type="button" data-draft-review-decision="keep_whole">保持整体</button><button type="button" data-draft-review-decision="merge">合并选中</button><button type="button" data-draft-review-decision="split">按事实拆分</button><button type="button" data-draft-review-decision="discard">丢弃选中</button><button type="button" data-draft-review-decision="reorder">按当前顺序保存</button><span class="help">显示 ${filteredCandidates.length}/${draftState.candidates.length} 条；已选 ${draftState.selectedCandidateIds.length} 条</span></div>${filteredCandidates.length?filteredCandidates.map((item,index)=>{const riskLabel={green:"低风险",yellow:"需复核",red:"高风险"}[item.risk]||"需复核";const reasons=item.reasonCodes?.length?`<p class="help">分割理由：${item.reasonCodes.map(value=>h(value)).join("、")}</p>`:"";const spans=item.sourceSpans?.length?`<p class="evidence">来源定位：${item.sourceSpans.map(value=>`${h(value.locator||value.heading_path||value.source_unit_id||"已记录 span")}（${value.verification_status==="verified"?"已验证":"未验证，需回看原文"}）`).join("；")}</p>`:"";const checked=draftState.selectedCandidateIds.includes(item.candidate_id)?" checked":"";const discarded=item.reviewStatus==="discarded"?" <span class="chip">已标记丢弃</span>":"";return `<article class="draft-card${item.candidate_id===draftState.selectedCandidateId?" accepted":""}"><h4><label><input type="checkbox" data-draft-candidate-check="${h(item.candidate_id)}"${checked}> ${h(item.metadata?.title||`候选 ${index+1}`)}</label> <span class="chip">${riskLabel}</span>${discarded}</h4><p class="muted">${h(item.facts?.[0]?.text||"该候选尚未提供摘要。")}</p><p class="help">去重后证据：${Number(item.evidenceCount||0)} 条</p>${reasons}${spans}<button type="button" data-draft-candidate="${h(item.candidate_id)}">${item.candidate_id===draftState.selectedCandidateId?"当前候选":"查看并选择"}</button></article>`}).join(""):"<p class=\"help\">没有匹配的候选；可清除筛选。</p>`:"<p class=\"help\">本次结果未拆出多个候选，将按兼容路径显示。</p>";
  */ draftRenderUserResultSummary();draftRenderDiagnostics();const detail=$("draftCandidateDetail");
  if(detail){const item=draftState.candidates.find(value=>value.candidate_id===draftState.candidateDetailId);detail.hidden=!item;detail.innerHTML=item?draftCandidateDetailMarkup(item):""}
  const facts=$("draftFactsResults");if(facts)facts.innerHTML=draftState.facts.map((item,index)=>draftCard(item,"fact",index)).join("");
  const expressions=$("draftExpressionsResults");if(expressions)expressions.innerHTML=draftState.expressions.map((item,index)=>draftCard(item,"expression",index)).join("");
  const create=$("draftCreateDocument");if(create)create.disabled=!draftCanCreate()||draftState.busy;
  draftApplyCreateBlockers();
}

function draftCreateBlockers(){
  const tier=$("draftContentTier");
  const tierRequired=tier?.tagName==="SELECT";
  const blockers=[];
  if(!draftState.draftId)blockers.push("还没有可采纳的草稿");
  if(draftState.candidates.length&&(!draftState.selectedCandidateId||draftState.selectedCandidateIds.length>1))blockers.push("候选还没有确定唯一一份（当前勾选 "+(draftState.selectedCandidateIds.length||0)+" 份）");
  if(!draftState.facts.some(item=>item.reviewStatus==="accepted"))blockers.push("还没有采纳任何资料内容");
  if(!($("draftTitleCandidate")?.value.trim()||draftState.metadata?.title))blockers.push("档案标题为空");
  if(!($("draftSummaryCandidate")?.value.trim()||draftState.metadata?.summary))blockers.push("档案摘要为空");
  if(!$("draftDomainCandidate")?.value)blockers.push("没有选择知识分类");
  if(tierRequired&&!tier.value)blockers.push("没有选择内容范围");
  if(tierRequired&&tier.value==="adult_optional"&&$("draftAdultConfirmed")?.checked!==true)blockers.push("成人拓展内容还没有确认");
  const blocking=draftCurrentUnresolved().filter(item=>item.blocking===true).length;
  if(blocking)blockers.push("还有 "+blocking+" 条阻断问题没有处理");
  return blockers;
}

function draftCanCreate(){return draftCreateBlockers().length===0}

function draftApplyCreateBlockers(){
  const button=$("draftCreateDocument");
  if(!button||!button.parentNode||typeof document.createElement!=="function")return;
  let node=$("draftCreateBlockers");
  if(!node){
    node=document.createElement("span");
    node.id="draftCreateBlockers";
    node.className="help";
    button.parentNode.insertBefore(node,button);
  }
  const blockers=draftCreateBlockers();
  node.textContent=blockers.length?`还差：${blockers.join("、")}`:"";
}

function draftSelectCandidate(candidateId,markChanged=true){
  const candidate=draftState.candidates.find(item=>item.candidate_id===candidateId);if(!candidate)return;
  if(markChanged)draftMarkChanged();
   draftState.selectedCandidateId=candidate.candidate_id;
   draftState.selectedCandidateIds=[candidate.candidate_id];
  draftState.candidateDetailId=candidate.candidate_id;
  draftState.facts=clone(candidate.facts||[]);
  draftState.metadata=clone(candidate.metadata||null);
  draftState.expressions=clone(candidate.expressions||[]);
  draftState.targetSpans=clone(candidate.targetSpans||[]);
  draftState.propositions=clone(candidate.propositions||[]);
  draftState.claims=clone(candidate.claims||[]);
  if($("draftTitleCandidate"))$("draftTitleCandidate").value=draftState.metadata?.title||"";
  if($("draftSummaryCandidate"))$("draftSummaryCandidate").value=draftState.metadata?.summary||"";
  if($("draftDomainCandidate"))$("draftDomainCandidate").value=Object.prototype.hasOwnProperty.call(draftDomainLabels,draftState.metadata?.domain)?draftState.metadata.domain:"";
  renderDraftResults();draftPersist();
}

async function draftReviewDecision(operation){
  const ids=draftState.selectedCandidateIds.length?draftState.selectedCandidateIds:(draftState.selectedCandidateId?[draftState.selectedCandidateId]:[]);
  if(operation==="keep_whole")ids.splice(0,ids.length,...draftState.candidates.map(item=>item.candidate_id));
  if(!ids.length){toast("请先勾选要审查的候选。","warn");return}
  let splitGroups=null;
  if(operation==="split"){
    const candidate=draftState.candidates.find(item=>item.candidate_id===ids[0]);
    if(!candidate||candidate.facts?.length<2){toast("拆分至少需要一条候选包含两条事实。","warn");return}
    splitGroups=candidate.facts.map(item=>[item.id]);
  }
  if(draftState.busy)return;
  const operationId=`review-${Date.now()}-${Math.random().toString(36).slice(2,10)}`;
  draftState.busy=true;renderDraftBusy(true);
  try{
    const data=await requestJson("/api/ai/authoring/draft/review-decision",{method:"POST",body:JSON.stringify({draftId:draftState.draftId,operation,candidateIds:ids,orderedCandidateIds:operation==="reorder"?draftState.candidates.map(item=>item.candidate_id):null,splitGroups,operationId,expectedGenerationId:draftState.candidateSetGenerationId||null,expectedSourceContentHash:draftState.candidateSetSourceContentHash||null,expectedPacketHash:draftState.candidateSetPacketHash||null})});
    const decision=data.decision||{};
    const projection=data.reviewProjection;
    const authoritative=data.candidateSet;
    if(Array.isArray(authoritative?.candidates)){
      draftState.candidateSetGenerationId=projection.generation_id||"";
      draftState.candidateSetSourceContentHash=projection.source_content_hash||"";
      draftState.candidateSetPacketHash=projection.packet_hash||"";
      draftState.candidates=authoritative.candidates.map(item=>{
        const candidate=draftNormalizeCandidate(item);
        const review=projection?.candidates?.find(value=>value.candidate_id===candidate?.candidate_id)||{};
        return candidate?{...candidate,risk:review.risk||"yellow",riskReasons:review.risk_reasons||[],evidenceCount:Number(review.evidence_count||0)}:null;
      }).filter(Boolean);
      const current=draftState.candidates.find(item=>item.candidate_id===draftState.selectedCandidateId&&(item.reviewStatus==="pending"||item.reviewStatus==="kept")&&item.evidenceCurrent!==false);
      const replacement=current||draftState.candidates.find(item=>(item.reviewStatus==="pending"||item.reviewStatus==="kept")&&item.evidenceCurrent!==false);
      if(replacement)draftSelectCandidate(replacement.candidate_id,false);
      else{draftState.selectedCandidateId="";draftState.candidateDetailId="";draftState.facts=[];draftState.metadata=null;draftState.expressions=[]}
    }else{
      const materialized=Array.isArray(decision.materialized_candidates)?decision.materialized_candidates.map(draftNormalizeCandidate):[];
      if(operation==="discard")draftState.candidates.forEach(item=>{if(ids.includes(item.candidate_id))item.reviewStatus="discarded"});
      if(materialized.length)draftState.candidates.push(...materialized);
    }
    draftState.selectedCandidateIds=[];
    renderDraftResults();draftPersist();
    toast(operation==="merge"?"已生成合并候选草稿。":operation==="split"?"已生成拆分候选草稿。":operation==="discard"?"已标记候选待丢弃。":"审查决策已记录，仍需继续复核。","good");
  }catch(error){handleError(error)}
  finally{draftState.busy=false;renderDraftBusy(false)}
}

function draftReview(type,index,action){
  const list=type==="fact"?draftState.facts:draftState.expressions;const item=list[index];if(!item)return;
  draftMarkChanged();
  item.reviewStatus=action==="accept"?"accepted":"pending";
  renderDraftResults();
  draftPersist();
}

function draftTextChanged(event){
  const editor=event.target.closest("[data-draft-text]");if(!editor)return;
  const list=editor.dataset.draftText==="fact"?draftState.facts:draftState.expressions;
  const item=list[Number(editor.dataset.draftIndex)];if(!item)return;
  draftMarkChanged();
  item.text=editor.value;
  if(item.reviewStatus==="accepted"){
    item.reviewStatus="pending";
    const card=editor.closest(".draft-card");
    card?.classList.remove("accepted");
    const button=card?.querySelector("[data-draft-review]");
    if(button){button.dataset.draftAction="accept";button.textContent="采纳这条"}
    const create=$("draftCreateDocument");
    if(create)create.disabled=!draftCanCreate()||draftState.busy;
  }
  draftPersist();
}

async function draftCreateDocument(){
  if(!draftCanCreate()||draftState.busy)return;
  if(window.awakeEnsureSaved&&!await window.awakeEnsureSaved("从参考资料创建新档案"))return;
  draftState.metadata={...(draftState.metadata||{}),title:$("draftTitleCandidate").value.trim(),summary:$("draftSummaryCandidate").value.trim(),domain:$("draftDomainCandidate")?.value||draftState.metadata?.domain||null};
  const snapshot=draftSnapshot("create-document");
  const acceptedFacts=clone(draftState.facts.filter(item=>item.reviewStatus==="accepted"));
  const acceptedExpressions=clone(draftState.expressions.filter(item=>item.reviewStatus==="accepted"));
  const metadata=clone(draftState.metadata);
  const referenceContext=draftContext();
  const request=draftBeginRequest(snapshot);
  draftState.busy=true;renderDraftBusy(true);
  try{
     const data=await requestJson("/api/ai/authoring/draft/create-document",{method:"POST",body:JSON.stringify({draftId:draftState.draftId,candidateId:draftState.selectedCandidateId||null,title:metadata.title,summary:metadata.summary,domain:metadata.domain||null,facts:acceptedFacts,expressions:acceptedExpressions,contentTier:$("draftContentTier")?.value||null,adultConfirmed:$("draftAdultConfirmed")?.checked===true,subdomain:metadata.subdomain||$("draftRequestedSubdomain")?.value.trim()||null,relatedDomains:metadata.relatedDomains||[]}),signal:request.controller?.signal});
    if(!draftRequestIsCurrent(snapshot)){toast("档案已经创建，但草稿在请求期间发生了变化，未自动切换。请在档案列表中打开并检查。","warn");return}
    await loadDocuments();
    if(!draftRequestIsCurrent(snapshot)){toast("档案已经创建，但草稿在请求期间发生了变化，未自动切换。请在档案列表中打开并检查。","warn");return}
    await openDocument(data.document.path);if(state.currentPath!==data.document.path){toast("档案已经创建，但没有切换当前编辑档案。请先处理当前档案的未保存修改。","warn");return}$("draftDialog").close();applyDraftToAuthorModel(acceptedFacts,acceptedExpressions,metadata);resetDraftState();toast("草稿已进入作者表单，请继续补充分类、身份权限并保存。","good");
    draftLocalStore.discard(referenceContext);
  }catch(error){
    if(error?.name!=="AbortError"){
      if(draftRequestIsCurrent(snapshot))handleError(error);
      else toast("建档请求已发出，但草稿已发生变化，暂时无法确认是否创建成功。请刷新档案列表检查。","warn");
    }
  }finally{if(draftState.activeRequest===request){draftState.busy=false;draftFinishRequest(request);renderDraftBusy(false)}}
}

function applyDraftToAuthorModel(facts,expressions,metadata){
  if(!state.authorModel)return;
  state.authorModel.title=metadata?.title||state.authorModel.title;
  state.authorModel.summary=metadata?.summary||state.authorModel.summary;
  if(metadata?.domain&&domainEntry(metadata.domain)){state.authorModel.domain=metadata.domain;if(metadata.subdomain&&subdomainEntry(metadata.domain,metadata.subdomain))state.authorModel.subdomain=metadata.subdomain}
  state.authorModel.assertions=[];
  facts.forEach((fact,index)=>{
    const assertion={clientKey:key(),kind:fact.kind||"fact",text:fact.text||"",sourceMode:"author_created",editable:true,expressions:[]};
    const related=expressions.filter(expression=>Array.isArray(expression.factIds)&&expression.factIds.includes(fact.id));
    related.forEach(expression=>{
      const grants=(expression.profileIds||[]).map(profileId=>{const profile=draftProfileDisplay(profileId,true);return {key:key(),profileId,profileLabel:profile.label,scope:"local",minDetail:expression.layer||"summary",conditions:{}}});
      assertion.expressions.push({clientKey:key(),layer:expression.layer||"summary",text:expression.text||"",sourceMode:"author_created",editable:true,grants,denies:[],fallbackReferrals:[]});
    });
    state.authorModel.assertions.push(assertion);
  });
  state.step=1;renderAll();refreshDirty();
}

function draftDefaultPerspectives(){return ""}

function draftResetVisibleFields(){
  const values={draftSourceText:"",draftSourceName:"",draftTitleCandidate:"",draftSummaryCandidate:"",draftAuthoringGoal:"",draftUserInstruction:"",draftDomainCandidate:"",draftRequestedDomain:"",draftRequestedSubdomain:"",draftRequestedAudience:"",draftStyleConstraints:"",draftMustPreserve:"",draftMustNotInvent:""};
  Object.entries(values).forEach(([id,value])=>{const node=$(id);if(node)node.value=value});
  if($("draftMode"))$("draftMode").value="quick_authoring";
  if($("draftProvider"))$("draftProvider").value="local";
  if($("draftEntryKind"))$("draftEntryKind").value="general";
  if($("draftContentTier"))$("draftContentTier").value="";
  if($("draftAdultConfirmed"))$("draftAdultConfirmed").checked=false;
  if($("draftPerspectives"))$("draftPerspectives").value=draftDefaultPerspectives();
  draftState.uiStep="input";
  if($("draftCandidateFilter"))$("draftCandidateFilter").value="";
  if($("draftCandidatesResults"))$("draftCandidatesResults").innerHTML="";
  if($("draftCandidateDetail")){$("draftCandidateDetail").hidden=true;$("draftCandidateDetail").innerHTML=""}
  if($("draftFactsResults"))$("draftFactsResults").innerHTML="";
  if($("draftExpressionsResults"))$("draftExpressionsResults").innerHTML="";
}

function openDraftDialog(){
  resetDraftState();
  const dialog=$("draftDialog");
  if(dialog?.dataset?.draftView!=="quick"){
    if(dialog?.remove)dialog.remove();
    document.body.insertAdjacentHTML("beforeend",draftQuickMarkup());
    draftEnsureDiagnosticsPanel();
    draftEnsureCandidatePanel();
    wireDraftDialog();
    draftEnsureProviderHint();
  }
  draftResetVisibleFields();draftSetStage("facts");draftUpdateStats();draftSetFlowStatus("准备好后点击“开始生成草稿”。");$("draftDialog").showModal();draftRestoreLocal();draftRestoreServer();
}

function openLegacyDraftDialog(){
  resetDraftState();
  const dialog=$("draftDialog");
  if(dialog?.remove)dialog.remove();
  document.body.insertAdjacentHTML("beforeend",draftLegacyMarkup());
  draftEnsureLegacyFields();
  draftEnsureDiagnosticsPanel();
  draftEnsureCandidatePanel();
  wireDraftDialog();
  draftResetVisibleFields();
  $("draftMode").value="legacy_staged";
  draftSetStage("facts");
  draftApplyFacadeMode();
  draftUpdateStats();
  draftSetFlowStatus("兼容分阶段流程：先提取事实，再生成简介和身份表达。");
  $("draftDialog").showModal();
  draftRestoreLocal();
  draftRestoreServer();
}

function closeDraftDialog(){
  draftPersistNow();
  draftInvalidate();
  draftState.busy=false;
  const dialog=$("draftDialog");
  if(dialog?.open)dialog.close();
  renderDraftBusy(false);
}

function wireDraftDialog(){
  $("draftCloseButton")?.addEventListener("click",closeDraftDialog);
  $("draftSourceText")?.addEventListener("input",()=>{draftSourceEdited();draftUpdateStats();draftPersist()});
  $("draftSourceName")?.addEventListener("input",()=>{draftMarkChanged();draftPersist()});
  $("draftProvider")?.addEventListener("change",()=>{draftMarkChanged();draftApplyProviderHint();draftPersist()});
  $("draftPerspectives")?.addEventListener("input",()=>{draftMarkChanged();draftPersist()});
  $("draftSourceFile")?.addEventListener("change",async event=>{const file=event.target.files?.[0];if(!file)return;try{$("draftSourceText").value=await file.text();if(!$("draftSourceName").value)$("draftSourceName").value=file.name;draftSourceEdited();draftUpdateStats();draftPersist()}catch{toast("无法读取这个文件，请改用 UTF-8 文本文件或直接粘贴内容。","warn")}});
  document.querySelectorAll("[data-draft-stage]").forEach(button=>button.addEventListener("click",()=>draftSetStage(button.dataset.draftStage)));
  $("draftGenerateFacts")?.addEventListener("click",draftGenerateLegacyFacts);
  $("draftGenerateMetadata")?.addEventListener("click",()=>draftGenerate("metadata"));
  $("draftGenerateExpressions")?.addEventListener("click",()=>draftGenerate("expressions"));
  $("draftStartQuickButton")?.addEventListener("click",draftGenerateQuickCandidate);
  $("draftOpenLegacyButton")?.addEventListener("click",openLegacyDraftDialog);
  $("draftAcceptAllFacts")?.addEventListener("click",()=>{draftMarkChanged();draftState.facts.forEach(item=>item.reviewStatus="accepted");renderDraftResults();draftPersist()});
  $("draftCreateDocument")?.addEventListener("click",draftCreateDocument);
   $("draftCandidatesResults")?.addEventListener("click",event=>{const decision=event.target.closest("[data-draft-review-decision]");if(decision){draftReviewDecision(decision.dataset.draftReviewDecision);return}const button=event.target.closest("[data-draft-candidate]");if(button)draftSelectCandidate(button.dataset.draftCandidate)});
   $("draftCandidatesResults")?.addEventListener("change",event=>{const checkbox=event.target.closest("[data-draft-candidate-check]");if(!checkbox)return;const id=checkbox.dataset.draftCandidateCheck;if(checkbox.checked){if(!draftState.selectedCandidateIds.includes(id))draftState.selectedCandidateIds.push(id)}else draftState.selectedCandidateIds=draftState.selectedCandidateIds.filter(value=>value!==id);draftMarkChanged();draftPersist()});
   $("draftCandidateFilter")?.addEventListener("input",event=>{draftState.candidateFilter=event.target.value||"";renderDraftResults();draftPersist()});
   $("draftFactsResults")?.addEventListener("click",draftReviewEvent);
  $("draftExpressionsResults")?.addEventListener("click",draftReviewEvent);
  $("draftFactsResults")?.addEventListener("input",draftTextChanged);
  $("draftExpressionsResults")?.addEventListener("input",draftTextChanged);
  $("draftTitleCandidate")?.addEventListener("input",()=>{draftMarkChanged();renderDraftResults();draftPersist()});
  $("draftSummaryCandidate")?.addEventListener("input",()=>{draftMarkChanged();renderDraftResults();draftPersist()});
  $("draftDomainCandidate")?.addEventListener("change",()=>{draftMarkChanged();renderDraftResults();draftPersist()});
  ["draftMode","draftEntryKind","draftAuthoringGoal","draftUserInstruction","draftRequestedDomain","draftRequestedSubdomain","draftRequestedAudience","draftStyleConstraints","draftMustPreserve","draftMustNotInvent","draftContentTier"].forEach(id=>{
    const node=$(id);if(!node)return;
    const eventName=node.tagName==="SELECT"?"change":"input";
    node.addEventListener(eventName,()=>{draftMarkChanged();if(id==="draftMode"||id==="draftContentTier")draftApplyFacadeMode();renderDraftResults();draftPersist()});
  });
  $("draftAdultConfirmed")?.addEventListener("change",()=>{draftState.adultConfirmed=$("draftAdultConfirmed").checked===true;draftMarkChanged();renderDraftResults();draftPersist()});
}

function draftReviewEvent(event){const button=event.target.closest("[data-draft-review]");if(button)draftReview(button.dataset.draftReview,Number(button.dataset.draftIndex),button.dataset.draftAction)}

$("newFromReferenceButton").addEventListener("click",openDraftDialog);
window.addEventListener?.("pagehide",draftPersistNow);
