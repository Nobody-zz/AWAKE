# -*- coding: utf-8 -*-
"""v12（cortain 摘要改口吻）复测：同批 68 条 × 上一版(v11) / 本版(v12)，逐条比。

回答两件事：
  ① **有没有回归** —— v12 相对 v11（现上线形态的上一版）判定有没有任何一条变坏；
  ② **命中集合有没有被挪动** —— 摘要进语义向量，改一句就可能让语义腿的近邻换人；
     所以除了判定翻转，还要报"判定没变但 hits 变了"的条目。

判定逻辑与 `_redtest_chain_judge2_20260917.py` **逐字相同**（同一批 expect、同一套
MISS-RISK／过匹配／权限黑名单口径），只换输入档；两次数字可直接比。
"""
import io
import json
import re
import sys

EXPECT = "tools/_redtest_chain_expect_20260917.json"

TIERS = [
    ("v11Off", "上一版(v11)·不挂语义腿", "tools/_redtest_chain_off_v11_20260917.json"),
    ("v11On", "上一版(v11)·挂语义腿", "tools/_redtest_chain_on_v11_20260917.json"),
    ("v12Off", "本版(v12)·不挂语义腿", "tools/_redtest_chain_off_v12_20260917.json"),
    ("v12On", "本版(v12)·挂语义腿（现上线形态）", "tools/_redtest_chain_on_v12_20260917.json"),
]

FLIP_WATCH = ["KWI16", "KWJ5", "KWI6", "KWI17", "KWI9", "KWI18", "KWA5", "KWC2"]
CORTAIN = "awake:entry:politics.clans-charas-cortain-secret"


def load(path):
    with io.open(path, encoding="utf-8") as fh:
        return json.load(fh)


def index_by_no(rows):
    out = {}
    for row in rows:
        m = re.search(r"(KW[A-Z]\d+)", row.get("name") or "")
        if m:
            out[m.group(1)] = row
    return out


def judge(expect, results):
    by_no = index_by_no(results)
    rows = []
    for case in expect:
        got = by_no.get(case["no"])
        if got is None:
            rows.append(dict(case, verdict="RED", kind="没跑到", harness=True, hits=[]))
            continue
        hits = got.get("hits") or []
        if case["cat"] == "MISS-RISK":
            ok = any(h in hits for h in case["targets"])
            verdict, kind = ("OK", "") if ok else ("RED", "漏报" if not hits else "命中错条目")
        elif case["group"] == "权限档位":
            ok = not any(h in hits for h in case["targets"])
            verdict, kind = ("OK", "") if ok else ("RED", "★权限泄漏")
        else:
            ok = not hits
            verdict, kind = ("OK", "") if ok else ("RED", "过匹配")
        rows.append(dict(case, verdict=verdict, kind=kind, hits=hits, harness=False,
                         playerText=(got.get("player_text") or case.get("text") or ""),
                         match_mode=got.get("match_mode"),
                         keywordHits=got.get("literal_keyword_hits"),
                         termHits=got.get("literal_term_hits")))
    return rows


def summarize(rows):
    graded = [r for r in rows if not r.get("harness")]
    red = [r for r in graded if r["verdict"] == "RED"]
    miss = [r for r in graded if r["cat"] == "MISS-RISK"]
    over = [r for r in graded if r["cat"] == "OVER-RISK"]
    return dict(graded=len(graded), harness=len(rows) - len(graded), red=len(red),
                missRed=sum(1 for r in miss if r["verdict"] == "RED"), missRisk=len(miss),
                overRed=sum(1 for r in over if r["verdict"] == "RED"), overRisk=len(over))


def main():
    expect = load(EXPECT)
    rows = {k: judge(expect, load(p)) for k, _l, p in TIERS}
    sums = {k: summarize(rows[k]) for k in rows}
    print("=== 四档小计 ===")
    for k, label, _p in TIERS:
        s = sums[k]
        print("%-32s RED %d/%d（MISS %d/%d，OVER %d/%d，仪器没跑到 %d）"
              % (label, s["red"], s["graded"], s["missRed"], s["missRisk"],
                 s["overRed"], s["overRisk"], s["harness"]))
    print()

    # ---- ① 回归：v11 vs v12（都取挂语义腿，即现上线形态） ----
    a = {r["no"]: r for r in rows["v11On"]}
    b = {r["no"]: r for r in rows["v12On"]}
    worse, better = [], []
    for no in sorted(set(a) & set(b)):
        ra, rb = a[no], b[no]
        if ra.get("harness") or rb.get("harness"):
            continue
        if ra["verdict"] == rb["verdict"]:
            continue
        (worse if ra["verdict"] == "OK" else better).append((no, ra, rb))
    print("① 回归检查（v11·挂 -> v12·挂，即两版上线形态）")
    print("   变好 %d 条" % len(better))
    for no, ra, rb in better:
        print("     `%s` %s ｜ %s -> %s（mode=%s kw=%s term=%s）"
              % (no, ra.get("playerText"), ra["kind"] or "OK", rb["kind"] or "OK",
                 rb.get("match_mode"), rb.get("keywordHits"), rb.get("termHits")))
    print("   **变坏 %d 条（必须为 0）**" % len(worse))
    for no, ra, rb in worse:
        print("     `%s` %s ｜ %s -> %s" % (no, ra.get("playerText"), ra["kind"], rb["kind"]))
    print()

    # ---- ② 判定没变、但命中集合被挪动的条目 ----
    print("② 命中集合被挪动（判定未变）的条目")
    moved = []
    for no in sorted(set(a) & set(b)):
        ra, rb = a[no], b[no]
        if ra.get("harness") or rb.get("harness"):
            continue
        ha, hb = tuple(sorted(ra.get("hits") or [])), tuple(sorted(rb.get("hits") or []))
        if ha != hb:
            moved.append((no, ra, rb))
    print("   共 %d 条" % len(moved))
    for no, ra, rb in moved:
        print("     `%s` 「%s」%s ｜ 命中 %s -> %s"
              % (no, (ra.get("playerText") or "")[:14], ra["verdict"],
                 "、".join(h.replace("awake:entry:", "").split(".")[-1] for h in (ra.get("hits") or [])[:4]) or "—",
                 "、".join(h.replace("awake:entry:", "").split(".")[-1] for h in (rb.get("hits") or [])[:4]) or "—"))
    print()

    # ---- ③ 关注项逐条 ----
    print("③ 关注项逐条（v11·挂 -> v12·挂）")
    for no in FLIP_WATCH:
        ra, rb = a.get(no), b.get(no)
        if ra is None or rb is None:
            print("     `%s`  库里没这条" % no)
            continue
        print("     `%s` %-14s %s -> %s ｜ 命中 %s -> %s"
              % (no, "「%s」" % (ra.get("playerText") or "")[:12], ra["verdict"], rb["verdict"],
                 "、".join(h.split(".")[-1] for h in (ra.get("hits") or [])[:3]) or "—",
                 "、".join(h.split(".")[-1] for h in (rb.get("hits") or [])[:3]) or "—"))
    print()

    # ---- ④ 缺口 ⑤ 在 本版上线形态 下仍应为 5/5 ----
    print("④ 缺口 ⑤（内部标识/兜底词不进索引）在**本版上线形态**下的 5 条")
    gap5 = ["KWI6", "KWI17", "KWI9", "KWI18", "KWI16"]
    passed = 0
    for no in gap5:
        rb = b.get(no)
        if rb is None:
            print("     `%s`  库里没这条" % no)
            continue
        ok = not (rb.get("hits") or [])
        passed += 1 if ok else 0
        print("     `%s` 「%s」%s ｜ 命中 %s"
              % (no, (rb.get("playerText") or "")[:16], "✅" if ok else "❌",
                 "、".join(h.split(".")[-1] for h in (rb.get("hits") or [])[:3]) or "—"))
    print("     小计 %d/%d" % (passed, len(gap5)))
    print()

    # ---- ⑤ cortain 这个条目作为命中出现在哪几条里 ----
    print("⑤ 命中里含 cortain 条目的 case")
    for tag, rws in (("v11·挂", rows["v11On"]), ("v12·挂", rows["v12On"])):
        hitcases = [r["no"] for r in rws if CORTAIN in (r.get("hits") or [])]
        print("     %s : %s" % (tag, hitcases or "无"))
    print()

    fail = bool(sums["v12On"]["harness"]) or bool(worse) or passed != len(gap5)
    print("VERDICT %s" % ("PASS" if not fail else "FAIL"))
    return 1 if fail else 0


sys.exit(main())
