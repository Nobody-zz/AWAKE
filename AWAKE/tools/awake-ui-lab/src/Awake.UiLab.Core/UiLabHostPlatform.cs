using System;

namespace Awake.UiLab
{
    /// <summary>顶层界面的最小身份。用于检测"开了界面之后玩家切了界面"。</summary>
    public interface IUiLabScreen
    {
        string ScreenId { get; }
    }

    /// <summary>movie 句柄。真机实现包住 GauntletMovieIdentifier，本地实现只记账。</summary>
    public interface IUiLabMovie
    {
        string MovieId { get; }
        bool IsLoaded { get; }
        bool IsReleased { get; }
        void Release();
    }

    /// <summary>
    /// layer 的最小能力面。真机实现包住 GauntletLayer；
    /// 本地夹具实现只记录创建/释放计数与故障注入。
    /// </summary>
    public interface IUiLabLayer
    {
        string LayerName { get; }
        int LocalOrder { get; }

        bool IsAdded { get; }
        bool IsInputRestricted { get; }
        bool IsFocusLayer { get; }

        IUiLabMovie LoadMovie(string movieId);
        void AddTo(IUiLabScreen screen);
        void RemoveFrom(IUiLabScreen screen);
        void SetInputRestrictions(bool restricted);
        void ResetInputRestrictions();
        void SetFocusLayer(bool value);
    }

    /// <summary>
    /// 宿主平台。这是本地端与游戏内壳共用的唯一对接面：
    /// 游戏内实现把 ScreenManager / GauntletLayer 包进来，本地实现给假数据。
    /// </summary>
    public interface IUiLabPlatform
    {
        /// <summary>当前顶层界面；不可用时返回 null。</summary>
        IUiLabScreen TopScreen { get; }

        /// <summary>构造一个 layer（尚未加入 screen）。</summary>
        IUiLabLayer CreateLayer(string layerName, int localOrder);

        void TrySetFocus(IUiLabLayer layer);
        void TryLoseFocus(IUiLabLayer layer);
    }
}
