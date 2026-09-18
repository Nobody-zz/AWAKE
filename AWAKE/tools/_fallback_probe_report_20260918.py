# -*- coding: utf-8 -*-
"""2026-09-18 · 把兜底通道的实测读数写成一份记录（表由读数生成，不手抄）。"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))   # AWAKE/
RES = os.path.join(ROOT, "tools", "_fallback_probe_result_20260918.json")
OUT = os.path.join(ROOT, "docs", "AUDIT-20260918-兜底通道实测.md")

rows = json.load(open(RES, encoding="utf-8"))


def short(i):
    return i.replace("awake:entry:", "")


def block(kinds, title, note):
    sel = [r for r in rows if r["kind"] in kinds]
    L = ["### %s" % title, "", note, "",
         "| 行 | 问法 | 关键词层命中词 | 兜底条数 | 目标在兜底排 | 目标在字面联合排 |",
         "|---|---|---|---|---|---|"]
    for r in sel:
        kw = "、".join(r["keyword_terms_hit"]) or "—（空手）"
        L.append("| %s | %s | %s | %d | %s | %s |" % (
            r["name"], r["player_text"].replace("|", "\\|"), kw,
            len(r["fallback_ids"]),
            r["target_rank_in_fallback"] or "不在名单",
            r["target_rank_in_literal_union"] or "不在名单"))
    L.append("")
    return L


def dump(name, label):
    r = next(x for x in rows if x["name"] == name)
    L = ["**%s**：`%s` → 兜底 %d 条" % (label, r["player_text"], len(r["fallback_ids"])), ""]
    if not r["fallback_ids"]:
        L += ["（空手）", ""]
        return L
    L += ["| # | 条目 | 标题 | 凭什么（共享的词） |", "|---|---|---|---|"]
    for i, (tid, ti, sh) in enumerate(zip(r["fallback_ids"], r["fallback_titles"], r["fallback_shared_terms"]), 1):
        L.append("| %d | `%s` | %s | %s |" % (i, short(tid), ti, "、".join(sh)))
    L.append("")
    return L


g = [r for r in rows if r["kind"] == "gate"]
gk0 = [r for r in g if not r["keyword_terms_hit"]]
j = [r for r in rows if r["kind"] == "junk"]
f = [r for r in rows if r["kind"] == "pos_fallback"]


def stat(rs):
    return (len(rs),
            sum(1 for r in rs if r["target_rank_in_fallback"]),
            sum(1 for r in rs if 1 <= r["target_rank_in_fallback"] <= 3),
            sum(1 for r in rs if r["target_rank_in_fallback"] == 1))


ng, ig, tg, og = stat(g)
n0, i0, t0, o0 = stat(gk0)
nf, if_, tf, of_ = stat(f)

L = []
L += [
    "# 世界书检索 · 「字面兜底通道」单独实测（2026-09-18）",
    "",
    "> 问的是：「那条兜底通道到底捞回了什么？」。只量**字面侧**，不含身份门、不含语义臂。",
    "",
    "## 一、怎么测的（零替身）",
    "",
    "* 在验台 `tools/worldbook-runtime-sim` 加了一个只读子命令 `fallback-probe`：",
    "  **反射调产品那两个私有方法本身**（`FindLiteralCandidates` / `FindFallbackCandidates`），",
    "  **不重写匹配与排序**。与产品同源的三处：入参先过 `NormalizeQueryText`（产品 `Query()` 第 62 行就这么做）、",
    "  两张索引都是加载器建出来的真索引、切词器就是 `WorldbookTermIndex.TermSet`。",
    "* 规格 39 行 = 25 道门禁题（真人问法）＋ 8 句日常闲话（书里没有对应的东西）＋ 3 条关键词层对照",
    "  ＋ 3 条**兜底专用阳性对照**（从条目正文里抠一段它自己的话，与任何登记关键词都不互为子串 ⇒ 关键词层必然空手）。",
    "* 读数：`tools/_fallback_probe_result_20260918.json`，控制台全文 `tools/_fallback_probe_stdout_20260918.txt`。",
    "* 复现：`dotnet run -c Release --project tools/worldbook-runtime-sim -- fallback-probe <manifest> <spec> <out>`",
    "",
    "## 二、读数",
    "",
    "| 组 | n | 目标进兜底名单 | 目标进前 3 | 目标排第 1 |",
    "|---|---|---|---|---|",
    "| 门禁 25 题（全部） | %d | %d | %d | %d |" % (ng, ig, tg, og),
    "| ↑ 其中**关键词层完全空手**（kw=0） | %d | %d | %d | %d |" % (n0, i0, t0, o0),
    "| 兜底专用阳性对照 | %d | %d | %d | %d |" % (nf, if_, tf, of_),
    "",
    "闲话 8 句：兜底非空 **%d 句**（各满 5 条），空手 %d 句。" % (
        sum(1 for r in j if r["fallback_ids"]), sum(1 for r in j if not r["fallback_ids"])),
    "",
    "## 三、结论两条（一好一坏，都不能省）",
    "",
    "**① 它不是废腿 —— 关键词层瞎掉的题，是它在捞。**",
    "关键词层完全空手的 13 道题里，兜底把正确答案带进名单 %d 道、排第 1 %d 道。" % (i0, o0),
    "⇒ 这条腿是**长句／绕开专名的问法**唯一的字面来源；砍掉它，那 13 道直接归零。",
    "",
    "**② 但它没有「该不该开口」这个概念 —— 门槛是「共享 1 个两字片段」。**",
    "下面两句是同一个病：句子跟世界书毫无关系，它照样给满 5 条，而且看起来像答案。",
    "",
]
L += dump("N1", "阴性对照（世上根本没有这个词）")
L += [
    "> 「凭什么」那一列是**条目正文**里的词，标题上看不出来，所以顺手核了一遍：",
    "> 「阿米」来自**阿米尼斯河**——`阿耳波提斯坐落于高崖之上，俯瞰**阿米**尼斯河…`、",
    "> `得拉狄俄斯坐落于自高地奔流而下的**阿米**尼斯河的支流小阿米尼…`。",
    "> 也就是说，**一条河名的头两个字，把 5 座不相干的村子送进了候选**。",
    "",
]
L += dump("J5", "日常指代句（没说出是什么）")
L += [
    "⇒ `阿米巴原虫` 被切成 `阿米 / 米巴 / 巴原 / 原虫`，其中 `阿米` 恰好在 5 座村的正文里 ⇒ 满 5 条。",
    "`那个东西多少钱？` 靠 `东西`、`多少` 两条常用词凑满。**没有一个字跟它想问的事有关。**",
    "",
    "## 四、逐行明细",
    "",
]
L += block(["gate"], "门禁 25 题（真人问法）",
           "「目标在兜底排」= 该题正确答案在**兜底这一腿**的名单里排第几；「不在名单」＝这一腿没捞到它。")
L += block(["junk"], "日常闲话 8 句", "书里没有对应的东西，理想是「空手」。")
L += block(["pos", "pos_fallback", "neg"], "对照 6 条",
           "F* 是**兜底专用**阳性对照（关键词层必然空手）；P* 走的是关键词层，不是兜底的对照。")
L += [
    "## 五、我在这轮里踩的两个坑（记下来，别重犯）",
    "",
    "1. **我自己的探针先给了一列恒 0。** 第一版拿「去掉 `awake:entry:` 前缀的短串」去比 `entry.Id`",
    "   （它是带前缀的）⇒ 那一列**恒 0**，看着像「一条都没中」，其实是**没验过**。",
    "   —— 与仓里那条「恒 0 命中＝没验过」是同一件事，犯在了新写的代码上。",
    "2. **阳性对照设计错了一次。** 第一版的 P1/P2 是「拉迈萨」「皇家侍卫是什么兵？」——",
    "   实测 kw≥1，**走的是关键词层**，跟兜底这一腿无关。补了 F1/F2/F3（正文原话、kw=0）之后，",
    "   三条全排第 1 ⇒ 才能说「通道没坏，坏的是弃权」。",
    "",
    "## 六、该怎么办（不在这批做，留给判断层）",
    "",
    "* **不要靠调门槛治**：`FallbackMinSharedTerms` 从 1 抬到 2 会把真题一起砍（`_retrieval_cases` 里整句问法",
    "  的有效信号本来就稀疏，共享 1 个长词就是强证据 —— 这条 09-17 已实测，B 组 hit3 会从 9/11 掉到 7/11）。",
    "* 真正的病是**它没有「该不该答」的判断**，而那个判断按设计在**检索之前**（指代绑定／意图）。",
    "  见 `docs/DESIGN-20260917-检索之前的判断层.md`：本版只做「指代＋前文」，意图先离线量。",
    "  本文件给的就是「意图」那条的离线基线：**门禁 25 题里兜底非空 25 条；闲话 8 句里非空 4 句** —— 后者是它的误开口率。",
    "",
]

open(OUT, "w", encoding="utf-8").write("\n".join(L))
print("写出 -> %s  (%d 行)" % (OUT, len(L)))
