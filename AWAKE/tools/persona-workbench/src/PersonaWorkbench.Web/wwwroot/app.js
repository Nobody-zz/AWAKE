const form = document.querySelector("#preview-form");
const output = document.querySelector("#dsl-output");
const status = document.querySelector("#status");
const loadButton = document.querySelector("#load-document");
const saveButton = document.querySelector("#save-document");
const approveButton = document.querySelector("#approve-document");
const reviewStatusElement = document.querySelector("#review-status");
const providerEndpointInput = document.querySelector("#provider-endpoint");
const providerModeInput = document.querySelector("#provider-mode");
const providerProtocolInput = document.querySelector("#provider-protocol");
const providerEndpointHint = document.querySelector("#provider-endpoint-hint");
const providerModelInput = document.querySelector("#provider-model");
const providerKeyInput = document.querySelector("#provider-api-key");
const providerPromptInput = document.querySelector("#provider-prompt");
const expansionDirectionInput = document.querySelector("#expansion-direction");
const expansionFocusPresetInput = document.querySelector("#expansion-focus-preset");
const expansionKeywordInput = document.querySelector("#expansion-keyword-input");
const expansionKeywordWeightInput = document.querySelector("#expansion-keyword-weight");
const addExpansionKeywordButton = document.querySelector("#add-expansion-keyword");
const expansionKeywordList = document.querySelector("#expansion-keyword-list");
const expansionAvoidTopicsInput = document.querySelector("#expansion-avoid-topics");
const providerStatus = document.querySelector("#provider-status");
let providerUsage = null;

function clearProviderUsage() { providerUsage = null; }
function renderProviderUsage() {
  if (!providerUsage) return "Provider 用量：不可用";
  return `Provider 用量：输入 ${providerUsage.promptTokens}，输出 ${providerUsage.completionTokens}，合计 ${providerUsage.totalTokens}`;
}
const setProviderKeyButton = document.querySelector("#set-provider-key");
const clearProviderKeyButton = document.querySelector("#clear-provider-key");
const confirmCloudProviderButton = document.querySelector("#confirm-cloud-provider");
const generateProviderDraftButton = document.querySelector("#generate-provider-draft");
const expandDescriptionButton = document.querySelector("#expand-description");
const cancelProviderDraftButton = document.querySelector("#cancel-provider-draft");
const expandedDescriptionInput = document.querySelector("#expanded-description");
const useExpandedDescriptionButton = document.querySelector("#use-expanded-description");
const convertExpandedToDslButton = document.querySelector("#convert-expanded-to-dsl");
const generateAwakeAuthoringPreviewButton = document.querySelector("#generate-awake-authoring-preview");
const downloadAwakeAuthoringButton = document.querySelector("#download-awake-authoring");
const authoringPreviewStatus = document.querySelector("#authoring-preview-status");
const authoringPreviewOutput = document.querySelector("#authoring-preview-output");
const providerFailures = document.querySelector("#provider-failures");
const materialPasteInput = document.querySelector("#material-paste");
const materialFilesInput = document.querySelector("#material-files");
const readMaterialsButton = document.querySelector("#read-materials");
const segmentMaterialsButton = document.querySelector("#segment-materials");
const materialStatus = document.querySelector("#material-status");
const materialSourcesElement = document.querySelector("#material-sources");
const materialSegmentsElement = document.querySelector("#material-segments");
const generateBatchButton = document.querySelector("#generate-batch");
const cancelBatchButton = document.querySelector("#cancel-batch");
const downloadBatchButton = document.querySelector("#download-batch");
const batchStatus = document.querySelector("#batch-status");
const batchResultsElement = document.querySelector("#batch-results");
const providerSettings = document.querySelector("#provider-settings");
let sessionGrant = null;
let contentHash = null;
let providerAbortController = null;
let providerCooldownTimer = null;
let providerOperationInFlight = false;
let providerOperationGeneration = 0;
let expansionKeywords = [];
let currentReviewStatus = "draft";
let documentMetadata = {
  schemaVersion: "persona-workbench.character.v1",
  sourcePackId: "",
  templateVersion: "persona-load.v2"
};
let documentEpoch = 0;
let documentEditEpoch = 0;
let expandedEditEpoch = 0;
let documentOperationGeneration = 0;
let documentOperationInFlight = false;
let previewRequestGeneration = 0;
let previewAbortController = null;
let authoringPreviewGeneration = 0;
let authoringPreviewAbortController = null;
let latestAuthoringPreview = null;
let providerFailureGeneration = 0;
let isApplyingDocument = false;
let expandedTextOrigin = "none";
let materialSources = [];
let materialSegments = [];
let latestBatchPayload = null;
let activeBatchId = null;
let batchProgressTimer = null;
let batchProgressGeneration = 0;

function documentWorkspaceKey() {
  return document.querySelector("#workspace-root").value + "\n" + document.querySelector("#file-name").value;
}

function captureDocumentState() {
  return {
    documentEpoch,
    documentEditEpoch,
    expandedEditEpoch,
    workspaceKey: documentWorkspaceKey()
  };
}

function isCurrentDocumentState(snapshot) {
  return snapshot
    && snapshot.documentEpoch === documentEpoch
    && snapshot.documentEditEpoch === documentEditEpoch
    && snapshot.expandedEditEpoch === expandedEditEpoch
    && snapshot.workspaceKey === documentWorkspaceKey();
}

function setDocumentOperationBusy(isBusy) {
  loadButton.disabled = isBusy;
  saveButton.disabled = isBusy;
  approveButton.disabled = isBusy;
}

function invalidateAsyncDocumentOperations() {
  documentEpoch++;
  providerOperationGeneration++;
  previewRequestGeneration++;
  authoringPreviewGeneration++;
  if (providerAbortController) providerAbortController.abort();
  if (previewAbortController) previewAbortController.abort();
  if (authoringPreviewAbortController) authoringPreviewAbortController.abort();
  latestAuthoringPreview = null;
  if (downloadAwakeAuthoringButton) downloadAwakeAuthoringButton.disabled = true;
}

function clearDocumentTransientState() {
  invalidateAsyncDocumentOperations();
  expandedDescriptionInput.value = "";
  providerPromptInput.value = "";
  providerStatus.textContent = "";
  expandedTextOrigin = "none";
  clearProviderUsage();
  providerFailures.replaceChildren();
  expandedEditEpoch++;
}

function applyDocumentMetadata(documentValue) {
  if (typeof documentValue.schemaVersion === "string" && documentValue.schemaVersion) {
    documentMetadata.schemaVersion = documentValue.schemaVersion;
  }
  if (Object.prototype.hasOwnProperty.call(documentValue, "sourcePackId")) {
    documentMetadata.sourcePackId = documentValue.sourcePackId || "";
  }
  if (typeof documentValue.templateVersion === "string" && documentValue.templateVersion) {
    documentMetadata.templateVersion = documentValue.templateVersion;
  }
}

async function requestLaunchToken() {
  const response = await fetch("/api/session/launch-token", { method: "POST" });
  if (!response.ok) return "";
  const payload = await response.json();
  return typeof payload.token === "string" ? payload.token : "";
}

async function exchangeBootstrapToken(token) {
  return fetch("/api/session/bootstrap", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ token })
  });
}

async function bootstrapSession() {
  let bootstrapToken = window.location.hash.slice(1);
  history.replaceState(null, "", window.location.pathname + window.location.search);
  if (!bootstrapToken) bootstrapToken = await requestLaunchToken();
  if (!bootstrapToken) {
    status.textContent = "本地保存会话初始化失败；可以继续预览。";
    return;
  }

  let response = await exchangeBootstrapToken(bootstrapToken);
  if (!response.ok) {
    bootstrapToken = await requestLaunchToken();
    if (bootstrapToken) response = await exchangeBootstrapToken(bootstrapToken);
  }
  if (!response.ok) {
    status.textContent = "本地保存会话初始化失败；可以继续预览。";
    return;
  }
  sessionGrant = await response.json();
  status.textContent = "本地会话已就绪；文件只会写入你选择的工作区。";
}
function selectedTags() {
  return Array.from(document.querySelectorAll("input[type=checkbox]:checked"), (input) => input.value);
}

function selectedFacetStrengths() {
  return Object.fromEntries(Array.from(document.querySelectorAll("select.facet-strength"))
    .filter((select) => select.value)
    .map((select) => [select.dataset.facetId, Number(select.value)]));
}

function selectedAxis(id) {
  const value = document.querySelector(id).value;
  return value === "" ? null : Number(value);
}

function traitProfile() {
  return {
    caution: selectedAxis("#trait-caution"),
    ambition: selectedAxis("#trait-ambition"),
    pride: selectedAxis("#trait-pride"),
    pragmatism: selectedAxis("#trait-pragmatism"),
    inGroupLoyalty: selectedAxis("#trait-in-group-loyalty"),
    tradition: selectedAxis("#trait-tradition")
  };
}

function expressionProfile() {
  return {
    restraint: selectedAxis("#expression-restraint"),
    directness: selectedAxis("#expression-directness"),
    formality: selectedAxis("#expression-formality"),
    playfulness: selectedAxis("#expression-playfulness"),
    warmth: selectedAxis("#expression-warmth")
  };
}

function behaviorProfile() {
  return {
    conditionality: selectedAxis("#behavior-conditionality"),
    deliberation: selectedAxis("#behavior-deliberation"),
    trustTesting: selectedAxis("#behavior-trust-testing"),
    leverage: selectedAxis("#behavior-leverage"),
    inGroupPriority: selectedAxis("#behavior-in-group-priority"),
    leadership: selectedAxis("#behavior-leadership")
  };
}

function reactionProfile() {
  return {
    confrontation: selectedAxis("#reaction-confrontation"),
    expression: selectedAxis("#reaction-expression"),
    timing: selectedAxis("#reaction-timing"),
    resentment: selectedAxis("#reaction-resentment"),
    supportSeeking: selectedAxis("#reaction-support-seeking"),
    sensitiveConditions: document.querySelector("#reaction-sensitive-conditions").value,
    conditionalResponses: document.querySelector("#reaction-conditional-responses").value
  };
}

function commitmentProfile() {
  return {
    promiseCaution: selectedAxis("#commitment-promise-caution"),
    promisePersistence: selectedAxis("#commitment-promise-persistence"),
    valueTradeability: selectedAxis("#commitment-value-tradeability"),
    priorityOrder: document.querySelector("#commitment-priority-order").value,
    protectedValues: document.querySelector("#commitment-protected-values").value,
    applicableScope: document.querySelector("#commitment-applicable-scope").value,
    exceptionCost: document.querySelector("#commitment-exception-cost").value,
    breachResponse: document.querySelector("#commitment-breach-response").value
  };
}

function setReviewStatus(value) {
  currentReviewStatus = value === "approved" ? "approved" : "draft";
  reviewStatusElement.textContent = currentReviewStatus === "approved" ? "审核状态：已批准" : "审核状态：草稿";
}

function linesFromField(id) {
  return document.querySelector(id).value.split(/\r?\n/).map((value) => value.trim()).filter(Boolean);
}

function setLinesField(id, values) {
  document.querySelector(id).value = Array.isArray(values) ? values.join("\n") : "";
}

function applyAuthorFields(documentValue) {
  document.querySelector("#persona-summary").value = documentValue.summary || "";
  document.querySelector("#source-description").value = documentValue.sourceDescription || "";
  document.querySelector("#public-description").value = documentValue.publicDescription || "";
  document.querySelector("#private-description").value = documentValue.privateDescription || "";
  document.querySelector("#contradiction-description").value = documentValue.contradictionDescription || "";
  setLinesField("#self-claim-rules", documentValue.selfClaimRules);
  setLinesField("#real-self-behaviors", documentValue.realSelfBehaviors);
  setLinesField("#self-claim-examples", documentValue.selfClaimExamples);
  setReviewStatus(documentValue.status);
}

function setAxis(id, value) {
  document.querySelector(id).value = value === null || value === undefined ? "" : String(value);
}
function applyProfiles(documentValue) {
  const traits = documentValue.traitProfile || {};
  setAxis("#trait-caution", traits.caution);
  setAxis("#trait-ambition", traits.ambition);
  setAxis("#trait-pride", traits.pride);
  setAxis("#trait-pragmatism", traits.pragmatism);
  setAxis("#trait-in-group-loyalty", traits.inGroupLoyalty);
  setAxis("#trait-tradition", traits.tradition);

  const expression = documentValue.expressionProfile || {};
  setAxis("#expression-restraint", expression.restraint);
  setAxis("#expression-directness", expression.directness);
  setAxis("#expression-formality", expression.formality);
  setAxis("#expression-playfulness", expression.playfulness);
  setAxis("#expression-warmth", expression.warmth);

  const behavior = documentValue.behaviorProfile || {};
  setAxis("#behavior-conditionality", behavior.conditionality);
  setAxis("#behavior-deliberation", behavior.deliberation);
  setAxis("#behavior-trust-testing", behavior.trustTesting);
  setAxis("#behavior-leverage", behavior.leverage);
  setAxis("#behavior-in-group-priority", behavior.inGroupPriority);
  setAxis("#behavior-leadership", behavior.leadership);

  const reaction = documentValue.reactionProfile || {};
  setAxis("#reaction-confrontation", reaction.confrontation);
  setAxis("#reaction-expression", reaction.expression);
  setAxis("#reaction-timing", reaction.timing);
  setAxis("#reaction-resentment", reaction.resentment);
  setAxis("#reaction-support-seeking", reaction.supportSeeking);
  document.querySelector("#reaction-sensitive-conditions").value = reaction.sensitiveConditions || "";
  document.querySelector("#reaction-conditional-responses").value = reaction.conditionalResponses || "";

  const commitment = documentValue.commitmentProfile || {};
  setAxis("#commitment-promise-caution", commitment.promiseCaution);
  setAxis("#commitment-promise-persistence", commitment.promisePersistence);
  setAxis("#commitment-value-tradeability", commitment.valueTradeability);
  document.querySelector("#commitment-priority-order").value = commitment.priorityOrder || "";
  document.querySelector("#commitment-protected-values").value = commitment.protectedValues || "";
  document.querySelector("#commitment-applicable-scope").value = commitment.applicableScope || "";
  document.querySelector("#commitment-exception-cost").value = commitment.exceptionCost || "";
  document.querySelector("#commitment-breach-response").value = commitment.breachResponse || "";
}

function applyEditableDocument(documentValue, options = {}) {
  const previousApplyingState = isApplyingDocument;
  isApplyingDocument = true;
  try {
    if (!options.preserveMetadata) applyDocumentMetadata(documentValue);
    document.querySelector("#persona-id").value = documentValue.id || "";
    document.querySelector("#display-name").value = documentValue.displayName || "";
    document.querySelector("#core").value = documentValue.core || "";
    document.querySelector("#identity-facts").value = documentValue.identityFacts || "";
    applyAuthorFields(documentValue);
    document.querySelectorAll("input[data-tag-id]").forEach((input) => {
      input.checked = (documentValue.tags || []).includes(input.value);
    });
    document.querySelectorAll("select.facet-strength").forEach((select) => {
      const strength = documentValue.facetStrengths?.[select.dataset.facetId];
      select.value = strength === null || strength === undefined ? "" : String(strength);
    });
    applyProfiles(documentValue);
  } finally {
    isApplyingDocument = previousApplyingState;
  }
}

function clearDerivedPersonaFields() {
  document.querySelector("#identity-facts").value = "";
  document.querySelector("#persona-summary").value = "";
  document.querySelector("#public-description").value = "";
  document.querySelector("#private-description").value = "";
  document.querySelector("#contradiction-description").value = "";
  setLinesField("#self-claim-rules", []);
  setLinesField("#real-self-behaviors", []);
  setLinesField("#self-claim-examples", []);
  document.querySelectorAll("input[data-tag-id]").forEach((input) => { input.checked = false; });
  document.querySelectorAll("select.facet-strength").forEach((select) => { select.value = ""; });
  applyProfiles({});
}

function derivePersonaFileName(displayName, stableId) {
  const fallback = (stableId || "persona-generated").split(".").pop() || "persona-generated";
  const stem = (displayName || fallback)
    .replace(/[<>:"/\\|?*\u0000-\u001F]/g, "_")
    .trim()
    .replace(/[. ]+$/g, "")
    .slice(0, 120);
  return (stem || "persona-generated") + ".persona.json";
}
function currentDocument() {
  return {
    schemaVersion: documentMetadata.schemaVersion,
    id: document.querySelector("#persona-id").value,
    displayName: document.querySelector("#display-name").value,
    core: document.querySelector("#core").value,
    identityFacts: document.querySelector("#identity-facts").value,
    sourcePackId: documentMetadata.sourcePackId,
    templateVersion: documentMetadata.templateVersion,
    status: currentReviewStatus,
    tags: selectedTags(),
    facetStrengths: selectedFacetStrengths(),
    traitProfile: traitProfile(),
    expressionProfile: expressionProfile(),
    behaviorProfile: behaviorProfile(),
    reactionProfile: reactionProfile(),
    commitmentProfile: commitmentProfile(),
    summary: document.querySelector("#persona-summary").value,
    sourceDescription: document.querySelector("#source-description").value,
    publicDescription: document.querySelector("#public-description").value,
    privateDescription: document.querySelector("#private-description").value,
    contradictionDescription: document.querySelector("#contradiction-description").value,
    selfClaimRules: linesFromField("#self-claim-rules"),
    realSelfBehaviors: linesFromField("#real-self-behaviors"),
    selfClaimExamples: linesFromField("#self-claim-examples")
  };
}

function currentAwakeAuthoringRequest() {
  return {
    ...currentDocument(),
    expandedText: expandedDescriptionInput.value,
    expandedTextOrigin: expandedDescriptionInput.value.trim() ? expandedTextOrigin : "none"
  };
}

async function readJsonOrEmpty(response) {
  try { return await response.json(); } catch { return {}; }
}

function handleExpiredSession(response) {
  if (response.status !== 401) return false;
  sessionGrant = null;
  status.textContent = "本地会话已过期；请重新打开工作台启动链接后再保存或读取。当前表单内容未被覆盖。";
  return true;
}

function workspaceRequest() {
  return {
    rootPath: document.querySelector("#workspace-root").value,
    fileName: document.querySelector("#file-name").value
  };
}

function sessionHeaders() {
  if (!sessionGrant) return null;
  return {
    "Content-Type": "application/json",
    "X-Pwb-Session": sessionGrant.sessionToken,
    "X-Pwb-Csrf": sessionGrant.csrfToken
  };
}

function providerRequest() {
  return {
    endpoint: providerEndpointInput.value,
    model: providerModelInput.value,
    providerProtocol: providerProtocolInput.value || "ollama",
    prompt: providerPromptInput.value
  };
}

function expansionControls() {
  return {
    direction: expansionDirectionInput.value,
    focusPreset: expansionFocusPresetInput.value || "balanced",
    focusKeywords: expansionKeywords.map((keyword) => ({ text: keyword.text, weight: keyword.weight })),
    avoidTopics: expansionAvoidTopicsInput.value.split(/[，,\n]/).map((item) => item.trim()).filter(Boolean)
  };
}

function createBatchId() {
  if (window.crypto && typeof window.crypto.randomUUID === "function") return "batch-" + window.crypto.randomUUID();
  return "batch-" + Date.now().toString(36) + "-" + Math.random().toString(36).slice(2, 12);
}

function stopBatchProgressPolling() {
  batchProgressGeneration++;
  if (batchProgressTimer !== null) {
    clearTimeout(batchProgressTimer);
    batchProgressTimer = null;
  }
}

function formatBatchProgress(payload) {
  const completed = Number.isFinite(Number(payload?.completed)) ? Number(payload.completed) : 0;
  const total = Number.isFinite(Number(payload?.total)) ? Number(payload.total) : 0;
  const succeeded = Number.isFinite(Number(payload?.succeeded)) ? Number(payload.succeeded) : 0;
  const failed = Number.isFinite(Number(payload?.failed)) ? Number(payload.failed) : 0;
  if (payload?.status !== "running") {
    const label = payload?.status === "cancelled" ? "批量制作已停止" : payload?.status === "completed" ? "批量制作完成" : "批量制作已结束";
    return label + "：成功 " + succeeded + " 条，失败 " + failed + " 条。";
  }
  const current = payload.currentTitle ? "当前：" + payload.currentTitle : "正在等待下一条资料";
  return "正在制作：" + completed + " / " + total + " 条；成功 " + succeeded + "，失败 " + failed + "。" + current;
}

function startBatchProgressPolling(batchId) {
  stopBatchProgressPolling();
  const generation = batchProgressGeneration;
  const poll = async () => {
    if (generation !== batchProgressGeneration || activeBatchId !== batchId) return;
    let shouldContinue = true;
    try {
      const headers = sessionHeaders();
      if (!headers) return;
      const response = await fetch("/api/provider/batch-progress/" + encodeURIComponent(batchId), { headers });
      const payload = await readJsonOrEmpty(response);
      if (generation !== batchProgressGeneration || activeBatchId !== batchId) return;
      if (response.ok && payload.status) {
        renderBatchResults(payload);
        batchStatus.textContent = formatBatchProgress(payload);
        shouldContinue = payload.status === "running";
      } else if (response.status !== 404) {
        batchStatus.textContent = "批量进度暂时无法读取；生成请求仍会继续，结果不会覆盖当前编辑器。";
      }
    } catch {
      if (generation === batchProgressGeneration && activeBatchId === batchId) {
        batchStatus.textContent = "正在等待批量结果；本机进度查询暂时中断。";
      }
    }
    if (shouldContinue && generation === batchProgressGeneration && activeBatchId === batchId) {
      batchProgressTimer = setTimeout(poll, 700);
    }
  };
  void poll();
}

function setBatchInputBusy(isBusy) {
  readMaterialsButton.disabled = isBusy;
  segmentMaterialsButton.disabled = isBusy || materialSources.length === 0;
  materialPasteInput.disabled = isBusy;
  materialFilesInput.disabled = isBusy;
  materialSegmentsElement.querySelectorAll("input, textarea").forEach((element) => { element.disabled = isBusy; });
}

function renderMaterialSources() {
  materialSourcesElement.replaceChildren();
  if (materialSources.length === 0) return;
  const heading = document.createElement("p");
  heading.className = "field-hint";
  heading.textContent = "已读取 " + materialSources.length + " 个资料来源：" + materialSources.map((source) => source.sourceFile).join("、");
  materialSourcesElement.appendChild(heading);
}

function renderMaterialSegments() {
  materialSegmentsElement.replaceChildren();
  if (materialSegments.length === 0) return;
  const heading = document.createElement("p");
  heading.className = "segment-heading";
  heading.textContent = "请在这里快速检查分段；不合适的段落可以取消勾选、改标题或直接修改文字。";
  materialSegmentsElement.appendChild(heading);
  materialSegments.forEach((segment, index) => {
    const card = document.createElement("article");
    card.className = "material-segment-card";

    const header = document.createElement("div");
    header.className = "material-segment-header";
    const keepLabel = document.createElement("label");
    const keep = document.createElement("input");
    keep.type = "checkbox";
    keep.checked = segment.selected !== false;
    keep.setAttribute("aria-label", "保留资料段 " + (index + 1));
    keep.addEventListener("change", () => { materialSegments[index].selected = keep.checked; });
    keepLabel.append(keep, document.createTextNode("用于批量制作"));
    const ordinal = document.createElement("span");
    ordinal.className = "field-hint";
    ordinal.textContent = "第 " + (index + 1) + " 段 · " + (segment.sourceFile || "粘贴资料");
    header.append(keepLabel, ordinal);

    const title = document.createElement("input");
    title.value = segment.title || "资料片段 " + (index + 1);
    title.maxLength = 120;
    title.setAttribute("aria-label", "资料段标题");
    title.addEventListener("input", () => { materialSegments[index].title = title.value; });

    const text = document.createElement("textarea");
    text.rows = 6;
    text.maxLength = 24576;
    text.value = segment.text || "";
    text.setAttribute("aria-label", "资料段正文");
    text.addEventListener("input", () => { materialSegments[index].text = text.value; });

    card.append(header, title, text);
    materialSegmentsElement.appendChild(card);
  });
}

function selectedBatchItems() {
  return materialSegments
    .filter((segment) => segment.selected !== false && typeof segment.text === "string" && segment.text.trim())
    .map((segment) => ({
      itemId: segment.itemId,
      sourceOrdinal: segment.sourceOrdinal,
      sourceFile: segment.sourceFile,
      title: segment.title,
      sourceText: segment.text.trim()
    }));
}

function batchErrorMessage(errorCode) {
  if (errorCode === "batch.not_started_after_cancel") return "已取消，尚未开始处理。";
  if (errorCode === "batch.not_started") return "尚未开始处理。";
  if (errorCode === "batch.source_ordinal_invalid") return "资料段顺序标识重复；请重新整理资料后再试。";
  if (errorCode === "batch.item_id_invalid") return "资料段身份标识无效；请重新分段，不要手动复制相同资料段。";
  if (errorCode === "batch.item_source_invalid") return "有资料段为空或过长；请在分段卡片中补充或缩短正文。";
  if (errorCode === "batch.item_count_invalid") return "一次最多处理 32 段资料；请减少勾选数量后再试。";
  if (errorCode === "batch.input_size_invalid") return "本批资料总量过大；请拆成几批制作。";
  if (errorCode === "batch.internal_error") return "本批某条资料处理时出现内部错误；其他已完成结果仍可使用。";
  return providerResponseFailureMessage({ status: 0 }, { errorCode });
}

function renderBatchResults(payload) {
  batchResultsElement.replaceChildren();
  const results = Array.isArray(payload?.results) ? payload.results : [];
  results.forEach((result) => {
    const card = document.createElement("article");
    card.className = result.success ? "batch-result-card success" : "batch-result-card failure";
    const heading = document.createElement("h3");
    heading.textContent = (result.sourceOrdinal || "") + " · " + (result.title || "未命名资料段");
    const source = document.createElement("p");
    source.className = "field-hint";
    source.textContent = result.sourceFile || "粘贴资料";
    const state = document.createElement("p");
    state.className = "result-state";
    state.textContent = result.success ? "已生成草稿，可载入编辑器检查。" : "未生成：" + batchErrorMessage(result.errorCode || "batch.result_failed");
    card.append(heading, source, state);
    if (result.success) {
      const actions = document.createElement("div");
      actions.className = "actions";
      const load = document.createElement("button");
      load.type = "button";
      load.className = "secondary";
      load.textContent = "载入当前编辑器";
      load.addEventListener("click", () => {
        applyProviderDslResult(result, {
          documentStatus: "批量结果已载入当前编辑器；尚未保存或批准。",
          providerStatus: "已载入“" + (result.title || "资料片段") + "”；请检查后再保存。"
        });
      });
      actions.appendChild(load);
      const details = document.createElement("details");
      const summary = document.createElement("summary");
      summary.textContent = "查看生成的 DSL";
      const pre = document.createElement("pre");
      pre.textContent = result.dsl || "";
      details.append(summary, pre);
      card.append(actions, details);
    }
    batchResultsElement.appendChild(card);
  });
}

function downloadBatchResults() {
  if (!latestBatchPayload) {
    batchStatus.textContent = "还没有可导出的批量结果。";
    return;
  }
  const exportValue = {
    schemaVersion: "persona-workbench.batch.v1",
    batchId: latestBatchPayload.batchId,
    status: latestBatchPayload.status,
    total: latestBatchPayload.total,
    completed: latestBatchPayload.completed,
    succeeded: latestBatchPayload.succeeded,
    failed: latestBatchPayload.failed,
    cancelReason: latestBatchPayload.cancelReason || "",
    exportedAtUtc: new Date().toISOString(),
    results: latestBatchPayload.results || []
  };
  const blob = new Blob([JSON.stringify(exportValue, null, 2) + "\n"], { type: "application/json" });
  const link = document.createElement("a");
  link.href = URL.createObjectURL(blob);
  link.download = "persona-batch-" + (latestBatchPayload.batchId || "results") + ".json";
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(link.href);
  batchStatus.textContent = "批量结果已导出；文件中不包含 API Key 或会话密钥。";
}

function renderExpansionKeywords() {
  expansionKeywordList.replaceChildren();
  expansionKeywords.forEach((keyword, index) => {
    const row = document.createElement("div");
    row.className = "keyword-row";

    const textInput = document.createElement("input");
    textInput.value = keyword.text;
    textInput.maxLength = 64;
    textInput.setAttribute("aria-label", "重点关键词");
    textInput.addEventListener("input", () => { expansionKeywords[index].text = textInput.value; });

    const weightSelect = document.createElement("select");
    weightSelect.setAttribute("aria-label", "关键词关注程度");
    [["light", "轻微"], ["medium", "适中"], ["strong", "重点"]].forEach(([value, label]) => {
      const option = document.createElement("option");
      option.value = value;
      option.textContent = label;
      option.selected = value === keyword.weight;
      weightSelect.appendChild(option);
    });
    weightSelect.addEventListener("change", () => { expansionKeywords[index].weight = weightSelect.value; });

    const removeButton = document.createElement("button");
    removeButton.type = "button";
    removeButton.className = "secondary";
    removeButton.textContent = "删除";
    removeButton.setAttribute("aria-label", "删除关键词 " + keyword.text);
    removeButton.addEventListener("click", () => {
      expansionKeywords.splice(index, 1);
      renderExpansionKeywords();
    });

    row.append(textInput, weightSelect, removeButton);
    expansionKeywordList.appendChild(row);
  });
}

addExpansionKeywordButton.addEventListener("click", () => {
  const text = expansionKeywordInput.value.trim();
  if (!text) {
    providerStatus.textContent = "请先输入重点关键词。";
    return;
  }
  if (expansionKeywords.length >= 12) {
    providerStatus.textContent = "重点关键词最多 12 个。";
    return;
  }
  const existing = expansionKeywords.find((keyword) => keyword.text.toLocaleLowerCase() === text.toLocaleLowerCase());
  if (existing) {
    existing.weight = expansionKeywordWeightInput.value;
  } else {
    expansionKeywords.push({ text, weight: expansionKeywordWeightInput.value });
  }
  expansionKeywordInput.value = "";
  renderExpansionKeywords();
});

function classifyProviderEndpoint(value) {
  try {
    const endpoint = new URL(value);
    const hostname = endpoint.hostname.replace(/^\[|\]$/g, "");
    const isLoopback = hostname === "127.0.0.1" || hostname === "localhost" || hostname === "::1";
    if (isLoopback && endpoint.protocol === "http:") return "local";
    if (endpoint.protocol === "https:") return "cloud";
    return "invalid";
  } catch {
    return "invalid";
  }
}

function updateProviderGuidance() {
  const kind = classifyProviderEndpoint(providerEndpointInput.value.trim());
  if (kind === "local") {
    providerModeInput.value = "local";
    providerEndpointHint.textContent = "当前识别为本机地址：通常不需要 API Key，也不会要求云端确认。";
    confirmCloudProviderButton.disabled = true;
    return;
  }
  if (kind === "cloud") {
    providerModeInput.value = "cloud";
    providerEndpointHint.textContent = "当前识别为 HTTPS 云端地址：需要 Key，并先点击“我确认使用这个云端地址”。";
    confirmCloudProviderButton.disabled = false;
    return;
  }
  providerEndpointHint.textContent = "地址尚未识别：本机只能使用回环 HTTP，云端必须使用 HTTPS。";
  confirmCloudProviderButton.disabled = true;
}

async function providerFetch(path, payload) {
  const headers = sessionHeaders();
  if (!headers) {
    providerStatus.textContent = "本机会话未就绪；请从工作台启动链接打开页面。";
    return null;
  }

  return fetch(path, { method: "POST", headers, body: JSON.stringify(payload || {}), signal: providerAbortController ? providerAbortController.signal : undefined });
}

async function setProviderSessionKeyFromInput() {
  const apiKey = providerKeyInput.value.trim();
  if (!apiKey) return true;

  const response = await providerFetch("/api/provider/session-key", { apiKey });
  if (!response) return false;
  if (!response.ok) {
    let payload = {};
    try { payload = await response.json(); } catch {}
    providerStatus.textContent = providerResponseFailureMessage(response, payload);
    return false;
  }

  providerKeyInput.value = "";
  return true;
}

function providerNetworkFailureMessage(error) {
  if (error && error.name === "AbortError") return "本次生成已取消；不会自动重试。";
  if (error instanceof TypeError) return "工作台服务连接失败；请重新启动工作台并从启动链接打开页面。当前表单内容未被覆盖。";
  return "工作台返回异常；当前表单内容未被覆盖。";
}

function providerResponseFailureMessage(response, payload) {
  if (response.status === 401) return "本机会话已失效；请重新启动工作台并从启动链接打开页面。当前表单内容未被覆盖。";
  if (payload.errorCode === "provider.cloud_confirmation_required") return "云端端点尚未确认；请先点击“确认云端端点”。";
  if (payload.errorCode === "provider.session_key_missing") return "本次会话尚未设置云端 API Key；请点击“设置本次会话 Key”后再扩展。当前表单内容未被覆盖。";
  if (payload.errorCode === "provider.cancelled") return "本次生成已取消；不会自动重试。";
  if (payload.errorCode === "provider.timeout") return "Provider 请求超时；不会自动重试。当前表单内容未被覆盖。";
  if (payload.errorCode === "provider.request_in_flight") return "Provider 正在处理另一个操作；请等待当前操作结束后再试。当前表单内容未被覆盖。";
  if (payload.errorCode === "provider.expansion_unchanged") return "Provider 没有扩充人物描述；当前扩充结果未被覆盖。";
  if (payload.errorCode === "provider.expansion_evidence_missing") return "扩充结果丢失了原文关键短语，已拒绝写入；当前扩充结果未被覆盖。";
  if (payload.errorCode === "provider.expansion_controls_invalid") return "扩写方向、关键词或避开主题的格式无效；请缩短或修正后重试。当前扩充结果未被覆盖。";
  if (payload.errorCode === "provider.expansion_envelope_size_invalid") return "人物描述与扩写控制合计过长；请缩短内容后重试。当前扩充结果未被覆盖。";
  if (payload.errorCode === "provider.expansion_truncated") return "Provider 输出因长度限制被截断；请缩小扩写范围后重试。当前扩充结果未被覆盖。";
  if (payload.errorCode === "provider.expansion_response_shape_invalid") return "Provider 返回结构不完整或包含多个候选；当前扩充结果未被覆盖。";
  if (payload.errorCode === "provider.expansion_text_invalid") return "Provider 返回了 JSON、代码围栏、Persona DSL 或其他非人物描述文本；当前扩充结果未被覆盖。";
  if (payload.errorCode === "provider.expansion_empty") return "Provider 没有返回人物描述；当前扩充结果未被覆盖。";
  if (payload.errorCode === "provider.intermediate_candidate_truncated") return "Provider 输出的结构化 JSON 过长，尚未完成就被截断；请重试，或缩短重复描述后再转换。当前表单内容未被覆盖。";
  if (payload.errorCode === "provider.dsl_candidate_format_invalid") return "Provider 没有返回受限 Persona DSL 候选；当前 DSL 未被覆盖。";
  if (payload.errorCode === "persona.dsl_candidate_local_metadata_forbidden") return "Provider 试图改写本地身份或审核元数据；当前 DSL 未被覆盖。";
  if (payload.errorCode === "persona.dsl_candidate_field_unknown") return "Provider 返回了未允许的 DSL 字段；当前 DSL 未被覆盖。";
  if (payload.errorCode === "provider.candidate_axis_invalid") return "Provider 某个人格轴不是可识别的 -2 到 2 数值；工作台会尽量保留其他有效字段。当前表单内容未被覆盖。";
  if (payload.errorCode === "provider.candidate_empty") return "Provider 的推理过程耗尽了输出额度，没有返回人物草稿；工作台已针对 DeepSeek 关闭 thinking，请重启新版后重试。当前表单内容未被覆盖。";
  if (payload.errorCode === "provider.candidate_id_invalid") return "Provider 返回的稳定 ID 不是字符串；工作台只能在空 ID 时本地补齐。当前表单内容未被覆盖。";
  if (payload.errorCode === "provider.candidate_display_name_invalid") return "Provider 返回的显示名不是字符串；当前表单内容未被覆盖。";
  if (payload.errorCode === "provider.candidate_core_invalid") return "Provider 返回的核心人格不是非空文本；当前表单内容未被覆盖。";
  if (payload.errorCode === "provider.candidate_identity_invalid") return "Provider 返回的身份事实不是文本；当前表单内容未被覆盖。";
  if (payload.errorCode === "provider.dsl_identity_required") return "请先填写当前角色的稳定 ID 和显示名；当前表单内容未被覆盖。";
  if (payload.errorCode === "provider.persona_structure_empty") return "Provider 只返回了人物摘要，没有可用的人格轴、标签或行为结构；当前表单与 DSL 未被覆盖。";
  if (payload.errorCode === "persona.template_core_budget_exceeded") return formatDslDiagnostic(payload.dslDiagnostic, "核心人格结构超过 DSL 安全上限；当前 DSL 未被覆盖。请减少重复证据或拆分角色内容。");
  if (payload.errorCode === "persona.template_budget_exceeded") return formatDslDiagnostic(payload.dslDiagnostic, "Persona DSL 超过安全上限；当前 DSL 未被覆盖。请缩短低价值重复证据。");
  return "Provider 返回失败（" + (payload.errorCode || "provider.request_failed") + "）；当前表单内容未被覆盖。";
}

function formatDslDiagnostic(diagnostic, fallback) {
  if (!diagnostic) return fallback;
  const detail = diagnostic.maximumBytes && diagnostic.actualBytes
    ? `（${diagnostic.actualBytes}/${diagnostic.maximumBytes} bytes）`
    : "";
  return fallback + detail;
}

function setProviderGenerating(isGenerating) {
  const cooldownActive = providerCooldownTimer !== null;
  generateProviderDraftButton.disabled = isGenerating || cooldownActive;
  expandDescriptionButton.disabled = isGenerating || cooldownActive;
  cancelProviderDraftButton.disabled = !isGenerating;
  convertExpandedToDslButton.disabled = isGenerating;
  setProviderKeyButton.disabled = isGenerating;
  clearProviderKeyButton.disabled = isGenerating;
  confirmCloudProviderButton.disabled = isGenerating || classifyProviderEndpoint(providerEndpointInput.value.trim()) !== "cloud";
  addExpansionKeywordButton.disabled = isGenerating;
  if (generateBatchButton) generateBatchButton.disabled = isGenerating || cooldownActive || selectedBatchItems().length === 0;
  if (cancelBatchButton) cancelBatchButton.disabled = !isGenerating || !activeBatchId;
  if (downloadBatchButton) downloadBatchButton.disabled = isGenerating || !latestBatchPayload;
  if (isGenerating) setBatchInputBusy(true);
  else setBatchInputBusy(false);
}

async function runProviderOperation(operation, options = {}) {
  if (providerOperationInFlight) {
    providerStatus.textContent = "Provider 正在处理另一个操作（provider.request_in_flight）；请等待当前操作结束。";
    return false;
  }

  providerOperationInFlight = true;
  const operationGeneration = ++providerOperationGeneration;
  clearProviderUsage();
  providerAbortController = options.cancellable === false ? null : new AbortController();
  setProviderGenerating(true);
  try {
    await operation(operationGeneration);
    return true;
  } finally {
    providerAbortController = null;
    providerOperationInFlight = false;
    setProviderGenerating(false);
  }
}

function isCurrentProviderOperation(generation) {
  return generation === providerOperationGeneration;
}

function captureProviderDocumentState(options = {}) {
  const snapshot = {
    document: captureDocumentState(),
    providerPrompt: providerPromptInput.value,
    providerEndpoint: providerEndpointInput.value,
    providerModel: providerModelInput.value,
    providerProtocol: providerProtocolInput.value
  };
  if (options.includeExpansionControls) snapshot.expansionControls = JSON.stringify(expansionControls());
  return snapshot;
}

function isCurrentProviderDocumentState(generation, snapshot) {
  return isCurrentProviderOperation(generation)
    && isCurrentDocumentState(snapshot.document)
    && providerPromptInput.value === snapshot.providerPrompt
    && providerEndpointInput.value === snapshot.providerEndpoint
    && providerModelInput.value === snapshot.providerModel
    && providerProtocolInput.value === snapshot.providerProtocol
    && (snapshot.expansionControls === undefined || JSON.stringify(expansionControls()) === snapshot.expansionControls);
}

function applyProviderDslResult(payload, messages) {
  const currentFileName = document.querySelector("#file-name").value.trim();
  applyEditableDocument(payload.draft, { preserveMetadata: true });
  if (!currentFileName || currentFileName === "persona.persona.json" || currentFileName === "sable.persona.json") {
    document.querySelector("#file-name").value = derivePersonaFileName(payload.draft.displayName, payload.draft.id);
  }
  contentHash = null;
  documentEditEpoch++;
  providerUsage = payload.usage ?? null;
  output.textContent = payload.dsl;
  status.textContent = messages.documentStatus;
  providerStatus.textContent = messages.providerStatus;
}
function startProviderCooldown(cooldownUntilUtc) {
  if (providerCooldownTimer) clearTimeout(providerCooldownTimer);
  const deadline = new Date(cooldownUntilUtc);
  if (!Number.isFinite(deadline.getTime())) {
    providerCooldownTimer = null;
    setProviderGenerating(false);
    return;
  }
  const update = () => {
    const seconds = Math.ceil((deadline.getTime() - Date.now()) / 1000);
    if (seconds <= 0) {
      providerCooldownTimer = null;
      setProviderGenerating(false);
      providerStatus.textContent = "Provider 冷却已结束；需要时请手动再次生成。";
      return;
    }
    generateProviderDraftButton.disabled = true;
    expandDescriptionButton.disabled = true;
    providerStatus.textContent = "Provider 正在冷却（约 " + seconds + " 秒）；不会自动重试。";
    providerCooldownTimer = setTimeout(update, 1000);
  };
  update();
}

function renderProviderFailures(failures) {
  providerFailures.replaceChildren();
  failures.forEach((failure) => {
    const failureItem = document.createElement("li");
    failureItem.textContent = failure.createdUtc + " · " + failure.status + " · " + failure.errorCode;
    providerFailures.appendChild(failureItem);
  });
}

async function refreshProviderFailures() {
  const headers = sessionHeaders();
  if (!headers) return;
  const requestGeneration = ++providerFailureGeneration;
  try {
    const response = await fetch("/api/provider/failures", { headers });
    if (response.ok && requestGeneration === providerFailureGeneration) renderProviderFailures(await response.json());
  } catch {
    if (requestGeneration === providerFailureGeneration) providerFailures.replaceChildren();
  }
}

function markEditableDocumentChanged() {
  if (isApplyingDocument) return;
  documentEditEpoch++;
  if (currentReviewStatus === "approved") {
    setReviewStatus("draft");
    status.textContent = "内容已修改；审核状态已退回草稿。";
  }
}

form.addEventListener("input", markEditableDocumentChanged);
form.addEventListener("change", markEditableDocumentChanged);
expandedDescriptionInput.addEventListener("input", () => {
  if (!isApplyingDocument) {
    expandedEditEpoch++;
    expandedTextOrigin = expandedDescriptionInput.value.trim() ? "user_edited" : "none";
  }
});

form.addEventListener("submit", async (event) => {
  event.preventDefault();
  const requestGeneration = ++previewRequestGeneration;
  if (previewAbortController) previewAbortController.abort();
  previewAbortController = new AbortController();
  const state = captureDocumentState();
  const request = currentDocument();
  status.textContent = "正在生成本地预览…";

  try {
    const response = await fetch("/api/preview", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request),
      signal: previewAbortController.signal
    });
    const payload = await readJsonOrEmpty(response);
    if (requestGeneration !== previewRequestGeneration || !isCurrentDocumentState(state)) return;
    if (!response.ok || !payload.isValid) {
      status.textContent = "校验未通过；当前 DSL 预览未被覆盖。";
      return;
    }

    status.textContent = payload.diagnostic?.compressed
      ? "预览已生成；已按优先级压缩低价值证据，当前不会上传数据。"
      : "预览已生成；当前不会上传数据。";
    output.textContent = payload.dsl;
  } catch (error) {
    if (requestGeneration !== previewRequestGeneration || !isCurrentDocumentState(state)) return;
    status.textContent = error && error.name === "AbortError"
      ? "预览请求已过期；当前 DSL 预览未被覆盖。"
      : "本地预览服务不可用；当前 DSL 预览未被覆盖。";
  } finally {
    if (requestGeneration === previewRequestGeneration) previewAbortController = null;
  }
});

generateAwakeAuthoringPreviewButton.addEventListener("click", async () => {
  const requestGeneration = ++authoringPreviewGeneration;
  if (authoringPreviewAbortController) authoringPreviewAbortController.abort();
  authoringPreviewAbortController = new AbortController();
  const state = captureDocumentState();
  latestAuthoringPreview = null;
  downloadAwakeAuthoringButton.disabled = true;
  authoringPreviewStatus.textContent = "正在生成 AWAKE authoring-v2 只读预览…";

  try {
    const response = await fetch("/api/preview/awake-authoring", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(currentAwakeAuthoringRequest()),
      signal: authoringPreviewAbortController.signal
    });
    const payload = await readJsonOrEmpty(response);
    if (requestGeneration !== authoringPreviewGeneration || !isCurrentDocumentState(state)) return;
    if (!response.ok || !payload.isValid) {
      authoringPreviewStatus.textContent = "authoring-v2 迁移预览未通过；原有表单与 DSL 未被覆盖。";
      authoringPreviewOutput.textContent = Array.isArray(payload.errors) && payload.errors.length ? payload.errors.join("\n") : "未返回可用诊断。";
      return;
    }
    latestAuthoringPreview = payload;
    downloadAwakeAuthoringButton.disabled = false;
    authoringPreviewOutput.textContent = payload.canonicalJson || "";
    authoringPreviewStatus.textContent = Array.isArray(payload.warnings) && payload.warnings.length
      ? "authoring-v2 只读预览已生成；存在 " + payload.warnings.length + " 项迁移提示。它不是 AWAKE 批准文件。"
      : "authoring-v2 只读预览已生成；它不是 AWAKE 批准文件。";
  } catch (error) {
    if (requestGeneration !== authoringPreviewGeneration || !isCurrentDocumentState(state)) return;
    authoringPreviewStatus.textContent = error && error.name === "AbortError"
      ? "authoring-v2 预览请求已过期；当前预览未被覆盖。"
      : "本地 authoring-v2 预览服务不可用；当前预览未被覆盖。";
  } finally {
    if (requestGeneration === authoringPreviewGeneration) authoringPreviewAbortController = null;
  }
});

downloadAwakeAuthoringButton.addEventListener("click", () => {
  if (!latestAuthoringPreview || !latestAuthoringPreview.canonicalUtf8Base64) {
    authoringPreviewStatus.textContent = "请先生成有效的 authoring-v2 预览。";
    return;
  }
  const binary = atob(latestAuthoringPreview.canonicalUtf8Base64);
  const bytes = Uint8Array.from(binary, character => character.charCodeAt(0));
  const blob = new Blob([bytes], { type: "application/json" });
  const link = document.createElement("a");
  link.href = URL.createObjectURL(blob);
  link.download = derivePersonaFileName(document.querySelector("#display-name").value.trim(), document.querySelector("#persona-id").value.trim()).replace(/\.persona\.json$/i, ".authoring.v2.json");
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(link.href);
});

async function runDocumentOperation(path, successMessage, onSuccess) {
  if (documentOperationInFlight) {
    status.textContent = "文档操作正在进行；请等待当前操作结束。";
    return;
  }
  const headers = sessionHeaders();
  if (!headers) {
    status.textContent = "本地保存会话未就绪。请从工作台启动链接打开页面。";
    return;
  }

  documentOperationInFlight = true;
  const operationGeneration = ++documentOperationGeneration;
  const state = captureDocumentState();
  setDocumentOperationBusy(true);
  try {
    const response = await fetch(path, { method: "POST", headers, body: JSON.stringify(onSuccess.request), signal: onSuccess.signal });
    const payload = await readJsonOrEmpty(response);
    if (operationGeneration !== documentOperationGeneration || !isCurrentDocumentState(state)) {
      status.textContent = "文档操作结果已过期；当前编辑未被覆盖。";
      return;
    }
    if (handleExpiredSession(response)) return;
    if (!response.ok || !payload.isSuccess) {
      status.textContent = payload.errorMessage || successMessage + "失败；当前编辑未被覆盖。";
      return;
    }
    onSuccess.apply(payload);
    status.textContent = successMessage;
  } catch (error) {
    if (operationGeneration !== documentOperationGeneration || !isCurrentDocumentState(state)) return;
    status.textContent = error && error.name === "AbortError"
      ? "文档操作已取消；当前编辑未被覆盖。"
      : successMessage + "失败；当前编辑未被覆盖。";
  } finally {
    if (operationGeneration === documentOperationGeneration) {
      documentOperationInFlight = false;
      setDocumentOperationBusy(false);
    }
  }
}

loadButton.addEventListener("click", async () => {
  if (documentOperationInFlight) {
    status.textContent = "文档操作正在进行；请等待当前操作结束。";
    return;
  }
  clearDocumentTransientState();
  const request = workspaceRequest();
  await runDocumentOperation("/api/documents/load", "本地文件已打开。", {
    request,
    apply(payload) {
      applyEditableDocument(payload.document);
      contentHash = payload.contentHash || null;
      documentEditEpoch++;
      form.requestSubmit();
    }
  });
});

saveButton.addEventListener("click", async () => {
  const request = { ...workspaceRequest(), document: currentDocument(), expectedContentHash: contentHash };
  await runDocumentOperation("/api/documents/save", "本地文件已保存为草稿。", {
    request,
    apply(payload) {
      applyDocumentMetadata(payload.document || {});
      contentHash = payload.contentHash || null;
      setReviewStatus(payload.document?.status);
    }
  });
});

approveButton.addEventListener("click", async () => {
  const request = { ...workspaceRequest(), document: currentDocument(), expectedContentHash: contentHash };
  await runDocumentOperation("/api/documents/approve", "当前 Persona 已由本地用户明确批准。", {
    request,
    apply(payload) {
      applyDocumentMetadata(payload.document || {});
      contentHash = payload.contentHash || null;
      setReviewStatus(payload.document?.status);
      form.requestSubmit();
    }
  });
});

setProviderKeyButton.addEventListener("click", async () => {
  const apiKey = providerKeyInput.value;
  if (!apiKey) {
    providerStatus.textContent = "请输入本次会话 API Key。";
    return;
  }

  await runProviderOperation(async () => {
    providerStatus.textContent = "正在设置本次会话 Key…";
    try {
      const response = await providerFetch("/api/provider/session-key", { apiKey });
      if (!response) return;
      providerStatus.textContent = response.ok ? "本次会话 Key 已设置；不会写入 Persona 文件或浏览器存储。" : "本次会话 Key 未被接受。";
    } catch {
      providerStatus.textContent = "本机 Provider 设置服务不可用。";
    } finally {
      providerKeyInput.value = "";
    }
  }, { cancellable: false });
});

clearProviderKeyButton.addEventListener("click", async () => {
  await runProviderOperation(async () => {
    try {
      const response = await providerFetch("/api/provider/clear-session-key");
      if (!response) return;
      if (response.ok) {
        providerStatus.textContent = "本次会话 Key 已清除。";
      } else {
        let payload = {};
        try { payload = await response.json(); } catch {}
        providerStatus.textContent = providerResponseFailureMessage(response, payload);
      }
    } catch {
      providerStatus.textContent = "本机 Provider 设置服务不可用。";
    } finally {
      providerKeyInput.value = "";
    }
  }, { cancellable: false });
});

confirmCloudProviderButton.addEventListener("click", async () => {
  await runProviderOperation(async () => {
    try {
      const response = await providerFetch("/api/provider/confirm-cloud", { endpoint: providerEndpointInput.value });
      if (!response) return;
      if (response.status === 401) {
        providerStatus.textContent = "本机会话已过期；请重启工作台，并从新打开的页面继续。当前地址没有被判定为错误。";
        return;
      }
      if (!response.ok) {
        let payload = {};
        try { payload = await response.json(); } catch {}
        providerStatus.textContent = "端点确认失败（" + (payload.errorCode || "provider.endpoint_invalid") + "）；请检查完整 HTTPS 地址。";
        return;
      }
      providerStatus.textContent = "端点已确认；可直接识别，也可按需扩展人物形象。";
    } catch {
      providerStatus.textContent = "本机 Provider 设置服务不可用。";
    }
  }, { cancellable: false });
});

providerEndpointInput.addEventListener("input", updateProviderGuidance);
providerModeInput.addEventListener("change", () => {
  if (providerModeInput.value === "local" && classifyProviderEndpoint(providerEndpointInput.value) === "cloud") {
    providerEndpointInput.value = "http://127.0.0.1:11434/v1/chat/completions";
  }
  updateProviderGuidance();
});

generateProviderDraftButton.addEventListener("click", async () => {
  const request = providerRequest();
  if (!request.model || !request.prompt) {
    providerStatus.textContent = "请填写模型 ID 和自由描述。";
    return;
  }

  await runProviderOperation(async (generation) => {
    const snapshot = captureProviderDocumentState();
    providerStatus.textContent = "正在从自由描述识别并生成 Persona…";
    try {
      if (classifyProviderEndpoint(request.endpoint) === "cloud") {
        providerStatus.textContent = "正在设置本次会话 Key 并识别 Persona…";
        if (!await setProviderSessionKeyFromInput()) return;
      }
      const response = await providerFetch("/api/provider/convert-to-dsl", {
        endpoint: request.endpoint,
        model: request.model,
        providerProtocol: request.providerProtocol,
        sourceText: request.prompt,
        localId: document.querySelector("#persona-id").value.trim(),
        localDisplayName: document.querySelector("#display-name").value.trim()
      });
      if (!response) return;
      let payload = {};
      try { payload = await response.json(); } catch {
        providerStatus.textContent = "工作台返回了无法解析的响应；当前表单内容未被覆盖。";
        return;
      }
      if (!response.ok || !payload.dsl || !payload.draft || !isCurrentProviderDocumentState(generation, snapshot)) {
        if (isCurrentProviderDocumentState(generation, snapshot)) {
          providerStatus.textContent = providerResponseFailureMessage(response, payload);
          if (payload.cooldownUntilUtc) startProviderCooldown(payload.cooldownUntilUtc);
        }
        await refreshProviderFailures();
        return;
      }
      applyProviderDslResult(payload, {
        documentStatus: "已从自由描述生成可编辑 Persona 和 canonical DSL；尚未保存或批准。",
        providerStatus: "直接识别完成；请检查字段后再保存。"
      });
    } catch (error) {
      if (isCurrentProviderOperation(generation)) providerStatus.textContent = providerNetworkFailureMessage(error);
      await refreshProviderFailures();
    }
  });
});

expandDescriptionButton.addEventListener("click", async () => {
  const request = providerRequest();
  if (!request.model || !request.prompt) {
    providerStatus.textContent = "请填写模型 ID 和自由描述。";
    return;
  }

  await runProviderOperation(async (generation) => {
    const snapshot = captureProviderDocumentState({ includeExpansionControls: true });
    providerStatus.textContent = "正在扩充人物描述…";
    try {
      if (classifyProviderEndpoint(request.endpoint) === "cloud") {
        providerStatus.textContent = "正在设置本次会话 Key 并扩充人物描述…";
        if (!await setProviderSessionKeyFromInput()) return;
      }
      const response = await providerFetch("/api/provider/expand-description", {
        endpoint: request.endpoint,
        model: request.model,
        providerProtocol: request.providerProtocol,
        description: request.prompt,
        controls: expansionControls()
      });
      if (!response) return;
      let payload;
      try { payload = await response.json(); } catch {
        providerStatus.textContent = "工作台返回了无法解析的响应；当前表单内容未被覆盖。";
        return;
      }
      if (!response.ok || !payload.expandedText || !isCurrentProviderDocumentState(generation, snapshot)) {
        if (isCurrentProviderDocumentState(generation, snapshot)) {
          providerStatus.textContent = providerResponseFailureMessage(response, payload);
          if (payload.cooldownUntilUtc) startProviderCooldown(payload.cooldownUntilUtc);
        }
        await refreshProviderFailures();
        return;
      }
      expandedDescriptionInput.value = payload.expandedText;
      expandedEditEpoch++;
      expandedTextOrigin = "provider";
      providerUsage = payload.usage ?? null;
      providerStatus.textContent = "已生成可编辑人物扩充；尚未写入 Persona，也未调用 DSL 生成。" + "（" + renderProviderUsage() + "）";
    } catch (error) {
      if (isCurrentProviderOperation(generation)) providerStatus.textContent = providerNetworkFailureMessage(error);
      await refreshProviderFailures();
    }
  });
});

cancelProviderDraftButton.addEventListener("click", () => {
  providerOperationGeneration++;
  if (providerAbortController) providerAbortController.abort();
});

useExpandedDescriptionButton.addEventListener("click", () => {
  const expanded = expandedDescriptionInput.value.trim();
  if (!expanded) {
    providerStatus.textContent = "当前没有可填入的人物扩充结果。";
    return;
  }
  clearDerivedPersonaFields();
  document.querySelector("#core").value = expanded;
  document.querySelector("#source-description").value = providerPromptInput.value.trim();
  documentEditEpoch++;
  setReviewStatus("draft");
  providerStatus.textContent = "扩充结果已填入核心人格；请继续人工修改，之后再生成本地 DSL 预览。";
});

convertExpandedToDslButton.addEventListener("click", async () => {
  const sourceText = expandedDescriptionInput.value.trim();
  const request = providerRequest();
  if (!sourceText) {
    providerStatus.textContent = "请先生成或填写确认后的人物扩充文本。";
    return;
  }
  if (!request.model) {
    providerStatus.textContent = "请填写模型 ID。";
    return;
  }

  await runProviderOperation(async (generation) => {
    const snapshot = captureProviderDocumentState();
    providerStatus.textContent = "正在将确认文本转换为结构化 Persona 并生成 DSL…";
    try {
      if (classifyProviderEndpoint(request.endpoint) === "cloud") {
        if (!await setProviderSessionKeyFromInput()) return;
      }
      const response = await providerFetch("/api/provider/convert-to-dsl", {
        endpoint: request.endpoint,
        model: request.model,
        providerProtocol: request.providerProtocol,
        sourceText,
        localId: document.querySelector("#persona-id").value.trim(),
        localDisplayName: document.querySelector("#display-name").value.trim()
      });
      if (!response) return;
      let payload = {};
      try { payload = await response.json(); } catch {
        providerStatus.textContent = "工作台返回了无法解析的 DSL 响应；当前 DSL 未被覆盖。";
        return;
      }
      if (!response.ok || !payload.dsl || !payload.draft || !isCurrentProviderDocumentState(generation, snapshot)) {
        if (isCurrentProviderDocumentState(generation, snapshot)) {
          providerStatus.textContent = providerResponseFailureMessage(response, payload);
          if (payload.cooldownUntilUtc) startProviderCooldown(payload.cooldownUntilUtc);
        }
        await refreshProviderFailures();
        return;
      }
      applyProviderDslResult(payload, {
        documentStatus: "结构化 Persona 已回填并生成 canonical DSL；尚未保存或批准。",
        providerStatus: "DSL 转换完成；请检查角色字段和英文稳定标签后再保存。"
      });
    } catch (error) {
      if (isCurrentProviderOperation(generation)) providerStatus.textContent = providerNetworkFailureMessage(error);
      await refreshProviderFailures();
    }
  });
});

materialFilesInput.addEventListener("change", () => {
  const files = Array.from(materialFilesInput.files || []);
  materialStatus.textContent = files.length === 0
    ? "尚未选择资料文件；也可以直接粘贴文字。"
    : "已选择 " + files.length + " 个文件；点击“读取资料”后才会载入。";
});

readMaterialsButton.addEventListener("click", async () => {
  const sources = [];
  const pastedText = materialPasteInput.value.trim();
  if (pastedText) sources.push({ sourceFile: "粘贴资料", text: pastedText });
  const files = Array.from(materialFilesInput.files || []);
  for (const file of files) {
    if (!/\.(txt|md)$/i.test(file.name)) {
      materialStatus.textContent = "文件“" + file.name + "”不是 .txt 或 .md，已停止读取。";
      return;
    }
    try {
      const text = await file.text();
      sources.push({ sourceFile: file.name, text });
    } catch {
      materialStatus.textContent = "无法读取文件“" + file.name + "”；请检查文件是否仍可访问。";
      return;
    }
  }
  if (sources.length === 0) {
    materialStatus.textContent = "没有可读取的资料；请粘贴文字或选择 .txt/.md 文件。";
    return;
  }
  materialSources = sources;
  materialSegments = [];
  latestBatchPayload = null;
  materialSegmentsElement.replaceChildren();
  batchResultsElement.replaceChildren();
  downloadBatchButton.disabled = true;
  segmentMaterialsButton.disabled = false;
  generateBatchButton.disabled = true;
  renderMaterialSources();
  materialStatus.textContent = "资料已读取；下一步请点击“整理资料并分段”。";
});

segmentMaterialsButton.addEventListener("click", async () => {
  if (materialSources.length === 0) {
    materialStatus.textContent = "请先点击“读取资料”。";
    return;
  }
  segmentMaterialsButton.disabled = true;
  materialStatus.textContent = "正在本机整理资料并分段；这一步不会调用 AI。";
  try {
    const response = await fetch("/api/materials/segment", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ sources: materialSources })
    });
    const payload = await readJsonOrEmpty(response);
    if (!response.ok || !payload.isValid) {
      materialStatus.textContent = Array.isArray(payload.errors) && payload.errors.length
        ? payload.errors.join("\n")
        : "资料分段失败；请缩短资料或检查文件内容。";
      materialSegments = [];
      materialSegmentsElement.replaceChildren();
      generateBatchButton.disabled = true;
      return;
    }
    materialSegments = (payload.segments || []).map((segment) => ({ ...segment, selected: true }));
    renderMaterialSegments();
    generateBatchButton.disabled = selectedBatchItems().length === 0;
    materialStatus.textContent = "已整理出 " + materialSegments.length + " 段；请检查勾选和文字后进入下一步。";
    batchStatus.textContent = "下一步已就绪：点击“进入 AI 批量制作”。";
  } catch {
    materialStatus.textContent = "本地资料分段服务不可用；请重新启动当前版本工作台。";
    generateBatchButton.disabled = true;
  } finally {
    if (!providerOperationInFlight) segmentMaterialsButton.disabled = materialSources.length === 0;
  }
});

generateBatchButton.addEventListener("click", async () => {
  const request = providerRequest();
  const items = selectedBatchItems();
  if (!request.endpoint || !request.model) {
    batchStatus.textContent = "请先在“AI 设置”中填写完整接口地址和模型 ID。";
    if (providerSettings) providerSettings.open = true;
    return;
  }
  if (items.length === 0) {
    batchStatus.textContent = "请至少保留一段资料，再进入 AI 批量制作。";
    return;
  }
  if (classifyProviderEndpoint(request.endpoint) === "cloud" && !await setProviderSessionKeyFromInput()) return;

  const batchId = createBatchId();
  activeBatchId = batchId;
  latestBatchPayload = null;
  downloadBatchButton.disabled = true;
  renderBatchResults({ results: [] });
  batchStatus.textContent = "正在按资料顺序生成第 1 / " + items.length + " 条；结果会在完成后保留在本页。";
  startBatchProgressPolling(batchId);
  await runProviderOperation(async () => {
    const headers = sessionHeaders();
    if (!headers) {
      batchStatus.textContent = "本机会话未就绪；请从当前启动链接重新打开工作台。";
      return;
    }
    try {
      const response = await fetch("/api/provider/generate-batch", {
        method: "POST",
        headers,
        body: JSON.stringify({
          batchId,
          endpoint: request.endpoint,
          model: request.model,
          providerProtocol: request.providerProtocol,
          items
        })
      });
      const payload = await readJsonOrEmpty(response);
      if (!response.ok || !payload.status) {
        batchStatus.textContent = Array.isArray(payload.errors) && payload.errors.length
          ? payload.errors.map(batchErrorMessage).join("\n")
          : "批量制作请求未被接受；当前编辑器内容未被覆盖。";
        return;
      }
      stopBatchProgressPolling();
      latestBatchPayload = payload;
      renderBatchResults(payload);
      downloadBatchButton.disabled = false;
      const label = payload.status === "cancelled" ? "批量制作已停止" : payload.status === "completed" ? "批量制作完成" : "批量制作完成，但有部分失败";
      batchStatus.textContent = label + "：成功 " + payload.succeeded + " 条，失败 " + payload.failed + " 条。可逐条载入编辑器或导出结果。";
    } catch (error) {
      stopBatchProgressPolling();
      batchStatus.textContent = providerNetworkFailureMessage(error);
    }
  }, { cancellable: false });
  stopBatchProgressPolling();
  activeBatchId = null;
  if (latestBatchPayload) downloadBatchButton.disabled = false;
});

cancelBatchButton.addEventListener("click", async () => {
  if (!activeBatchId) return;
  const headers = sessionHeaders();
  if (!headers) {
    batchStatus.textContent = "本机会话已失效；当前批量请求会在 Provider 返回后停止更新。";
    return;
  }
  cancelBatchButton.disabled = true;
  batchStatus.textContent = "正在请求停止；当前 Provider 请求结束后不会开始下一段。";
  try {
    const response = await fetch("/api/provider/cancel-batch", {
      method: "POST",
      headers,
      body: JSON.stringify({ batchId: activeBatchId })
    });
    const payload = await readJsonOrEmpty(response);
    if (!response.ok || !payload.isAccepted) batchStatus.textContent = "停止请求未被接受；请等待当前条目结束。";
  } catch {
    batchStatus.textContent = "停止请求发送失败；请等待当前条目结束，或关闭当前页面。";
  }
});

downloadBatchButton.addEventListener("click", downloadBatchResults);

// ── 全库体检：把整个文件夹的卡放在一起比，只读，不改任何文件 ──
const auditRootInput = document.querySelector("#audit-root");
const runCorpusAuditButton = document.querySelector("#run-corpus-audit");
const auditStatus = document.querySelector("#audit-status");
const auditFindings = document.querySelector("#audit-findings");

function auditFieldLabel(field) {
  if (field === "selfClaimRules") return "自称与称呼规则";
  if (field === "realSelfBehaviors") return "真实自我行为";
  if (field === "selfClaimExamples") return "自称示例";
  if (field === "core") return "核心人格";
  return field || "未标注字段";
}

function auditRuleLabel(rule) {
  if (rule === "corpus.text_reused") return "整条跨卡逐字相同";
  if (rule === "corpus.skeleton_prefix") return "开头一样、后面各写各的";
  if (rule === "card.self_reference_only") return "这句话对谁都成立";
  if (rule === "card.repeated_line") return "同一张卡里说了两遍";
  if (rule === "card.editor_sample_text") return "照抄了界面上的示例";
  return rule;
}

function renderCorpusAuditFindings(findings) {
  auditFindings.replaceChildren();
  if (findings.length === 0) {
    const clean = document.createElement("p");
    clean.className = "helper";
    clean.textContent = "没有发现跨卡重复、对谁都成立的空话或卡内重复。";
    auditFindings.append(clean);
    return;
  }

  findings.forEach((finding) => {
    const card = document.createElement("article");
    card.className = "audit-finding " + (finding.severity === "error" ? "error" : "warning");
    const heading = document.createElement("h3");
    heading.textContent = auditRuleLabel(finding.rule) + "（" + auditFieldLabel(finding.field) + "）";
    const why = document.createElement("p");
    why.className = "audit-why";
    why.textContent = finding.message || "";
    const quote = document.createElement("p");
    quote.className = "audit-quote";
    quote.textContent = "涉及的文字：" + (finding.text || "");
    const names = Array.isArray(finding.cards) ? finding.cards : [];
    const cards = document.createElement("p");
    cards.className = "audit-cards";
    cards.textContent = "涉及 " + names.length + " 张卡：" + names.join("、");
    card.append(heading, why, quote, cards);
    auditFindings.append(card);
  });
}

async function runCorpusAudit() {
  const headers = sessionHeaders();
  if (!headers) {
    auditStatus.textContent = "本地会话未就绪。请从工作台启动链接打开页面。";
    return;
  }

  runCorpusAuditButton.disabled = true;
  auditStatus.textContent = "正在读取并比较整个文件夹的卡…";
  auditFindings.replaceChildren();
  try {
    const response = await fetch("/api/audit/corpus", {
      method: "POST",
      headers,
      body: JSON.stringify({ rootPath: auditRootInput.value })
    });
    const payload = await readJsonOrEmpty(response);
    if (response.status === 401) {
      auditStatus.textContent = "本地会话已过期；请重新打开工作台启动链接后再体检。";
      return;
    }
    if (!response.ok || !payload.isSuccess) {
      auditStatus.textContent = payload.errorMessage || "体检没跑起来；请检查卡目录是否填对。";
      return;
    }

    if (!auditRootInput.value.trim() && payload.resolvedRootPath) {
      auditRootInput.value = payload.resolvedRootPath;
    }

    const findings = Array.isArray(payload.findings) ? payload.findings : [];
    const unreadable = Array.isArray(payload.unreadableFiles) ? payload.unreadableFiles : [];
    const parts = [
      "体检目录：" + (payload.resolvedRootPath || "未记录"),
      "读入 " + payload.cardCount + " 张卡",
      "错误 " + payload.errorCount + " 条，提醒 " + payload.warningCount + " 条"
    ];
    if (unreadable.length > 0) {
      parts.push("读不进来的文件 " + unreadable.length + " 个：" + unreadable.join("、"));
    }
    auditStatus.textContent = parts.join("；");
    renderCorpusAuditFindings(findings);
  } catch {
    auditStatus.textContent = "体检请求发送失败；请确认工作台仍在运行。";
  } finally {
    runCorpusAuditButton.disabled = false;
  }
}

runCorpusAuditButton.addEventListener("click", runCorpusAudit);

updateProviderGuidance();
bootstrapSession().then(refreshProviderFailures).catch(() => { status.textContent = "本地保存会话初始化失败；可以继续预览。"; });









