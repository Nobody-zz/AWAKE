using System;

namespace Awake.UiLab
{
    /// <summary>
    /// AWAKE 自有 UI Lab 宿主的唯一身份。
    /// 本地夹具与以后的游戏内 F10 壳必须共用这一份取值，不得各自另起名。
    /// </summary>
    public static class UiLabHostIdentity
    {
        /// <summary>Gauntlet movie 名。独占，禁止与任何生产 movie 重名。</summary>
        public const string MovieId = "AwakeUiLab";

        /// <summary>Gauntlet layer 名，与 movie 同名便于日志定位。</summary>
        public const string LayerName = "AwakeUiLab";

        /// <summary>进程内唯一 owner 标识。</summary>
        public const string OwnerId = "AwakeUiLabHost";

        /// <summary>版本化候选标识，参与 generation 栅栏。</summary>
        public const string CandidateId = "AwakeUiLabHost-runtime-v1";

        /// <summary>
        /// layer 排序值。待验证：E4 前需与生产对话入口 NpcDialogue(541)
        /// 及其它 AWAKE layer 的实际层序确认。
        /// </summary>
        public const int LocalOrder = 560;

        /// <summary>生产对话入口使用的 movie 名。Lab 一律不得借用。</summary>
        public const string ReservedProductionMovieId = "NpcDialogue";

        /// <summary>该 movie 名是否属于生产入口，被 Lab 保留禁用。</summary>
        public static bool IsReservedProductionMovie(string movieId)
        {
            if (string.IsNullOrEmpty(movieId)) return false;
            return string.Equals(movieId, ReservedProductionMovieId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>把身份渲染成一行可审计文本。</summary>
        public static string Describe()
        {
            return "movie=" + MovieId + " layer=" + LayerName + " owner=" + OwnerId
                + " candidate=" + CandidateId + " order=" + LocalOrder.ToString();
        }
    }
}
