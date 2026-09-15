using System;
using System.Collections.Generic;

namespace Awake.UiLab
{
    public enum UiLabSessionState
    {
        Closed = 0,
        Opening = 1,
        Open = 2,
        Closing = 3,
        Faulted = 4
    }

    public enum UiLabRequestSource
    {
        Local = 0,
        F10 = 1,
        TestHarness = 2
    }

    public readonly struct UiLabRequestOutcome
    {
        public readonly long RequestId;
        public readonly long Generation;
        public readonly string Code;
        public readonly UiLabSessionState State;

        internal UiLabRequestOutcome(long requestId, long generation, string code, UiLabSessionState state)
        {
            RequestId = requestId;
            Generation = generation;
            Code = code;
            State = state;
        }

        public override string ToString()
        {
            return "#" + RequestId + " gen=" + Generation + " code=" + Code + " state=" + State;
        }
    }

    public readonly struct UiLabOwnershipSnapshot
    {
        public readonly string MovieId;
        public readonly bool MovieLoaded;
        public readonly bool LayerAdded;
        public readonly bool InputRestricted;
        public readonly bool FocusAcquired;
        public readonly bool OwnerHeld;
        public readonly long Generation;
        public readonly UiLabSessionState State;
        public readonly string CleanupState;
        public readonly int CleanupAttempt;

        /// <summary>movie / layer / 输入限制 / 焦点四项是否全部已释放。</summary>
        public bool AllOwnershipReleased
        {
            get { return !MovieLoaded && !LayerAdded && !InputRestricted && !FocusAcquired; }
        }

        internal UiLabOwnershipSnapshot(
            string movieId, bool movieLoaded, bool layerAdded, bool inputRestricted, bool focusAcquired,
            bool ownerHeld, long generation, UiLabSessionState state, string cleanupState, int cleanupAttempt)
        {
            MovieId = movieId;
            MovieLoaded = movieLoaded;
            LayerAdded = layerAdded;
            InputRestricted = inputRestricted;
            FocusAcquired = focusAcquired;
            OwnerHeld = ownerHeld;
            Generation = generation;
            State = state;
            CleanupState = cleanupState;
            CleanupAttempt = cleanupAttempt;
        }
    }

    /// <summary>
    /// AWAKE 自有 UI Lab 的宿主会话：负责 movie / layer / 焦点 / 输入限制的取得与释放，
    /// 以及重复打开合并、关闭重开、界面切换、部分加载失败回滚、卸载 close-intent + tick 重试。
    ///
    /// 本地端与以后游戏内 F10 壳共用本类型：游戏内实现注入真机 IUiLabPlatform，
    /// 本地 E2 注入 FakeUiLabPlatform。本类型不引用任何 TaleWorlds 类型。
    /// </summary>
    public sealed class UiLabHostSession : IDisposable
    {
        /// <summary>清理连续失败的上限；超过即 Faulted 并保留持有（不静默）。</summary>
        public const int MaxCleanupAttempts = 3;

        private static readonly object OwnerGate = new object();
        private static readonly HashSet<string> ClaimedOwners = new HashSet<string>(StringComparer.Ordinal);

        private readonly object _sync = new object();
        private readonly IUiLabPlatform _platform;
        private readonly string _movieId;
        private readonly string _layerName;
        private readonly int _localOrder;
        private readonly string _ownerId;
        private readonly string _candidateId;
        private readonly List<UiLabRequestOutcome> _journal = new List<UiLabRequestOutcome>();
        private readonly List<string> _teardownSteps = new List<string>();

        private UiLabSessionState _state = UiLabSessionState.Closed;
        private UiLabRequestSource _lastSource = UiLabRequestSource.Local;
        private long _nextRequestId;
        private long _generation;
        private bool _ownerHeld;

        private IUiLabScreen _screen;
        private string _screenId;
        private IUiLabLayer _layer;
        private IUiLabMovie _movie;

        private bool _layerAdded;
        private bool _movieLoaded;
        private bool _inputRestricted;
        private bool _focusAcquired;
        private bool _layerFinalized;

        private bool _reopenRequested;
        private bool _unloading;
        private bool _teardownActive;
        private int _cleanupAttempt;
        private string _cleanupState = "idle";
        private string _cleanupReason = string.Empty;
        private string _lastError = string.Empty;
        private string _lastFailureCode = string.Empty;

        private UiLabFixtureView _fixture;

        public UiLabHostSession(IUiLabPlatform platform)
            : this(platform, UiLabHostIdentity.MovieId, UiLabHostIdentity.LayerName,
                   UiLabHostIdentity.LocalOrder, UiLabHostIdentity.OwnerId, UiLabHostIdentity.CandidateId)
        {
        }

        public UiLabHostSession(
            IUiLabPlatform platform, string movieId, string layerName, int localOrder,
            string ownerId, string candidateId)
        {
            if (platform == null) throw new ArgumentNullException("platform");
            if (string.IsNullOrWhiteSpace(movieId)) throw new ArgumentException("movieId 不能为空", "movieId");
            if (UiLabHostIdentity.IsReservedProductionMovie(movieId))
            {
                throw new ArgumentException(
                    "movieId 不能使用生产入口保留名 " + UiLabHostIdentity.ReservedProductionMovieId, "movieId");
            }

            _platform = platform;
            _movieId = movieId;
            _layerName = string.IsNullOrWhiteSpace(layerName) ? movieId : layerName;
            _localOrder = localOrder;
            _ownerId = string.IsNullOrWhiteSpace(ownerId) ? string.Empty : ownerId;
            _candidateId = string.IsNullOrWhiteSpace(candidateId) ? string.Empty : candidateId;
        }

        public string MovieId { get { return _movieId; } }
        public string LayerName { get { return _layerName; } }
        public string OwnerId { get { return _ownerId; } }
        public string CandidateId { get { return _candidateId; } }
        public UiLabSessionState State { get { lock (_sync) { return _state; } } }
        public long Generation { get { lock (_sync) { return _generation; } } }
        public bool OwnerHeld { get { lock (_sync) { return _ownerHeld; } } }
        public string CleanupState { get { lock (_sync) { return _cleanupState; } } }
        public int CleanupAttempt { get { lock (_sync) { return _cleanupAttempt; } } }
        public string LastFailureCode { get { lock (_sync) { return _lastFailureCode; } } }
        public string LastError { get { lock (_sync) { return _lastError; } } }
        public UiLabFixtureView CurrentFixture { get { lock (_sync) { return _fixture; } } }

        public IReadOnlyList<UiLabRequestOutcome> Journal { get { lock (_sync) { return _journal.ToArray(); } } }
        public IReadOnlyList<string> TeardownSteps { get { lock (_sync) { return _teardownSteps.ToArray(); } } }

        public UiLabOwnershipSnapshot Ownership
        {
            get
            {
                lock (_sync)
                {
                    return new UiLabOwnershipSnapshot(
                        _movieId, _movieLoaded, _layerAdded, _inputRestricted, _focusAcquired,
                        _ownerHeld, _generation, _state, _cleanupState, _cleanupAttempt);
                }
            }
        }

        /// <summary>movie / layer / 输入限制 / 焦点四项是否全部已释放。</summary>
        public bool AllOwnershipReleased
        {
            get { lock (_sync) { return !_movieLoaded && !_layerAdded && !_inputRestricted && !_focusAcquired; } }
        }

        /// <summary>当前是否还持有任何一项界面所有权（含 owner 本身）。</summary>
        public bool HasAnyOwnership
        {
            get
            {
                lock (_sync)
                {
                    return _ownerHeld || _movieLoaded || _layerAdded || _inputRestricted || _focusAcquired || _layer != null;
                }
            }
        }

        /// <summary>请求打开（沿用当前夹具）。</summary>
        public UiLabRequestOutcome RequestOpen(UiLabRequestSource source)
        {
            lock (_sync)
            {
                return RequestOpenInternal(source, null);
            }
        }

        /// <summary>请求打开并绑定指定夹具状态 ID（只读假数据）。</summary>
        public UiLabRequestOutcome RequestOpen(UiLabRequestSource source, string fixtureStateId)
        {
            if (!UiLabFixtureStateIds.IsKnown(fixtureStateId))
            {
                throw new ArgumentOutOfRangeException(
                    "fixtureStateId", fixtureStateId, "未知的夹具状态 ID；请先登记到 fixtures/fixture-state-ids.v1.json");
            }

            lock (_sync)
            {
                return RequestOpenInternal(source, fixtureStateId);
            }
        }

        /// <summary>请求关闭。关闭失败会转为 Tick 重试，不会假装成功。</summary>
        public UiLabRequestOutcome RequestClose(UiLabRequestSource source)
        {
            lock (_sync)
            {
                return RequestCloseInternal(source, _generation);
            }
        }

        /// <summary>带 generation 栅栏的关闭：旧 generation 的关闭结果一律 no-op。</summary>
        public UiLabRequestOutcome RequestClose(UiLabRequestSource source, long observedGeneration)
        {
            lock (_sync)
            {
                return RequestCloseInternal(source, observedGeneration);
            }
        }

        /// <summary>驱动清理、重试、界面切换检测与合并重开。</summary>
        public bool Tick()
        {
            lock (_sync)
            {
                return TickInternal(_generation);
            }
        }

        /// <summary>带 generation 栅栏的 tick：旧 generation 的迟到 tick 一律 no-op。</summary>
        public bool Tick(long observedGeneration)
        {
            lock (_sync)
            {
                return TickInternal(observedGeneration);
            }
        }

        /// <summary>
        /// 卸载：只写 close intent，不阻塞、不做同步清理。
        /// 真正的清理由随后的 Tick 驱动；全部确认释放后才释放 owner。
        /// </summary>
        public UiLabRequestOutcome Unload()
        {
            lock (_sync)
            {
                _unloading = true;
                _reopenRequested = false;
                if (!_teardownActive && !HasAnyOwnershipInternal())
                {
                    _state = UiLabSessionState.Closed;
                    _cleanupState = "idle";
                    return Record(NewRequestId(), "closed");
                }
                if (!_teardownActive)
                {
                    BeginTeardown("unload");
                }
                return Record(NewRequestId(), "cleanup_queued");
            }
        }

        /// <summary>释放进程内 owner 记账（测试隔离与宿主退出兜底用）。</summary>
        public void Dispose()
        {
            lock (_sync)
            {
                ReleaseOwner();
                _teardownActive = false;
                _layer = null;
                _movie = null;
                _screen = null;
                _screenId = null;
                _layerAdded = false;
                _movieLoaded = false;
                _inputRestricted = false;
                _focusAcquired = false;
            }
        }

        // ---------------------------------------------------------------- 内部实现

        private UiLabRequestOutcome RequestOpenInternal(UiLabRequestSource source, string fixtureStateId)
        {
            _lastSource = source;
            if (fixtureStateId != null)
            {
                _fixture = UiLabFixtureCatalog.Create(fixtureStateId);
            }

            if (_unloading)
            {
                _lastFailureCode = "cleanup_queued";
                return Record(NewRequestId(), "cleanup_queued");
            }
            if (_state == UiLabSessionState.Faulted)
            {
                _lastFailureCode = "cleanup_retry_exhausted";
                return Record(NewRequestId(), "cleanup_retry_exhausted");
            }
            if (_teardownActive || _state == UiLabSessionState.Closing)
            {
                _reopenRequested = true;
                return Record(NewRequestId(), "coalesced");
            }
            if (_state == UiLabSessionState.Open)
            {
                return Record(NewRequestId(), "already_open");
            }
            return OpenNow(source);
        }

        private UiLabRequestOutcome RequestCloseInternal(UiLabRequestSource source, long observedGeneration)
        {
            _lastSource = source;
            if (observedGeneration != _generation)
            {
                return Record(NewRequestId(), "stale_ignored");
            }
            if (!_teardownActive && !HasAnyOwnershipInternal())
            {
                _state = UiLabSessionState.Closed;
                return Record(NewRequestId(), "closed");
            }
            if (_teardownActive)
            {
                return Record(NewRequestId(), "cleanup_queued");
            }
            BeginTeardown("user_close");
            ContinueTeardown();
            return Record(NewRequestId(), _state == UiLabSessionState.Closed ? "closed" : "cleanup_incomplete");
        }

        private bool TickInternal(long observedGeneration)
        {
            if (observedGeneration != _generation)
            {
                Record(NewRequestId(), "stale_ignored");
                return false;
            }

            if (_state == UiLabSessionState.Faulted)
            {
                return false;
            }

            bool didWork = false;

            if (_state == UiLabSessionState.Open && ScreenChanged())
            {
                _cleanupReason = "screen_changed";
                BeginTeardown("screen_changed");
                didWork = true;
            }

            if (_teardownActive)
            {
                ContinueTeardown();
                didWork = true;
            }

            if (_reopenRequested && !_unloading && !_teardownActive)
            {
                _reopenRequested = false;
                OpenNow(_lastSource);
                didWork = true;
            }

            return didWork;
        }

        private bool ScreenChanged()
        {
            IUiLabScreen current = _platform.TopScreen;
            if (current == null) return true;
            if (_screenId == null) return true;
            return !string.Equals(current.ScreenId, _screenId, StringComparison.Ordinal);
        }

        private UiLabRequestOutcome OpenNow(UiLabRequestSource source)
        {
            long requestId = NewRequestId();
            _lastSource = source;

            IUiLabScreen screen = _platform.TopScreen;
            if (screen == null)
            {
                _state = UiLabSessionState.Closed;
                _lastFailureCode = "screen_unavailable";
                return Record(requestId, "screen_unavailable");
            }

            if (!TryClaimOwner())
            {
                _lastFailureCode = "owner_conflict";
                return Record(requestId, "owner_conflict");
            }

            _state = UiLabSessionState.Opening;
            try
            {
                _screen = screen;
                _screenId = screen.ScreenId;
                _layer = _platform.CreateLayer(_layerName, _localOrder);
                _layer.AddTo(_screen);
                _layerAdded = true;

                _movie = _layer.LoadMovie(_movieId);
                if (_movie == null || !_movie.IsLoaded)
                {
                    _lastFailureCode = "movie_load_failed";
                    BeginRollback("open_movie_load_failed");
                    return Record(requestId, "movie_load_failed");
                }
                _movieLoaded = true;

                _layer.SetInputRestrictions(true);
                _inputRestricted = true;

                _layer.SetFocusLayer(true);
                _platform.TrySetFocus(_layer);
                _focusAcquired = true;

                _generation++;
                _state = UiLabSessionState.Open;
                _cleanupState = "idle";
                _cleanupReason = string.Empty;
                _lastFailureCode = string.Empty;
                _lastError = string.Empty;
                return Record(requestId, "opened");
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                _lastFailureCode = "open_exception";
                BeginRollback("open_exception");
                return Record(requestId, "open_exception");
            }
        }

        private void BeginRollback(string reason)
        {
            _cleanupReason = reason;
            _teardownActive = true;
            _cleanupAttempt = 0;
            _cleanupState = "rollback";
            ContinueTeardown();
        }

        private void BeginTeardown(string reason)
        {
            _cleanupReason = reason;
            _teardownActive = true;
            _cleanupAttempt = 0;
            _cleanupState = "pending";
            _state = UiLabSessionState.Closing;
            _teardownSteps.Clear();
            _teardownSteps.Add("begin:" + reason);
        }

        /// <summary>
        /// 清理次序固定：失焦 → 复位输入限制 → 释放 movie → 移除 layer → finalize。
        /// 每一步幂等；某步失败则保留未完成的持有，留给下一次 Tick 重试。
        /// </summary>
        private void ContinueTeardown()
        {
            if (!_teardownActive) return;
            if (_state == UiLabSessionState.Faulted) return;

            try
            {
                if (_layer != null && (_focusAcquired || _layer.IsFocusLayer))
                {
                    _platform.TryLoseFocus(_layer);
                    _layer.SetFocusLayer(false);
                    if (_layer.IsFocusLayer) throw new InvalidOperationException("focus_release_postcondition_failed");
                    _focusAcquired = false;
                    _teardownSteps.Add("release:focus");
                }

                if (_layer != null && (_inputRestricted || _layer.IsInputRestricted))
                {
                    _layer.ResetInputRestrictions();
                    if (_layer.IsInputRestricted) throw new InvalidOperationException("input_release_postcondition_failed");
                    _inputRestricted = false;
                    _teardownSteps.Add("release:input");
                }

                // movie 句柄只要拿到就必须释放——即使加载本身未成功也留着资源。
                if (_movie != null && (_movieLoaded || _movie.IsLoaded || !_movie.IsReleased))
                {
                    _movie.Release();
                    if (!_movie.IsReleased) throw new InvalidOperationException("movie_release_postcondition_failed");
                    _movieLoaded = false;
                    _teardownSteps.Add("release:movie");
                }

                if (_layer != null && (_layerAdded || _layer.IsAdded))
                {
                    _layer.RemoveFrom(_screen);
                    if (_layer.IsAdded) throw new InvalidOperationException("layer_removal_postcondition_failed");
                    _layerAdded = false;
                    _teardownSteps.Add("release:layer");
                }

                if (_layer != null && !_layerFinalized)
                {
                    _layerFinalized = true;
                    _teardownSteps.Add("release:finalize");
                }

                _layer = null;
                _movie = null;
                _screen = null;
                _screenId = null;
                CompleteTeardown();
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                _cleanupAttempt++;
                _cleanupState = "pending";
                _lastFailureCode = "cleanup_incomplete";
                if (_cleanupAttempt >= MaxCleanupAttempts)
                {
                    _state = UiLabSessionState.Faulted;
                    _cleanupState = "faulted";
                    _lastFailureCode = "cleanup_retry_exhausted";
                    _teardownSteps.Add("retained:cleanup_retry_exhausted");
                }
                else
                {
                    _teardownSteps.Add("retry_pending:" + _cleanupAttempt.ToString());
                }
            }
        }

        private void CompleteTeardown()
        {
            _teardownActive = false;
            _cleanupAttempt = 0;
            _cleanupState = "complete";
            _lastFailureCode = string.Empty;
            _layerFinalized = false;

            // 四项持有全部确认释放后，才轮到 owner 释放；owner 永远是最后一步。
            _teardownSteps.Add("release:owner");
            ReleaseOwner();
            _state = UiLabSessionState.Closed;
            _cleanupReason = string.Empty;
            Record(NewRequestId(), "closed");

            if (_reopenRequested && !_unloading)
            {
                _reopenRequested = false;
                OpenNow(_lastSource);
            }
        }

        private bool TryClaimOwner()
        {
            lock (OwnerGate)
            {
                if (ClaimedOwners.Contains(_movieId)) return false;
                ClaimedOwners.Add(_movieId);
                _ownerHeld = true;
                return true;
            }
        }

        private void ReleaseOwner()
        {
            lock (OwnerGate)
            {
                if (!_ownerHeld) return;
                ClaimedOwners.Remove(_movieId);
                _ownerHeld = false;
            }
        }

        private bool HasAnyOwnershipInternal()
        {
            return _ownerHeld || _movieLoaded || _layerAdded || _inputRestricted || _focusAcquired || _layer != null;
        }

        private long NewRequestId()
        {
            _nextRequestId++;
            return _nextRequestId;
        }

        private UiLabRequestOutcome Record(long requestId, string code)
        {
            UiLabRequestOutcome outcome = new UiLabRequestOutcome(requestId, _generation, code, _state);
            _journal.Add(outcome);
            return outcome;
        }
    }
}
