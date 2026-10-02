using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Awake;

internal sealed class NpcDialogueService : IDisposable
{

    private readonly object _gate = new object();
    private readonly IMarcusAiFrameworkHost _host;
    private readonly AiTaskGateway _gateway;
    private readonly CloudExportGate _cloudExportGate;
    private readonly AwakePortraitGenerator _portraitGenerator;
    private readonly AwakeNpcTarget _target;
    private readonly string _heroId;
    private readonly string _heroName;
    private readonly string _sceneKeywords;
    private readonly bool _isSceneShout;
    private NpcDialogueActionMode _actionMode;
    private readonly string _contactKey;
    private readonly string _entrySource;
    private readonly ConcurrentQueue<NpcDialogueUiEvent> _uiEvents = new ConcurrentQueue<NpcDialogueUiEvent>();
    private readonly List<NpcDialogueChatEntry> _history = new List<NpcDialogueChatEntry>();
    private readonly object _commandGate = new object();
    private readonly List<NpcMemoryFact> _settledFacts = new List<NpcMemoryFact>();
    private readonly List<Task> _commandTasks = new List<Task>();
    private readonly CancellationTokenSource _lifetimeCts = new CancellationTokenSource();

    private bool _disposed;
    private bool _ready;
    private bool _initStarted;
    private bool _openingHintConsumed;
    private bool _sending;
    private int _generation;
    private long _nextDirectTurnId;
    private int _lastCompletedGeneration = -1;
    private string _pendingPlayerText = string.Empty;
    private DateTimeOffset? _waitingSinceUtc;
    private int _playerKnownRefreshDay = -1;
    private string _playerName = string.Empty;
    private string _clanName = string.Empty;
    private string _kingdomName = string.Empty;
    private string _heroGender = "unknown";
    private string _heroCulture = string.Empty;
    private string _heroRole = "hero";
    private int _heroAge;
    private string _heroKingdomId = string.Empty;
    private string _heroSettlementId = string.Empty;
    private string _heroClanId = string.Empty;
    // 具体某人（2026-09-25）：问话对象本人的 id，喂给 hero_ids 条件。
    // 与 _heroId 的区别：那个是"本次对话的对象标识"（构造时即定），这个只在
    // RefreshHeroInfo 确认他确实是个 hero 之后才填；不是 hero ⇒ 清空（同 clan 口径）。
    private string _heroPersonId = string.Empty;
    private bool _heroIsClanLeader;
    private readonly Dictionary<string, int> _heroSkills = new Dictionary<string, int>(StringComparer.Ordinal);
    private string _openingHint = string.Empty;
    private string _memoryBlock = string.Empty;
    private string _npcState = string.Empty;
    private string _npcCommitments = string.Empty;
    private NpcDialogueCommandConfirmation _pendingConfirmation;
    private string _memoryConversationId = string.Empty;
    private string _transcriptConversationId = string.Empty;
    private int _transcriptTurnSequence;

    internal NpcDialogueService(IMarcusAiFrameworkHost host, string heroId, string heroName, string sceneKeywords)
        : this(host, heroId, heroName, sceneKeywords, false, "npc_dialogue", NpcDialogueActionMode.Chat)
    {
    }

    internal NpcDialogueService(
        IMarcusAiFrameworkHost host,
        string heroId,
        string heroName,
        string sceneKeywords,
        bool isSceneShout,
        string entrySource = "npc_dialogue",
        NpcDialogueActionMode actionMode = NpcDialogueActionMode.Chat)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _target = null;
        _heroId = heroId ?? string.Empty;
        _heroName = string.IsNullOrWhiteSpace(heroName) ? _heroId : heroName;
        _sceneKeywords = sceneKeywords ?? string.Empty;
        _isSceneShout = isSceneShout;
        _actionMode = isSceneShout ? NpcDialogueActionMode.Chat : actionMode;
        _entrySource = string.IsNullOrWhiteSpace(entrySource) ? "npc_dialogue" : entrySource;
        _contactKey = ResolveContactKey(heroId);
        _gateway = new AiTaskGateway(host);
        _cloudExportGate = new CloudExportGate(_gateway.PermissionGate);
        _portraitGenerator = new AwakePortraitGenerator(host, _cloudExportGate);
    }

    internal NpcDialogueService(IMarcusAiFrameworkHost host, AwakeNpcTarget target, string sceneKeywords)
        : this(host, target, sceneKeywords, "npc_dialogue", NpcDialogueActionMode.Chat)
    {
    }

    internal NpcDialogueService(IMarcusAiFrameworkHost host, AwakeNpcTarget target, string sceneKeywords, string entrySource)
        : this(host, target, sceneKeywords, entrySource, NpcDialogueActionMode.Chat)
    {
    }

    private NpcDialogueService(
        IMarcusAiFrameworkHost host,
        AwakeNpcTarget target,
        string sceneKeywords,
        string entrySource,
        NpcDialogueActionMode actionMode)
        : this(host, target.StableId, target.DisplayName, sceneKeywords, false, entrySource, actionMode)
    {
        _target = target;
    }

    internal static NpcDialogueService CreateNegotiation(
        IMarcusAiFrameworkHost host,
        AwakeNpcTarget target,
        string sceneKeywords,
        string entrySource = "npc_dialogue.negotiation")
    {
        return new NpcDialogueService(host, target, sceneKeywords, entrySource, NpcDialogueActionMode.Negotiation);
    }

    internal static NpcDialogueService CreateSceneShout(IMarcusAiFrameworkHost host, string sceneKeywords)
    {
        return new NpcDialogueService(
            host,
            "scene:current",
            AwakeLocalization.Resolve("awake.scene_shout.speaker", "附近的人们"),
            sceneKeywords,
            true,
            "scene_shout");
    }

    internal bool IsAvailable
    {
        get { lock (_gate) return !_disposed && _host != null; }
    }

    internal bool IsSending
    {
        get { lock (_gate) return _sending; }
    }

    internal DateTimeOffset? WaitingSinceUtc
    {
        get { lock (_gate) return _waitingSinceUtc; }
    }

    internal bool CanEscCancel
    {
        get
        {
            lock (_gate)
            {
                return _sending
                    && _waitingSinceUtc.HasValue
                    && (DateTimeOffset.UtcNow - _waitingSinceUtc.Value).TotalSeconds
                        >= NpcDialogueConstants.LongWaitCancelSeconds;
            }
        }
    }

    internal bool IsSceneShout => _isSceneShout;

    internal NpcDialogueActionMode ActionMode
    {
        get { lock (_gate) return _actionMode; }
    }

    internal bool CanChangeActionMode
    {
        get
        {
            lock (_gate)
            {
                return !_isSceneShout && !_disposed && !_sending;
            }
        }
    }

    internal bool TrySetActionMode(NpcDialogueActionMode mode)
    {
        lock (_gate)
        {
            if (_isSceneShout || _disposed || _sending) return false;
            _actionMode = mode;
            return true;
        }
    }

    internal string DisplayTitle
    {
        get
        {
            if (_isSceneShout)
            {
                return AwakeLocalization.Resolve("awake.scene_shout.title", "向场景喊话");
            }
            return AwakeLocalization.Resolve(
                "awake.dialogue.npc_title",
                "醒世·与 " + _heroName + " 交谈",
                new Dictionary<string, string> { ["HERO"] = _heroName });
        }
    }

    internal string SpeakerName => _heroName;

    /// <summary>
    /// 立绘生成的唯一入口。**它自己过云外发门**，所以调用方不许绕过它直连
    /// <see cref="AwakeImageClient"/> —— 否则就是一条不受治理的出网路。
    /// </summary>
    internal AwakePortraitGenerator PortraitGenerator => _portraitGenerator;

    /// <summary>给立绘那条后台任务用的请求上下文（与文本路径共用同一个网关与权限门）。</summary>
    internal RequestContext CreateAiContext(TimeSpan budget)
    {
        return _gateway.CreateContext(budget);
    }

    internal void Initialize()
    {
        lock (_gate)
        {
            if (_ready || _disposed || _initStarted) return;
            _initStarted = true;
        }
        _ = InitializeCoreAsync();
    }

    internal bool TryDrainUiEvent(out NpcDialogueUiEvent evt)
    {
        return _uiEvents.TryDequeue(out evt);
    }

    internal async Task<NpcDialogueTurnResult> SendAsync(string playerText, CancellationToken cancellationToken)
    {
        string trimmedPlayerText = playerText.Trim();
        if (string.IsNullOrWhiteSpace(trimmedPlayerText))
        {
            return ImmediateFail("对方在等你开口。", "npc_dialogue.empty_input");
        }
        if (trimmedPlayerText.Length > NpcDialogueConstants.MaxPlayerInputLength)
        {
            trimmedPlayerText = AwakeRuntime.TruncateTextElements(trimmedPlayerText, NpcDialogueConstants.MaxPlayerInputLength);
        }

        RequestContext turnContext = AwakeRuntime.CreateContext(_host, Guid.NewGuid().ToString("N"));
        NpcDialogueTurnResult ready = await EnsureReadyAsync(turnContext, cancellationToken).ConfigureAwait(false);
        if (!ready.Ok)
        {
            return ready;
        }

        lock (_gate)
        {
            if (_disposed)
            {
                return ImmediateFail("对话已结束。", "npc_dialogue.disposed");
            }
            if (_sending)
            {
                return ImmediateFail("对方还在回应上一位访客。", "npc_dialogue.busy");
            }
            _sending = true;
            _pendingPlayerText = trimmedPlayerText;
        }

        CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _lifetimeCts.Token,
            AwakeRuntime.SessionCancellationToken);
        try
        {
            await RefreshPlayerKnownAsync(turnContext, linkedCts.Token).ConfigureAwait(false);
            if (!_isSceneShout)
            {
                await LoadNpcStateAsync(linkedCts.Token).ConfigureAwait(false);
            }

            List<NpcDialogueChatEntry> snapshot;
            lock (_gate)
            {
                snapshot = new List<NpcDialogueChatEntry>(_history);
            }

            NpcKnowledgePromptBuildResult promptBuild = await BuildPromptInputAsync(
                snapshot,
                trimmedPlayerText,
                turnContext,
                linkedCts.Token).ConfigureAwait(false);
            if (!promptBuild.ShouldCallAi)
            {
                return CompleteDirectKnowledgeTurn(trimmedPlayerText, promptBuild);
            }
            string inputText = promptBuild.PromptText;
            if (string.IsNullOrWhiteSpace(inputText))
            {
                ClearActive();
                return ImmediateFail("对方没能成句。", "npc_dialogue.prompt_build_failed");
            }

            int generation = 0;
            bool generationAssigned = false;
            List<AiTaskEvent> earlyEvents = new List<AiTaskEvent>();
            Action<AiTaskEvent> onEvent = evt =>
            {
                lock (_gate)
                {
                    if (!generationAssigned)
                    {
                        earlyEvents.Add(evt);
                        return;
                    }
                }
                OnTaskEvent(generation, turnContext.CorrelationId, evt);
            };

            AiTaskSubmitResult submitted = await _gateway.SubmitAsync(
                NpcDialogueConstants.RouteId,
                inputText,
                _isSceneShout
                    ? NpcDialogueConstants.SceneShoutOutputContractId
                    : NpcDialogueConstants.OutputContractId,
                CloudExportPolicy.ResolveDialogueClassification(AwakeSettings.Current),
                true,
                onEvent,
                turnContext,
                linkedCts.Token).ConfigureAwait(false);
            if (!submitted.Ok)
            {
                ClearActive();
                return ImmediateFail(submitted.ErrorDisplay, submitted.ErrorCode, submitted.Error);
            }

            generation = submitted.Generation;
            AiTaskEvent[] replay;
            bool cancelledAfterSubmit = false;
            lock (_gate)
            {
                if (_disposed || !_sending)
                {
                    cancelledAfterSubmit = true;
                }
                else
                {
                    _generation = generation;
                    generationAssigned = true;
                }
                replay = earlyEvents.ToArray();
                earlyEvents.Clear();
            }
            if (cancelledAfterSubmit)
            {
                _gateway.CancelRoute(NpcDialogueConstants.RouteId);
                return ImmediateFail("对话已结束。", "npc_dialogue.cancelled", FrameworkErrors.Create(
                    "awake.cancelled",
                    FrameworkErrorCategory.Cancelled,
                    "The NPC dialogue turn was cancelled after submit.",
                    turnContext.CorrelationId,
                    owner: AwakeConstants.OwnerValue));
            }
            foreach (AiTaskEvent evt in replay)
            {
                OnTaskEvent(generation, turnContext.CorrelationId, evt);
            }
            AwakeLog.Write("npc_dialogue_submit_accepted hero=" + _heroId + " generation=" + generation + " route=" + NpcDialogueConstants.RouteId);
            PushStatus(_heroName + "正在回应……");
            lock (_gate) _waitingSinceUtc = DateTimeOffset.UtcNow;
            return new NpcDialogueTurnResult(true, string.Empty, string.Empty, string.Empty);
        }
        catch (OperationCanceledException)
        {
            ClearActive();
            return ImmediateFail("对话静默了。", "npc_dialogue.cancelled", FrameworkErrors.Create(
                "awake.cancelled",
                FrameworkErrorCategory.Cancelled,
                "The NPC dialogue turn was cancelled.",
                turnContext.CorrelationId,
                owner: AwakeConstants.OwnerValue));
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_dialogue_send_error error=" + ex.Message);
            ClearActive();
            return ImmediateFail("对方似乎没有回应。", "npc_dialogue.send_error", FrameworkErrors.Create(
                "awake.send_error",
                FrameworkErrorCategory.InternalFailure,
                "The NPC dialogue turn failed.",
                turnContext.CorrelationId,
                owner: AwakeConstants.OwnerValue));
        }
        finally
        {
            linkedCts.Dispose();
        }
    }

    internal void CancelActiveAsync()
    {
        lock (_gate)
        {
            _sending = false;
            _pendingPlayerText = string.Empty;
            _generation++;
            _waitingSinceUtc = null;
            _pendingConfirmation = null;
        }
        _gateway?.CancelRoute(NpcDialogueConstants.RouteId);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _waitingSinceUtc = null;
            if (!_isSceneShout)
            {
                NpcMemoryService memory = NpcMemoryService.Current;
                bool hasContent = _history.Count > 0;
                if (!hasContent)
                {
                    lock (_commandGate) hasContent = _settledFacts.Count > 0;
                }
                if (memory != null && !string.IsNullOrWhiteSpace(_heroId) && hasContent)
                {
                    string conversationId;
                    if (memory.Reserve(_heroId, "npc_dialogue", AwakeRuntime.CurrentGameDay(), out conversationId))
                    {
                        _memoryConversationId = conversationId;
                        string hint = BuildMemorySummaryHint();
                        Task closeTask = CloseConversationAfterCommandsAsync(memory, conversationId, AwakeRuntime.CurrentGameDay(), hint);
                        memory.TrackBackground(closeTask);
                    }
                }
            }
            if (!_openingHintConsumed)
            {
                string pendingHero;
                string pendingText;
                if (NpcDialogueContext.TryTake(out pendingHero, out pendingText)
                    && !StringComparer.Ordinal.Equals(pendingHero, _heroId))
                {
                    NpcDialogueContext.Record(pendingHero, pendingText);
                }
            }
        }
        _lifetimeCts.Cancel();
        try
        {
            _gateway?.CancelRoute(NpcDialogueConstants.RouteId);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_dialogue_cancel_dispose_error error=" + ex.Message);
        }
        try
        {
            _gateway?.Dispose();
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_dialogue_gateway_dispose_error error=" + ex.Message);
        }
        AwakeLog.Write("npc_dialogue_service_disposed hero=" + _heroId);
        if (_isSceneShout)
        {
            AwakeLog.Write("scene_shout_closed");
        }
    }

    private async Task InitializeCoreAsync()
    {
        int sessionGeneration = AwakeRuntime.SessionGeneration;
        CancellationToken sessionCancellationToken = AwakeRuntime.SessionCancellationToken;
        using (CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            sessionCancellationToken,
            _lifetimeCts.Token))
        try
        {
            if (!AwakeRuntime.IsCurrentSessionGeneration(sessionGeneration)) return;
            lock (_gate)
            {
                if (_disposed) return;
            }
            bool bound = await AwakeRuntime.EnsureCurrentHeroBoundAsync(_host, linkedCts.Token, requestPermission: true).ConfigureAwait(false);
            if (!bound || !AwakeRuntime.IsCurrentSessionGeneration(sessionGeneration) || linkedCts.IsCancellationRequested) return;
            if (!await AwakeRuntime.EnsureWorldStateReadyAsync(_host, linkedCts.Token).ConfigureAwait(false))
            {
                AwakeLog.Write("npc_dialogue_init_blocked world_state_not_ready hero=" + _heroId);
                PushStatus("对话存储未就绪。");
                return;
            }
            WorldStateStore expectedStore = AwakeRuntime.WorldStateStore;
            if (!AwakeRuntime.IsCurrentSession(sessionGeneration, expectedStore)) return;
            RequestContext context = AwakeRuntime.CreateContext(_host, Guid.NewGuid().ToString("N"));
            RefreshHeroInfo();
            await RefreshPlayerKnownAsync(context, linkedCts.Token).ConfigureAwait(false);
            if (!_isSceneShout)
            {
                await LoadMemoryBlockAsync(linkedCts.Token).ConfigureAwait(false);
                await LoadNpcStateAsync(linkedCts.Token).ConfigureAwait(false);
            }
            if (!AwakeRuntime.IsCurrentSession(sessionGeneration, expectedStore)) return;
            if (!await RegisterPromptBestEffortAsync(context, linkedCts.Token).ConfigureAwait(false))
            {
                AwakeLog.Write("npc_dialogue_init_blocked prompt_registration");
                PushStatus("对话提示词未就绪。");
                return;
            }
            if (!AwakeRuntime.IsCurrentSession(sessionGeneration, expectedStore)) return;
            lock (_gate)
            {
                if (_disposed || linkedCts.IsCancellationRequested) return;
                _ready = true;
            }
            PushStatus("对话已就绪。");
            AwakeLog.Write("npc_dialogue_ready hero=" + _heroId);
        }
        catch (OperationCanceledException)
        {
            if (!_lifetimeCts.IsCancellationRequested) PushStatus("对话已取消。");
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_dialogue_init_error error=" + ex.Message);
            PushStatus("对话未就绪。");
        }
    }

    private async Task<NpcDialogueTurnResult> EnsureReadyAsync(RequestContext context, CancellationToken cancellationToken)
    {
        bool readyEarly = false;
        lock (_gate)
        {
            if (_disposed)
            {
                return ImmediateFail("对话已结束。", "npc_dialogue.disposed");
            }
            if (_ready
                && !AwakeRuntime.SessionEnded
                && !string.IsNullOrWhiteSpace(AwakeRuntime.CurrentHeroId))
            {
                readyEarly = true;
            }
        }
        if (readyEarly)
        {
            ConsumeOpeningContext();
            return new NpcDialogueTurnResult(true, string.Empty, string.Empty, string.Empty);
        }

        bool bound = await AwakeRuntime.EnsureCurrentHeroBoundAsync(_host, cancellationToken, requestPermission: true).ConfigureAwait(false);
        if (!bound || string.IsNullOrWhiteSpace(AwakeRuntime.CurrentHeroId))
        {
            AwakeLog.Write("npc_dialogue_turn_blocked player_unbound hero=" + _heroId);
            return ImmediateFail("玩家未绑定，暂时无法交谈。", "npc_dialogue.player_unbound", FrameworkErrors.Create(
                "awake.player_unbound",
                FrameworkErrorCategory.Denied,
                "The current player could not be bound.",
                context?.CorrelationId ?? string.Empty,
                retryable: true,
                owner: AwakeConstants.OwnerValue));
        }
        ConsumeOpeningContext();
        if (!await AwakeRuntime.EnsureWorldStateReadyAsync(_host, cancellationToken).ConfigureAwait(false))
        {
            AwakeLog.Write("npc_dialogue_turn_blocked world_state_not_ready hero=" + _heroId);
            return ImmediateFail("对话存储未就绪。", "npc_dialogue.world_state_unavailable");
        }
        if (!await RegisterPromptBestEffortAsync(context, cancellationToken).ConfigureAwait(false))
        {
            return ImmediateFail("对话提示词未就绪。", "npc_dialogue.prompt_unavailable");
        }
        RefreshHeroInfo();
        await RefreshPlayerKnownAsync(context, cancellationToken).ConfigureAwait(false);
        if (!_isSceneShout)
        {
            await LoadMemoryBlockAsync(cancellationToken).ConfigureAwait(false);
            await LoadNpcStateAsync(cancellationToken).ConfigureAwait(false);
        }
        lock (_gate) _ready = true;
        PushStatus("对话已就绪。");
        return new NpcDialogueTurnResult(true, string.Empty, string.Empty, string.Empty);
    }

    private async Task<bool> RegisterPromptBestEffortAsync(RequestContext sourceContext, CancellationToken cancellationToken)
    {
        string promptId = _isSceneShout ? NpcDialogueConstants.SceneShoutPromptId : NpcDialogueConstants.PromptId;
        string promptVersion = _isSceneShout ? NpcDialogueConstants.SceneShoutPromptVersion : NpcDialogueConstants.PromptVersion;
        string promptRevision = _isSceneShout ? NpcDialogueConstants.SceneShoutPromptRevision : NpcDialogueConstants.PromptRevision;
        string attemptKey = promptId + "|" + promptVersion + "|" + promptRevision;
        try
        {
            RequestContext registerContext = AwakeRuntime.CreateContext(_host, sourceContext.CorrelationId);
            OperationResult<bool> registered = await PromptRegistrationCoordinator.EnsureAsync(
                attemptKey,
                token => _host.Prompts.RegisterAsync(
                    _isSceneShout ? SceneShoutPromptTemplate.CreateDefinition() : NpcPromptTemplate.CreateDefinition(),
                    registerContext,
                    token),
                cancellationToken).ConfigureAwait(false);
            if (!AiTaskConstants.IsPromptRegistrationUsable(registered))
            {
                AwakeLog.Write("npc_prompt_register_failed code=" + (registered.Error?.Code ?? "unknown")
                    + " category=" + (registered.Error?.Category.ToString() ?? "none")
                    + " retryable=" + (registered.Error?.Retryable.ToString() ?? "false")
                    + " correlation=" + (registered.Error?.CorrelationId ?? registerContext.CorrelationId)
                    + " detail=" + (registered.Error?.SafeFallback ?? ""));
                return false;
            }
            return true;
        }
        catch (OperationCanceledException)
        {
            AwakeLog.Write("npc_prompt_register_cancelled");
            return false;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_prompt_register_error error=" + ex.Message);
            return false;
        }
    }
    private async Task RefreshPlayerKnownAsync(RequestContext context, CancellationToken cancellationToken)
    {
        int day = AwakeRuntime.CurrentGameDay();
        if (!AwakeRuntime.ShouldRefreshPlayerKnown(_playerName, _playerKnownRefreshDay, day)) return;
        PermissionDefinition playerKnown;
        if (!PermissionCatalog.TryGet(AwakeConstants.PermissionPlayerKnownRead, out playerKnown))
        {
            AwakeLog.Write("npc_player_known_catalog_missing");
            return;
        }
        PermissionGateResult gate = new PermissionGate(_host).Evaluate(
            playerKnown,
            context);
        if (!gate.Granted)
        {
            AwakeLog.Write("npc_player_known_degraded code=" + (gate.Error?.Code ?? "none"));
            return;
        }
        try
        {
            OperationResult<PlayerSnapshotDto> result = await _host.GameData.GetCurrentPlayerAsync(
                context,
                cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess && result.Value != null)
            {
                _playerName = result.Value.Hero?.Name ?? string.Empty;
                _clanName = result.Value.Clan?.Name ?? string.Empty;
                _kingdomName = result.Value.Kingdom?.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(_clanName) && Hero.MainHero?.Clan != null)
                {
                    _clanName = Hero.MainHero.Clan.Name?.ToString() ?? string.Empty;
                }
                if (string.IsNullOrWhiteSpace(_kingdomName) && Hero.MainHero?.Clan?.Kingdom != null)
                {
                    _kingdomName = Hero.MainHero.Clan.Kingdom.Name?.ToString() ?? string.Empty;
                }
                _playerKnownRefreshDay = day;
                AwakeLog.Write("npc_player_known_loaded hero=" + _heroId + " player=" + _playerName);
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_player_known_error error=" + ex.Message);
        }
    }

    private async Task LoadMemoryBlockAsync(CancellationToken cancellationToken)
    {
        if (_isSceneShout) return;
        try
        {
            NpcMemoryService memory = NpcMemoryService.Current;
            if (memory != null)
            {
                _memoryBlock = await memory.LoadMemoryBlockAsync(_heroId, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_dialogue_memory_load_error hero=" + _heroId + " error=" + ex.Message);
        }
    }

    private async Task LoadNpcStateAsync(CancellationToken cancellationToken)
    {
        if (_isSceneShout) return;
        try
        {
            WorldStateStore store = AwakeRuntime.WorldStateStore;
            if (store == null)
            {
                _npcState = string.Empty;
                _npcCommitments = string.Empty;
                return;
            }
            IMarcusAiFrameworkHost host = _host;
            if (host == null)
            {
                _npcState = string.Empty;
                _npcCommitments = string.Empty;
                return;
            }
            RequestContext context = AwakeRuntime.CreateContext(host, Guid.NewGuid().ToString("N"));
            Newtonsoft.Json.Linq.JObject relationship = await store.GetRelationshipAsync(
                _heroId,
                context,
                cancellationToken).ConfigureAwait(false);
            Newtonsoft.Json.Linq.JObject interactions = await store.GetInteractionsAsync(
                _contactKey,
                context,
                cancellationToken).ConfigureAwait(false);
            _npcState = NpcDialogueStateFormatter.FormatState(relationship, null, null);
            _npcCommitments = NpcDialogueStateFormatter.FormatCommitments(interactions);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_dialogue_state_load_error hero=" + _heroId + " error=" + ex.Message);
            _npcState = string.Empty;
            _npcCommitments = string.Empty;
        }
    }

    private string BuildMemorySummaryHint()
    {
        List<string> tail = new List<string>();
        lock (_gate)
        {
            int start = _history.Count > 4 ? _history.Count - 4 : 0;
            for (int i = start; i < _history.Count; i++)
            {
                NpcDialogueChatEntry entry = _history[i];
                tail.Add((entry.Role == "player" ? "玩家" : _heroName) + "：" + entry.Text);
            }
        }
        return AwakeRuntime.TruncateTextElements(string.Join("\n", tail), 400);
    }

    private async Task CloseConversationAfterCommandsAsync(NpcMemoryService memory, string conversationId, int day, string hint)
    {
        int sessionGeneration = AwakeRuntime.SessionGeneration;
        WorldStateStore expectedStore = AwakeRuntime.WorldStateStore;
        CancellationToken sessionCancellationToken = AwakeRuntime.SessionCancellationToken;
        try
        {
            if (!AwakeRuntime.IsCurrentSession(sessionGeneration, expectedStore)) return;
            Task[] tasks;
            lock (_commandGate) tasks = _commandTasks.ToArray();
            if (tasks.Length > 0)
            {
                Task all = Task.WhenAll(tasks);
                Task delay = Task.Delay(TimeSpan.FromSeconds(3));
                await Task.WhenAny(all, delay).ConfigureAwait(false);
            }
            if (!AwakeRuntime.IsCurrentSession(sessionGeneration, expectedStore)) return;
            List<NpcMemoryFact> facts;
            lock (_commandGate) facts = new List<NpcMemoryFact>(_settledFacts);
            await memory.CloseConversationAsync(
                _heroId,
                conversationId,
                day,
                facts,
                hint,
                "npc_dialogue",
                sessionCancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_dialogue_memory_close_error hero=" + _heroId + " error=" + ex.Message);
        }
    }

    private void RefreshHeroInfo()
    {
        if (_isSceneShout) return;
        try
        {
            if (_target != null && !_target.IsHero)
            {
                _heroGender = _target.IsFemale ? "female" : "male";
                _heroCulture = _target.CultureId ?? string.Empty;
                _heroRole = string.IsNullOrWhiteSpace(_target.UnnamedRank) ? "unknown" : _target.UnnamedRank;
                _heroAge = (int)_target.Age;
                _heroKingdomId = string.Empty;
                _heroSettlementId = string.Empty;
                _heroClanId = string.Empty;
                _heroPersonId = string.Empty;
                _heroIsClanLeader = false;
                _heroSkills.Clear();
                return;
            }
            if (Campaign.Current?.CampaignObjectManager?.AliveHeroes == null) return;
            foreach (Hero hero in Campaign.Current.CampaignObjectManager.AliveHeroes)
            {
                if (hero == null || !StringComparer.Ordinal.Equals(hero.StringId, _heroId)) continue;
                _heroGender = hero.IsFemale ? "female" : "male";
                _heroCulture = hero.Culture?.StringId ?? string.Empty;
                _heroRole = "hero";
                _heroAge = (int)hero.Age;
                _heroKingdomId = hero.Clan?.Kingdom?.StringId ?? string.Empty;
                _heroSettlementId = (hero.CurrentSettlement ?? hero.StayingInSettlement)?.StringId ?? string.Empty;
                _heroClanId = hero.Clan?.StringId ?? string.Empty;
                _heroPersonId = hero.StringId ?? string.Empty;
                _heroIsClanLeader = hero.Clan?.Leader == hero;
                _heroSkills.Clear();
                AddHeroSkill(hero, "steward", DefaultSkills.Steward);
                AddHeroSkill(hero, "trade", DefaultSkills.Trade);
                AddHeroSkill(hero, "leadership", DefaultSkills.Leadership);
                AddHeroSkill(hero, "tactics", DefaultSkills.Tactics);
                AddHeroSkill(hero, "scouting", DefaultSkills.Scouting);
                return;
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_dialogue_hero_info_error error=" + ex.Message);
        }
    }

    private void AddHeroSkill(Hero hero, string key, SkillObject skill)
    {
        try
        {
            int value = hero?.GetSkillValue(skill) ?? 0;
            _heroSkills[key] = value;
            if (StringComparer.Ordinal.Equals(key, "steward")) _heroSkills["management"] = value;
        }
        catch
        {
        }
    }

    private string BuildNpcIdentity()
    {
        if (_isSceneShout)
        {
            return AwakeLocalization.Resolve("awake.scene_shout.identity", "场景中的人们");
        }
        if (_target != null && !_target.IsHero)
        {
            return AwakeUnnamedProfileService.BuildIdentity(_target);
        }
        return NpcDialogueStateFormatter.FormatIdentity(_heroName, _heroGender, _heroCulture);
    }

    private static string ResolveContactKey(string heroId)
    {
        if (string.IsNullOrWhiteSpace(heroId)) return string.Empty;
        if (heroId.StartsWith("hero:", StringComparison.Ordinal))
        {
            return heroId;
        }
        if (heroId.StartsWith("npc:", StringComparison.Ordinal))
        {
            string rest = heroId.Substring(4);
            int agentMarker = rest.IndexOf(":a", StringComparison.Ordinal);
            return agentMarker > 0 ? "npc:" + rest.Substring(0, agentMarker) : "npc:" + rest;
        }
        return string.Empty;
    }

    private static string BuildScenePeopleBlock()
    {
        List<string> names = new List<string>();
        foreach (AwakeNpcTarget target in NpcDialogueLauncher.GetSceneCandidates())
        {
            if (target == null || string.IsNullOrWhiteSpace(target.DisplayName)) continue;
            names.Add(target.DisplayName);
            if (names.Count >= 16) break;
        }
        return names.Count == 0
            ? "附近没有可辨认的人。"
            : "在场可辨认的人：" + string.Join("、", names);
    }

    private static List<string> SplitSceneKeywords(string text)
    {
        List<string> result = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return result;
        foreach (string item in text.Split(
            new[] { ' ', '　', '\t', '，', '。', '！', '？', '、', '；', '：', '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries))
        {
            string value = item.Trim();
            if (value.Length > 0) result.Add(value);
        }
        return result;
    }

    private WorldbookMappingContext BuildMappingContext()
    {
        WorldbookMappingContext context = new WorldbookMappingContext();
        try
        {
            if (_isSceneShout)
            {
                context.BoundSettlementName = Settlement.CurrentSettlement?.Name?.ToString() ?? string.Empty;
                return context;
            }
            if (_target != null && !_target.IsHero)
            {
                context.BoundHeroName = _heroName;
                context.BoundSettlementName = Settlement.CurrentSettlement?.Name?.ToString() ?? string.Empty;
                return context;
            }
            FillHeroMappingContext(context);
            FillKingdomMappingContext(context);
            FillClanMappingContext(context);
            FillSettlementMappingContext(context);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_dialogue_mapping_context_error error=" + ex.Message);
        }
        return context;
    }

    private void FillHeroMappingContext(WorldbookMappingContext context)
    {
        FillHero(context, Hero.AllAliveHeroes);
        FillHero(context, Hero.DeadOrDisabledHeroes);
    }

    private void FillHero(WorldbookMappingContext context, IEnumerable<Hero> heroes)
    {
        if (heroes == null) return;
        foreach (Hero hero in heroes)
        {
            if (hero == null || string.IsNullOrWhiteSpace(hero.StringId)) continue;
            context.HeroNames[hero.StringId] = hero.Name?.ToString() ?? string.Empty;
            context.Statuses["status|hero|is_alive|" + hero.StringId] = hero.IsAlive;
            context.Statuses["status|hero|is_dead|" + hero.StringId] = hero.IsDead;
            if (!StringComparer.Ordinal.Equals(hero.StringId, _heroId)) continue;
            context.BoundHeroName = hero.Name?.ToString() ?? string.Empty;
            context.BoundClanName = hero.Clan?.Name?.ToString() ?? string.Empty;
            context.BoundSettlementName = hero.CurrentSettlement?.Name?.ToString()
                ?? hero.StayingInSettlement?.Name?.ToString() ?? string.Empty;
            context.BoundKingdomName = hero.Clan?.Kingdom?.Name?.ToString() ?? string.Empty;
        }
    }

    private static void FillKingdomMappingContext(WorldbookMappingContext context)
    {
        if (Kingdom.All == null) return;
        foreach (Kingdom kingdom in Kingdom.All)
        {
            if (kingdom == null || string.IsNullOrWhiteSpace(kingdom.StringId)) continue;
            context.KingdomNames[kingdom.StringId] = kingdom.Name?.ToString() ?? string.Empty;
            context.KingdomLeaderNames[kingdom.StringId] = kingdom.Leader?.Name?.ToString() ?? string.Empty;
            context.Statuses["status|kingdom|is_eliminated|" + kingdom.StringId] = kingdom.IsEliminated;
        }
    }

    private static void FillClanMappingContext(WorldbookMappingContext context)
    {
        if (Clan.All == null) return;
        foreach (Clan clan in Clan.All)
        {
            if (clan == null || string.IsNullOrWhiteSpace(clan.StringId)) continue;
            context.ClanNames[clan.StringId] = clan.Name?.ToString() ?? string.Empty;
            context.ClanLeaderNames[clan.StringId] = clan.Leader?.Name?.ToString() ?? string.Empty;
            bool hasTown = false;
            if (clan.Settlements != null)
            {
                foreach (Settlement settlement in clan.Settlements)
                {
                    if (settlement != null && settlement.IsTown)
                    {
                        hasTown = true;
                        break;
                    }
                }
            }
            context.Statuses["status|clan|has_any_town|" + clan.StringId] = hasTown;
        }
    }

    private static void FillSettlementMappingContext(WorldbookMappingContext context)
    {
        if (Settlement.All == null) return;
        foreach (Settlement settlement in Settlement.All)
        {
            if (settlement == null || string.IsNullOrWhiteSpace(settlement.StringId)) continue;
            string name = settlement.Name?.ToString() ?? string.Empty;
            context.SettlementNames[settlement.StringId] = name;
            Clan owner = settlement.OwnerClan;
            if (owner == null || string.IsNullOrWhiteSpace(owner.StringId)) continue;
            context.SettlementOwnerClanNames[settlement.StringId] = owner.Name?.ToString() ?? string.Empty;
            context.SettlementOwnerLeaderNames[settlement.StringId] = owner.Leader?.Name?.ToString() ?? string.Empty;
            AddSettlementName(context.ClanTowns, owner.StringId, settlement.IsTown ? name : string.Empty);
            AddSettlementName(context.ClanVillages, owner.StringId, settlement.IsVillage ? name : string.Empty);
            AddSettlementName(context.ClanSettlements, owner.StringId, name);
        }
        Settlement current = Settlement.CurrentSettlement;
        if (current == null) return;
        context.BoundSettlementOwnerClanName = current.OwnerClan?.Name?.ToString() ?? string.Empty;
        context.BoundSettlementOwnerLeaderName = current.OwnerClan?.Leader?.Name?.ToString() ?? string.Empty;
    }

    private static void AddSettlementName(
        Dictionary<string, List<string>> values,
        string clanId,
        string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        List<string> list;
        if (!values.TryGetValue(clanId, out list))
        {
            list = new List<string>();
            values[clanId] = list;
        }
        if (!list.Contains(name)) list.Add(name);
    }

    private async Task<NpcKnowledgePromptBuildResult> BuildPromptInputAsync(
        IReadOnlyList<NpcDialogueChatEntry> history,
        string playerText,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        string openingHint;
        lock (_gate)
        {
            openingHint = _openingHint;
            _openingHint = string.Empty;
        }

        ContextSnapshot personaSnapshot = new ContextSnapshot
        {
            CharacterId = _target?.Character?.StringId ?? _heroId,
            PersonaSubjectStableId = _target?.StableId ?? string.Empty,
            HeroName = _heroName,
            CultureId = _heroCulture,
            KingdomId = _heroKingdomId,
            KingdomName = _kingdomName,
            ClanName = _clanName,
            Role = _heroRole,
            SceneKeywords = SplitSceneKeywords(_sceneKeywords),
            CurrentState = _npcState ?? string.Empty,
            MemoryHint = _isSceneShout ? string.Empty : (_memoryBlock ?? string.Empty),
            PlayerInput = playerText ?? string.Empty,
            BundleId = WorldbookRuntime.Persona?.Bundle?.BundleId ?? string.Empty,
            BundleRevision = WorldbookRuntime.PersonaBundleRevision,
            BundleDigest = WorldbookRuntime.PersonaBundleDigest,
            OverlayRevision = WorldbookRuntime.OverlayRevision
        };
        long perfStart = AwakePerfProbe.StartMilliseconds();
        IWorldKnowledgeQuery worldbook = WorldbookRuntime.Knowledge;
        WorldbookQuery worldbookQuery = new WorldbookQuery
        {
            HeroId = _heroId,
            CharacterId = personaSnapshot.CharacterId,
            IdentityId = string.Empty,
            CultureId = personaSnapshot.CultureId,
            KingdomId = personaSnapshot.KingdomId,
            SettlementId = _heroSettlementId,
            Role = personaSnapshot.Role,
            ClanId = _heroClanId,
            PersonHeroId = _heroPersonId,
            IsFemale = StringComparer.Ordinal.Equals(_heroGender, "female") ? true
                : StringComparer.Ordinal.Equals(_heroGender, "male") ? false : (bool?)null,
            IsClanLeader = _heroIsClanLeader,
            Skills = new Dictionary<string, int>(_heroSkills, StringComparer.Ordinal),
            ContentTier = "pure",
            SceneKeywords = new List<string>(personaSnapshot.SceneKeywords),
            PlayerText = playerText,
            MaximumBytes = KnowledgeConstants.MaximumRetrievedBlockBytes
        };
        BannerlordWorldbookIdentityAdapter.Apply(worldbookQuery, _target, _heroRole, _heroAge, _heroSkills);

        WorldKnowledgeQueryResult worldbookResult;
        if (worldbook == null)
        {
            worldbookResult = new WorldKnowledgeQueryResult
            {
                State = WorldKnowledgeDecisionPolicy.Blocked,
                BlockedReason = "worldbook_unavailable"
            };
            worldbookResult.Errors.Add("WB2-WORLDBOOK-UNAVAILABLE");
            AwakeLog.Write("npc_dialogue_worldbook_unavailable hero=" + _heroId
                + " correlation=" + (context?.CorrelationId ?? "none"));
        }
        else
        {
            try
            {
                worldbookResult = worldbook.Query(worldbookQuery) ?? new WorldKnowledgeQueryResult
                {
                    State = WorldKnowledgeDecisionPolicy.Blocked,
                    BlockedReason = "query_empty"
                };
                if (worldbookResult.Errors.Count > 0)
                {
                    AwakeLog.Write("npc_dialogue_worldbook_errors hero=" + _heroId
                        + " errors=" + string.Join(",", worldbookResult.Errors));
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                worldbookResult = new WorldKnowledgeQueryResult
                {
                    State = WorldKnowledgeDecisionPolicy.Blocked,
                    BlockedReason = "query_error"
                };
                worldbookResult.Errors.Add("WB2-QUERY-EXCEPTION");
                AwakeLog.Write("npc_dialogue_knowledge_error hero=" + _heroId
                    + " correlation=" + (context?.CorrelationId ?? "none")
                    + " error=" + ex.Message);
            }
        }
        AwakePerfProbe.Record("worldbook_query", perfStart);

        WorldKnowledgeDecision knowledgeDecision = WorldKnowledgeDecisionPolicy.Create(
            worldbookQuery,
            worldbookResult,
            context?.CorrelationId);
        AwakeLog.Write("npc_dialogue_knowledge_decision hero=" + _heroId
            + " state=" + knowledgeDecision.State
            // 2026-09-18 拆字段：原先只有一个 ai=，把「知不知道」与「让不让说」记成了一件事。
            // 现在 may_speak 只回答让不让说；「有没有知识喂进去」看知识块是否为空。
            + " may_speak=" + knowledgeDecision.AllowsAi
            + " has_knowledge=" + !string.IsNullOrWhiteSpace(WorldKnowledgeDecisionPolicy.BuildPromptBlock(knowledgeDecision))
            + " identity=" + knowledgeDecision.Identity
            + " scope=" + knowledgeDecision.Scope
            + " detail=" + knowledgeDecision.Detail
            + " hits=" + string.Join(",", knowledgeDecision.HitIds)
            + " referrals=" + string.Join(",", knowledgeDecision.ReferralIds)
            + " blocked_reason=" + knowledgeDecision.BlockedReason
            + " errors=" + string.Join(",", knowledgeDecision.Errors)
            + " correlation=" + knowledgeDecision.CorrelationId);
        // 走到这里还 AllowsAi=false 的只剩两种情况：blocked（世界书不可用/权限/内容门，该拦）
        // 与 referral（本版保持现状）。2026-09-18 起 not_found 不再在此短路 ——
        // 它允许开口，只是 BuildPromptBlock 不给知识，靠模板里那句"这段为空意味着什么"兜。
        // ⚠️ 那句今天还没写（【检索到的知识】仍是裸格子），与"内置提示词怎么完善"一起规划。
        if (!knowledgeDecision.AllowsAi)
        {
            return new NpcKnowledgePromptBuildResult(knowledgeDecision, string.Empty);
        }
        string retrievedKnowledge = WorldKnowledgeDecisionPolicy.BuildPromptBlock(knowledgeDecision);
        WorldStateStore personaStore = AwakeRuntime.WorldStateStore;
        Hero eligibleHero;
        string eligibleSubjectStableId;
        if (personaStore != null
            && PersonaSubjectEligibility.TryGetEligibleHeroSubject(_target, out eligibleHero, out eligibleSubjectStableId))
        {
            personaSnapshot = await new PersonaSessionHydrationAdapter(personaStore).HydrateAsync(
                personaSnapshot,
                _target,
                WorldbookRuntime.Persona?.Bundle,
                AwakeRuntime.SessionGeneration,
                context?.DeadlineUtc ?? DateTimeOffset.UtcNow.Add(AwakeConstants.RequestTimeout),
                cancellationToken).ConfigureAwait(false);
        }
        PersonaGenerationResult persona = WorldbookRuntime.BuildPersonaProjection(personaSnapshot);
        string personaDsl = persona?.Dsl ?? string.Empty;
        if (persona != null && persona.Warnings.Count > 0)
        {
            AwakeLog.Write("npc_persona_projection hero=" + _heroId
                + " runtime_fallback=" + persona.IsRuntimeFallback
                + " trimmed=" + persona.WasTrimmed
                + " warnings=" + string.Join(",", persona.Warnings));
        }
        string npcState = _npcState ?? string.Empty;
        if (_isSceneShout)
        {
            npcState = "场景喊话不结算个人状态。";
        }
        else
        {
            string unnamedConstraint = AwakeUnnamedProfileService.BuildStateConstraint(_target);
            if (!string.IsNullOrWhiteSpace(unnamedConstraint))
            {
                npcState = string.IsNullOrWhiteSpace(npcState)
                    ? unnamedConstraint
                    : unnamedConstraint + "\n" + npcState;
            }
            if (string.IsNullOrWhiteSpace(npcState)) npcState = "当前没有已记录的角色状态。";
        }

        Dictionary<string, string> rawVariables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["retrieved_knowledge"] = retrievedKnowledge,
            ["npc_memory"] = _isSceneShout ? string.Empty : (_memoryBlock ?? string.Empty),
            ["npc_identity"] = BuildNpcIdentity(),
            ["persona_dsl"] = personaDsl,
            ["npc_state"] = npcState,
            ["npc_commitments"] = string.IsNullOrWhiteSpace(_npcCommitments)
                ? "当前没有已记录的未决承诺。"
                : _npcCommitments,
            ["player_known"] = SerializePlayerKnown(_playerName, _clanName, _kingdomName),
            ["scene"] = _sceneKeywords,
            ["scene_people"] = _isSceneShout ? BuildScenePeopleBlock() : string.Empty,
            ["opening_hint"] = openingHint,
            ["player_turn"] = playerText,
            ["npc_id"] = _heroId,
            ["dialogue_action_mode"] = _actionMode == NpcDialogueActionMode.Negotiation
                ? "negotiation：玩家正在明确提出行动或条件；只有明确接受且关系确实改变时才可提出 command。"
                : "chat：本轮只进行普通交谈；不得输出 command。"
        };
        NpcDialoguePromptPipeline.RecordContextDiagnostics(_heroId, _isSceneShout, rawVariables);
        string template = _isSceneShout
            ? SceneShoutPromptTemplate.TemplateText
            : NpcPromptTemplate.TemplateText;
        NpcPromptBoundedResult bounded = NpcDialoguePromptPipeline.BuildBounded(
            rawVariables,
            history,
            template,
            NpcDialogueConstants.MaxPromptUtf8Bytes);
        if (bounded.IsDirectOnly)
        {
            return new NpcKnowledgePromptBuildResult(knowledgeDecision, bounded.DirectText);
        }

        PermissionDefinition promptPermission;
        string promptPermissionId = _isSceneShout
            ? NpcDialogueConstants.PermissionSceneShoutPromptCompile
            : NpcDialogueConstants.PermissionPromptCompile;
        if (!PermissionCatalog.TryGet(promptPermissionId, out promptPermission))
        {
            return new NpcKnowledgePromptBuildResult(knowledgeDecision, bounded.DirectText);
        }
        PermissionGateResult gate = new PermissionGate(_host).Evaluate(promptPermission, context);
        if (!gate.Granted)
        {
            return new NpcKnowledgePromptBuildResult(knowledgeDecision, bounded.DirectText);
        }
        try
        {
            OperationResult<PromptCompilation> compiled = await _host.Prompts.CompileAsync(
                new PromptCompileRequest(
                    _isSceneShout ? NpcDialogueConstants.SceneShoutPromptId : NpcDialogueConstants.PromptId,
                    _isSceneShout ? NpcDialogueConstants.SceneShoutPromptVersion : NpcDialogueConstants.PromptVersion,
                    _isSceneShout ? NpcDialogueConstants.SceneShoutPromptRevision : NpcDialogueConstants.PromptRevision,
                    bounded.BoundedVariables),
                context,
                cancellationToken).ConfigureAwait(false);
            if (compiled.IsSuccess && compiled.Value != null && !string.IsNullOrWhiteSpace(compiled.Value.CompiledText))
            {
                return new NpcKnowledgePromptBuildResult(knowledgeDecision, NpcDialoguePromptPipeline.EnsureBudget(compiled.Value.CompiledText, NpcDialogueConstants.MaxPromptUtf8Bytes));
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_prompt_compile_error error=" + ex.Message);
        }
        return new NpcKnowledgePromptBuildResult(knowledgeDecision, bounded.DirectText);
    }

    private NpcDialogueTurnResult CompleteDirectKnowledgeTurn(
        string playerText,
        NpcKnowledgePromptBuildResult promptBuild)
    {
        WorldKnowledgeDecision knowledge = promptBuild?.Knowledge;
        string reply = string.IsNullOrWhiteSpace(promptBuild?.DirectReply)
            ? "我没有可靠的说法。"
            : promptBuild.DirectReply;
        long directTurnId;
        lock (_gate)
        {
            if (_disposed)
            {
                return ImmediateFail("对话已结束。", "npc_dialogue.disposed");
            }
            _pendingPlayerText = string.Empty;
            _history.Add(new NpcDialogueChatEntry("player", playerText ?? string.Empty));
            _history.Add(new NpcDialogueChatEntry("npc", reply));
            while (_history.Count > NpcDialogueConstants.HistoryCapacity) _history.RemoveAt(0);
            directTurnId = ++_nextDirectTurnId;
        }
        AppendTranscriptTurn(playerText ?? string.Empty, reply);
        ClearActive();
        AwakeLog.Write("npc_dialogue_worldbook_direct hero=" + _heroId
            + " state=" + (knowledge?.State ?? WorldKnowledgeDecisionPolicy.NotFound)
            + " blocked_reason=" + (knowledge?.BlockedReason ?? string.Empty)
            + " referrals=" + string.Join(",", knowledge?.ReferralIds ?? new List<string>())
            + " hits=" + string.Join(",", knowledge?.HitIds ?? new List<string>())
            + " errors=" + string.Join(",", knowledge?.Errors ?? new List<string>())
            + " correlation=" + (knowledge?.CorrelationId ?? string.Empty));
        CompleteVisibleTurn(0, "dialogue:" + _heroId + ":direct:" + directTurnId,
            reply, promptBuild?.Mood ?? "茫然", "worldbook_direct", false);
        return new NpcDialogueTurnResult(true, reply, string.Empty, promptBuild?.Mood ?? "茫然");
    }
    private void OnTaskEvent(int generation, string correlationId, AiTaskEvent evt)
    {
        try
        {
            if (evt == null || generation != Volatile.Read(ref _generation)) return;
            // 用量记账必须在分支之前兜住全部事件：UsageUpdate 走不出结算会被丢，
            // 而框架兜底发出的取消/失败终态携带 0，只有靠前面攒下的 UsageUpdate 才对得上账。
            AwakeTokenUsage.Track(evt, NpcDialogueConstants.RouteId);
            switch (evt.Kind)
            {
                case AiTaskEventKind.TextDelta:
                    if (!string.IsNullOrWhiteSpace(evt.Text)) PushStreamDelta(evt.Text);
                    break;
                case AiTaskEventKind.RouteChanged:
                    AwakeLog.Write("npc_dialogue_route_changed model=" + (evt.ResolvedModel ?? "unknown"));
                    break;
                case AiTaskEventKind.Completed:
                    HandleCompleted(generation, correlationId, evt);
                    break;
                case AiTaskEventKind.Failed:
                    HandleFailed(generation, evt);
                    break;
                case AiTaskEventKind.Cancelled:
                    FinishTurn(generation);
                    PushTurnFailed("对方沉默了。");
                    break;
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_dialogue_event_error error=" + ex.Message);
        }
    }

    private void HandleCompleted(int generation, string correlationId, AiTaskEvent evt)
    {
        lock (_gate)
        {
            if (generation == _lastCompletedGeneration || _disposed) return;
            _lastCompletedGeneration = generation;
        }
        NpcDialogueValidatedOutput output;
        string error;
        string validationText = string.IsNullOrWhiteSpace(evt.StructuredJson) ? evt.Text : evt.StructuredJson;
        bool valid = NpcDialogueOutputValidator.TryValidate(
            validationText,
            _isSceneShout
                ? NpcDialogueConstants.SceneShoutOutputContractId
                : NpcDialogueConstants.OutputContractId,
            !_isSceneShout && _actionMode == NpcDialogueActionMode.Negotiation,
            out output,
            out error);
        if (!valid)
        {
            AwakeLog.Write("npc_dialogue_output_invalid error=" + error);
            FinishTurn(generation);
            PushTurnFailed("对方的话未能成形。");
            return;
        }

        if (output.CommandSuppressed)
        {
            AwakeLog.Write("npc_dialogue_command_suppressed hero=" + _heroId + " mode=" + _actionMode);
        }

        string normalizedReply = NpcDialogueReplyNormalizer.Normalize(output.Reply);
        string playerText;
        lock (_gate)
        {
            if (generation != _generation || _disposed) return;
            playerText = _pendingPlayerText ?? string.Empty;
            _pendingPlayerText = string.Empty;
            _history.Add(new NpcDialogueChatEntry("player", playerText));
            _history.Add(new NpcDialogueChatEntry("npc", normalizedReply));
            while (_history.Count > NpcDialogueConstants.HistoryCapacity) _history.RemoveAt(0);
        }
        AppendTranscriptTurn(playerText, normalizedReply);

        if (output.Command != null)
        {
            NpcDialogueCommandConfirmation confirmation = new NpcDialogueCommandConfirmation(
                generation, correlationId, output.Command, normalizedReply, output.Mood);
            lock (_gate) _pendingConfirmation = confirmation;
            PushCommandConfirmation(confirmation);
            return;
        }

        CompleteVisibleTurn(generation, correlationId, normalizedReply, output.Mood, "ai_reply", true);
    }

    internal void ConfirmPendingCommand()
    {
        NpcDialogueCommandConfirmation confirmation;
        lock (_gate)
        {
            confirmation = _pendingConfirmation;
            if (confirmation == null || !_sending || confirmation.Generation != _generation) return;
            _pendingConfirmation = null;
        }
        Task<NpcDialogueCommandSettlement> commandTask = ExecuteCommandAsync(confirmation.Proposal, confirmation.CorrelationId);
        lock (_commandGate) _commandTasks.Add(commandTask);
        NpcDialogueConfirmedSettlementRunner.Track(_heroId, confirmation.Generation, confirmation.CorrelationId, commandTask);
        _ = CompleteTurnAfterCommandAsync(confirmation.Generation, confirmation.Reply, confirmation.Mood, commandTask);
    }

    internal void RejectPendingCommand()
    {
        NpcDialogueCommandConfirmation confirmation;
        lock (_gate)
        {
            confirmation = _pendingConfirmation;
            if (confirmation == null || !_sending || confirmation.Generation != _generation) return;
            _pendingConfirmation = null;
        }
        PushStatus(AwakeLocalization.Resolve("awake.ui.dialogue_proposal_cancelled", "提案未确认，未写入游戏状态。"));
        CompleteVisibleTurn(confirmation.Generation, confirmation.CorrelationId,
            confirmation.Reply, confirmation.Mood, "command_rejected", true);
    }

    private string BuildTranscriptSpeaker()
    {
        if (_target == null) return _heroName;
        if (_target.Hero != null)
        {
            string clanName = _target.Hero.Clan?.Name?.ToString() ?? string.Empty;
            string kingdomName = _target.Hero.Clan?.Kingdom?.Name?.ToString() ?? string.Empty;
            string settlementName = _target.Hero.CurrentSettlement?.Name?.ToString()
                ?? _target.Hero.StayingInSettlement?.Name?.ToString()
                ?? string.Empty;
            return AwakeContactLabelBuilder.Build(
                _heroName,
                clanName,
                kingdomName,
                settlementName,
                _target.Hero.IsWanderer,
                _target.Hero.IsNotable && string.IsNullOrWhiteSpace(clanName));
        }
        string npcSettlement = Settlement.CurrentSettlement?.Name?.ToString() ?? string.Empty;
        return AwakeContactLabelBuilder.Build(
            _heroName,
            string.Empty,
            string.Empty,
            npcSettlement,
            false,
            true);
    }

    private void AppendTranscriptTurn(string playerText, string npcText)
    {
        if (_isSceneShout || string.IsNullOrWhiteSpace(_contactKey))
        {
            return;
        }
        try
        {
            string conversationId;
            lock (_gate)
            {
                if (string.IsNullOrWhiteSpace(_transcriptConversationId))
                {
                    _transcriptConversationId = "conv|" + Guid.NewGuid().ToString("N");
                }
                conversationId = _transcriptConversationId;
            }
            string contactKey = _contactKey;
            string source = _entrySource;
            string npcName = BuildTranscriptSpeaker();
            int day = AwakeRuntime.CurrentGameDay();
            string location = Settlement.CurrentSettlement?.Name?.ToString() ?? string.Empty;
            int sequence;
            lock (_gate) sequence = ++_transcriptTurnSequence;
            int sessionGeneration = AwakeRuntime.SessionGeneration;
            WorldStateStore expectedStore = AwakeRuntime.WorldStateStore;
            CancellationToken sessionCancellationToken = AwakeRuntime.SessionCancellationToken;
            if (!AwakeRuntime.IsCurrentSession(sessionGeneration, expectedStore)) return;
            AwakeBackgroundTask.Run(
                () => AwakeTranscriptService.AppendTurnAsync(
                    sessionGeneration,
                    expectedStore,
                    contactKey,
                    conversationId,
                    day,
                    location,
                    playerText,
                    npcText,
                    npcName,
                    source,
                    "turn|" + conversationId + "|" + day + "|" + sequence,
                    sessionCancellationToken),
                "transcript_turn");
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_dialogue_transcript_append_error error=" + ex.Message);
        }
    }

    private async Task CompleteTurnAfterCommandAsync(
        int generation,
        string reply,
        string mood,
        Task<NpcDialogueCommandSettlement> commandTask)
    {
        NpcDialogueCommandSettlement settlement;
        try
        {
            settlement = await commandTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_dialogue_command_result_wait_error error=" + ex.Message);
            settlement = new NpcDialogueCommandSettlement(
                false,
                AwakeLocalization.Resolve("awake.ui.dialogue_settlement_failed", "关系没有改变。", new Dictionary<string, string>
                {
                    ["CODE"] = "unknown"
                }));
        }
        if (!IsCurrentTurn(generation)) return;
        if (!string.IsNullOrWhiteSpace(settlement?.StatusText)) PushStatus(settlement.StatusText);
        FinishTurn(generation);
        PushTurnCompleted(reply, mood);
    }

    private async Task<NpcDialogueCommandSettlement> ExecuteCommandAsync(NpcDialogueCommandProposal proposal, string turnIntentId)
    {
        int sessionGeneration = AwakeRuntime.SessionGeneration;
        CancellationToken sessionCancellationToken = AwakeRuntime.SessionCancellationToken;
        try
        {
            if (!AwakeRuntime.IsCurrentSessionGeneration(sessionGeneration))
            {
                AwakeLog.Write("npc_dialogue_command_ignored reason=stale_session_before_start hero=" + _heroId);
                return new NpcDialogueCommandSettlement(false, string.Empty);
            }
            if (_isSceneShout)
            {
                AwakeLog.Write("scene_shout_command_rejected command=" + (proposal?.CommandId ?? "unknown"));
                return new NpcDialogueCommandSettlement(false, "场景喊话不结算单条关系。");
            }
            if (_actionMode != NpcDialogueActionMode.Negotiation)
            {
                AwakeLog.Write("npc_dialogue_command_rejected mode=" + _actionMode + " command=" + (proposal?.CommandId ?? "unknown"));
                return new NpcDialogueCommandSettlement(false, string.Empty);
            }
            if (proposal == null || Array.IndexOf(NpcDialogueConstants.AllowedCommandIds, proposal.CommandId) < 0)
            {
                return new NpcDialogueCommandSettlement(false, "对方的要求没有越过界线。");
            }
            JObject arguments;
            try { arguments = JObject.Parse(proposal.ArgumentsJson); }
            catch { arguments = null; }
            if (arguments == null)
            {
                return new NpcDialogueCommandSettlement(false, "对方的话没有形成有效请求。");
            }

            bool commandAllowed = false;
            foreach (string commandId in NpcDialogueConstants.AllowedCommandIds)
            {
                if (StringComparer.Ordinal.Equals(commandId, proposal.CommandId))
                {
                    commandAllowed = true;
                    break;
                }
            }
            if (!commandAllowed)
            {
                return new NpcDialogueCommandSettlement(false, "对方没有提出可结算的请求。");
            }

            if (!await AwakeRuntime.EnsureWorldStateReadyAsync(_host, sessionCancellationToken).ConfigureAwait(false))
            {
                if (AwakeRuntime.IsCurrentSessionGeneration(sessionGeneration))
                {
                    return new NpcDialogueCommandSettlement(false,
                        AwakeLocalization.Resolve("awake.ui.dialogue_settlement_unavailable", "关系未能结算：运行时状态不可用。"));
                }
                return new NpcDialogueCommandSettlement(false, string.Empty);
            }
            WorldStateStore expectedStore = AwakeRuntime.WorldStateStore;
            if (!AwakeRuntime.IsCurrentSession(sessionGeneration, expectedStore))
            {
                AwakeLog.Write("npc_dialogue_command_ignored reason=stale_session_after_store_ready hero=" + _heroId);
                return new NpcDialogueCommandSettlement(false, string.Empty);
            }
            OperationResult<string> result = await new WorldCommandBridge(_host).ExecuteAsync(
                new WorldCommandProposal(
                    proposal.CommandId,
                    arguments.ToString(Newtonsoft.Json.Formatting.None),
                    string.IsNullOrWhiteSpace(proposal.Reason) ? "NPC 对话结算" : proposal.Reason),
                turnIntentId,
                sessionCancellationToken).ConfigureAwait(false);
            if (!AwakeRuntime.IsCurrentSession(sessionGeneration, expectedStore))
            {
                AwakeLog.Write("npc_dialogue_command_ignored reason=stale_session_after_execute hero=" + _heroId);
                return new NpcDialogueCommandSettlement(false, string.Empty);
            }
            if (result.IsSuccess)
            {
                lock (_commandGate)
                {
                    int trust = IntValue(arguments["trustDelta"]);
                    int love = IntValue(arguments["loveDelta"]);
                    int hostility = IntValue(arguments["hostilityDelta"]);
                    _settledFacts.Add(new NpcMemoryFact(
                        "关系变化：信任" + Sign(trust) + trust + "、爱意" + Sign(love) + love + "、敌意" + Sign(hostility) + hostility));
                }
            }
            AwakeLog.Write("npc_dialogue_command_result hero=" + _heroId + " command=" + proposal.CommandId + " ok=" + result.IsSuccess + " code=" + (result.Error?.Code ?? "none"));
            if (result.IsSuccess)
            {
                return new NpcDialogueCommandSettlement(true,
                    AwakeLocalization.Resolve("awake.ui.dialogue_settled", "关系变化已落账。"));
            }
            return new NpcDialogueCommandSettlement(false,
                AwakeLocalization.Resolve("awake.ui.dialogue_settlement_failed", "关系没有改变：{CODE}", new Dictionary<string, string>
                {
                    ["CODE"] = result.Error?.Code ?? "unknown"
                }));
        }
        catch (OperationCanceledException)
        {
            AwakeLog.Write("npc_dialogue_command_cancelled hero=" + _heroId);
            return new NpcDialogueCommandSettlement(false, string.Empty);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_dialogue_command_error error=" + ex.Message);
            return new NpcDialogueCommandSettlement(false,
                AwakeLocalization.Resolve("awake.ui.dialogue_settlement_failed", "关系没有改变：{CODE}", new Dictionary<string, string>
                {
                    ["CODE"] = "exception"
                }));
        }
    }

    private bool IsCurrentTurn(int generation)
    {
        lock (_gate)
        {
            return !_disposed && _sending && generation == _generation;
        }
    }

    private void HandleFailed(int generation, AiTaskEvent evt)
    {
        FrameworkError error = evt.Error;
        AwakeLog.Write("npc_dialogue_turn_failed code=" + (error?.Code ?? "none") + " category=" + (error?.Category.ToString() ?? "none"));
        string display;
        if (error != null && StringComparer.Ordinal.Equals(error.Code, "ai.cloud_export_denied"))
        {
            display = AwakeLocalization.Resolve(
                "awake.dialogue.cloud_export_denied",
                "Cloud export denied: enable cloud export for AWAKE.route.npc.dialogue in Marcus AI settings and in AWAKE MCM.");
        }
        else if (error != null && StringComparer.Ordinal.Equals(error.Code, "awake.cloud_export_disabled"))
        {
            display = AwakeLocalization.Resolve(
                "awake.dialogue.cloud_export_disabled",
                "AWAKE cloud export is disabled: enable it under MCM Data & Debug.");
        }
        else if (error != null && error.Category == FrameworkErrorCategory.Timeout) display = "对方回应超时了。";
        else if (error != null && error.Category == FrameworkErrorCategory.Unavailable) display = "对方暂时无法开口。";
        else if (error != null && error.Category == FrameworkErrorCategory.Denied) display = "对话被拒绝了。";
        else if (error != null && error.Category == FrameworkErrorCategory.Expired) display = "时机已经过去。";
        else display = "对方似乎没有开口。" + (string.IsNullOrWhiteSpace(error?.Code) ? string.Empty : "（" + error.Code + "）");
        FinishTurn(generation);
        PushTurnFailed(display);
    }

    private void FinishTurn(int generation)
    {
        lock (_gate)
        {
            if (generation != _generation) return;
            _sending = false;
            _pendingPlayerText = string.Empty;
            _waitingSinceUtc = null;
        }
        _gateway?.FinishTurn(NpcDialogueConstants.RouteId, generation);
    }

    private void CompleteVisibleTurn(
        int generation,
        string correlationId,
        string reply,
        string mood,
        string completionKind,
        bool finishTurn)
    {
        if (finishTurn) FinishTurn(generation);
        AwakeLog.Write("npc_dialogue_turn_completed hero=" + _heroId
            + " generation=" + generation
            + " correlation=" + (correlationId ?? string.Empty)
            + " completion_kind=" + (completionKind ?? string.Empty));
        PushTurnCompleted(reply, mood);
    }

    private void ClearActive()
    {
        lock (_gate)
        {
            _sending = false;
            _pendingPlayerText = string.Empty;
            _waitingSinceUtc = null;
        }
    }

    private static int IntValue(Newtonsoft.Json.Linq.JToken token)
    {
        if (token == null || token.Type != Newtonsoft.Json.Linq.JTokenType.Integer) return 0;
        try { return (int)token; } catch { return 0; }
    }

    private static string SerializePlayerKnown(string playerName, string clanName, string kingdomName)
    {
        if (string.IsNullOrWhiteSpace(playerName)) return string.Empty;
        return "姓名 " + playerName + "；家族 " + clanName + "；王国 " + kingdomName;
    }

    private static string Sign(int value)
    {
        return value >= 0 ? "+" : string.Empty;
    }

    private NpcDialogueTurnResult ImmediateFail(string display, string code, FrameworkError error = null)
    {
        PushTurnFailed(display);
        return new NpcDialogueTurnResult(false, string.Empty, display, string.Empty, error);
    }

    private void PushStatus(string text)
    {
        _uiEvents.Enqueue(new NpcDialogueUiEvent(NpcDialogueUiEventKind.Status, text, null));
    }

    private void ConsumeOpeningContext()
    {
        string heroId;
        string text;
        if (!NpcDialogueContext.TryTake(out heroId, out text)) return;
        if (!StringComparer.Ordinal.Equals(heroId, _heroId))
        {
            NpcDialogueContext.Record(heroId, text);
            return;
        }
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(_openingHint))
            {
                _openingHint = text ?? string.Empty;
                _openingHintConsumed = true;
            }
        }
    }

    private void PushStreamDelta(string text)
    {
        _uiEvents.Enqueue(new NpcDialogueUiEvent(NpcDialogueUiEventKind.StreamDelta, text, null));
    }

    private void PushCommandConfirmation(NpcDialogueCommandConfirmation confirmation)
    {
        _uiEvents.Enqueue(new NpcDialogueUiEvent(
            NpcDialogueUiEventKind.CommandConfirmationRequired,
            string.Empty,
            null,
            confirmation));
    }

    private void PushTurnCompleted(string reply, string mood)
    {
        _uiEvents.Enqueue(new NpcDialogueUiEvent(
            NpcDialogueUiEventKind.TurnCompleted,
            string.Empty,
            new NpcDialogueTurnResult(true, reply, string.Empty, mood)));
    }

    private void PushTurnFailed(string display)
    {
        _uiEvents.Enqueue(new NpcDialogueUiEvent(
            NpcDialogueUiEventKind.TurnFailed,
            string.Empty,
            new NpcDialogueTurnResult(false, string.Empty, display, string.Empty)));
    }
}
