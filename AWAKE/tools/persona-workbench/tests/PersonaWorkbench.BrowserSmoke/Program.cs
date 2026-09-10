using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

const int WorkbenchPort = 51337;
const int ProviderPort = 51437;
const int CdpPort = 51339;
string root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
string webProject = Path.GetFullPath(Path.Combine(root, "src", "PersonaWorkbench.Web", "PersonaWorkbench.Web.csproj"));
string webDirectory = Path.GetDirectoryName(webProject)!;
Process? webProcess = null;
Process? edgeProcess = null;
CdpClient? cdp = null;
string userData = Path.Combine(Path.GetTempPath(), "persona-workbench-cdp-" + Guid.NewGuid().ToString("N"));
using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(45));
using FakeProvider fakeProvider = new(ProviderPort);

try
{
    fakeProvider.Start();
    webProcess = StartProcess("dotnet", $"run --project \"{webProject}\" --no-launch-profile -- --no-browser", webDirectory);
    await WaitForHttpAsync($"http://127.0.0.1:{WorkbenchPort}/", timeout.Token);
    string edge = FindEdge();
    edgeProcess = StartProcess(edge, $"--headless=new --disable-gpu --disable-extensions --no-first-run --no-default-browser-check --edge-skip-compat-layer-relaunch --remote-debugging-port={CdpPort} --user-data-dir=\"{userData}\" http://127.0.0.1:{WorkbenchPort}/", Environment.CurrentDirectory);
    await WaitForHttpAsync($"http://127.0.0.1:{CdpPort}/json/list", timeout.Token);

    cdp = await CdpClient.ConnectAsync($"http://127.0.0.1:{CdpPort}/json/list", $"http://127.0.0.1:{WorkbenchPort}/", timeout.Token);
    await cdp.CallAsync("Runtime.enable", null, timeout.Token);
    await cdp.CallAsync("Page.enable", null, timeout.Token);
    await Task.Delay(1200, timeout.Token);
    string setup = """
    (() => {
      const set = (selector, value) => {
        const element = document.querySelector(selector);
        if (!element) throw new Error('missing ' + selector);
        element.value = value;
        element.dispatchEvent(new Event('input', { bubbles: true }));
        element.dispatchEvent(new Event('change', { bubbles: true }));
      };
      set('#provider-prompt', '一名谨慎的年轻领主。她在亲密关系中不擅长表达。');
      set('#provider-endpoint', 'http://127.0.0.1:51437/v1/chat/completions');
      set('#provider-model', 'fake-model');
      document.querySelector('#expansion-guidance summary').click();
      set('#expansion-direction', '重点深化警惕与关系反差。');
      set('#expansion-focus-preset', 'relationship_emotion');
      set('#expansion-keyword-input', '戒备');
      set('#expansion-keyword-weight', 'strong');
      document.querySelector('#add-expansion-keyword').click();
      set('#expansion-avoid-topics', '外貌');
      const generate = document.querySelector('#expand-description');
      generate.click();
      generate.dispatchEvent(new MouseEvent('click', { bubbles: true }));
      return document.querySelector('#provider-status').textContent;
    })()
    """;
    string busyStatus = await cdp.EvaluateAsync(setup, timeout.Token);
    if (!busyStatus.Contains("provider.request_in_flight", StringComparison.Ordinal)) throw new InvalidOperationException("Browser operation lease did not reject the second Provider request.");
    await WaitForConditionAsync(cdp, "document.querySelector('#expanded-description')?.value.includes('fake provider expansion')", timeout.Token);
    string state = await cdp.EvaluateAsync("JSON.stringify({ expanded: document.querySelector('#expanded-description')?.value, keyword: document.querySelector('#expansion-keyword-list input')?.value, preset: document.querySelector('#expansion-focus-preset')?.value })", timeout.Token);
    using JsonDocument stateJson = JsonDocument.Parse(state);
    JsonElement stateRoot = stateJson.RootElement;
    if (!stateRoot.GetProperty("expanded").GetString()!.Contains("fake provider expansion", StringComparison.Ordinal)) throw new InvalidOperationException("Provider expansion did not reach the editable result.");
    if (stateRoot.GetProperty("keyword").GetString() != "戒备" || stateRoot.GetProperty("preset").GetString() != "relationship_emotion") throw new InvalidOperationException("Expansion controls did not persist.");
    if (fakeProvider.RequestCount != 1) throw new InvalidOperationException($"Expected one Provider request, received {fakeProvider.RequestCount}.");

    string endpointClassification = await cdp.EvaluateAsync("classifyProviderEndpoint('http://[::1]:11434/v1/chat/completions')", timeout.Token);
    if (endpointClassification != "local") throw new InvalidOperationException("IPv6 loopback Provider endpoints must be classified as local.");
    Console.WriteLine("PASS IPv6 loopback Provider classification");

    string staleExpansionSetup = """
    (() => {
      const set = (selector, value) => {
        const element = document.querySelector(selector);
        if (!element) throw new Error('missing ' + selector);
        element.value = value;
        element.dispatchEvent(new Event('input', { bubbles: true }));
        element.dispatchEvent(new Event('change', { bubbles: true }));
      };
      set('#provider-protocol', 'openai_compatible');
      set('#provider-endpoint', 'http://127.0.0.1:51437/delayed');
      set('#expanded-description', '');
      set('#expansion-direction', '先测试旧方向');
      document.querySelector('#expand-description').click();
      set('#expansion-direction', '后测试新方向');
      return true;
    })()
    """;
    await cdp.EvaluateAsync(staleExpansionSetup, timeout.Token);
    await Task.Delay(1200, timeout.Token);
    string staleExpansion = await cdp.EvaluateAsync("document.querySelector('#expanded-description')?.value || ''", timeout.Token);
    if (staleExpansion.Length != 0) throw new InvalidOperationException("A stale Provider expansion overwrote the result after expansion controls changed.");
    Console.WriteLine("PASS stale Provider expansion responses are discarded");

    string cooldownSetup = """
    (() => {
      const set = (selector, value) => {
        const element = document.querySelector(selector);
        if (!element) throw new Error('missing ' + selector);
        element.value = value;
        element.dispatchEvent(new Event('input', { bubbles: true }));
        element.dispatchEvent(new Event('change', { bubbles: true }));
      };
      set('#provider-endpoint', 'http://127.0.0.1:51437/rate-limit');
      set('#expanded-description', '');
      document.querySelector('#expand-description').click();
      return true;
    })()
    """;
    await cdp.EvaluateAsync(cooldownSetup, timeout.Token);
    await WaitForConditionAsync(cdp, "document.querySelector('#provider-status')?.textContent.includes('正在冷却')", timeout.Token);
    string cooldownControls = await cdp.EvaluateAsync("JSON.stringify({ convert: document.querySelector('#convert-expanded-to-dsl')?.disabled, key: document.querySelector('#set-provider-key')?.disabled, cancel: document.querySelector('#cancel-provider-draft')?.disabled })", timeout.Token);
    using JsonDocument cooldownJson = JsonDocument.Parse(cooldownControls);
    if (cooldownJson.RootElement.GetProperty("convert").GetBoolean() || cooldownJson.RootElement.GetProperty("key").GetBoolean() || !cooldownJson.RootElement.GetProperty("cancel").GetBoolean()) throw new InvalidOperationException("Provider cooldown left unrelated controls disabled or cancellation enabled.");
    await WaitForConditionAsync(cdp, "document.querySelector('#provider-status')?.textContent.includes('冷却已结束')", timeout.Token);
    string recoveredControls = await cdp.EvaluateAsync("JSON.stringify({ convert: document.querySelector('#convert-expanded-to-dsl')?.disabled, key: document.querySelector('#set-provider-key')?.disabled, cancel: document.querySelector('#cancel-provider-draft')?.disabled })", timeout.Token);
    using JsonDocument recoveredJson = JsonDocument.Parse(recoveredControls);
    if (recoveredJson.RootElement.GetProperty("convert").GetBoolean() || recoveredJson.RootElement.GetProperty("key").GetBoolean() || !recoveredJson.RootElement.GetProperty("cancel").GetBoolean()) throw new InvalidOperationException("Provider controls did not recover after cooldown.");
    Console.WriteLine("PASS Provider controls recover after cooldown");

    string deferredPreviewSetup = """
    (() => {
      const set = (selector, value) => {
        const element = document.querySelector(selector);
        element.value = value;
        element.dispatchEvent(new Event('input', { bubbles: true }));
        element.dispatchEvent(new Event('change', { bubbles: true }));
      };
      const originalFetch = window.fetch;
      const pending = [];
      window.__pwbDeferredPreview = { originalFetch, pending };
      window.fetch = (input, init) => {
        const url = typeof input === 'string' ? input : input?.url;
        if (url === '/api/preview') return new Promise(resolve => pending.push({ resolve }));
        return originalFetch(input, init);
      };
      set('#core', '旧预览文本');
      document.querySelector('#preview-form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
      set('#core', '新预览文本');
      document.querySelector('#preview-form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
      return 'started';
    })()
    """;
    await cdp.EvaluateAsync(deferredPreviewSetup, timeout.Token);
    await WaitForConditionAsync(cdp, "window.__pwbDeferredPreview?.pending.length === 2", timeout.Token);
    await cdp.EvaluateAsync("window.__pwbDeferredPreview.pending[1].resolve(new Response(JSON.stringify({ isValid: true, dsl: 'DSL-NEW' }), { status: 200, headers: { 'Content-Type': 'application/json' } }))", timeout.Token);
    await WaitForConditionAsync(cdp, "document.querySelector('#dsl-output')?.textContent === 'DSL-NEW'", timeout.Token);
    await cdp.EvaluateAsync("window.__pwbDeferredPreview.pending[0].resolve(new Response(JSON.stringify({ isValid: true, dsl: 'DSL-OLD' }), { status: 200, headers: { 'Content-Type': 'application/json' } }))", timeout.Token);
    await Task.Delay(150, timeout.Token);
    string previewOutput = await cdp.EvaluateAsync("document.querySelector('#dsl-output')?.textContent || ''", timeout.Token);
    if (previewOutput != "DSL-NEW") throw new InvalidOperationException("A stale preview response overwrote the newer DSL.");
    await cdp.EvaluateAsync("window.fetch = window.__pwbDeferredPreview.originalFetch;", timeout.Token);
    Console.WriteLine("PASS stale preview responses are discarded");

    string documentStateSetup = """
    (() => {
      const set = (selector, value) => {
        const element = document.querySelector(selector);
        element.value = value;
        element.dispatchEvent(new Event('input', { bubbles: true }));
        element.dispatchEvent(new Event('change', { bubbles: true }));
      };
      const response = payload => new Response(JSON.stringify(payload), { status: 200, headers: { 'Content-Type': 'application/json' } });
      const originalFetch = window.fetch;
      window.__pwbDocumentTest = { originalFetch, savedBody: null };
      window.fetch = (input, init) => {
        const url = typeof input === 'string' ? input : input?.url;
        if (url === '/api/documents/load') return Promise.resolve(response({
          isSuccess: true,
          contentHash: 'hash-b',
          document: {
            schemaVersion: 'persona-workbench.character.v1',
            id: 'free.browser.b',
            displayName: '角色 B',
            core: '角色 B 核心人格。',
            sourcePackId: 'source.browser.b',
            templateVersion: 'persona-load.browser-b',
            status: 'draft',
            tags: [],
            facetStrengths: {},
            traitProfile: {}, expressionProfile: {}, behaviorProfile: {}, reactionProfile: {}, commitmentProfile: {}
          }
        }));
        if (url === '/api/documents/save') {
          window.__pwbDocumentTest.savedBody = JSON.parse(init.body);
          return Promise.resolve(response({ isSuccess: true, contentHash: 'hash-c', document: window.__pwbDocumentTest.savedBody.document }));
        }
        if (url === '/api/preview') return Promise.resolve(response({ isValid: true, dsl: 'DSL-B' }));
        return originalFetch(input, init);
      };
      set('#expanded-description', '角色 A 的旧扩充');
      set('#provider-prompt', '角色 A 的旧提示');
      document.querySelector('#load-document').click();
      return 'started';
    })()
    """;
    await cdp.EvaluateAsync(documentStateSetup, timeout.Token);
    await WaitForConditionAsync(cdp, "document.querySelector('#core')?.value === '角色 B 核心人格。' && document.querySelector('#expanded-description')?.value === '' && document.querySelector('#provider-prompt')?.value === ''", timeout.Token);
    await cdp.EvaluateAsync("document.querySelector('#save-document').click()", timeout.Token);
    await WaitForConditionAsync(cdp, "window.__pwbDocumentTest?.savedBody !== null", timeout.Token);
    string metadata = await cdp.EvaluateAsync("JSON.stringify({ sourcePackId: window.__pwbDocumentTest.savedBody.document.sourcePackId, templateVersion: window.__pwbDocumentTest.savedBody.document.templateVersion })", timeout.Token);
    using JsonDocument metadataJson = JsonDocument.Parse(metadata);
    if (metadataJson.RootElement.GetProperty("sourcePackId").GetString() != "source.browser.b" || metadataJson.RootElement.GetProperty("templateVersion").GetString() != "persona-load.browser-b") throw new InvalidOperationException("Browser save dropped loaded document metadata.");
    await cdp.EvaluateAsync("window.fetch = window.__pwbDocumentTest.originalFetch;", timeout.Token);
    Console.WriteLine("PASS document load clears transient state and preserves metadata");

    string dslSuccessSetup = """
    (() => {
      const set = (selector, value) => {
        const element = document.querySelector(selector);
        if (!element) throw new Error('missing ' + selector);
        element.value = value;
        element.dispatchEvent(new Event('input', { bubbles: true }));
        element.dispatchEvent(new Event('change', { bubbles: true }));
      };
      const response = payload => new Response(JSON.stringify(payload), { status: 200, headers: { 'Content-Type': 'application/json' } });
      const originalFetch = window.fetch.bind(window);
      const directDraft = {
        schemaVersion: 'persona-workbench.character.v1', id: 'free.direct', displayName: '直测角色', core: '直接生成核心', identityFacts: '直接生成身份',
        sourcePackId: 'provider.must.not.replace', templateVersion: 'provider.must.not.replace', status: 'draft', tags: ['direct-tag'], facetStrengths: {},
        traitProfile: { caution: 1 }, expressionProfile: {}, behaviorProfile: {}, reactionProfile: {}, commitmentProfile: {}, summary: '直接摘要'
      };
      const expandedDraft = {
        schemaVersion: 'persona-workbench.character.v1', id: 'free.expanded', displayName: '扩充角色', core: '扩充生成核心', identityFacts: '扩充生成身份',
        sourcePackId: 'provider.must.not.replace', templateVersion: 'provider.must.not.replace', status: 'draft', tags: ['expanded-tag'], facetStrengths: {},
        traitProfile: { caution: -1 }, expressionProfile: {}, behaviorProfile: {}, reactionProfile: {}, commitmentProfile: {}, summary: '扩充摘要'
      };
      const staleDirectDraft = { ...directDraft, id: 'stale.direct', displayName: '过期直接角色', core: '过期直接核心', identityFacts: '过期直接身份', sourcePackId: 'stale.source', templateVersion: 'stale.template' };
      const staleExpandedDraft = { ...expandedDraft, id: 'stale.expanded', displayName: '过期扩充角色', core: '过期扩充核心', identityFacts: '过期扩充身份', sourcePackId: 'stale.expanded.source', templateVersion: 'stale.expanded.template' };
      window.__pwbDslTest = { originalFetch, requests: [], savedBodies: [], pending: [], directDraft, expandedDraft };
      window.fetch = (input, init) => {
        const url = typeof input === 'string' ? input : input?.url;
        if (url === '/api/provider/convert-to-dsl') {
          const request = JSON.parse(init.body);
          window.__pwbDslTest.requests.push(request);
          if (request.sourceText === '旧直接请求') return new Promise(resolve => window.__pwbDslTest.pending.push({ kind: 'direct', resolve }));
          if (request.sourceText === '旧扩充请求') return new Promise(resolve => window.__pwbDslTest.pending.push({ kind: 'expanded', resolve }));
          const isExpanded = request.sourceText === '已确认扩充文本';
          return Promise.resolve(response({
            isSuccess: true,
            dsl: isExpanded ? 'DSL-EXPANDED' : 'DSL-DIRECT',
            draft: isExpanded ? expandedDraft : directDraft,
            usage: isExpanded ? { promptTokens: 29, completionTokens: 37, totalTokens: 66 } : { promptTokens: 17, completionTokens: 23, totalTokens: 40 }
          }));
        }
        if (url === '/api/documents/save') {
          const request = JSON.parse(init.body);
          window.__pwbDslTest.savedBodies.push(request);
          return Promise.resolve(response({ isSuccess: true, contentHash: 'hash-save-' + window.__pwbDslTest.savedBodies.length, document: request.document }));
        }
        return originalFetch(input, init);
      };
      set('#provider-endpoint', 'http://127.0.0.1:51437/structured-output');
      set('#provider-model', 'fake-model');
      set('#provider-protocol', 'openai_compatible');
      set('#provider-prompt', '直接识别输入');
      set('#file-name', 'persona.persona.json');
      document.querySelector('#generate-provider-draft').click();
      return true;
    })()
    """;
    await cdp.EvaluateAsync(dslSuccessSetup, timeout.Token);
    await WaitForConditionAsync(cdp, "document.querySelector('#dsl-output')?.textContent === 'DSL-DIRECT' && document.querySelector('#persona-id')?.value === 'free.direct' && document.querySelector('#file-name')?.value === '直测角色.persona.json'", timeout.Token);
    string directDslState = await cdp.EvaluateAsync("JSON.stringify({ core: document.querySelector('#core')?.value, identity: document.querySelector('#identity-facts')?.value, dsl: document.querySelector('#dsl-output')?.textContent, fileName: document.querySelector('#file-name')?.value, status: document.querySelector('#review-status')?.textContent, providerStatus: document.querySelector('#provider-status')?.textContent, metadata: documentMetadata, usage: providerUsage })", timeout.Token);
    using JsonDocument directDslJson = JsonDocument.Parse(directDslState);
    JsonElement directDslRoot = directDslJson.RootElement;
    if (directDslRoot.GetProperty("core").GetString() != "直接生成核心" || directDslRoot.GetProperty("identity").GetString() != "直接生成身份" || directDslRoot.GetProperty("dsl").GetString() != "DSL-DIRECT" || directDslRoot.GetProperty("fileName").GetString() != "直测角色.persona.json" || directDslRoot.GetProperty("status").GetString() != "审核状态：草稿" || !directDslRoot.GetProperty("providerStatus").GetString()!.Contains("直接识别完成", StringComparison.Ordinal) || directDslRoot.GetProperty("metadata").GetProperty("sourcePackId").GetString() != "source.browser.b" || directDslRoot.GetProperty("metadata").GetProperty("templateVersion").GetString() != "persona-load.browser-b" || directDslRoot.GetProperty("usage").GetProperty("totalTokens").GetInt32() != 40) throw new InvalidOperationException("Direct DSL result did not update the editable document while preserving metadata and usage.");
    await cdp.EvaluateAsync("document.querySelector('#save-document').click()", timeout.Token);
    await WaitForConditionAsync(cdp, "window.__pwbDslTest?.savedBodies.length === 1", timeout.Token);
    string directSaveState = await cdp.EvaluateAsync("JSON.stringify({ expectedContentHash: window.__pwbDslTest.savedBodies[0].expectedContentHash, sourcePackId: window.__pwbDslTest.savedBodies[0].document.sourcePackId, templateVersion: window.__pwbDslTest.savedBodies[0].document.templateVersion })", timeout.Token);
    using JsonDocument directSaveJson = JsonDocument.Parse(directSaveState);
    JsonElement directSaveRoot = directSaveJson.RootElement;
    if (directSaveRoot.GetProperty("expectedContentHash").ValueKind != JsonValueKind.Null || directSaveRoot.GetProperty("sourcePackId").GetString() != "source.browser.b" || directSaveRoot.GetProperty("templateVersion").GetString() != "persona-load.browser-b") throw new InvalidOperationException("Direct DSL application did not clear the stale content hash or preserve loaded metadata on save.");
    Console.WriteLine("PASS direct DSL result updates fields, metadata, filename, usage, and content hash");

    string expandedDslSetup = """
    (() => {
      const set = (selector, value) => {
        const element = document.querySelector(selector);
        if (!element) throw new Error('missing ' + selector);
        element.value = value;
        element.dispatchEvent(new Event('input', { bubbles: true }));
        element.dispatchEvent(new Event('change', { bubbles: true }));
      };
      set('#expanded-description', '已确认扩充文本');
      set('#file-name', 'persona.persona.json');
      document.querySelector('#convert-expanded-to-dsl').click();
      return true;
    })()
    """;
    await cdp.EvaluateAsync(expandedDslSetup, timeout.Token);
    await WaitForConditionAsync(cdp, "document.querySelector('#dsl-output')?.textContent === 'DSL-EXPANDED' && document.querySelector('#persona-id')?.value === 'free.expanded' && document.querySelector('#file-name')?.value === '扩充角色.persona.json'", timeout.Token);
    string expandedDslState = await cdp.EvaluateAsync("JSON.stringify({ core: document.querySelector('#core')?.value, identity: document.querySelector('#identity-facts')?.value, dsl: document.querySelector('#dsl-output')?.textContent, fileName: document.querySelector('#file-name')?.value, metadata: documentMetadata, usage: providerUsage })", timeout.Token);
    using JsonDocument expandedDslJson = JsonDocument.Parse(expandedDslState);
    JsonElement expandedDslRoot = expandedDslJson.RootElement;
    if (expandedDslRoot.GetProperty("core").GetString() != "扩充生成核心" || expandedDslRoot.GetProperty("identity").GetString() != "扩充生成身份" || expandedDslRoot.GetProperty("dsl").GetString() != "DSL-EXPANDED" || expandedDslRoot.GetProperty("fileName").GetString() != "扩充角色.persona.json" || expandedDslRoot.GetProperty("metadata").GetProperty("sourcePackId").GetString() != "source.browser.b" || expandedDslRoot.GetProperty("metadata").GetProperty("templateVersion").GetString() != "persona-load.browser-b" || expandedDslRoot.GetProperty("usage").GetProperty("totalTokens").GetInt32() != 66) throw new InvalidOperationException("Expanded DSL result did not update the editable document while preserving metadata and usage.");
    await cdp.EvaluateAsync("document.querySelector('#save-document').click()", timeout.Token);
    await WaitForConditionAsync(cdp, "window.__pwbDslTest?.savedBodies.length === 2", timeout.Token);
    string expandedSaveState = await cdp.EvaluateAsync("JSON.stringify({ expectedContentHash: window.__pwbDslTest.savedBodies[1].expectedContentHash, sourcePackId: window.__pwbDslTest.savedBodies[1].document.sourcePackId, templateVersion: window.__pwbDslTest.savedBodies[1].document.templateVersion })", timeout.Token);
    using JsonDocument expandedSaveJson = JsonDocument.Parse(expandedSaveState);
    JsonElement expandedSaveRoot = expandedSaveJson.RootElement;
    if (expandedSaveRoot.GetProperty("expectedContentHash").ValueKind != JsonValueKind.Null || expandedSaveRoot.GetProperty("sourcePackId").GetString() != "source.browser.b" || expandedSaveRoot.GetProperty("templateVersion").GetString() != "persona-load.browser-b") throw new InvalidOperationException("Expanded DSL application did not clear the stale content hash or preserve loaded metadata on save.");
    Console.WriteLine("PASS expanded DSL result updates fields, metadata, filename, usage, and content hash");

    string staleDslSetup = """
    (() => {
      const set = (selector, value) => {
        const element = document.querySelector(selector);
        if (!element) throw new Error('missing ' + selector);
        element.value = value;
        element.dispatchEvent(new Event('input', { bubbles: true }));
        element.dispatchEvent(new Event('change', { bubbles: true }));
      };
      set('#provider-prompt', '旧直接请求');
      set('#core', '直接过期前核心');
      set('#display-name', '直接过期前角色');
      set('#file-name', '直接过期前.persona.json');
      set('#expanded-description', '直接过期前扩充');
      document.querySelector('#dsl-output').textContent = 'DSL-BASE-DIRECT';
      document.querySelector('#generate-provider-draft').click();
      return true;
    })()
    """;
    await cdp.EvaluateAsync(staleDslSetup, timeout.Token);
    await WaitForConditionAsync(cdp, "window.__pwbDslTest?.pending.length === 1", timeout.Token);
    await cdp.EvaluateAsync("(() => { const set = (selector, value) => { const element = document.querySelector(selector); element.value = value; element.dispatchEvent(new Event('input', { bubbles: true })); element.dispatchEvent(new Event('change', { bubbles: true })); }; set('#core', '直接过期后用户编辑'); set('#display-name', '直接过期后角色'); set('#file-name', '直接过期后.persona.json'); set('#expanded-description', '直接过期后扩充'); return true; })()", timeout.Token);
    await cdp.EvaluateAsync("window.__pwbDslTest.pending[0].resolve(new Response(JSON.stringify({ isSuccess: true, dsl: 'DSL-STALE-DIRECT', draft: { ...window.__pwbDslTest.directDraft, id: 'stale.direct', displayName: '过期直接角色', core: '过期直接核心', identityFacts: '过期直接身份', sourcePackId: 'stale.source', templateVersion: 'stale.template' }, usage: { promptTokens: 101, completionTokens: 103, totalTokens: 204 } }), { status: 200, headers: { 'Content-Type': 'application/json' } }))", timeout.Token);
    await Task.Delay(180, timeout.Token);
    string staleDirectState = await cdp.EvaluateAsync("JSON.stringify({ core: document.querySelector('#core')?.value, displayName: document.querySelector('#display-name')?.value, fileName: document.querySelector('#file-name')?.value, expanded: document.querySelector('#expanded-description')?.value, dsl: document.querySelector('#dsl-output')?.textContent, metadata: documentMetadata, contentHash, usage: providerUsage })", timeout.Token);
    using JsonDocument staleDirectJson = JsonDocument.Parse(staleDirectState);
    JsonElement staleDirectRoot = staleDirectJson.RootElement;
    if (staleDirectRoot.GetProperty("core").GetString() != "直接过期后用户编辑" || staleDirectRoot.GetProperty("displayName").GetString() != "直接过期后角色" || staleDirectRoot.GetProperty("fileName").GetString() != "直接过期后.persona.json" || staleDirectRoot.GetProperty("expanded").GetString() != "直接过期后扩充" || staleDirectRoot.GetProperty("dsl").GetString() != "DSL-BASE-DIRECT" || staleDirectRoot.GetProperty("metadata").GetProperty("sourcePackId").GetString() != "source.browser.b" || staleDirectRoot.GetProperty("metadata").GetProperty("templateVersion").GetString() != "persona-load.browser-b" || staleDirectRoot.GetProperty("contentHash").GetString() != "hash-save-2" || staleDirectRoot.GetProperty("usage").ValueKind != JsonValueKind.Null) throw new InvalidOperationException("A stale direct DSL response overwrote current document, metadata, hash, DSL, or usage state.");
    await cdp.EvaluateAsync("window.__pwbDslTest.pending[0].resolve(new Response(JSON.stringify({ isSuccess: true, dsl: 'DSL-STALE-DIRECT-CLEANUP', draft: window.__pwbDslTest.directDraft, usage: { promptTokens: 1, completionTokens: 1, totalTokens: 2 } }), { status: 200, headers: { 'Content-Type': 'application/json' } }))", timeout.Token);
    await Task.Delay(150, timeout.Token);
    await cdp.EvaluateAsync("document.querySelector('#save-document').click()", timeout.Token);
    await WaitForConditionAsync(cdp, "window.__pwbDslTest?.savedBodies.length === 3", timeout.Token);
    string staleDirectSaveState = await cdp.EvaluateAsync("JSON.stringify({ expectedContentHash: window.__pwbDslTest.savedBodies[2].expectedContentHash, sourcePackId: window.__pwbDslTest.savedBodies[2].document.sourcePackId })", timeout.Token);
    using JsonDocument staleDirectSaveJson = JsonDocument.Parse(staleDirectSaveState);
    JsonElement staleDirectSaveRoot = staleDirectSaveJson.RootElement;
    if (staleDirectSaveRoot.GetProperty("expectedContentHash").GetString() != "hash-save-2" || staleDirectSaveRoot.GetProperty("sourcePackId").GetString() != "source.browser.b") throw new InvalidOperationException("A stale direct DSL response changed save conflict state or metadata.");
    Console.WriteLine("PASS stale direct DSL responses are discarded without overwriting state");

    string staleExpandedSetup = """
    (() => {
      const set = (selector, value) => {
        const element = document.querySelector(selector);
        if (!element) throw new Error('missing ' + selector);
        element.value = value;
        element.dispatchEvent(new Event('input', { bubbles: true }));
        element.dispatchEvent(new Event('change', { bubbles: true }));
      };
      set('#provider-prompt', '扩充过期测试提示');
      set('#core', '扩充过期前核心');
      set('#display-name', '扩充过期前角色');
      set('#file-name', '扩充过期前.persona.json');
      set('#expanded-description', '旧扩充请求');
      document.querySelector('#dsl-output').textContent = 'DSL-BASE-EXPANDED';
      document.querySelector('#convert-expanded-to-dsl').click();
      return true;
    })()
    """;
    await cdp.EvaluateAsync(staleExpandedSetup, timeout.Token);
    await WaitForConditionAsync(cdp, "window.__pwbDslTest?.pending.length === 2", timeout.Token);
    await cdp.EvaluateAsync("(() => { const set = (selector, value) => { const element = document.querySelector(selector); element.value = value; element.dispatchEvent(new Event('input', { bubbles: true })); element.dispatchEvent(new Event('change', { bubbles: true })); }; set('#core', '扩充过期后用户编辑'); set('#display-name', '扩充过期后角色'); set('#file-name', '扩充过期后.persona.json'); set('#expanded-description', '扩充过期后用户编辑'); return true; })()", timeout.Token);
    await cdp.EvaluateAsync("window.__pwbDslTest.pending[1].resolve(new Response(JSON.stringify({ isSuccess: true, dsl: 'DSL-STALE-EXPANDED', draft: { ...window.__pwbDslTest.expandedDraft, id: 'stale.expanded', displayName: '过期扩充角色', core: '过期扩充核心', identityFacts: '过期扩充身份', sourcePackId: 'stale.expanded.source', templateVersion: 'stale.expanded.template' }, usage: { promptTokens: 201, completionTokens: 203, totalTokens: 404 } }), { status: 200, headers: { 'Content-Type': 'application/json' } }))", timeout.Token);
    await Task.Delay(180, timeout.Token);
    string staleExpandedState = await cdp.EvaluateAsync("JSON.stringify({ core: document.querySelector('#core')?.value, displayName: document.querySelector('#display-name')?.value, fileName: document.querySelector('#file-name')?.value, expanded: document.querySelector('#expanded-description')?.value, dsl: document.querySelector('#dsl-output')?.textContent, metadata: documentMetadata, contentHash, usage: providerUsage })", timeout.Token);
    using JsonDocument staleExpandedJson = JsonDocument.Parse(staleExpandedState);
    JsonElement staleExpandedRoot = staleExpandedJson.RootElement;
    if (staleExpandedRoot.GetProperty("core").GetString() != "扩充过期后用户编辑" || staleExpandedRoot.GetProperty("displayName").GetString() != "扩充过期后角色" || staleExpandedRoot.GetProperty("fileName").GetString() != "扩充过期后.persona.json" || staleExpandedRoot.GetProperty("expanded").GetString() != "扩充过期后用户编辑" || staleExpandedRoot.GetProperty("dsl").GetString() != "DSL-BASE-EXPANDED" || staleExpandedRoot.GetProperty("metadata").GetProperty("sourcePackId").GetString() != "source.browser.b" || staleExpandedRoot.GetProperty("metadata").GetProperty("templateVersion").GetString() != "persona-load.browser-b" || staleExpandedRoot.GetProperty("contentHash").GetString() != "hash-save-3" || staleExpandedRoot.GetProperty("usage").ValueKind != JsonValueKind.Null) throw new InvalidOperationException("A stale expanded DSL response overwrote current document, metadata, hash, DSL, or usage state.");
    await cdp.EvaluateAsync("window.__pwbDslTest.pending[1].resolve(new Response(JSON.stringify({ isSuccess: true, dsl: 'DSL-STALE-EXPANDED-CLEANUP', draft: window.__pwbDslTest.expandedDraft, usage: { promptTokens: 1, completionTokens: 1, totalTokens: 2 } }), { status: 200, headers: { 'Content-Type': 'application/json' } }))", timeout.Token);
    await Task.Delay(150, timeout.Token);
    await cdp.EvaluateAsync("document.querySelector('#save-document').click()", timeout.Token);
    await WaitForConditionAsync(cdp, "window.__pwbDslTest?.savedBodies.length === 4", timeout.Token);
    string staleExpandedSaveState = await cdp.EvaluateAsync("JSON.stringify({ expectedContentHash: window.__pwbDslTest.savedBodies[3].expectedContentHash, sourcePackId: window.__pwbDslTest.savedBodies[3].document.sourcePackId })", timeout.Token);
    using JsonDocument staleExpandedSaveJson = JsonDocument.Parse(staleExpandedSaveState);
    JsonElement staleExpandedSaveRoot = staleExpandedSaveJson.RootElement;
    if (staleExpandedSaveRoot.GetProperty("expectedContentHash").GetString() != "hash-save-3" || staleExpandedSaveRoot.GetProperty("sourcePackId").GetString() != "source.browser.b") throw new InvalidOperationException("A stale expanded DSL response changed save conflict state or metadata.");
    await cdp.EvaluateAsync("window.fetch = window.__pwbDslTest.originalFetch;", timeout.Token);
    Console.WriteLine("PASS stale expanded DSL responses are discarded without overwriting state");
    File.WriteAllBytes(Path.Combine(root, "browser-smoke-expansion.png"), Convert.FromBase64String(await cdp.ScreenshotAsync(timeout.Token)));
    Console.WriteLine("PASS browser page and expansion interaction");

    string batchSetup = """
    (() => {
      const set = (selector, value) => {
        const element = document.querySelector(selector);
        if (!element) throw new Error('missing ' + selector);
        element.value = value;
        element.dispatchEvent(new Event('input', { bubbles: true }));
        element.dispatchEvent(new Event('change', { bubbles: true }));
      };
      set('#material-paste', '第一段资料，角色骄傲。\n\n第二段资料，角色骄傲。');
      set('#provider-endpoint', 'http://127.0.0.1:51437/batch-success/v1/chat/completions');
      set('#provider-model', 'fake-model');
      document.querySelector('#read-materials').click();
      return true;
    })()
    """;
    await cdp.EvaluateAsync(batchSetup, timeout.Token);
    await WaitForConditionAsync(cdp, "document.querySelector('#material-status')?.textContent.includes('资料已读取')", timeout.Token);
    await cdp.EvaluateAsync("document.querySelector('#segment-materials').click()", timeout.Token);
    await WaitForConditionAsync(cdp, "document.querySelectorAll('#material-segments .material-segment-card').length === 2 && !document.querySelector('#generate-batch').disabled", timeout.Token);
    await cdp.EvaluateAsync("document.querySelector('#generate-batch').click()", timeout.Token);
    await WaitForConditionAsync(cdp, "document.querySelector('#cancel-batch')?.disabled === true && (document.querySelector('#batch-status')?.textContent || '').length > 0", timeout.Token);
    string batchState = await cdp.EvaluateAsync("JSON.stringify({ status: document.querySelector('#batch-status')?.textContent, cards: document.querySelectorAll('#batch-results .batch-result-card.success').length, source: document.querySelector('#batch-results .field-hint')?.textContent })", timeout.Token);
    using JsonDocument batchJson = JsonDocument.Parse(batchState);
    if (batchJson.RootElement.GetProperty("cards").GetInt32() != 2 || !batchJson.RootElement.GetProperty("status").GetString()!.Contains("成功 2 条", StringComparison.Ordinal)) throw new InvalidOperationException("Browser batch generation did not render two successful result cards: " + batchState);
    Console.WriteLine("PASS browser material import, segmentation, batch generation, and result cards");

    if (fakeProvider.RequestCount < 5) throw new InvalidOperationException($"Expected stale, cooldown, and batch Provider requests, received {fakeProvider.RequestCount}.");
    Console.WriteLine("PASS fake Provider modes: rate-limit, truncated, malformed-json, structured-output, batch-success");
}
finally
{
    if (cdp != null)
    {
        try { await cdp.CallAsync("Browser.close", null, CancellationToken.None); } catch { }
        cdp.Dispose();
    }
    StopProcess(edgeProcess);
    StopProcess(webProcess);
}

static Process StartProcess(string fileName, string arguments, string workingDirectory)
{
    return Process.Start(new ProcessStartInfo
    {
        FileName = fileName,
        Arguments = arguments,
        WorkingDirectory = workingDirectory,
        UseShellExecute = false,
        CreateNoWindow = true
    }) ?? throw new InvalidOperationException($"Unable to start {fileName}.");
}

static void StopProcess(Process? process)
{
    if (process == null) return;
    try
    {
        if (!process.HasExited)
        {
            process.Kill(true);
            process.WaitForExit(5000);
        }
    }
    catch { }
    process.Dispose();
}

static string FindEdge()
{
    string[] candidates =
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe")
    };
    return candidates.FirstOrDefault(File.Exists) ?? throw new FileNotFoundException("Microsoft Edge was not found.");
}

static async Task WaitForHttpAsync(string url, CancellationToken cancellationToken)
{
    using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(2) };
    for (int attempt = 0; attempt < 120; attempt++)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using HttpResponseMessage response = await client.GetAsync(url, cancellationToken);
            if ((int)response.StatusCode < 500) return;
        }
        catch { }
        await Task.Delay(250, cancellationToken);
    }
    throw new TimeoutException($"Timed out waiting for {url}.");
}

static async Task WaitForConditionAsync(CdpClient cdp, string expression, CancellationToken cancellationToken)
{
    for (int attempt = 0; attempt < 120; attempt++)
    {
        if (await cdp.EvaluateAsync(expression, cancellationToken) == "true") return;
        await Task.Delay(250, cancellationToken);
    }
    throw new TimeoutException("Timed out waiting for browser condition.");
}

sealed class FakeProvider : IDisposable
{
    private readonly TcpListener listener;
    private CancellationTokenSource? stop;
    private int requestCount;

    public int RequestCount => Volatile.Read(ref requestCount);

    public FakeProvider(int port)
    {
        listener = new TcpListener(IPAddress.Loopback, port);
    }

    public void Start()
    {
        stop = new CancellationTokenSource();
        listener.Start();
        _ = RunAsync(stop.Token);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await listener.AcceptTcpClientAsync(cancellationToken); }
            catch { break; }
            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                {
                    string target = await ReadRequestTargetAsync(stream, cancellationToken);
                    Interlocked.Increment(ref requestCount);
                    string mode = target.Trim('/').Split('/').FirstOrDefault() ?? string.Empty;
                    if (mode is not ("rate-limit" or "truncated" or "malformed-json" or "structured-output")) await Task.Delay(500, cancellationToken);
                    string body = mode switch
                    {
                        "rate-limit" => "{\"error\":\"rate limited\"}",
                        "truncated" => "{\"choices\":[{\"message\":{\"content\":\"truncated",
                        "malformed-json" => "not-json",
                        "structured-output" => "{\"choices\":[{\"message\":{\"content\":\"{\\\"core\\\":\\\"structured\\\"}\"}}]}",
                        "batch-success" => "{\"choices\":[{\"message\":{\"content\":\"{\\\"summary\\\":\\\"批量测试角色\\\",\\\"axes\\\":[{\\\"index\\\":2,\\\"value\\\":2,\\\"source\\\":\\\"骄傲\\\"}]}\"}}]}",
                        _ => "{\"choices\":[{\"message\":{\"content\":\"fake provider expansion: a cautious person tests trust before showing affection.\"}}]}"
                    };
                    int statusCode = mode == "rate-limit" ? 429 : 200;
                    byte[] bytes = Encoding.UTF8.GetBytes(body);
                    StringBuilder response = new StringBuilder()
                        .Append("HTTP/1.1 ").Append(statusCode).Append(statusCode == 200 ? " OK\r\n" : " Too Many Requests\r\n")
                        .Append("Content-Type: application/json\r\n")
                        .Append("Content-Length: ").Append(bytes.Length).Append("\r\n")
                        .Append("Connection: close\r\n");
                    if (statusCode == 429) response.Append("Retry-After: 1\r\n");
                    response.Append("\r\n");
                    byte[] headerBytes = Encoding.ASCII.GetBytes(response.ToString());
                    await stream.WriteAsync(headerBytes, cancellationToken);
                    await stream.WriteAsync(bytes, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
            }
        }
    }

    private static async Task<string> ReadRequestTargetAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        List<byte> request = new List<byte>();
        byte[] buffer = new byte[1024];
        while (true)
        {
            int count = await stream.ReadAsync(buffer, cancellationToken);
            if (count == 0) return "/";
            request.AddRange(buffer.AsSpan(0, count).ToArray());
            string headers = Encoding.ASCII.GetString(request.ToArray());
            int end = headers.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (end < 0) continue;
            if (headers.Contains("Expect: 100-continue", StringComparison.OrdinalIgnoreCase))
            {
                byte[] continueBytes = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");
                await stream.WriteAsync(continueBytes, cancellationToken);
            }
            int bodyLength = 0;
            foreach (string header in headers[..end].Split("\r\n", StringSplitOptions.None))
            {
                if (!header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) continue;
                _ = int.TryParse(header["Content-Length:".Length..].Trim(), out bodyLength);
            }
            int bodyBytesRead = request.Count - (end + 4);
            while (bodyBytesRead < bodyLength)
            {
                int readCount = await stream.ReadAsync(buffer, cancellationToken);
                if (readCount == 0) break;
                bodyBytesRead += readCount;
            }
            string requestLine = headers[..end].Split("\r\n", StringSplitOptions.None)[0];
            string[] parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 ? parts[1] : "/";
        }
    }

    public void Dispose()
    {
        stop?.Cancel();
        try { listener.Stop(); } catch { }
        stop?.Dispose();
    }
}

sealed class CdpClient : IDisposable
{
    private readonly ClientWebSocket socket;
    private int nextId;

    private CdpClient(ClientWebSocket socket) { this.socket = socket; }

    public static async Task<CdpClient> ConnectAsync(string listUrl, string expectedUrl, CancellationToken cancellationToken)
    {
        using HttpClient http = new();
        string? webSocketUrl = null;
        for (int attempt = 0; attempt < 80 && webSocketUrl == null; attempt++)
        {
            using JsonDocument list = JsonDocument.Parse(await http.GetStringAsync(listUrl, cancellationToken));
            webSocketUrl = list.RootElement.EnumerateArray()
                .Where(item => item.TryGetProperty("type", out JsonElement type) && type.GetString() == "page")
                .Where(item => item.TryGetProperty("url", out JsonElement url) && string.Equals(url.GetString()?.TrimEnd('/'), expectedUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                .Select(item => item.TryGetProperty("webSocketDebuggerUrl", out JsonElement value) ? value.GetString() : null)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            if (webSocketUrl == null) await Task.Delay(100, cancellationToken);
        }
        if (webSocketUrl == null) throw new InvalidOperationException("Edge did not expose the Workbench CDP page target.");
        ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri(webSocketUrl), cancellationToken);
        return new CdpClient(socket);
    }

    public async Task<JsonElement> CallAsync(string method, object? parameters, CancellationToken cancellationToken)
    {
        int id = Interlocked.Increment(ref nextId);
        string message = JsonSerializer.Serialize(new { id, method, @params = parameters ?? new { } });
        byte[] bytes = Encoding.UTF8.GetBytes(message);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
        while (true)
        {
            byte[] buffer = new byte[64 * 1024];
            using MemoryStream stream = new();
            WebSocketReceiveResult result = await socket.ReceiveAsync(buffer, cancellationToken);
            stream.Write(buffer, 0, result.Count);
            while (!result.EndOfMessage)
            {
                result = await socket.ReceiveAsync(buffer, cancellationToken);
                stream.Write(buffer, 0, result.Count);
            }
            using JsonDocument document = JsonDocument.Parse(stream.ToArray());
            if (!document.RootElement.TryGetProperty("id", out JsonElement responseId) || responseId.GetInt32() != id) continue;
            return document.RootElement.Clone();
        }
    }

    public async Task<string> EvaluateAsync(string expression, CancellationToken cancellationToken)
    {
        JsonElement response = await CallAsync("Runtime.evaluate", new
        {
            expression,
            awaitPromise = true,
            returnByValue = true,
            userGesture = true
        }, cancellationToken);
        JsonElement commandResult = response.GetProperty("result");
        if (commandResult.TryGetProperty("exceptionDetails", out JsonElement exceptionDetails))
        {
            throw new InvalidOperationException("Browser evaluation failed: " + exceptionDetails.GetRawText());
        }
        JsonElement remoteResult = commandResult.GetProperty("result");
        if (!remoteResult.TryGetProperty("value", out JsonElement value))
        {
            return remoteResult.TryGetProperty("description", out JsonElement description)
                ? description.GetString() ?? string.Empty
                : string.Empty;
        }
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText();
    }

    public async Task<string> ScreenshotAsync(CancellationToken cancellationToken)
    {
        JsonElement response = await CallAsync("Page.captureScreenshot", new { format = "png" }, cancellationToken);
        return response.GetProperty("result").GetProperty("data").GetString() ?? throw new InvalidOperationException("Edge returned no screenshot.");
    }

    public void Dispose() { socket.Dispose(); }
}
