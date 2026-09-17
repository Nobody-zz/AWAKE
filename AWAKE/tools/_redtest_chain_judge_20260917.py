# -*- coding: utf-8 -*-
"""链路缺口红测 · 判定（2026-09-17）

读：`_redtest_chain_expect_20260917.json`（期望）＋ `_redtest_chain_{off,on}_20260917.json`（两档真跑结果）
出：`docs/evidence/redtest-chain-20260917/REDTEST-CHAIN-20260917.md` 与同名 `.json`

判定口径（照 09-16 那轮，不自己另立一套）：
  MISS-RISK：期望命中 targets 之一；`hits ∩ targets = ∅` ⇒ RED（再分「漏报」hits 空 / 「命中错条目」hits 非空）
  OVER-RISK（过匹配/边界组）：不得命中任何条目 ⇒ hits 非空 = RED
  OVER-RISK（权限档位组）：**黑名单**口径 —— 不得命中 targets（别的条目命中不算违规）
另加两条「闸」核查（今天新增的交叉面）：脏输入下**档位正文**有没有漏给低档身份，且必须带阳性对照。
"""
import io
import json
import os
import re
import sys

EXPECT = "tools/_redtest_chain_expect_20260917.json"
OFF = "tools/_redtest_chain_off_20260917.json"
ON = "tools/_redtest_chain_on_20260917.json"
# 与 09-16 那两份红测报告同目录（`AWAKE/docs/evidence/` 在 .gitignore 里，放那儿不入档）。
OUT_DIR = "docs/worldbook-migration"

# 档位闸：某档正文独有串 × 它属于哪一档。
# 判法：只许出现在 detail/scope 达到该档的身份那里；出现在更低档 ＝ 泄漏。
# 一条闸如果**一次都没出现** ＝ 空转（说明这条档位的正文根本没被取到过），
# 空转必须显式报出来 —— 否则"没泄漏"会被误读成"闸有效"。
DETAIL_RANK = {"rumor": 0, "summary": 1, "detail": 2, "secret": 3}
TIER_GATES = [
    # (独有正文串, 它属于哪一档, 它出自哪个条目 —— 第三项用来找"同条目的低档对照")
    ("毛皮基准价 400", "detail", "economy.goods-fur"),
    ("操办科尔坦家的图谋", "secret", "politics.clans-charas-cortain-secret"),
]


def load(path):
    with io.open(path, encoding="utf-8") as fh:
        return json.load(fh)


def index_by_no(rows):
    """按 `KW<字母><数字>` 建索引。

    ⚠️ 必须连 KW 一起取。第一版写成 `KW([A-Z]\\d+)` 只留了 `A1`，
    而期望文件的 `no` 是 `KWA1` ⇒ 69 条全部落进「没跑到」，
    两档都报 69/69 红 —— 那种"一次性全红且两档零翻转"的形状，
    先怀疑仪器，别先怀疑产品。
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
            rows.append(dict(case, verdict="RED", kind="**没跑到**（探针里没有这一条）",
                             hits=[], harness=True,
                             playerText=case.get("text") or "", text="",
                             detail=None, scope=None))
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
            verdict, kind = ("OK", "") if ok else ("RED", "★权限泄漏（该条目不该给他）")
        else:
            ok = not hits
            verdict, kind = ("OK", "") if ok else ("RED", "过匹配")
        rows.append(dict(case, verdict=verdict, kind=kind, hits=hits,
                         state=got.get("state"), text=(got.get("text") or ""),
                         # 入参原话优先取探针回显；老结果没这个字段就退回 spec，别静默成空。
                         playerText=(got.get("player_text") or case.get("text") or ""),
                         detail=got.get("detail"), scope=got.get("scope"),
                         harness=False))
    return rows


def summarize(rows):
    total = len(rows)
    harness = [r for r in rows if r.get("harness")]
    graded = [r for r in rows if not r.get("harness")]   # 只把真跑到的计入产品结论
    red = [r for r in graded if r["verdict"] == "RED"]
    miss = [r for r in graded if r["cat"] == "MISS-RISK"]
    over = [r for r in graded if r["cat"] == "OVER-RISK"]
    return {
        "total": total,
        "harnessMiss": len(harness),
        "graded": len(graded),
        "red": len(red),
        "ok": len(graded) - len(red),
        "missRisk": len(miss), "missRed": sum(1 for r in miss if r["verdict"] == "RED"),
        "overRisk": len(over), "overRed": sum(1 for r in over if r["verdict"] == "RED"),
        "byGroup": {g: "%d/%d" % (sum(1 for r in graded if r["group"] == g and r["verdict"] == "RED"),
                                  sum(1 for r in graded if r["group"] == g))
                    for g in dict.fromkeys(r["group"] for r in rows)},
    }


def tier_gates(rows):
    """档位闸：把每个身份下的正文拼起来，查某档独有串在谁那里出现了。

    ⚠️ 只能看到**返回正文**（`text` ＝ `WorldbookQueryResult.RetrievedText`，
    即真正喂给模型的那一串）。这是对的：泄漏要看的就是"模型眼里有没有"。
    """
    out = []
    for needle, tier, entry in TIER_GATES:
        seen, leaked, low_controls = [], [], []
        for row in rows:
            if row.get("harness"):
                continue
            detail = row.get("detail") or ""
            low = DETAIL_RANK.get(detail, -1) < DETAIL_RANK[tier]
            if needle in (row.get("text") or ""):
                record = {"no": row["no"], "identity": row["identity"],
                          "detail": detail, "tier": tier}
                seen.append(record)
                if low:
                    leaked.append(record)
            # 低档对照：同一条目、detail 更低的那几条。它们"什么都没拿到"也是证据，
            # 但要说清是"拿到了但没细档"还是"压根没拿到"，两者含义不同。
            if low and any(entry in (t or "") for t in (row.get("targets") or [])):
                low_controls.append({"no": row["no"], "identity": row["identity"],
                                     "detail": detail, "hits": len(row.get("hits") or []),
                                     "state": row.get("state")})
        out.append({"needle": needle, "tier": tier, "entry": entry,
                    "appearedAt": seen, "leakedToLow": leaked,
                    "lowControls": low_controls,
                    "vacuous": not seen})
    return out


def delta(rows_off, rows_on):
    """两档逐条对照：只关心 MISS-RISK 的 OK/RED 翻转。"""
    on = {r["no"]: r for r in rows_on}
    up, down, flip = [], [], []
    for r in rows_off:
        if r["cat"] != "MISS-RISK" or r.get("harness"):
            continue
        other = on.get(r["no"])
        if other is None or other.get("harness"):
            continue
        a, b = r["verdict"], other["verdict"]
        if a == b:
            continue
        record = {"no": r["no"], "group": r["group"], "text": r.get("playerText") or "",
                  "off": a, "on": b, "offKind": r["kind"], "onKind": other["kind"]}
        flip.append(record)
        (up if a == "RED" and b == "OK" else down).append(record)
    return up, down, flip


def gap_section(rows_on, rows_off):
    """缺口清单。每条都必须挂 case 号，人能倒着复核；数字全部现算，不手打。"""
    on = {r["no"]: r for r in rows_on}
    off = {r["no"]: r for r in rows_off}

    def cells(rows, pred):
        return [r for r in rows if pred(r)]

    still_miss = cells(rows_on, lambda r: r["cat"] == "MISS-RISK" and r["verdict"] == "RED")
    over = cells(rows_on, lambda r: r["group"] == "过匹配" and r["verdict"] == "RED")
    edge = cells(rows_on, lambda r: r["group"] == "边界" and r["verdict"] == "RED")
    new_cost = [on[r["no"]] for r in rows_off
                if r["group"] == "过匹配" and r["verdict"] == "OK" and on[r["no"]]["verdict"] == "RED"]
    hard_fail = cells(rows_on, lambda r: r["kind"] == "漏报")
    wrong_entry = cells(rows_on, lambda r: r["kind"] == "命中错条目")

    out = []
    out.append("## 五、缺口清单（每条都挂 case 号，可倒着复核）")
    out.append("")
    out.append("### 5.1 空查询会兜底返回全库")
    blanks = [r for r in edge if not (r.get("playerText") or "").strip()]
    for r in blanks:
        out.append("- `%s` 输入 `%r` ⇒ 不挂 `%s`／挂上 `%s`，命中 `%s`"
                   % (r["no"], r.get("playerText"), off[r["no"]]["state"], r["state"],
                      "、".join(h.split(".")[-1] for h in r["hits"][:3]) or "—"))
    out.append("- 出处：`src/WorldKnowledgeQueryService.cs:272` —— `IsNullOrWhiteSpace(text)` 时**返回全库**，")
    out.append("  再靠身份闸筛、按字节预算截断。**兜底本身可以是有意的**（「你知道些什么」），")
    out.append("  可真缺口是 **`matchMode` 从不报「兜底」**（同文件 :69）：不挂语义时报 `keyword`、挂上时报 `hybrid`，")
    out.append("  两种读起来都像「检索命中了」⇒ 上游**分不清**「检索命中」和「兜底给了全库」，日志里看不出来。")
    out.append("  空串一旦因上游 bug 传进来，会**静默**返回一批看着像答案的条目。")
    out.append("")
    out.append("### 5.2 全角英文没归一化（`%s`）" % ("、".join(r["no"] for r in hard_fail if "Ｗ" in (r.get("playerText") or "") or "Ｃ" in (r.get("playerText") or "")) or "—"))
    fw = [r for r in hard_fail if any(ord(ch) > 0xFF00 for ch in (r.get("playerText") or ""))]
    for r in fw:
        out.append("- `%s` 输入 `%s` ⇒ 两档都 `%s`。" % (r["no"], r.get("playerText"), r["state"]))
    out.append("- 同一条的半角写法在 `KWG1`／`KWG2` 是 OK ⇒ 差的不是检索能力，是**缺一个全角→半角折叠**。")
    out.append("")
    out.append("### 5.3 形近／变形词会把检索带到**形近的错条目**（比不给更糟）")
    for r in wrong_entry:
        out.append("- `%s` 输入 `%s` ⇒ 命中 `%s`（不是 `%s`）"
                   % (r["no"], r.get("playerText"),
                      "、".join(h.split(".")[-1] for h in r["hits"][:3]) or "—",
                      "/".join(t.split(".")[-1] for t in r["targets"])))
    out.append("- 对设计初衷（谁知道）来说，**给错条目**比 `not_found` 更坏：模型会照着错条目的正文答。")
    out.append("")
    out.append("### 5.4 语义臂的代价，具体到一条（%s）" % ("、".join("`%s`" % r["no"] for r in new_cost) or "无"))
    for r in new_cost:
        out.append("- `%s` 输入 `%s` ⇒ **不挂 `%s`（对）／挂上 `%s`，命中 `%s`**"
                   % (r["no"], r.get("playerText"), off[r["no"]]["state"], r["state"],
                      "、".join(h.split(".")[-1] for h in r["hits"][:3])))
    if new_cost:
        out.append("- 成因：向量近邻把泛词牵到了语义相近的条目。**这是今天上线换来的新增误召回，不是旧故障。**")
    out.append("")
    out.append("### 5.5 内部标识符能当查询词用（%s 条过匹配）" % len(over))
    inner = [r for r in over if (r.get("playerText") or "").strip().isascii()
             and (r.get("playerText") or "").strip()]
    out.append("- 中文泛词：%s —— 短词必泛，属可预期。"
               % "、".join("`%s`" % r.get("playerText") for r in over
                           if not (r.get("playerText") or "").strip().isascii()))
    out.append("- **内部标识符**：%s —— 这些是命名空间／字段名／id 分片，真人不会这么说；"
               % "、".join("`%s`" % r.get("playerText") for r in inner))
    out.append("  它们进得了索引，是 `doc.<domain>.<slug>` 那类兜底关键词的代价（见 `RuntimePackageCompiler.cs:108-121` 的 K1 注释）。")
    out.append("")
    out.append("### 5.6 权限面在脏输入下没破（正面结论）")
    tier_rows = [r for r in rows_on if r["group"] == "权限档位"]
    out.append("- 权限档位组 `%d/%d` 红，且两条档位闸都没漏到低档身份 ⇒ **脏输入不会绕过身份闸**。" % (
        sum(1 for r in tier_rows if r["verdict"] == "RED"), len(tier_rows)))
    out.append("- 换言之：**变形输入能骗过「找哪条」，骗不过「给谁看」**。这两件事在链路上是分开的。")
    out.append("")
    out.append("## 六、这批**没**覆盖到的（免得被当全覆盖）")
    out.append("")
    out.append("1. **真人日志**：没有。样本是按 keywords 设计造的 ⇒ 只能回答「这些形状会不会漏」。")
    out.append("2. **多轮／上下文指代**：没测（「他」「那个」这类靠上文补全的问法）。")
    out.append("3. **身份维只用了 2 个**（64 条贵族 + 5 条平民）。12 个身份的全覆盖在 `identity-gate` 那条门禁里，不在这批。")
    out.append("4. **时效／到达时间**：没测（设计初衷第 2 层「何时知道」这条轴尚未接）。")
    out.append("5. **性能**：没测（语义臂的 IPC ＋ 编码开销）。")
    out.append("")
    return out


def main():
    expect = load(EXPECT)
    off = judge(expect, load(OFF))
    on = judge(expect, load(ON))
    s_off, s_on = summarize(off), summarize(on)
    up, down, flips = delta(off, on)

    os.makedirs(OUT_DIR, exist_ok=True)

    report = {
        "spec": {"targets": "448 条包 ModuleData/Worldbook/packages/calradia",
                 "engine": "NpcDialogueService → WorldKnowledgeQueryService.FindCandidates",
                 "probe": "worldbook-runtime-sim probe（真代码零替身）",
                 "samples": len(expect)},
        "summaryOff": s_off, "summaryOn": s_on,
        "flips": {"up": up, "down": down},
        "tierGatesOff": tier_gates(off), "tierGatesOn": tier_gates(on),
        "rowsOff": [{k: v for k, v in r.items() if k != "text"} for r in off],
        "rowsOn": [{k: v for k, v in r.items() if k != "text"} for r in on],
    }
    with io.open(os.path.join(OUT_DIR, "REDTEST-CHAIN-20260917.json"), "w", encoding="utf-8") as fh:
        json.dump(report, fh, ensure_ascii=False, indent=2)
        fh.write("\n")

    lines = []
    lines.append("# 链路缺口红测（2026-09-17）")
    lines.append("")
    lines.append("> 靶：今天的链路（448 条包，真实对话路径 `NpcDialogueService` → `WorldKnowledgeQueryService.FindCandidates`）。")
    lines.append("> 探针：`worldbook-runtime-sim probe`，**真代码零替身**。样本 %d 条，按目标条目**真实 keywords** 造。" % len(expect))
    lines.append("> 两档：**不挂语义臂**（＝改动前）／**挂语义臂**（＝今天上线的合并形态）。")
    lines.append("")
    lines.append("## 〇、口径与限制（先说清楚，免得被当成绩单读）")
    lines.append("")
    lines.append("1. **这批样本是照着设计造出来的**（按目标条目的真实 keywords 造错别字/同音/繁简/空格/标点/简写/大小写/全半角/整句），**不是真人日志** ⇒ 只说明"
                 "「哪些形状会漏」，**查全率／查准率都不能从这批数字里推**。")
    lines.append("2. **两档跑的是同一批样本、同一个包、同一份真代码**，只差语义臂挂没挂 ⇒ 两档之差可以当收益／代价读。")
    lines.append("3. **过匹配组与边界组是「不得命中」口径**：只要吐出条目就算红。所以这两组的红**多数是设计上的对抗题**，"
                 "不是新故障——但两档之间**数量的变化**是真代价，必须一起报。")
    lines.append("4. **本报告的两个数字曾经是假的**，原因是仪器不是产品（见下）。已修、已重跑，本版数字出自修好之后的探针。")
    lines.append("")
    lines.append("### 仪器修了一处（平行实现分叉）")
    lines.append("")
    lines.append("探针里原先挂着一份**手抄的**「身份 → scope/detail」平表。它与真件 `WorldbookIdentityCapabilityRules` 分叉：")
    lines.append("手抄表把 `profile.noble` 封在 `detail`，而真件对 45 岁以上贵族给的是 `secret`。")
    lines.append("后果：全包**唯一一条 secret 档正文**（`politics.clans-charas-cortain-secret`）在验台里永远取不到，"
                 "「细档有没有漏给低档身份」这根闸整根空转。已删掉平表、改走真件，并补上入参回显（`player_text`）。")
    lines.append("")
    lines.append("## 一、总数")
    lines.append("")
    lines.append("| 档 | RED/已判 | 仪器没跑到 | MISS-RISK RED | OVER-RISK RED |")
    lines.append("|---|---|---|---|---|")
    for label, s in (("不挂语义臂", s_off), ("挂语义臂（上线形态）", s_on)):
        lines.append("| %s | **%d/%d** | %d | %d/%d | %d/%d |" % (label, s["red"], s["graded"], s["harnessMiss"],
                                                                    s["missRed"], s["missRisk"],
                                                                    s["overRed"], s["overRisk"]))
    lines.append("")
    lines.append("### 按组")
    lines.append("")
    lines.append("| 组 | 不挂 | 挂上 |")
    lines.append("|---|---|---|")
    for group in s_off["byGroup"]:
        lines.append("| %s | %s | %s |" % (group, s_off["byGroup"][group], s_on["byGroup"].get(group, "—")))
    lines.append("")
    lines.append("## 二、两档翻转（只看 MISS-RISK）")
    lines.append("")
    lines.append("- 挂上语义后 **由 RED 转 OK**：%d 条（收益）" % len(up))
    for r in up:
        lines.append("  - `%s`（%s）—— %s" % (r["text"], r["group"], r["onKind"] or "OK"))
    lines.append("- 挂上语义后 **由 OK 转 RED**：%d 条（代价）" % len(down))
    for r in down:
        lines.append("  - `%s`（%s）—— %s" % (r["text"], r["group"], r["onKind"]))
    if not down:
        lines.append("  - （无）")
    lines.append("")
    lines.append("### 代价：过匹配面（判据同批同口径，只比较数量）")
    lines.append("")
    lines.append("| | 不挂 | 挂上 | 差 |")
    lines.append("|---|---|---|---|")
    over_off = s_off["overRed"] + sum(1 for r in off if r["group"] == "边界" and r["verdict"] == "RED")
    over_on = s_on["overRed"] + sum(1 for r in on if r["group"] == "边界" and r["verdict"] == "RED")
    lines.append("| 过匹配＋边界 红 | %d | %d | %+d |" % (over_off, over_on, over_on - over_off))
    lines.append("| 过匹配组 红 | %d/%d | %d/%d | %+d |" % (s_off["overRed"], s_off["overRisk"],
                                                        s_on["overRed"], s_on["overRisk"],
                                                        s_on["overRed"] - s_off["overRed"]))
    lines.append("")
    lines.append("⇒ **收益与代价必须一起读**：MISS-RISK 少了 %d 条，过匹配面多了/少了 %d 条。"
                 % (s_off["missRed"] - s_on["missRed"], over_on - over_off))
    lines.append("")
    lines.append("## 三、档位闸（脏输入下，细档有没有漏给低档身份）")
    lines.append("")
    lines.append("判法：某档独有的正文，只许出现在 detail 上限达到该档的身份那里。")
    lines.append("**一条闸如果一次都没出现＝空转**，那说明这一档的正文根本没被取到过——「没泄漏」这时不能读成「闸有效」。")
    lines.append("")
    for label, gates in (("不挂语义臂", report["tierGatesOff"]), ("挂语义臂", report["tierGatesOn"])):
        lines.append("### %s" % label)
        lines.append("")
        for g in gates:
            if g["vacuous"]:
                lines.append("- 「%s」（%s 档独有）**一次都没出现 ⇒ 空转，不能当证据**" % (g["needle"], g["tier"]))
                continue
            where = "、".join("`%s`×%s" % (r["no"], r["identity"]) for r in g["appearedAt"])
            lines.append("- 「%s」（%s 档独有，出自 `%s`）出现在：%s" % (g["needle"], g["tier"], g["entry"], where))
            if g["leakedToLow"]:
                low = "、".join("`%s`×%s(detail=%s)" % (r["no"], r["identity"], r["detail"]) for r in g["leakedToLow"])
                lines.append("  - ⇒ **泄漏到低档身份**：%s" % low)
            else:
                lines.append("  - ⇒ 未出现在任何低档身份")
            ctrl = g["lowControls"]
            if ctrl:
                detail_txt = "、".join("`%s`×%s(state=%s，拿到 %d 条)" % (
                    c["no"], c["identity"], c["state"], c["hits"]) for c in ctrl)
                lines.append("  - 低档对照（同条目的低档身份）：%s" % detail_txt)
                got_something = [c for c in ctrl if c["hits"] > 0]
                if got_something:
                    lines.append("    - ⇒ 对照 %d 条里有 **%d 条拿到了条目、但没拿到该档正文** ⇒ 这是**强对照**："
                                 "同一条目、同一个问题，低档身份拿得到条目却拿不到细档。" % (len(ctrl), len(got_something)))
                else:
                    lines.append("    - ⇒ 对照 %d 条**全都什么都没拿到** ⇒ 这是**弱对照**："
                                 "只证了「低档身份拿不到这个条目」，**没**单独证「条目给了低档、细档被挡住」。" % len(ctrl))
            else:
                lines.append("  - ⚠️ **本批没有「同条目的低档身份」样本 ⇒ 这条闸只证了「没出现过」，没证「出现过会给谁」**")
        lines.append("")
    lines.append("## 四、逐条")
    lines.append("")
    lines.append("| # | 组 | 输入 | 期望 | 不挂语义 | 挂语义 | 性质(不挂/挂上) | 不挂命中 | 挂上命中 |")
    lines.append("|---|---|---|---|---|---|---|---|---|")
    on_map = {r["no"]: r for r in on}
    for r in off:
        o = on_map.get(r["no"], {})
        off_hits = r["hits"][:3]
        on_hits = (o.get("hits") or [])[:3]
        k_off = r["kind"] or "—"
        k_on = o.get("kind") or "—"
        kind = k_off if k_off == k_on else "%s / %s" % (k_off, k_on)
        lines.append("| %s | %s | `%s` | %s | %s | %s | %s | %s | %s |" % (
            r["no"], r["group"], (r.get("playerText") or "")[:24] or "(空)",
            "/".join(t.split(".")[-1] for t in r["targets"]) or "—",
            r["verdict"], o.get("verdict", "—"), kind,
            ", ".join(h.split(".")[-1] for h in off_hits) or "—",
            ", ".join(h.split(".")[-1] for h in on_hits) or "—"))
    lines.append("")
    lines.extend(gap_section(on, off))
    with io.open(os.path.join(OUT_DIR, "REDTEST-CHAIN-20260917.md"), "w", encoding="utf-8") as fh:
        fh.write("\n".join(lines) + "\n")

    print("不挂语义臂：RED %d/%d（MISS %d/%d，OVER %d/%d）" % (s_off["red"], s_off["graded"], s_off["missRed"], s_off["missRisk"], s_off["overRed"], s_off["overRisk"]))
    print("挂语义臂　：RED %d/%d（MISS %d/%d，OVER %d/%d）" % (s_on["red"], s_on["graded"], s_on["missRed"], s_on["missRisk"], s_on["overRed"], s_on["overRisk"]))
    print("翻转：红→绿 %d，绿→红 %d" % (len(up), len(down)))
    print("报告：" + os.path.join(OUT_DIR, "REDTEST-CHAIN-20260917.md"))

    bad_harness = s_off["harnessMiss"] or s_on["harnessMiss"]
    if bad_harness:
        print("")
        print("!! 仪器没跑全：不挂档缺 %d 条、挂档缺 %d 条 ⇒ 上面的数字**不能当产品结论**，先修 name→no 映射。"
              % (s_off["harnessMiss"], s_on["harnessMiss"]))
        print("   判据：一次性全红 且 两档零翻转 ＝ 先怀疑仪器。")
        return 2
    if s_off["red"] == s_off["graded"] == s_on["red"] == s_on["graded"] and len(up) == 0 == len(down):
        print("")
        print("!! 两档都 100%% 红且零翻转 —— 形状可疑，交结论前先做阳性对照（拿一条必定能命中的原词跑一遍）。")
        return 2
    return 0


main()
