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
    private readonly AwakeNpcTarget _target;
    private readonly string _heroId;
    private readonly string _heroName;
    private readonly string _sceneKeywords;
    private readonly bool _isSceneShout;
    private readonly string _contactKey;
    private readonly string _entrySource;
    private readonly ConcurrentQueue<NpcDialogueUiEvent> _uiEvents = new ConcurrentQueue<NpcDialogueUiEvent>();
    private readonly List<NpcDialogueChatEntry> _history = new List<NpcDialogueChatEntry>();
    private readonly object _commandGate = new object();
    private readonly List<NpcMemoryFact> _settledFacts = new List<NpcMemoryFact>();
    private readonly List<Task> _commandTasks = new List<Task>();

    private bool _disposed;
    private bool _ready;
    private bool _initStarted;
    private bool _openingHintConsumed;
    private bool _sending;
    private int _generation;
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
    private bool _heroIsClanLeader;
    private readonly Dictionary<string, int> _heroSkills = new Dictionary<string, int>(StringComparer.Ordinal);
    private string _openingHint = string.Empty;
    private string _memoryBlock = string.Empty;
    private string _npcState = string.Empty;
    private string _memoryConversationId = string.Empty;
    private string _transcriptConversationId = string.Empty;
    private int _transcriptTurnSequence;

    internal NpcDialogueService(IMarcusAiFrameworkHost host, string heroId, string heroName, string sceneKeywords)
        : this(host, heroId, heroName, sceneKeywords, false, "npc_dialogue")
    {
    }

    internal NpcDialogueService(
        IMarcusAiFrameworkHost host,
        string heroId,
        string heroName,
        string sceneKeywords,
        bool isSceneShout,
        string entrySource = "npc_dialogue")
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _target = null;
        _heroId = heroId ?? string.Empty;
        _heroName = string.IsNullOrWhiteSpace(heroName) ? _heroId : heroName;
        _sceneKeywords = sceneKeywords ?? string.Empty;
        _isSceneShout = isSceneShout;
        _entrySource = string.IsNullOrWhiteSpace(entrySource) ? "npc_dialogue" : entrySource;
        _contactKey = ResolveContactKey(heroId);
        _gateway = new AiTaskGateway(host);
    }

    internal NpcDialogueService(IMarcusAiFrameworkHost host, AwakeNpcTarget target, string sceneKeywords)
        : this(host, target.StableId, target.DisplayName, sceneKeywords, false, "npc_dialogue")
    {
        _target = target;
    }

    internal NpcDialogueService(IMarcusAiFrameworkHost host, AwakeNpcTarget target, string sceneKeywords, string entrySource)
        : this(host, target.StableId, target.DisplayName, sceneKeywords, false, entrySource)
    {
        _target = target;
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

        CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            await RefreshPlayerKnownAsync(turnContext, linkedCts.Token).ConfigureAwait(false);

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
        try
        {
            if (!AwakeRuntime.IsCurrentSessionGeneration(sessionGeneration)) return;
            lock (_gate)
            {
                if (_disposed) return;
            }
            await AwakeRuntime.EnsureCurrentHeroBoundAsync(_host, sessionCancellationToken, requestPermission: true).ConfigureAwait(false);
            if (!AwakeRuntime.IsCurrentSessionGeneration(sessionGeneration)) return;
            if (!await AwakeRuntime.EnsureWorldStateReadyAsync(_host, sessionCancellationToken).ConfigureAwait(false)) return;
            WorldStateStore expectedStore = AwakeRuntime.WorldStateStore;
            if (!AwakeRuntime.IsCurrentSession(sessionGeneration, expectedStore)) return;
            RequestContext context = AwakeRuntime.CreateContext(_host, Guid.NewGuid().ToString("N"));
            RefreshHeroInfo();
            await RefreshPlayerKnownAsync(context, sessionCancellationToken).ConfigureAwait(false);
            if (!_isSceneShout)
            {
                await LoadMemoryBlockAsync(sessionCancellationToken).ConfigureAwait(false);
                await LoadNpcStateAsync(sessionCancellationToken).ConfigureAwait(false);
            }
            if (!AwakeRuntime.IsCurrentSession(sessionGeneration, expectedStore)) return;
            if (!await RegisterPromptBestEffortAsync(context, sessionCancellationToken).ConfigureAwait(false))
            {
                AwakeLog.Write("npc_dialogue_init_blocked prompt_registration");
                PushStatus("对话提示词未就绪。");
                return;
            }
            if (!AwakeRuntime.IsCurrentSession(sessionGeneration, expectedStore)) return;
            lock (_gate) _ready = true;
            PushStatus("对话已就绪。");
            AwakeLog.Write("npc_dialogue_ready hero=" + _heroId);
        }
        catch (OperationCanceledException)
        {
            PushStatus("对话已取消。");
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
            return ImmediateFail("对话已结束。", "npc_dialogue.player_unbound", FrameworkErrors.Create(
                "awake.player_unbound",
                FrameworkErrorCategory.Denied,
                "The current player could not be bound.",
                context?.CorrelationId ?? string.Empty,
                retryable: true,
                owner: AwakeConstants.OwnerValue));
        }
        ConsumeOpeningContext();
        await AwakeRuntime.EnsureWorldStateReadyAsync(_host, cancellationToken).ConfigureAwait(false);
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
                    _isSceneShout ? NpcPromptTemplate.CreateSceneShoutDefinition() : NpcPromptTemplate.CreateDefinition(),
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
                return;
            }
            IMarcusAiFrameworkHost host = AwakeRuntime.ResolveHost();
            if (host == null)
            {
                _npcState = string.Empty;
                return;
            }
            RequestContext context = AwakeRuntime.CreateContext(host, Guid.NewGuid().ToString("N"));
            Newtonsoft.Json.Linq.JObject relationship = await store.GetRelationshipAsync(
                _heroId,
                context,
                cancellationToken).ConfigureAwait(false);
            _npcState = NpcDialogueStateFormatter.FormatState(relationship, null, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_dialogue_state_load_error hero=" + _heroId + " error=" + ex.Message);
            _npcState = string.Empty;
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

        long perfStart = AwakePerfProbe.StartMilliseconds();
        IWorldKnowledgeQuery worldbook = WorldbookRuntime.Knowledge;
        WorldbookQuery worldbookQuery = new WorldbookQuery
        {
            HeroId = _heroId,
            CharacterId = _target?.Character?.StringId ?? string.Empty,
            IdentityId = string.Empty,
            CultureId = _heroCulture,
            KingdomId = _heroKingdomId,
            SettlementId = _heroSettlementId,
            Role = _heroRole,
            IsFemale = StringComparer.Ordinal.Equals(_heroGender, "female") ? true
                : StringComparer.Ordinal.Equals(_heroGender, "male") ? false : (bool?)null,
            IsClanLeader = _heroIsClanLeader,
            Skills = new Dictionary<string, int>(_heroSkills, StringComparer.Ordinal),
            ContentTier = "pure",
            SceneKeywords = SplitSceneKeywords(_sceneKeywords),
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
            + " ai=" + knowledgeDecision.AllowsAi
            + " identity=" + knowledgeDecision.Identity
            + " scope=" + knowledgeDecision.Scope
            + " detail=" + knowledgeDecision.Detail
            + " hits=" + string.Join(",", knowledgeDecision.HitIds)
            + " referrals=" + string.Join(",", knowledgeDecision.ReferralIds)
            + " blocked_reason=" + knowledgeDecision.BlockedReason
            + " errors=" + string.Join(",", knowledgeDecision.Errors)
            + " correlation=" + knowledgeDecision.CorrelationId);
        if (!knowledgeDecision.AllowsAi)
        {
            return new NpcKnowledgePromptBuildResult(knowledgeDecision, string.Empty);
        }
        string retrievedKnowledge = WorldKnowledgeDecisionPolicy.BuildPromptBlock(knowledgeDecision);
        string personaDsl = string.Empty;
        WorldbookService legacyWorldbook = WorldbookRuntime.Current;
        if (!_isSceneShout && legacyWorldbook != null)
        {
            try
            {
                PersonaGenerationResult persona = legacyWorldbook.BuildPersona(
                    new WorldbookQuery
                    {
                        HeroId = _heroId,
                        CharacterId = _target?.Character?.StringId ?? string.Empty,
                        IdentityId = _target?.UnnamedKey ?? string.Empty,
                        CultureId = _heroCulture,
                        Role = _heroRole,
                        IsFemale = StringComparer.Ordinal.Equals(_heroGender, "female") ? true
                            : StringComparer.Ordinal.Equals(_heroGender, "male") ? false : (bool?)null,
                        SceneKeywords = SplitSceneKeywords(_sceneKeywords),
                        ContentTier = "pure",
                        MaximumBytes = 4096
                    },
                    BuildMappingContext(),
                    4096);
                personaDsl = persona?.Dsl ?? string.Empty;
                if (persona != null && persona.Warnings.Count > 0)
                {
                    AwakeLog.Write("npc_persona_generation hero=" + _heroId
                        + " fallback=" + persona.UsedLegacyFallback
                        + " trimmed=" + persona.WasTrimmed
                        + " warnings=" + string.Join(",", persona.Warnings));
                }
            }
            catch (Exception ex)
            {
                AwakeLog.Write("npc_persona_generation_error hero=" + _heroId + " error=" + ex.Message);
            }
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
            ["player_known"] = SerializePlayerKnown(_playerName, _clanName, _kingdomName),
            ["scene"] = _sceneKeywords,
            ["scene_people"] = _isSceneShout ? BuildScenePeopleBlock() : string.Empty,
            ["opening_hint"] = openingHint,
            ["player_turn"] = playerText,
            ["npc_id"] = _heroId
        };
        string template = _isSceneShout
            ? NpcPromptTemplate.SceneShoutTemplateText
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
        PushTurnCompleted(reply, promptBuild?.Mood ?? "茫然");
        return new NpcDialogueTurnResult(true, reply, string.Empty, promptBuild?.Mood ?? "茫然");
    }
    private void OnTaskEvent(int generation, string correlationId, AiTaskEvent evt)
    {
        try
        {
            if (evt == null || generation != Volatile.Read(ref _generation)) return;
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
            out output,
            out error);
        if (!valid)
        {
            AwakeLog.Write("npc_dialogue_output_invalid error=" + error);
            FinishTurn(generation);
            PushTurnFailed("对方的话未能成形。");
            return;
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

        string persistCorrelation = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId;
        if (output.Command != null)
        {
            Task commandTask = ExecuteCommandAsync(output.Command, persistCorrelation);
            lock (_commandGate) _commandTasks.Add(commandTask);
        }

        FinishTurn(generation);
        PushTurnCompleted(normalizedReply, output.Mood);
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

    private async Task ExecuteCommandAsync(NpcDialogueCommandProposal proposal, string turnIntentId)
    {
        int sessionGeneration = AwakeRuntime.SessionGeneration;
        CancellationToken sessionCancellationToken = AwakeRuntime.SessionCancellationToken;
        try
        {
            if (!AwakeRuntime.IsCurrentSessionGeneration(sessionGeneration))
            {
                AwakeLog.Write("npc_dialogue_command_ignored reason=stale_session_before_start hero=" + _heroId);
                return;
            }
            if (_isSceneShout)
            {
                PushStatus("场景喊话不结算单条关系。");
                AwakeLog.Write("scene_shout_command_rejected command=" + (proposal?.CommandId ?? "unknown"));
                return;
            }
            if (proposal == null || Array.IndexOf(NpcDialogueConstants.AllowedCommandIds, proposal.CommandId) < 0)
            {
                PushStatus("对方的要求没有越过界线。");
                return;
            }
            JObject arguments;
            try { arguments = JObject.Parse(proposal.ArgumentsJson); }
            catch { arguments = null; }
            if (arguments == null)
            {
                PushStatus("对方的话没有形成有效请求。");
                return;
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
                PushStatus("对方没有提出可结算的请求。");
                return;
            }

            if (!await AwakeRuntime.EnsureWorldStateReadyAsync(_host, sessionCancellationToken).ConfigureAwait(false))
            {
                if (AwakeRuntime.IsCurrentSessionGeneration(sessionGeneration))
                {
                    PushStatus("关系未能结算：运行时状态不可用。");
                }
                return;
            }
            WorldStateStore expectedStore = AwakeRuntime.WorldStateStore;
            if (!AwakeRuntime.IsCurrentSession(sessionGeneration, expectedStore))
            {
                AwakeLog.Write("npc_dialogue_command_ignored reason=stale_session_after_store_ready hero=" + _heroId);
                return;
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
                return;
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
            PushStatus(result.IsSuccess ? "对方的态度有了变化。" : ("关系未能结算：" + (result.Error?.Code ?? "unknown")));
            AwakeLog.Write("npc_dialogue_command_result hero=" + _heroId + " command=" + proposal.CommandId + " ok=" + result.IsSuccess + " code=" + (result.Error?.Code ?? "none"));
        }
        catch (OperationCanceledException)
        {
            AwakeLog.Write("npc_dialogue_command_cancelled hero=" + _heroId);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_dialogue_command_error error=" + ex.Message);
            if (AwakeRuntime.IsCurrentSessionGeneration(sessionGeneration))
            {
                PushStatus("对方的要求没能落账。");
            }
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






