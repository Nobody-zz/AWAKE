using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Awake;

/// <summary>
/// 语义召回这一路（**可空**）。实现在别处（`AwakeWorldKnowledgeSemanticIndex`），这里只定形状，
/// 好让 `WorldKnowledgeQueryService` 不依赖宿主、离线验台也能绑一个真件进来。
///
/// 契约（窄到不能再窄）：
///   · 入参是**玩家原话**，不做任何改写（查询侧不加指令，见 docs/FEED-20260917 §2.6）；
///   · 出参是**按相关度降序的条目 id**，与包里的 `WorldKnowledgeEntry.Id` 同形；
///   · **失败一律返回空表**，不抛 —— 语义路不许把整条检索链拖死。
/// </summary>
internal interface IWorldKnowledgeSemanticIndex
{
    IReadOnlyList<string> Search(string playerText, int limit);
}

/// <summary>
/// 词条 → 喂给嵌入模型的那段文本。这就是**拼法 D**，逐字段对齐
/// `tools/_feed_sweep_20260917.py` 的 `doc_text("D", e)`：
///
///     join(title, "、".join(含汉字的 keywords), summary, 第一条 expression.text)
///
/// 三个细节都是有意的，别顺手"改好"：
///   ① **只喂中文** —— `keywords` 里那些英文串（`Fur` / `Husn Fulq` 这类）**要滤掉**。
///      实测：带上英文串每一档都掉分（B 组 hit3 掉 1–2 条）。英文要有索引是另一条线的事。
///   ② **不喂整段正文** —— 只取**第一条** `expression.text`。全部塞进去会撞 512 token 上限被截断，
///      截断后的向量是残缺的（拼法 E 实测：中位涨到 167 token、最大撞满 512）。
///   ③ **不做小写化/去标点** —— 分词器的开关在服务侧按 `tokenizer_config.json` 逐个抄过了，
///      这里再动一次手就等于两处各改一半。
///
/// ⚠️ **改这里等于改索引**：`Revision` 必须跟着动，否则新旧向量混在同一个库里，
/// 排序会静默变差而没有任何报错（`WorldKnowledgePassage.Revision` → 服务侧的 ModelId）。
/// </summary>
internal static class WorldKnowledgePassage
{
    /// <summary>
    /// 拼装规则的版本号。**改了 `Compose` 就必须改这里**，否则服务侧不知道要重算。
    /// 与 `RuntimeServiceHost.EmbeddingPassageRevision` 同值 —— 一处动、两处都动。
    /// </summary>
    internal const string Revision = "feed-D-v1";

    private const string TitleJoiner = "、";
    private const string PartJoiner = "。";

    internal static string Compose(WorldKnowledgeEntry entry)
    {
        if (entry == null) return string.Empty;
        string keywords = string.Join(TitleJoiner, entry.Keywords.Where(k => !string.IsNullOrWhiteSpace(k) && ContainsHan(k)));
        string firstExpression = FirstExpressionText(entry);
        return JoinParts(entry.Title, keywords, entry.Summary, firstExpression);
    }

    /// <summary>第一条 `expression.text`，**按 JSON 里的原序取**（不看 `enabled`）——
    /// 与 Python 侧 `expressions[0]` 一致；装载器也保留原序（`WorldKnowledgeLoader` 逐条 Add）。</summary>
    private static string FirstExpressionText(WorldKnowledgeEntry entry)
    {
        if (entry.Expressions.Count == 0) return string.Empty;
        return entry.Expressions[0].Text ?? string.Empty;
    }

    /// <summary>与 Python `join()` 等价：空段直接丢掉，剩下的用 `。` 连。</summary>
    private static string JoinParts(params string[] parts)
    {
        var builder = new StringBuilder();
        foreach (string part in parts)
        {
            if (string.IsNullOrEmpty(part)) continue;
            if (builder.Length > 0) builder.Append(PartJoiner);
            builder.Append(part);
        }
        return builder.ToString();
    }

    /// <summary>汉字判定，与 Python `has_cjk()` 一致：只看 U+4E00–U+9FFF 这一段。</summary>
    internal static bool ContainsHan(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        for (int index = 0; index < value.Length; index++)
        {
            char ch = value[index];
            if (ch >= '\u4e00' && ch <= '\u9fff') return true;
        }
        return false;
    }
}

/// <summary>
/// 两条通道的**名次融合**（RRF，k=60）。
///
/// 为什么不是"字面排前面，语义接后面"、也不是"语义排前面"：这两种都在 26 条题集上量过
/// （`tools/worldbook-rag-merge` 用真代码跑，逐条明细见 `docs/DECISION-20260917-两条通道怎么合.md`）。
///   · 字面优先   ⇒ 17/26
///   · 语义优先   ⇒ 20/26（涨 6、**跌 2**：`什么人拿大圆盾扔飞斧？`／`游牧的人怎么用骑马射手打仗？`）
///   · 对称 RRF   ⇒ **19/26（涨 3、跌 0）** ← 选它
/// 加权 RRF 从 k=1 扫到 k=100、字面权重 0.25–1.0，**没有一档超过 19**；想拿到 20 就必须在两条臂
/// 分歧时把首位判给语义，代价是丢掉 2 条现在字面能命中的问法。本项目对"回退"的纪律是硬的
/// ⇒ 取零回退的那一档。
/// ⚠️ 平手（`ThenBy`）的方向**不是**装饰：单独试过"平手判给语义"，实测 **18/26**，比字面优先还差
/// —— 别以为换个平手方向就等于"语义优先"，它是第三种结果。
///
/// 融合只用到**名次**，不碰相似度分数 —— 字面臂那套 `MatchQuality` 的名次与余弦值本来就不在一个量纲上，
/// 折到同一个分数量纲是没有依据的。
/// </summary>
internal static class WorldKnowledgeRankFusion
{
    /// <summary>RRF 的 k。标准取值；本量级下 1–100 结果完全一样（已扫过），取标准值免得后人猜。</summary>
    internal const int RankConstant = 60;

    internal static List<string> Merge(IReadOnlyList<string> literalIds, IReadOnlyList<string> semanticIds, int limit)
    {
        var scores = new Dictionary<string, double>(StringComparer.Ordinal);
        var literalRank = new Dictionary<string, int>(StringComparer.Ordinal);
        var semanticRank = new Dictionary<string, int>(StringComparer.Ordinal);
        Accumulate(literalIds, literalRank, scores);
        Accumulate(semanticIds, semanticRank, scores);

        return scores.Keys
            .OrderByDescending(id => scores[id])
            // 平手：先看字面名次，再看语义名次，最后按 id 稳定排序。
            .ThenBy(id => literalRank.TryGetValue(id, out int left) ? left : int.MaxValue)
            .ThenBy(id => semanticRank.TryGetValue(id, out int right) ? right : int.MaxValue)
            .ThenBy(id => id, StringComparer.Ordinal)
            .Take(limit <= 0 ? 0 : limit)
            .ToList();
    }

    private static void Accumulate(IReadOnlyList<string> ids, Dictionary<string, int> ranks, Dictionary<string, double> scores)
    {
        if (ids == null) return;
        for (int index = 0; index < ids.Count; index++)
        {
            string id = ids[index];
            if (string.IsNullOrWhiteSpace(id) || ranks.ContainsKey(id)) continue;
            ranks[id] = index + 1;
            scores[id] = (scores.TryGetValue(id, out double current) ? current : 0d) + 1d / (RankConstant + index + 1);
        }
    }
}
