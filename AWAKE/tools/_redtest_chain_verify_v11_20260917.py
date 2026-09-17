# -*- coding: utf-8 -*-
"""v11（摘要台账清理）复测：同批 68 条 × 上线形态新旧两版，逐条比。

回答两件事：
  ① **有没有回归** —— v11 相对 post（清理前）的判定有没有任何一条变坏；
  ② **缺口 ⑤ 的第 5 条（`KWI16 economy`）是否随之关掉** —— 它的残留来自
     `geography.mines-lycaron` 的 summary，那句台账清掉后应自然消失。

判定逻辑与 `_redtest_chain_judge2_20260917.py` **逐字相同**（同一批 expect、同一套
MISS-RISK／过匹配／权限黑名单口径），只换输入档；这样两次的数字可以直接比。
"""
import io
import json
import re
import sys

EXPECT = "tools/_redtest_chain_expect_20260917.json"

TIERS = [
    ("postOff", "清理前·不挂语义腿", "tools/_redtest_chain_off_20260917.json"),
    ("postOn", "清理前·挂语义腿", "tools/_redtest_chain_on_20260917.json"),
    ("v11Off", "清理后·不挂语义腿", "tools/_redtest_chain_off_v11_20260917.json"),
    ("v11On", "清理后·挂语义腿（现上线形态）", "tools/_redtest_chain_on_v11_20260917.json"),
]

FLIP_WATCH = ["KWI16", "KWJ5", "KWI6", "KWI17", "KWI9", "KWI18", "KWA5", "KWC2"]


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
    out = []
    for k, label, _p in TIERS:
        s = sums[k]
        out.append("%-30s RED %d/%d（MISS %d/%d，OVER %d/%d，仪器没跑到 %d）"
                   % (label, s["red"], s["graded"], s["missRed"], s["missRisk"],
                      s["overRed"], s["overRisk"], s["harness"]))
    print("\n".join(out))
    print()

    # ---- ① 回归：清理前 vs 清理后（都取挂语义腿，即现上线形态） ----
    a = {r["no"]: r for r in rows["postOn"]}
    b = {r["no"]: r for r in rows["v11On"]}
    worse, better = [], []
    for no in sorted(set(a) & set(b)):
        ra, rb = a[no], b[no]
        if ra.get("harness") or rb.get("harness"):
            continue
        if ra["verdict"] == rb["verdict"]:
            continue
        (worse if ra["verdict"] == "OK" else better).append((no, ra, rb))
    print("① 回归检查（清理前·挂 -> 清理后·挂）")
    print("   变好 %d 条" % len(better))
    for no, ra, rb in better:
        print("     `%s` %s ｜ %s -> %s（mode=%s kw=%s term=%s）"
              % (no, ra.get("playerText"), ra["kind"] or "OK", rb["kind"] or "OK",
                 rb.get("match_mode"), rb.get("keywordHits"), rb.get("termHits")))
    print("   **变坏 %d 条（必须为 0）**" % len(worse))
    for no, ra, rb in worse:
        print("     `%s` %s ｜ %s -> %s" % (no, ra.get("playerText"), ra["kind"], rb["kind"]))
    print()

    # ---- ② 关注的那几条逐条看 ----
    print("② 关注项逐条（清理前·挂 -> 清理后·挂）")
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

    # ---- ③ 缺口 ⑤ 小结（5 条） ----
    print("③ 缺口 ⑤（内部标识/兜底词不进索引）在**上线形态**下的 5 条")
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

    fail = bool(sums["v11On"]["harness"]) or bool(worse)
    print("VERDICT %s" % ("PASS" if not fail else "FAIL"))
    return 1 if fail else 0


sys.exit(main())
