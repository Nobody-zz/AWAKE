using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Awake;

internal sealed class WorldKnowledgeQueryService : IWorldKnowledgeQuery
{
    private sealed class EntrySelection
    {
        internal WorldKnowledgeExpression Expression { get; set; }
        internal bool PermissionLimited { get; set; }
        internal List<string> ReferralIds { get; } = new List<string>();
    }

    private readonly WorldKnowledgeSnapshot _snapshot;
    private readonly WorldKnowledgeOverlayState _overlay = new WorldKnowledgeOverlayState();
    private readonly object _gate = new object();
    private readonly HashSet<string> _dynamicEntryIds = new HashSet<string>(StringComparer.Ordinal);
    private int _dynamicRevision;

    /// <summary>
    /// 最近一次 Overlay **导入**里没落地的那些操作（2026-09-22）。
    /// 导入的口径是「逐条尽力、允许部分成功」，所以必须有这一栏 —— 否则就是**静默的半成功**，
    /// 而项目口径是「静默成功比抛错危险」。`TryApplyOverlay`（玩家单条编辑）不写这里：
    /// 它一次只有一条，返回值本身就是全部信息。
    /// </summary>
    private readonly List<string> _overlayImportFailures = new List<string>();

    // 语义召回这一路。**可空**且**可后挂**：没挂上时整个类与从前一字不差（融合退化回原序，
    // 见 `WorldKnowledgeRankFusion`）。挂载点见 `AwakeWorldKnowledgeSemanticIndex`。
    private IWorldKnowledgeSemanticIndex _semantic;

    /// <summary>语义臂一次最多收几条。为什么是 3：融合只用名次，进池的条数直接决定
    /// 「笼统话炸出一片」的风险；实测 26 条里语义臂能排到第 1 的，目标也都在它的前 3 里。</summary>
    private const int SemanticCandidateLimit = 3;

    internal WorldKnowledgeQueryService(WorldKnowledgeSnapshot snapshot) { _snapshot = snapshot; }

    internal WorldKnowledgeQueryService(WorldKnowledgeSnapshot snapshot, IWorldKnowledgeSemanticIndex semantic) : this(snapshot)
    {
        _semantic = semantic;
    }

    /// <summary>挂上/摘掉语义通道（传 null 即摘掉）。世界书重载后要用新的语料重新挂。</summary>
    internal void AttachSemanticIndex(IWorldKnowledgeSemanticIndex semantic)
    {
        lock (_gate) _semantic = semantic;
    }

    internal bool HasSemanticIndex { get { lock (_gate) return _semantic != null; } }

    internal int EntryCount { get { lock (_gate) return _snapshot.Entries.Count; } }
    internal int IdentityCount { get { lock (_gate) return _snapshot.Identities.Count; } }
    internal int ReferralCount { get { lock (_gate) return _snapshot.Referrals.Count; } }
    internal int OverlayRevision { get { lock (_gate) return _overlay.Revision; } }
    internal int DynamicRevision { get { lock (_gate) return _dynamicRevision; } }
    internal IReadOnlyList<string> Warnings { get { lock (_gate) return _snapshot.Warnings.Concat(_overlayImportFailures).ToArray(); } }

    /// <summary>最近一次 Overlay 导入没落地的操作明细（空 = 全落地）。与 <see cref="Warnings"/> 同源。</summary>
    internal IReadOnlyList<string> OverlayImportFailures { get { lock (_gate) return _overlayImportFailures.ToArray(); } }

    public WorldKnowledgeQueryResult Query(WorldbookQuery query)
    {
        query = query ?? new WorldbookQuery();
        // 入口归一化（2026-09-17）：全角→半角等走**标准 Unicode NFKC**，不自己造表。
        // 红测：`Ｃｌｏｓｅｄ Ｗａｒｌｏｒｄ Ｈｅｌｍｅｔ` 两档都 not_found，而半角写法命中
        // ⇒ 差的不是检索能力，是这一层。**归一化之后必须再判一次空**：`？？？` 这类整串都是标点时
        // 归一化前非空、归一化后为空，不判就会一路走到「空查询」那条兜底上去（见 `FindCandidates`）。
        query.PlayerText = NormalizeQueryText(query.PlayerText);
        // 语义召回在锁**外**跑：它要走一次 IPC 加一次编码（几十毫秒），而 `_gate` 后面还站着
        // Overlay / 动态条目的写者。锁里只做「把两臂合起来」这件纯内存的事。
        IReadOnlyList<string> semanticIds = QuerySemanticIds(query.PlayerText);
        lock (_gate)
        {
            var result = new WorldKnowledgeQueryResult
            {
                ByteBudget = query.MaximumBytes > 0 ? query.MaximumBytes : 4096,
                SourceVersion = _snapshot.PackageId + "@" + _snapshot.Version + "+dynamic=" + _dynamicRevision
            };
            if (!ContentGateAllows(query, result)) return result;
            var candidates = FindCandidates(query.PlayerText, semanticIds, out string matchMode);
            // 第二趟：沿互引边扩一跳。**必须在 `FindCandidates` 之后、装填循环之前** ——
            // 它改的是候选表本身，扩进来的条目照常过身份闸（见 `ExpandByLinks`）。
            HashSet<string> expandedIds = ExpandByLinks(candidates);
            if (expandedIds.Count > 0) matchMode = matchMode + "+link";
            result.MatchMode = candidates.Count == 0 ? "identity" : matchMode;
            WorldKnowledgeIdentityEvaluation evaluation = WorldbookIdentityEvaluator.Evaluate(query, _snapshot);
            var builder = new StringBuilder();
            bool sawKnown = false;
            bool sawPartial = false;
            bool sawHardBlocked = false;
            bool sawPermissionLimited = false;
            foreach (WorldKnowledgeEntry entry in candidates)
            {
                if (HasMatchingDeny(entry, query, evaluation))
                {
                    sawHardBlocked = true;
                    continue;
                }
                EntrySelection selection = SelectExpression(entry, query, evaluation);
                if (selection.Expression == null)
                {
                    if (selection.PermissionLimited) sawPermissionLimited = true;
                    foreach (string referralId in selection.ReferralIds) AddReferral(result, referralId);
                    continue;
                }
                bool partial = WorldbookIdentityEvaluator.DetailRank(selection.Expression.Detail) < WorldbookIdentityEvaluator.DetailRank(query.RequestedDetail);
                string text = FormatEntry(entry, selection.Expression, !partial);
                if (Encoding.UTF8.GetByteCount(builder.ToString() + text + Environment.NewLine) > result.ByteBudget) break;
                if (builder.Length > 0) builder.AppendLine();
                builder.Append(text);
                result.HitIds.Add(entry.Id);
                if (expandedIds.Contains(entry.Id)) result.LinkIds.Add(entry.Id);
                if (!string.IsNullOrWhiteSpace(entry.SourceId) && !result.SourceIds.Contains(entry.SourceId, StringComparer.Ordinal)) result.SourceIds.Add(entry.SourceId);
                if (!string.IsNullOrWhiteSpace(entry.ReportId) && !result.ReportIds.Contains(entry.ReportId, StringComparer.Ordinal)) result.ReportIds.Add(entry.ReportId);
                sawKnown |= !partial;
                sawPartial |= partial;
            }
            if (builder.Length > 0)
            {
                result.State = sawKnown ? "known" : "partial";
            }
            else if (result.ReferralIds.Count > 0 && !sawHardBlocked)
            {
                result.State = "referral";
                builder.Append("这方面我不清楚。你可以去问：");
                builder.Append(string.Join("、", result.ReferralIds.Select(id => _snapshot.Referrals.TryGetValue(id, out WorldKnowledgeReferral referral) ? referral.DisplayName : id)));
            }
            else if (sawHardBlocked || sawPermissionLimited)
            {
                SetBlocked(result, "permission");
            }
            result.RetrievedText = builder.ToString().Trim();
            return result;
        }
    }

    public List<WorldKnowledgeEntry> Search(string text, int limit)
    {
        if (string.IsNullOrWhiteSpace(text) || limit <= 0) return new List<WorldKnowledgeEntry>();
        lock (_gate)
        {
            return _snapshot.Entries.Values.Where(x => x.Id.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 || x.Title.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 || x.Keywords.Any(k => k.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)).OrderBy(x => x.Id, StringComparer.Ordinal).Take(limit).ToList();
        }
    }

    public string BuildStatusText()
    {
        lock (_gate)
        {
            return "v2 package=" + _snapshot.PackageId + " entries=" + _snapshot.Entries.Count + " dynamic=" + _dynamicEntryIds.Count + " identities=" + _snapshot.Identities.Count + " referrals=" + _snapshot.Referrals.Count + " revision=" + _snapshot.Revision + " dynamic_revision=" + _dynamicRevision
                // 2026-09-22：导入件里没落地几条。**只在不为零时才出现**，免得常驻噪声；
                // 落点是这条状态串（游戏内「重载世界书」那个反馈直接显示它，见 AwakeDeveloperTestActions:153）。
                + (_overlayImportFailures.Count == 0 ? string.Empty : " overlay_unapplied=" + _overlayImportFailures.Count);
        }
    }

    internal void ReplaceDynamicEntries(IEnumerable<WorldKnowledgeEntry> entries)
    {
        lock (_gate)
        {
            foreach (string entryId in _dynamicEntryIds)
                _snapshot.Entries.Remove(entryId);
            _dynamicEntryIds.Clear();
            foreach (WorldKnowledgeEntry entry in entries ?? Enumerable.Empty<WorldKnowledgeEntry>())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || _snapshot.Entries.ContainsKey(entry.Id)) continue;
                _snapshot.Entries[entry.Id] = entry;
                _dynamicEntryIds.Add(entry.Id);
            }
            RebuildKeywordIndex();
            _dynamicRevision++;
        }
    }

    internal bool TryApplyOverlay(string kind, string targetId, string value, int baseRevision, string reason, out string error)
    {
        lock (_gate)
        {
            error = string.Empty;
            if (baseRevision != _overlay.Revision) { error = "WB2-OVERLAY-CAS"; return false; }
            return TryApplyOverlayCore(kind, targetId, value, new JObject { ["zh-CN"] = reason ?? "玩家编辑" }, out error);
        }
    }

    /// <summary>
    /// 真正改数据、记账、推进 revision 的那一步，**不含 CAS**（2026-09-22 拆出来）。
    ///
    /// 为什么要拆：导入要「逐条尽力」，而 CAS 是拿 `baseRevision` 跟 **live revision** 逐条比。
    /// 只要中间有一条没落地，live revision 就不再前进，后面每一条都会撞 CAS
    /// ⇒ 尾段连锁全灭，`continue` 形同虚设。所以 CAS 的归属是**批次边界**，
    /// 由调用方决定用 live revision 比（单条编辑）还是用导入游标比（整批导入）。
    /// </summary>
    /// <param name="reason">
    /// 这条操作**为什么发生**，原样记进导出件。导入时必须把操作自带的那个传进来，
    /// **不能盖成"导入战役 Overlay"** —— 否则每读一次存档，玩家所有编辑的缘由都被改写成同一句
    ///（2026-09-22 由「导出→导入→导出 逐字相同」这条阳性对照抓到）。
    /// </param>
    private bool TryApplyOverlayCore(string kind, string targetId, string value, JObject reason, out string error)
    {
        error = string.Empty;
        bool changed = false;
        // ⚠️ 「kind 合法」与「目标找得到」必须是**两个判断**。原实现把两者并进同一个 if，
        // 于是「kind 对、档不在了」顺势掉进最后的 else，报成 `WB2-OVERLAY-FORBIDDEN`
        // —— 把「引用没了」说成「这种操作不允许」，玩家按错码查不出真因（2026-09-22 修）。
        if (kind == "replace_title" || kind == "replace_summary" || kind == "add_keyword" || kind == "remove_keyword")
        {
            WorldKnowledgeEntry entry;
            if (!_snapshot.Entries.TryGetValue(targetId, out entry)) { error = "WB2-REFERENCE-MISSING"; return false; }
            if (kind == "replace_title") { entry.Title = value ?? string.Empty; changed = true; }
            else if (kind == "replace_summary") { entry.Summary = value ?? string.Empty; changed = true; }
            else if (kind == "add_keyword" && !entry.Keywords.Contains(value ?? string.Empty)) { entry.Keywords.Add(value ?? string.Empty); changed = true; }
            else if (kind == "remove_keyword") { changed = entry.Keywords.Remove(value ?? string.Empty); }
            if (changed) RebuildKeywordIndex();
        }
        else if (kind == "replace_text" || kind == "disable_expression" || kind == "enable_expression")
        {
            WorldKnowledgeExpression expression = FindExpression(targetId);
            if (expression == null) { error = "WB2-REFERENCE-MISSING"; return false; }
            if (kind == "replace_text") { expression.Text = value ?? string.Empty; changed = true; }
            else { expression.Enabled = kind == "enable_expression"; changed = true; }
        }
        else { error = "WB2-OVERLAY-FORBIDDEN"; return false; }
        if (!changed) { error = "WB2-REFERENCE-MISSING"; return false; }
        // 记的 baseRevision 是**落地那一刻的 live revision**，不是调用方传进来的那个数。
        // 两者在单条编辑下恒等；在导入下**故意不等** —— 中间有 op 没落地时，live revision
        // 落后于导入游标；若照抄导出件里那个声明值，重新导出就会得到一条带空洞的序列
        //（0,2,4…），下次导入必在第二个 op 撞 CAS。记 live 值才能保证导出序列始终连续、可再导入。
        int appliedAt = _overlay.Revision;
        _overlay.Revision++;
        _overlay.Operations.Add(new JObject
        {
            ["operationId"] = "awake:operation:" + _overlay.Revision,
            ["targetId"] = targetId,
            ["kind"] = kind,
            ["baseRevision"] = appliedAt,
            ["value"] = value,
            ["reason"] = reason ?? new JObject { ["zh-CN"] = "玩家编辑" }
        });
        return true;
    }

    internal JObject ExportOverlay()
    {
        lock (_gate)
        {
            return new JObject
            {
                ["schemaVersion"] = "awake.worldbook.overlay.v1",
                ["overlayId"] = "awake:overlay:campaign",
                ["baseActivationId"] = "awake:activation:current",
                ["revision"] = _overlay.Revision,
                ["operations"] = _overlay.Operations.DeepClone()
            };
        }
    }

    /// <summary>
    /// 导入战役 Overlay。口径（2026-09-22 定）：**逐条尽力、允许部分成功，不做全批原子事务**
    /// —— 玩家覆盖本来就是以「单条」为粒度产生的，且存储层没有事务能力，硬做原子只能靠上一层补，
    /// 成本远大于收益。
    ///
    /// 但**必须补上另一半**：哪一条没进去，要让玩家看得见（见 <see cref="OverlayImportFailures"/>
    /// 与 <see cref="Warnings"/>，那条数还会进 <see cref="BuildStatusText"/>）。
    /// 「静默的部分成功」比抛错危险。
    ///
    /// <paramref name="error"/> 保持旧语义：**第一条**失败的原因码（调用方按它记日志）；
    /// 完整的失败清单走 <see cref="OverlayImportFailures"/>。
    /// </summary>
    internal bool TryImportOverlay(JObject overlay, out string error)
    {
        lock (_gate)
        {
            error = string.Empty;
            _overlayImportFailures.Clear();
            if (overlay == null) { error = "WB2-OVERLAY-IMPORT"; return false; }
            int targetRevision = overlay["revision"]?.Value<int>() ?? 0;
            if (targetRevision <= _overlay.Revision) return true;
            JArray operations = overlay["operations"] as JArray;
            if (operations == null) { error = "WB2-OVERLAY-IMPORT"; return false; }
            // 导入游标：从 live revision 起，**每处理一条就 +1（成功失败都加）**。
            // 为什么失败的也要加：导出序列是连续的 0,1,2…，中间一条没落地只是「这一格空了」，
            // 不是「后面的格子都作废」。不加就会让后面每条都撞 CAS —— 那等于没改，
            // 只是把「停在第 2 条」换成「第 2 条以后全报失败」。
            int cursor = _overlay.Revision;
            int index = 0;
            foreach (JObject operation in operations)
            {
                index++;
                string kind = operation["kind"]?.Value<string>() ?? string.Empty;
                string targetId = operation["targetId"]?.Value<string>() ?? string.Empty;
                string value = operation["value"]?.Value<string>() ?? string.Empty;
                int baseRevision = operation["baseRevision"]?.Value<int>() ?? -1;
                string operationError = string.Empty;
                // 缘由**原样带回**（导出件里每条都记着"为什么改"）。不传就等于每读一次存档把它盖成同一句。
                JObject operationReason = (operation["reason"] as JObject)?.DeepClone() as JObject;
                bool applied = baseRevision == cursor
                    && TryApplyOverlayCore(kind, targetId, value,
                        operationReason ?? new JObject { ["zh-CN"] = "导入战役 Overlay" }, out operationError);
                if (!applied && string.IsNullOrEmpty(operationError)) operationError = "WB2-OVERLAY-CAS";
                cursor++;
                if (applied) continue;
                if (string.IsNullOrEmpty(error)) error = operationError;
                string failure = "WB2-OVERLAY-PARTIAL index=" + index
                    + " targetId=" + targetId
                    + " kind=" + kind
                    + " baseRevision=" + baseRevision
                    + " cause=" + operationError;
                _overlayImportFailures.Add(failure);
                // 门牌日志：与信件半提交（`AwakeLetterCommit.cs:127`，同日 P1-05）同一套做法 ——
                // 带齐「哪一条、改谁、为什么」再落日志。**没有它这一步就是静默的**：
                // 玩家以为编辑都恢复了，其实少了几条，谁也不知道少了哪几条。
                AwakeLog.Write("worldbook_overlay_import_partial " + failure);
            }
            if (_overlayImportFailures.Count > 0) return false;
            return _overlay.Revision == targetRevision;
        }
    }

    /// <summary>
    /// 查询侧归一化（2026-09-17）。**只做"同一个字的另一种写法"，不做"另一个字"**：
    /// 错别字/同音要编辑距离或拼音通道（那是另一条线），这里不碰。
    ///
    /// ① `FormKC` ＝ 兼容分解再合成：全角字母/数字/空格→半角、连字与罗马数字归一。**标准库，零表**。
    /// ② 去零宽与不可见字符：复制粘贴常带 `U+200B~200D` / `U+FEFF`，肉眼看不见但会让子串匹配失败。
    /// ③ 两端去空白。**中间的空白与标点不动** —— 动了就是另一套规则（09-16 量过它会再造出新的空串）。
    /// ④ 繁→简：走系统 `LCMapStringEx`，**不造表**。
    /// 为什么不用表：仓里没有可信的对照表来源；09-16 那版 `_redtest_claim2_20260916.py` 的 `TRAD`
    /// 只有 12 个字、是照着样本选的，拿它进产品就是对样本过拟合（纪律：手工造表＝循环论证）。
    /// 系统 API 是同一份对照的权威来源，且本作只跑 Windows。
    /// ⚠️ 调用失败就**原样返回**，不让它成为新的失败点（见 `TryFoldToSimplified`）。
    /// 索引侧不做同样处理：包里 keywords 本来都是半角简体；真要出现别的写法，那是数据问题。
    /// </summary>
    internal static string NormalizeQueryText(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        string normalized;
        try { normalized = text.Normalize(NormalizationForm.FormKC); }
        catch (ArgumentException) { normalized = text; }
        var builder = new StringBuilder(normalized.Length);
        foreach (char ch in normalized)
        {
            if (ch == '\u200B' || ch == '\u200C' || ch == '\u200D' || ch == '\uFEFF') continue;
            builder.Append(ch);
        }
        string compact = builder.ToString().Trim();
        return TryFoldToSimplified(compact, out string folded) ? folded : compact;
    }

    // LCMAP_SIMPLIFIED_CHINESE：把繁体映射成简体（已是简体的字符原样返回）。
    // 只在**含非 ASCII 字符**时才调 —— 纯西文不可能有繁简之分，省掉这次系统调用。
    private const uint LCMAP_SIMPLIFIED_CHINESE = 0x02000000;
    private const string Kernel32 = "kernel32.dll";

    [DllImport(Kernel32, CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int LCMapStringEx(string localeName, uint dwMapFlags, string lpSrcStr, int cchSrc,
                                            StringBuilder lpDestStr, int cchDest, IntPtr lpVersionInformation,
                                            IntPtr lpReserved, IntPtr sortHandle);

    private static bool TryFoldToSimplified(string text, out string folded)
    {
        folded = text;
        if (string.IsNullOrEmpty(text)) return false;
        bool hasNonAscii = false;
        foreach (char ch in text) { if (ch > 0x7F) { hasNonAscii = true; break; } }
        if (!hasNonAscii) return false;
        try
        {
            var buffer = new StringBuilder(text.Length + 8);
            int written = LCMapStringEx(null, LCMAP_SIMPLIFIED_CHINESE, text, text.Length,
                                        buffer, buffer.Capacity, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (written <= 0) return false;
            folded = buffer.ToString(0, written);
            return true;
        }
        catch (Exception)
        {
            // 拿不到系统映射就退回原文。**降级不是失败**：最多是繁体问法照旧查不到，
            // 不能让一个可选的语言折叠把整条检索带崩。
            return false;
        }
    }

    // 语义腿的最短查询长度（2026-09-17）。红测：单字「货」在字面侧 `not_found`（对），
    // 挂上语义腿后命中 `items-mule`（骡子）—— 一个字的向量近乎均匀，取回的"最近邻"其实是任意的。
    // ⇒ 短到不构成一次查询的输入，不给语义腿。**这是代价换代价**：单字查询确实也救不回来，
    //    见 `docs/worldbook-migration/REDTEST-CHAIN-20260917.md` §5.4。
    private const int MinSemanticQueryLength = 2;

    /// <summary>
    /// 走一次语义召回。**任何失败都当"这一路没说话"**（返回空表），绝不向外抛 ——
    /// 语义是加分项，字面链才是底线；模型缺失、服务没起、超时，都不该让玩家问不成话。
    /// </summary>
    private IReadOnlyList<string> QuerySemanticIds(string playerText)
    {
        IWorldKnowledgeSemanticIndex index = _semantic;
        if (index == null) return Array.Empty<string>();
        string text = playerText ?? string.Empty;
        if (text.Trim().Length < MinSemanticQueryLength) return Array.Empty<string>();
        try
        {
            return index.Search(text, SemanticCandidateLimit) ?? (IReadOnlyList<string>)Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private List<WorldKnowledgeEntry> FindCandidates(string text, IReadOnlyList<string> semanticIds, out string matchMode)
    {
        List<WorldKnowledgeEntry> literal = FindLiteralCandidates(text);
        // 空查询（归一化之后仍为空）走的是"整库按 id 兜底"那条路（见 `FindLiteralCandidates`）。
        // 那不是检索命中，必须自己占一个 `matchMode`：红测发现它以前报 `keyword`（挂语义腿时报 `hybrid`），
        // 读起来都像"找到了" ⇒ 上游与日志**分不清**「检索命中」和「兜底给了整库」，
        // 上游一旦因 bug 传进空串，就会**静默**拿到一批看着像答案的条目。
        if (string.IsNullOrWhiteSpace(text)) { matchMode = "blank"; return literal; }
        matchMode = "keyword";
        if (semanticIds == null || semanticIds.Count == 0) return literal;

        // 两臂**合并**，而不是「字面空手才问语义」。这一条是实测改过来的：语义单独救回来的 6 条里，
        // 5 条的字面侧并非空手，而是「返回了 5 条、全错」（见 docs/FEED-20260917 §2.4）。
        // ⇒ 触发线必须是无条件的，合并规则见 `WorldKnowledgeRankFusion`（对称 RRF，k=60）。
        matchMode = literal.Count == 0 ? "semantic" : "hybrid";
        var literalIds = literal.Select(x => x.Id).ToList();
        List<string> fused = WorldKnowledgeRankFusion.Merge(literalIds, semanticIds, literalIds.Count + semanticIds.Count);
        var candidates = new List<WorldKnowledgeEntry>(fused.Count);
        foreach (string id in fused)
        {
            if (_snapshot.Entries.TryGetValue(id, out WorldKnowledgeEntry entry)) candidates.Add(entry);
        }
        return candidates;
    }

    // ── 互引边扩召回（2026-09-20）────────────────────────────────────────────
    //
    // 边从哪来：编译期把「A 条正文点名了 B 条的名字」这条判据的产物写进每个条目的
    //   `extensions.links`（判据与生成器见 `tools/_link_registry_20260920.py`，
    //   可行性核查见 `docs/worldbook-migration/EDGE-TO-RECALL-FEASIBILITY-20260920.md`）。
    //
    // ★ 为什么是「追加在候选表末尾」，而不是与字面/语义两腿一起过 RRF：
    //   边的判据保的是**提到**，不是**该一起答** —— 拿它和直接命中平起平坐，问「村子」就会被
    //   沿边扩出一大片。追加在末尾＝位次最低；`Query()` 按顺序装填且受 `ByteBudget` 截断
    //   ⇒ 预算够才捎带出来，不够就自然被截掉。这就是「低权重提示」在这套代码里的现成说法：
    //   **不新造融合权重，用既有的排序与预算。**
    //
    // ★ 两个旋钮（保守起点，按需调）：
    //   `LinkExpandPerSeed`  —— 每个种子最多带几条；
    //   `LinkExpandMaxTotal` —— 一次查询总共最多带几条。
    //
    // ⚠️ 扩进来的条目**照常过身份闸**（`HasMatchingDeny` ＋ `SelectExpression`）。
    //    这一层只决定「谁进候选表」，不决定「谁知道」—— 别在这里做任何权限判断。
    //
    // ⚠️ 只沿**出边**走。双向边的两个方向在边表里各有一条独立记录（判据对每档各跑一遍），
    //    所以不需要在这里反着查；`usableAs` 因此仅作标注、不作分支。
    //    边表缺失（旧包 / 别的工作区）⇒ `Links` 为空 ⇒ 本方法返回空集，一切照旧。
    private const int LinkExpandPerSeed = 2;
    private const int LinkExpandMaxTotal = 3;

    private HashSet<string> ExpandByLinks(List<WorldKnowledgeEntry> candidates)
    {
        var expanded = new HashSet<string>(StringComparer.Ordinal);
        if (candidates.Count == 0) return expanded;        // 没有种子就没有可扩的东西
        var present = new HashSet<string>(candidates.Select(x => x.Id), StringComparer.Ordinal);
        // ToList()：下面会往 candidates 里追加，不能一边遍历一边加。
        foreach (WorldKnowledgeEntry seed in candidates.ToList())
        {
            if (expanded.Count >= LinkExpandMaxTotal) break;
            int taken = 0;
            foreach (WorldKnowledgeLink link in seed.Links)    // 编译器已排序：强→弱、专名→枢纽
            {
                if (taken >= LinkExpandPerSeed || expanded.Count >= LinkExpandMaxTotal) break;
                if (!present.Add(link.To)) continue;           // 已在候选里（含刚扩进来的）⇒ 不占配额
                if (!_snapshot.Entries.TryGetValue(link.To, out WorldKnowledgeEntry target)) continue;
                candidates.Add(target);
                expanded.Add(target.Id);
                taken++;
            }
        }
        return expanded;
    }

    private List<WorldKnowledgeEntry> FindLiteralCandidates(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return _snapshot.Entries.Values.OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pair in _snapshot.KeywordIndex)
        {
            if (text.IndexOf(pair.Key, StringComparison.OrdinalIgnoreCase) < 0 && pair.Key.IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0) continue;
            foreach (string id in pair.Value) ids.Add(id);
        }
        if (ids.Count == 0) return FindFallbackCandidates(text);

        List<WorldKnowledgeEntry> keywordLayer = ids
                  .Select(id => _snapshot.Entries.TryGetValue(id, out WorldKnowledgeEntry entry) ? entry : null)
                  .Where(x => x != null)
                  .Select(x => (Entry: x, Quality: MatchQuality(x, text)))
                  .OrderBy(x => x.Quality.Rank)
                  .ThenByDescending(x => x.Quality.Length)
                  .ThenBy(x => x.Entry.Id, StringComparer.Ordinal)
                  .Select(x => x.Entry)
                  .ToList();

        // 兜底通道**并列补齐**，而不是被关键词层关在门外（2026-09-18 修）。
        //
        // 旧写法是 `if (ids.Count == 0) return FindFallbackCandidates(text);` —— 只要句子里
        // 出现任意一个被索引的词，兜底通道就整个不跑。后果不是"少几条"，是**候选集被压成一条**：
        //   · `什么人拿大圆盾扔飞斧？`：加护甲形制卡之前 `hits=5`，正确条目 `war.troops-royal-guard`
        //     在 top3 内（RETRIEVAL_GATE B 组命中）；加卡之后 `hits=1`，只剩新卡的 `圆盾`
        //     —— 因为"大圆盾"成了那张卡的别名/关键词 ⇒ 关键词层命中 ⇒ 兜底通道被跳过。
        //   · 同一个形态在头盔批已出现一次：`哪种头盔护到腮帮子和耳朵？` 5→1，只剩 `护颊盔`。
        // 也就是说：**句子越长、越像人话，越容易因一个词碰巧撞上某条目而被独自接管**——
        // 这与"用 AI 模拟真实对话"的初衷正好相反。本方法的注释早就写着两条通道"完全并列"，
        // 代码却是"关键词层优先独占" ⇒ 这里把它改成字面意义上的并列。
        //
        // 排序不变：关键词层仍按 `MatchQuality` 的位次 0/1/2/3 排在前面，兜底候选只补在后面
        // （按 id 去重）。**既有位次与排序一个字不动**，只是不再丢候选。
        List<WorldKnowledgeEntry> layer = keywordLayer;
        var seen = new HashSet<string>(layer.Select(x => x.Id), StringComparer.Ordinal);
        foreach (WorldKnowledgeEntry entry in FindFallbackCandidates(text))
        {
            if (seen.Add(entry.Id)) layer.Add(entry);
        }
        return layer;
    }

    // 兜底通道（2026-09-16）：只读 `FallbackTermIndex`，与关键词路径**完全并列**。
    // 排序不套用 MatchQuality（那四级的语义是「整串」），而是按「共享 term 数 → 最长共享 term → id」。
    // 两个旋钮（都按数调，见 docs/PLAN-SUMMARY-INTO-INDEX-20260916.md 第 3 步）：
    //   `FallbackMinSharedTerms`  —— 至少共享几个 term 才算候选；
    //   `FallbackMaxCandidates`   —— 最多吐几条。为什么需要这条上限：`Query()` 的输出受 `ByteBudget`
    //      截断且**按本方法的排序顺序装**（`WorldKnowledgeQueryService.cs` 的循环），所以排第一的答案
    //      一定进得去；把尾巴砍掉只省 token、不丢答案。
    private const int FallbackMinSharedTerms = 1;
    private const int FallbackMaxCandidates = 5;

    // "看起来像**一个名字**而不是一句话"时，兜底通道要求**至少共享 2 个 term**（2026-09-17）。
    // 红测：`斯特基亚`（4 字一段）只与错误条目共享一个碎片（`特基` 或 `基亚`）⇒ 共享 1 就够的话，
    // 一个错别字会把一批「碰巧含这两个字」的条目全捞上来。收紧后它变成 `not_found`
    // —— 按设计初衷，「答不出」比「答错条目」好。
    //
    // ⚠️ 判据是**查询的形状**（单段且 ≤6 字），不是 term 个数。第一版按 term 个数（≥3 就要 2 个共享）
    // 会把整句问法一起打死：`RETRIEVAL_GATE` 的 B 组就是整句，实测 hit3 从 9/11 掉到 7/11。
    //    整句为什么该宽松：句子里的有效信号本来就稀疏，共享 1 个长词就是强证据。
    private const int NameLikeQueryMaxLength = 6;
    private const int NameLikeMinSharedTerms = 2;

    private List<WorldKnowledgeEntry> FindFallbackCandidates(string text)
    {
        HashSet<string> queryTerms = WorldbookTermIndex.TermSet(text);
        if (queryTerms.Count == 0) return new List<WorldKnowledgeEntry>();
        int requiredShared = WorldbookTermIndex.LooksLikeSingleShortToken(text, NameLikeQueryMaxLength)
            ? NameLikeMinSharedTerms : FallbackMinSharedTerms;
        var shared = new Dictionary<string, int>(StringComparer.Ordinal);
        var longest = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string term in queryTerms)
        {
            if (!_snapshot.FallbackTermIndex.TryGetValue(term, out List<string> ids)) continue;
            foreach (string id in ids)
            {
                shared[id] = shared.TryGetValue(id, out int count) ? count + 1 : 1;
                longest[id] = Math.Max(longest.TryGetValue(id, out int len) ? len : 0, term.Length);
            }
        }
        return shared.Where(x => x.Value >= requiredShared)
                     .OrderByDescending(x => x.Value)
                     .ThenByDescending(x => longest[x.Key])
                     .ThenBy(x => x.Key, StringComparer.Ordinal)
                     .Take(FallbackMaxCandidates)
                     .Select(x => _snapshot.Entries.TryGetValue(x.Key, out WorldKnowledgeEntry entry) ? entry : null)
                     .Where(x => x != null)
                     .ToList();
    }

    // 命中相关度：0=标题即所问，1=别名/关键词即所问，2=标题互为子串，3=仅关键词子串。
    // 同级比「匹配串长度」降序（越长越具体），再按 Id 稳定排序。
    // 起因：同名聚落在本作是常态（67 座城堡里 66 座有同名下属村），旧的 Id 字母序会让
    //       `geography.castle-*`（c）恒排在 `geography.village-*`（v）之前 —— 问村名却先答堡档。
    private static (int Rank, int Length) MatchQuality(WorldKnowledgeEntry entry, string text)
    {
        string title = entry.Title ?? string.Empty;
        if (title.Length > 0 && title.Equals(text, StringComparison.OrdinalIgnoreCase)) return (0, title.Length);
        int exactKeyword = entry.Keywords
            .Where(k => !string.IsNullOrEmpty(k) && k.Equals(text, StringComparison.OrdinalIgnoreCase))
            .Select(k => k.Length).DefaultIfEmpty(0).Max();
        if (exactKeyword > 0) return (1, exactKeyword);
        if (title.Length > 0 && (title.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf(title, StringComparison.OrdinalIgnoreCase) >= 0)) return (2, title.Length);
        int matchedKeyword = entry.Keywords
            .Where(k => !string.IsNullOrEmpty(k) && (text.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0
                || k.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0))
            .Select(k => k.Length).DefaultIfEmpty(0).Max();
        return (3, matchedKeyword);
    }

    private bool HasMatchingDeny(WorldKnowledgeEntry entry, WorldbookQuery query, WorldKnowledgeIdentityEvaluation evaluation)
    {
        foreach (WorldKnowledgeExpression expression in entry.Expressions)
        {
            if (!expression.Enabled) continue;
            if (expression.Denies.Any(rule => WorldbookIdentityEvaluator.MatchesIdentityAndConditions(rule, query, _snapshot, evaluation, out _))) return true;
        }
        return false;
    }

    private EntrySelection SelectExpression(WorldKnowledgeEntry entry, WorldbookQuery query, WorldKnowledgeIdentityEvaluation evaluation)
    {
        var selection = new EntrySelection();
        WorldKnowledgeExpression best = null;
        int bestScore = -1;
        int requestedDetail = WorldbookIdentityEvaluator.DetailRank(query.RequestedDetail);
        foreach (WorldKnowledgeExpression expression in entry.Expressions)
        {
            if (!expression.Enabled) continue;
            foreach (WorldKnowledgeRule grant in expression.Grants)
            {
                if (!WorldbookIdentityEvaluator.MatchesIdentityAndConditions(grant, query, _snapshot, evaluation, out int ruleScore)) continue;
                bool publicGrant = StringComparer.Ordinal.Equals(WorldbookIdentityEvaluator.NormalizeIdentity(grant.IdentityId), "awake:identity:public");
                if (!publicGrant && !evaluation.CapabilitiesAvailable)
                {
                    selection.PermissionLimited = true;
                    continue;
                }
                if (!WorldbookIdentityEvaluator.CapabilityMatches(grant, evaluation))
                {
                    selection.PermissionLimited = true;
                    if (evaluation.CapabilitiesAvailable) AddReferralIds(selection.ReferralIds, grant.ReferralIds);
                    continue;
                }
                int expressionDetail = WorldbookIdentityEvaluator.DetailRank(expression.Detail);
                if (expressionDetail < 0 || requestedDetail < 0 || expressionDetail > requestedDetail)
                {
                    selection.PermissionLimited = true;
                    continue;
                }
                int score = ruleScore * 10 + expressionDetail;
                if (score > bestScore)
                {
                    best = expression;
                    bestScore = score;
                }
            }
        }
        selection.Expression = best;
        return selection;
    }

    private void AddReferralIds(List<string> target, IEnumerable<string> ids)
    {
        foreach (string id in ids ?? Enumerable.Empty<string>())
            if (!string.IsNullOrWhiteSpace(id) && _snapshot.Referrals.TryGetValue(id, out WorldKnowledgeReferral referral) && referral.PubliclyAskable && !target.Contains(id, StringComparer.Ordinal)) target.Add(id);
    }

    private void AddReferral(WorldKnowledgeQueryResult result, string id)
    {
        if (!string.IsNullOrWhiteSpace(id) && _snapshot.Referrals.TryGetValue(id, out WorldKnowledgeReferral referral) && referral.PubliclyAskable && !result.ReferralIds.Contains(id, StringComparer.Ordinal)) result.ReferralIds.Add(id);
    }

    private static string FormatEntry(WorldKnowledgeEntry entry, WorldKnowledgeExpression expression, bool includeHeader)
    {
        if (!includeHeader) return expression.Text;
        string summary = string.IsNullOrWhiteSpace(entry.Summary) ? string.Empty : entry.Summary + Environment.NewLine;
        return "【" + entry.Title + "】" + Environment.NewLine + summary + expression.Text;
    }

    private bool ContentGateAllows(WorldbookQuery query, WorldKnowledgeQueryResult result)
    {
        string packageTier = WorldbookIdentityEvaluator.NormalizeCapability(_snapshot.ContentTier);
        string requestedTier = WorldbookIdentityEvaluator.NormalizeCapability(query.ContentTier);
        if (packageTier != "base" && packageTier != "adult_optional")
        {
            SetBlocked(result, "content_gate");
            return false;
        }
        if (packageTier == "adult_optional" && requestedTier != "adult_optional")
        {
            SetBlocked(result, "content_gate");
            return false;
        }
        return true;
    }

    private static void SetBlocked(WorldKnowledgeQueryResult result, string reason)
    {
        result.State = "blocked";
        result.BlockedReason = reason ?? "unknown";
        result.Errors.Add("WB2-QUERY-BLOCKED");
        result.RetrievedText = string.Empty;
        result.HitIds.Clear();
        result.ReferralIds.Clear();
    }

    private WorldKnowledgeExpression FindExpression(string id)
    {
        foreach (WorldKnowledgeEntry entry in _snapshot.Entries.Values)
        {
            WorldKnowledgeExpression expression = entry.Expressions.FirstOrDefault(x => StringComparer.Ordinal.Equals(x.Id, id));
            if (expression != null) return expression;
        }
        return null;
    }

    private void RebuildKeywordIndex()
    {
        // 与加载期同源（`WorldKnowledgeLoader.BuildKeywordIndex` 调的是同一个 build），
        // 别再在这儿写第二遍循环 —— 剔除规则一改就会两边不一致。
        WorldbookKeywordIndex.Build(_snapshot);
        WorldbookTermIndex.Build(_snapshot);
    }
}

