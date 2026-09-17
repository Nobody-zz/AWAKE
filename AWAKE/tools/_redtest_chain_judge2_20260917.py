# -*- coding: utf-8 -*-
"""链路红测 · 三档判定（2026-09-17 补完版）

为什么要三档：甲方 `五条缺口补上`。要证"补上了"，必须是**同一批样本 × 改动前 / 改动后**。
改动前的逐条读数已入库（commit `66649c5`），由 `_redtest_chain_recover_prefix_20260917.py` 还原成同形 json。

四份输入：
    改动前 · 不挂语义腿   `_redtest_chain_prefix_off_20260917.json`
    改动前 · 挂语义腿     `_redtest_chain_prefix_on_20260917.json`
    改动后 · 不挂语义腿   `_redtest_chain_off_20260917.json`
    改动后 · 挂语义腿     `_redtest_chain_on_20260917.json`

判据与上一版**逐字相同**（MISS-RISK / 过匹配 / 权限黑名单），只多了「缺口验收」一节：
把五条缺口各自挂到具体 case 上，报改前→改后。
"""
import io
import json
import os
import re
import sys

EXPECT = "tools/_redtest_chain_expect_20260917.json"
TIERS = [
    ("preOff", "改动前·不挂语义腿", "tools/_redtest_chain_prefix_off_20260917.json", True),
    ("preOn", "改动前·挂语义腿", "tools/_redtest_chain_prefix_on_20260917.json", True),
    ("postOff", "改动后·不挂语义腿", "tools/_redtest_chain_off_20260917.json", False),
    ("postOn", "改动后·挂语义腿（上线形态）", "tools/_redtest_chain_on_20260917.json", False),
]
OUT_DIR = "docs/worldbook-migration"
OUT_BASE = "REDTEST-CHAIN-20260917"

DETAIL_RANK = {"rumor": 0, "summary": 1, "detail": 2, "secret": 3}
TIER_GATES = [
    ("毛皮基准价 400", "detail", "economy.goods-fur"),
    ("操办科尔坦家的图谋", "secret", "politics.clans-charas-cortain-secret"),
]

# 五条缺口 → 挂到具体 case 上。逐条给判据，**逐条报**（一条不中不等于整条缺口没补）。
# `ok` 的语义：这一条"补上了"该看到的样子。
GAPS = [
    ("缺口 1 · 空查询不再冒充「命中」",
     [
         ("KWJ1", "match_mode 必须占一个独立值 blank（以前报 keyword）",
          lambda r: r is not None and r.get("match_mode") == "blank", None),
         ("KWJ2", "纯空格同上",
          lambda r: r is not None and r.get("match_mode") == "blank", None),
     ]),
    ("缺口 2 · 全角→半角折叠",
     [
         ("KWG5", "必须命中原条目 —— 半角写法本来就中，差的只有这一层",
          lambda r: r is not None and r.get("verdict") == "OK", None),
     ]),
    ("缺口 3 · 形近词不再抢走错条目",
     [
         ("KWC2", "命中原条目（繁体问法）",
          lambda r: r is not None and r.get("verdict") == "OK", None),
         ("KWA5", "至少不许再吐出无关条目",
          lambda r: r is not None and r.get("verdict") == "OK",
          "字面两条腿已归零（kw=0 term 过滤后 0 候选），残留来自**语义腿**："
          "而语义腿带不出相似度（`RagHit` 只有 `Rank`，框架协议里没有分数）⇒ 没有可卡的阈值。"
          "要关掉得先在 RAG 契约里开放相似度，属框架级改动。"),
     ]),
    ("缺口 4 · 单字查询不给语义腿",
     [
         ("KWI11", "挂语义腿也不得吐出任何条目（以前命中 items-mule）",
          lambda r: r is not None and not (r.get("hits") or []), None),
     ]),
    ("缺口 5 · 内部标识/兜底词不进索引",
     [
         ("KWI6", "`doc` 不得命中（以前 448）",
          lambda r: r is not None and not (r.get("hits") or []), None),
         ("KWI17", "`geography` 不得命中（以前 408）",
          lambda r: r is not None and not (r.get("hits") or []), None),
         ("KWI9", "`castle_village` 不得命中（以前 131）",
          lambda r: r is not None and not (r.get("hits") or []), None),
         ("KWI18", "`entry` 不得命中",
          lambda r: r is not None and not (r.get("hits") or []), None),
         ("KWI16", "`economy` 不得命中（以前 21）",
          lambda r: r is not None and not (r.get("hits") or []),
          "关键词侧已归零；残留来自**综述正文**：`geography.mines-lycaron` 的 summary 里写着"
          "「承 IMPL §3.1 留痕：economy 档承载一条政治沿革」—— 内部记账文字进了正文，"
          "而 term 索引是从标题＋综述建的 ⇒ **数据问题，不是检索规则问题**。"
          "改它要动世界书源档并重编译（编译器那份 K1 修正目前还没入库，属另一条线）。"),
     ]),
]


def load(path):
    with io.open(path, encoding="utf-8") as fh:
        return json.load(fh)


def index_by_no(rows):
    """按 `KW<字母><数字>` 建索引。

    ⚠️ 必须**连 KW 一起取**。第一版写成 `KW([A-Z]\\d+)`（只取 group(1)）得到 `A1`，
    而期望侧的 `no` 是 `KWA1` ⇒ 69 条全落「没跑到」，两档都报 69/69 红、零翻转。
    那种"一次性全红且两档一模一样"的形状，先怀疑仪器 —— 真故障不会让两档塌成同一个数。
    """
    out = {}
    for row in rows:
        match = re.search(r"(KW[A-Z]\d+)", row.get("name") or "")
        if match:
            out[match.group(1)] = row
    return out


def judge(expect, results):
    by_no = index_by_no(results)
    rows = []
    for case in expect:
        no = case["no"]
        got = by_no.get(no)
        if got is None:
            rows.append(dict(case, verdict="RED", kind="没跑到", harness=True,
                             hits=[], playerText=case.get("text") or "", text="",
                             match_mode=None, detail=None, scope=None,
                             keywordHits=None, termHits=None))
            continue
        hits = got.get("hits") or []
        targets = case["targets"]
        if case["cat"] == "MISS-RISK":
            ok = any(h in hits for h in targets)
            if ok:
                verdict, kind = "OK", ""
            elif not hits:
                verdict, kind = "RED", "漏报"
            else:
                verdict, kind = "RED", "命中错条目"
        elif case["group"] == "权限档位":
            ok = not any(h in hits for h in targets)
            verdict, kind = ("OK", "") if ok else ("RED", "★权限泄漏")
        else:
            ok = not hits
            verdict, kind = ("OK", "") if ok else ("RED", "过匹配")
        rows.append(dict(case, verdict=verdict, kind=kind, hits=hits,
                         state=got.get("state"), text=(got.get("text") or ""),
                         playerText=(got.get("player_text") or case.get("text") or ""),
                         match_mode=got.get("match_mode"),
                         keywordHits=got.get("literal_keyword_hits"),
                         termHits=got.get("literal_term_hits"),
                         detail=got.get("detail"), scope=got.get("scope"),
                         harness=False))
    return rows


def summarize(rows):
    total = len(rows)
    harness = [r for r in rows if r.get("harness")]
    graded = [r for r in rows if not r.get("harness")]
    red = [r for r in graded if r["verdict"] == "RED"]
    miss = [r for r in graded if r["cat"] == "MISS-RISK"]
    over = [r for r in graded if r["cat"] == "OVER-RISK"]
    return {
        "total": total, "harnessMiss": len(harness), "graded": len(graded),
        "red": len(red), "ok": len(graded) - len(red),
        "missRisk": len(miss), "missRed": sum(1 for r in miss if r["verdict"] == "RED"),
        "overRisk": len(over), "overRed": sum(1 for r in over if r["verdict"] == "RED"),
        "byGroup": {g: "%d/%d" % (sum(1 for r in graded if r["group"] == g and r["verdict"] == "RED"),
                                  sum(1 for r in graded if r["group"] == g))
                    for g in dict.fromkeys(r["group"] for r in rows)},
    }


def tier_gates(rows):
    out = []
    for needle, tier, entry in TIER_GATES:
        seen = []
        for row in rows:
            if row.get("harness"):
                continue
            if not (row.get("text") or ""):
                continue
            if needle in (row.get("text") or ""):
                seen.append((row["no"], row["identity"], row.get("detail")))
        out.append((needle, tier, entry, seen))
    return out


def recoverable(rows):
    """能不能读这一档的正文（还原档没有正文 ⇒ 档位闸不许拿它当证据）。"""
    return any((r.get("text") or "") for r in rows)


def delta_changed(rows_a, rows_b, only_cat=None):
    """前→后 翻转清单。返回 (红转绿, 绿转红, 其他变化)。"""
    b = {r["no"]: r for r in rows_b}
    up, down = [], []
    for r in rows_a:
        if r.get("harness"):
            continue
        if only_cat and r["cat"] != only_cat:
            continue
        other = b.get(r["no"])
        if other is None or other.get("harness"):
            continue
        if r["verdict"] == other["verdict"]:
            continue
        rec = {"no": r["no"], "group": r["group"], "text": r.get("playerText") or "",
               "before": r["verdict"], "after": other["verdict"],
               "beforeKind": r["kind"], "afterKind": other["kind"]}
        (up if r["verdict"] == "RED" else down).append(rec)
    return up, down


def main():
    expect = load(EXPECT)
    rows = {}
    summaries = {}
    for key, label, path, is_recovered in TIERS:
        rows[key] = judge(expect, load(path))
        summaries[key] = summarize(rows[key])
    s_pre, s_post = summaries["preOn"], summaries["postOn"]
    up, down = delta_changed(rows["preOn"], rows["postOn"])

    os.makedirs(OUT_DIR, exist_ok=True)
    report = {
        "spec": {"target": "448 条包 ModuleData/Worldbook/packages/calradia",
                 "engine": "NpcDialogueService → WorldKnowledgeQueryService.FindCandidates",
                 "probe": "worldbook-runtime-sim probe（真代码零替身）",
                 "samples": len(expect),
                 "baseline": "改动前逐条读数还原自 commit 66649c5（入库报告缺返回正文 ⇒ 那一档不进档位闸）"},
        "summaries": summaries,
        "flips": {"up": up, "down": down},
        "tierGates": {k: tier_gates(rows[k]) for k in rows},
        "gaps": [],
        "rows": {k: [{kk: vv for kk, vv in r.items() if kk != "text"} for r in rows[k]] for k in rows},
    }

    lines = []
    lines.append("# 链路缺口红测（2026-09-17）")
    lines.append("")
    lines.append("> 靶：对话链路（448 条包，`NpcDialogueService` → `WorldKnowledgeQueryService.FindCandidates`）。探针 `worldbook-runtime-sim probe`，**真代码零替身**。")
    lines.append("> 样本 %d 条，按目标条目**真实 keywords** 造；**拼音首字母这类已按甲方口径移出样本**（链路上没有拼音通道，拿它判红量到的是设计如此，不是缺口）。" % len(expect))
    lines.append("> **三档**：改动前（还原自入库报告 `66649c5`）／改动后 × 不挂语义腿 ／ 挂语义腿（上线形态）。")
    lines.append("")
    lines.append("## 〇、口径与限制")
    lines.append("")
    lines.append("1. 样本是**照着设计造的**，不是真人日志 ⇒ 只说明「哪些形状会漏」，**查全/查准率不能从这批数字推**。")
    lines.append("2. 四档跑的是同一批样本、同一个包、同一份真代码（改动前后只差那几个补丁）⇒ 前后之差可以当收益读。")
    lines.append("3. **过匹配组与边界组是「不得命中」口径**：吐出任何条目就算红。这两组的红多数是设计上的对抗题。")
    lines.append("4. 改动前那一档缺**返回正文**（入库时为瘦身剔掉了）⇒ 它只进判定表，**不进档位闸**。")
    lines.append("")
    lines.append("## 一、五条缺口验收（本次的主问题）")
    lines.append("")
    lines.append("**逐条报**：一条 case 不中不等于整条缺口没补 —— 所以下表一行一个 case，缺口级结论看「小计」。")
    lines.append("")
    lines.append("| 缺口 | case | 判据 | 改动前 | 改动后 | 结论 |")
    lines.append("|---|---|---|---|---|---|")
    pre_map = {r["no"]: r for r in rows["preOn"]}
    post_map = {r["no"]: r for r in rows["postOn"]}
    residual = []
    for title, cases in GAPS:
        before_oks, after_oks = [], []
        for no, criterion, ok_fn, note in cases:
            before_ok = ok_fn(pre_map.get(no))
            after_ok = ok_fn(post_map.get(no))
            before_oks.append(before_ok)
            after_oks.append(after_ok)
            verdict = "**补上**" if (after_ok and not before_ok) else ("本来就是好的" if after_ok else "**没补上**")
            lines.append("| %s | `%s` | %s | %s | %s | %s |"
                         % (title, no, criterion, "✅" if before_ok else "❌",
                            "✅" if after_ok else "❌", verdict))
            if not after_ok and note:
                residual.append((no, title, note))
        lines.append("| %s | **小计** | %d 条 | %d/%d | **%d/%d** | %s |"
                     % (title, len(cases), sum(before_oks), len(cases), sum(after_oks), len(cases),
                        "**全部补上**" if all(after_oks) else "**%d 条未关**" % (len(cases) - sum(after_oks))))
        report["gaps"].append({"title": title,
                               "cases": [{"no": n, "beforeOk": b, "afterOk": a}
                                         for (n, _c, _f, _note), b, a in zip(cases, before_oks, after_oks)]})
    lines.append("")
    lines.append("> ⚠️ **别把「缺口 1 已补」与 `KWJ1`/`KWJ2` 在 §二/§五 里仍是红看成矛盾。** "
                 "缺口 1 的原话是「空查询兜底全库**且** matchMode 谎报」——上一版报告自己就写了"
                 "「**兜底本身可以是有意的**（玩家问『你知道些什么』），可真缺口是 `matchMode` 从不报兜底」。"
                 "所以本次补的是**后半截**：`matchMode` 现在占一个独立值 `blank`（以前不挂语义报 `keyword`、"
                 "挂上报 `hybrid`，两种读起来都像「检索命中了」），上游从此分得清「检索命中」和「兜底给了全库」。"
                 "**兜底返回全库照旧**，而 §二/§五 的「边界组＝不得命中」口径没变 ⇒ 这两条按**组口径**仍计红，"
                 "属口径问题，不是未修。")
    lines.append("")
    if residual:
        lines.append("### 没关掉的那几条，逐条写明成因（不写成「待办」两字糊过去）")
        lines.append("")
        for no, title, note in residual:
            lines.append("- **`%s`（%s）**：%s" % (no, title, note))
        lines.append("")
    lines.append("逐条明细：")
    lines.append("")
    for title, cases in GAPS:
        lines.append("- **%s**" % title)
        for no, _c, _f, _note in cases:
            a, b = pre_map.get(no), post_map.get(no)
            if b is None:
                continue
            lines.append("  - `%s` 「%s」：改动前 `%s` → 改动后 `%s`（mode=%s，通道 kw=%s term=%s），命中 %s"
                         % (no, (b.get("playerText") or "")[:20],
                            (a or {}).get("state", "—"), b["state"], b.get("match_mode"),
                            b.get("keywordHits"), b.get("termHits"),
                            "、".join(h.split(".")[-1] for h in (b.get("hits") or [])[:3]) or "—"))
    lines.append("")

    lines.append("## 二、总账（同一批 %d 条 × 四档）" % len(expect))
    lines.append("")
    lines.append("| 档 | RED/已判 | 仪器没跑到 | MISS-RISK 红 | OVER-RISK 红 |")
    lines.append("|---|---|---|---|---|")
    for key, label, _p, _r in TIERS:
        s = summaries[key]
        lines.append("| %s | **%d/%d** | %d | %d/%d | %d/%d |"
                     % (label, s["red"], s["graded"], s["harnessMiss"],
                        s["missRed"], s["missRisk"], s["overRed"], s["overRisk"]))
    lines.append("")
    lines.append("### 按组（改动前·挂 → 改动后·挂）")
    lines.append("")
    lines.append("| 组 | 改动前 | 改动后 |")
    lines.append("|---|---|---|")
    for group in s_pre["byGroup"]:
        lines.append("| %s | %s | %s |" % (group, s_pre["byGroup"][group], s_post["byGroup"].get(group, "—")))
    lines.append("")
    lines.append("### 红转绿 / 绿转红（改动前·挂 → 改动后·挂）")
    lines.append("")
    lines.append("- **红转绿 %d 条（收益）**" % len(up))
    for r in up:
        lines.append("  - `%s`（%s）—— %s" % (r["text"], r["group"], r["beforeKind"] or "RED"))
    lines.append("- **绿转红 %d 条（代价）**" % len(down))
    for r in down:
        lines.append("  - `%s`（%s）—— %s" % (r["text"], r["group"], r["afterKind"]))
    if not down:
        lines.append("  - （无）")
    lines.append("")

    lines.append("## 三、档位闸（细档有没有漏给低档身份）")
    lines.append("")
    lines.append("判法：某档独有的正文，只许出现在 detail 上限达到该档的身份那里。**一次都没出现＝空转**，不能当证据。")
    lines.append("")
    for key, label, _p, recovered in TIERS:
        if recovered:
            lines.append("### %s —— **跳过**（这一档没有返回正文，闸不成立）" % label)
            lines.append("")
            continue
        lines.append("### %s" % label)
        lines.append("")
        for needle, tier, entry, seen in report["tierGates"][key]:
            if not seen:
                lines.append("- 「%s」（%s 档独有，出自 `%s`）**一次都没出现 ⇒ 空转，不能当证据**" % (needle, tier, entry))
                continue
            low = [x for x in seen if DETAIL_RANK.get(x[2] or "", -1) < DETAIL_RANK[tier]]
            where = "、".join("`%s`×%s" % (x[0], x[1]) for x in seen)
            lines.append("- 「%s」（%s 档独有）出现在：%s" % (needle, tier, where))
            lines.append("  - ⇒ %s" % ("**泄漏到低档身份**：" + "、".join("`%s`×%s" % (x[0], x[1]) for x in low)
                                       if low else "未出现在任何低档身份"))
        lines.append("")

    lines.append("## 四、逐条（改动前·挂 / 改动后·挂 / 改动后·不挂）")
    lines.append("")
    lines.append("| # | 组 | 输入 | 期望 | 改动前 | 改动后 | 性质(前/后) | 改动后命中 | 通道 |")
    lines.append("|---|---|---|---|---|---|---|---|---|")
    off_map = {r["no"]: r for r in rows["postOff"]}
    for r in rows["preOn"]:
        no = r["no"]
        a = pre_map.get(no)
        b = post_map.get(no)
        c = off_map.get(no)
        if b is None:
            continue
        k_a = (a or {}).get("kind") or "—"
        k_b = b.get("kind") or "—"
        kind = k_a if k_a == k_b else "%s / %s" % (k_a, k_b)
        chan = "kw=%s term=%s mode=%s" % (b.get("keywordHits"), b.get("termHits"), b.get("match_mode"))
        lines.append("| %s | %s | `%s` | %s | %s | %s | %s | %s | %s |"
                     % (no, r["group"], (b.get("playerText") or "")[:22] or "(空)",
                        "/".join(t.split(".")[-1] for t in r["targets"]) or "—",
                        (a or {}).get("verdict", "—"), b["verdict"], kind,
                        ", ".join(h.split(".")[-1] for h in (b.get("hits") or [])[:2]) or "—",
                        chan))
    lines.append("")

    lines.append("## 五、还剩下的（补完五条之后仍红）")
    lines.append("")
    remain = [r for r in rows["postOn"] if r["verdict"] == "RED"]
    for group in dict.fromkeys(r["group"] for r in remain):
        same = [r for r in remain if r["group"] == group]
        lines.append("- **%s**：%d 条" % (group, len(same)))
        for r in same:
            note = r["kind"]
            if r.get("keywordHits") == 0 and r.get("termHits", 0) > 0:
                note += "（走的是兜底 term 通道，不是关键词通道）"
            if (r.get("keywordHits") or 0) == 0 and (r.get("termHits") or 0) == 0:
                note += "（字面侧两条腿都没动，只能靠语义腿）"
            lines.append("  - `%s` → %s" % ((r.get("playerText") or "")[:24], note))
    lines.append("")
    lines.append("## 六、这批没覆盖到的")
    lines.append("")
    lines.append("1. **真人日志**：没有。样本按 keywords 设计造 ⇒ 只回答「这些形状会不会漏」。")
    lines.append("2. **繁→简只做到「系统能折的那部分」**：走系统 `LCMapStringEx`（不造表），折不动就原样返回。`KWC2「拉邁薩」` 折对了；`斯特基亚` 那类字序差异（斯特 vs 斯提）不是繁简问题，折不了，残留走语义腿。")
    lines.append("3. **语义腿带不出相似度**：`RagHit` 只有 `Rank`（`framework/.../StorageAndRagApi.cs:135`），协议里没有分数 ⇒ 无法对语义腿卡阈值。缺口 3 / 5 的残留都堵在这儿。")
    lines.append("4. **多轮／指代**、**身份维只用了 2 个**（全覆盖在 `identity-gate` 那条门禁）、**性能**：都没测。")
    lines.append("")

    with io.open(os.path.join(OUT_DIR, OUT_BASE + ".md"), "w", encoding="utf-8") as fh:
        fh.write("\n".join(lines) + "\n")
    with io.open(os.path.join(OUT_DIR, OUT_BASE + ".json"), "w", encoding="utf-8") as fh:
        json.dump(report, fh, ensure_ascii=False, indent=2)
        fh.write("\n")

    for key, label, _p, _r in TIERS:
        s = summaries[key]
        print("%-26s RED %d/%d（MISS %d/%d，OVER %d/%d）"
              % (label, s["red"], s["graded"], s["missRed"], s["missRisk"], s["overRed"], s["overRisk"]))
    print("红转绿 %d，绿转红 %d" % (len(up), len(down)))
    for gap in report["gaps"]:
        cases = gap["cases"]
        after_ok = sum(1 for c in cases if c["afterOk"])
        open_cases = [c["no"] for c in cases if not c["afterOk"]]
        head = "**全部补上**" if not open_cases else ("%d/%d 条补上，未关 %s"
                                                     % (after_ok, len(cases), "、".join("`%s`" % n for n in open_cases)))
        print("  %s：%s" % (head, gap["title"]))
    print("报告：" + os.path.join(OUT_DIR, OUT_BASE + ".md"))

    if any(summaries[k]["harnessMiss"] for k, _l, _p, _r in TIERS):
        print("")
        print("!! 仪器没跑全 ⇒ 上面的数字不能当产品结论，先修 name→no 映射。")
        return 2
    return 0


sys.exit(main() or 0)
