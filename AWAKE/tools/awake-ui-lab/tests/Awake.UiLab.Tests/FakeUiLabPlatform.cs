using System;
using System.Collections.Generic;
using Awake.UiLab;

namespace Awake.UiLab.Tests
{
    /// <summary>本地假顶层界面。ScreenId 变化即模拟"玩家切了界面"。</summary>
    public sealed class FakeUiLabScreen : IUiLabScreen
    {
        public string ScreenId { get; private set; }

        public FakeUiLabScreen(string screenId)
        {
            ScreenId = screenId ?? string.Empty;
        }
    }

    /// <summary>本地假 movie：只记账已加载/已释放，并支持在释放步骤注入故障。</summary>
    public sealed class FakeUiLabMovie : IUiLabMovie
    {
        private readonly FakeUiLabPlatform _owner;

        public string MovieId { get; private set; }
        public bool IsLoaded { get; private set; }
        public bool IsReleased { get; private set; }
        public int ReleaseCount { get; private set; }

        internal FakeUiLabMovie(FakeUiLabPlatform owner, string movieId, bool loaded)
        {
            _owner = owner;
            MovieId = movieId ?? string.Empty;
            IsLoaded = loaded;
        }

        public void Release()
        {
            _owner.ThrowIfCleanupFault("movie");
            if (IsReleased) return;
            IsReleased = true;
            ReleaseCount++;
            _owner.OnMovieReleased(this);
        }
    }

    /// <summary>本地假 layer：所有能力都被会话使用到，且每一步都可注入故障。</summary>
    public sealed class FakeUiLabLayer : IUiLabLayer
    {
        private readonly FakeUiLabPlatform _owner;

        internal FakeUiLabLayer(FakeUiLabPlatform owner, string layerName, int localOrder)
        {
            _owner = owner;
            LayerName = layerName ?? string.Empty;
            LocalOrder = localOrder;
        }

        public string LayerName { get; private set; }
        public int LocalOrder { get; private set; }
        public bool IsAdded { get; internal set; }
        public bool IsInputRestricted { get; internal set; }
        public bool IsFocusLayer { get; internal set; }

        public IUiLabMovie LoadMovie(string movieId)
        {
            return _owner.LoadMovie(this, movieId);
        }

        public void AddTo(IUiLabScreen screen)
        {
            _owner.OnLayerAdded(this, screen);
        }

        public void RemoveFrom(IUiLabScreen screen)
        {
            _owner.ThrowIfCleanupFault("layer");
            _owner.OnLayerRemoved(this, screen);
        }

        public void SetInputRestrictions(bool restricted)
        {
            if (restricted)
            {
                _owner.ThrowIfInputFailure();
            }
            _owner.OnSetInputRestrictions(this, restricted);
        }

        public void ResetInputRestrictions()
        {
            _owner.ThrowIfCleanupFault("input");
            _owner.OnSetInputRestrictions(this, false);
        }

        public void SetFocusLayer(bool value)
        {
            _owner.OnSetFocusLayer(this, value);
        }
    }

    /// <summary>
    /// 本地假平台：记录创建/释放计数、持有标志与释放次序，并支持故障注入。
    /// 它只模拟所有权台账，不渲染任何东西——本地 E2 不等于真机渲染。
    /// </summary>
    public sealed class FakeUiLabPlatform : IUiLabPlatform
    {
        private readonly List<string> _events = new List<string>();
        private readonly HashSet<FakeUiLabLayer> _liveLayers = new HashSet<FakeUiLabLayer>();
        private readonly List<FakeUiLabMovie> _movies = new List<FakeUiLabMovie>();
        private readonly HashSet<FakeUiLabLayer> _inputRestricted = new HashSet<FakeUiLabLayer>();
        private readonly HashSet<FakeUiLabLayer> _focusLayers = new HashSet<FakeUiLabLayer>();
        private readonly Dictionary<string, int> _cleanupFailures = new Dictionary<string, int>(StringComparer.Ordinal);

        public FakeUiLabScreen Screen { get; set; }
        public IUiLabScreen TopScreen { get { return Screen; } }

        public int LayersCreated { get; private set; }
        public int LayersAdded { get; private set; }
        public int LayersRemoved { get; private set; }
        public int MoviesLoaded { get; private set; }
        public int MoviesReleased { get; private set; }
        public int InputSetCount { get; private set; }
        public int InputResetCount { get; private set; }
        public int FocusSetCount { get; private set; }
        public int FocusLostCount { get; private set; }

        /// <summary>true 时 LoadMovie 返回一个"未加载成功"的 movie。</summary>
        public bool FailMovieLoad { get; set; }

        /// <summary>true 时 LoadMovie 直接抛异常。</summary>
        public bool ThrowOnMovieLoad { get; set; }

        /// <summary>剩余多少次要让 SetInputRestrictions(true) 抛异常。</summary>
        public int InputRestrictionFailures { get; set; }

        /// <summary>剩余多少次要让 TrySetFocus 抛异常。</summary>
        public int FocusFailures { get; set; }

        public FakeUiLabPlatform()
        {
            Screen = new FakeUiLabScreen("campaign");
        }

        /// <summary>给某个清理步骤注入 N 次故障（focus / input / movie / layer）。</summary>
        public void SetCleanupFailure(string step, int times)
        {
            _cleanupFailures[step] = times;
        }

        public IReadOnlyList<string> Events { get { return _events; } }

        public bool HasLiveLayers { get { return _liveLayers.Count > 0; } }
        public bool AnyInputRestricted { get { return _inputRestricted.Count > 0; } }
        public bool AnyFocusLayer { get { return _focusLayers.Count > 0; } }

        public int UnreleasedMovieCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _movies.Count; i++)
                {
                    if (!_movies[i].IsReleased) count++;
                }
                return count;
            }
        }

        /// <summary>四项持有是否全部释放，且加载过的 movie 全部释放。</summary>
        public bool IsClean
        {
            get
            {
                return !HasLiveLayers && !AnyInputRestricted && !AnyFocusLayer && UnreleasedMovieCount == 0;
            }
        }

        public string LeakReport()
        {
            return "liveLayers=" + _liveLayers.Count.ToString()
                + " unreleasedMovies=" + UnreleasedMovieCount.ToString()
                + " inputRestricted=" + _inputRestricted.Count.ToString()
                + " focusLayers=" + _focusLayers.Count.ToString();
        }

        /// <summary>释放事件的实际发生次序。</summary>
        public IReadOnlyList<string> ReleaseSequence()
        {
            List<string> sequence = new List<string>();
            for (int i = 0; i < _events.Count; i++)
            {
                string e = _events[i];
                if (e == "focus:lost" || e == "input:reset" || e == "movie:released" || e == "layer:removed")
                {
                    sequence.Add(e);
                }
            }
            return sequence;
        }

        public IUiLabLayer CreateLayer(string layerName, int localOrder)
        {
            LayersCreated++;
            return new FakeUiLabLayer(this, layerName, localOrder);
        }

        public void TrySetFocus(IUiLabLayer layer)
        {
            if (FocusFailures > 0)
            {
                FocusFailures--;
                throw new InvalidOperationException("injected_focus_failure");
            }
            FakeUiLabLayer fake = (FakeUiLabLayer)layer;
            _focusLayers.Add(fake);
            FocusSetCount++;
            _events.Add("focus:set");
        }

        public void TryLoseFocus(IUiLabLayer layer)
        {
            ThrowIfCleanupFault("focus");
            FakeUiLabLayer fake = (FakeUiLabLayer)layer;
            _focusLayers.Remove(fake);
            FocusLostCount++;
            _events.Add("focus:lost");
        }

        internal void ThrowIfCleanupFault(string step)
        {
            int remaining;
            if (!_cleanupFailures.TryGetValue(step, out remaining)) return;
            if (remaining <= 0) return;
            _cleanupFailures[step] = remaining - 1;
            throw new InvalidOperationException("injected_cleanup_failure:" + step);
        }

        internal void ThrowIfInputFailure()
        {
            if (InputRestrictionFailures <= 0) return;
            InputRestrictionFailures--;
            throw new InvalidOperationException("injected_input_failure");
        }

        internal IUiLabMovie LoadMovie(FakeUiLabLayer layer, string movieId)
        {
            if (ThrowOnMovieLoad)
            {
                throw new InvalidOperationException("injected_movie_load_throw");
            }
            MoviesLoaded++;
            _events.Add("movie:load_attempt");
            FakeUiLabMovie movie = new FakeUiLabMovie(this, movieId, !FailMovieLoad);
            _movies.Add(movie);
            if (movie.IsLoaded)
            {
                _events.Add("movie:loaded");
            }
            return movie;
        }

        internal void OnMovieReleased(FakeUiLabMovie movie)
        {
            MoviesReleased++;
            _events.Add("movie:released");
        }

        internal void OnLayerAdded(FakeUiLabLayer layer, IUiLabScreen screen)
        {
            layer.IsAdded = true;
            _liveLayers.Add(layer);
            LayersAdded++;
            _events.Add("layer:added");
        }

        internal void OnLayerRemoved(FakeUiLabLayer layer, IUiLabScreen screen)
        {
            layer.IsAdded = false;
            _liveLayers.Remove(layer);
            LayersRemoved++;
            _events.Add("layer:removed");
        }

        internal void OnSetInputRestrictions(FakeUiLabLayer layer, bool restricted)
        {
            layer.IsInputRestricted = restricted;
            if (restricted)
            {
                _inputRestricted.Add(layer);
                InputSetCount++;
                _events.Add("input:set");
            }
            else
            {
                _inputRestricted.Remove(layer);
                InputResetCount++;
                _events.Add("input:reset");
            }
        }

        internal void OnSetFocusLayer(FakeUiLabLayer layer, bool value)
        {
            layer.IsFocusLayer = value;
            if (value)
            {
                _focusLayers.Add(layer);
                _events.Add("focus:layer-on");
            }
            else
            {
                _focusLayers.Remove(layer);
                _events.Add("focus:layer-off");
            }
        }
    }
}
