# -*- coding: utf-8 -*-
"""并集评估：字面臂 top3 ∪ 语义臂 top3，目标落在并集里算不算命中（2026-09-17）。

为什么单看语义臂的 hit1 不够：字面臂和语义臂在 26 例上互补（各自漏的对方能捡回来），
真实设计是**两条通道合并**再交给权限/分级/拼块。合并后要看的不是「谁排第一」，
而是「目标有没有进到这个候选池里」—— 所以这里算 merged_hit3。

两条臂的来源（都不在本脚本里复刻）：
  · 字面臂：tools/_smoke_run_20260917.txt 的 RETRIEVAL_CASE 行（C# 验台真代码）。
  · 语义臂：tools/_feed_sweep_20260917.json 的 rows[].top3（本机实测）。

运行：
  "C:/Users/26811/.workbuddy/binaries/python/envs/default/Scripts/python.exe" -u AWAKE/tools/_merge_eval_20260917.py
"""
import io
import json
import os
import re

ROOT = r"D:/AWAKE-Dev/AWAKE"
SWEEP = os.path.join(ROOT, "tools/_feed_sweep_20260917.json")
SMOKE = os.path.join(ROOT, "tools/_smoke_run_20260917.txt")
CASES = os.path.join(ROOT, "tools/_retrieval_cases_20260916.json")


def literal():
    txt = io.open(SMOKE, encoding="utf-8", errors="replace").read()
    out = {}
    for line in txt.splitlines():
        if "RETRIEVAL_CASE" not in line:
            continue
        q = re.search(r"q=(\S.*)$", line)
        h1 = re.search(r"h1=(\d)", line)
        t3 = re.search(r"top3=\[([^\]]*)\]", line)
        if not (q and h1):
            continue
        ids = [x.strip() for x in t3.group(1).split(",")] if t3 and t3.group(1).strip() else []
        out[q.group(1).strip()] = (int(h1.group(1)), ids)
    return out


def main():
    cases = json.load(io.open(CASES, encoding="utf-8"))["cases"]
    sweep = json.load(io.open(SWEEP, encoding="utf-8"))
    lit = literal()

    lit_h1 = sum(1 for c in cases if lit.get(c["query"], (0, []))[0] == 1)
    lit_h3 = sum(1 for c in cases if c["target"].split(":")[-1] in lit.get(c["query"], (0, []))[1:][0] or
                 c["target"].split(":")[-1] in lit.get(c["query"], (0, []))[1])
    print("字面臂（C# 验台真代码）：hit1 %d/26，top3 内含目标 %d/26" % (lit_h1, lit_h3))
    print()

    rows = []
    for model, arm in sorted(sweep["arms"].items()):
        for key, v in sorted(arm.items()):
            if not isinstance(v, dict) or "stats" not in v:
                continue
            m_h1 = m_h3 = s_h1 = s_h3 = 0
            pool_sum = 0
            for r in v["rows"]:
                tgt = r["target"].split(":")[-1]
                l = lit.get(r["query"], (0, []))
                l_ids = l[1] if l and isinstance(l[1], list) else []
                l_h1 = (l[0] == 1) if l else False
                s_ids = [x.split(":")[-1] for x in r["top3"]]
                s_h1 = bool(r["hit1"])
                incl = (tgt in l_ids) or (tgt in s_ids)
                m_h1 += (l_h1 or s_h1)
                m_h3 += incl
                s_h1 += 0  # 只是为了让上面那行读起来顺，语义臂 hit1 已记在 r 里
                pool_sum += len(set(l_ids) | set(s_ids))
            sem_h1 = sum(1 for r in v["rows"] if r["hit1"])
            sem_h3 = sum(1 for r in v["rows"] if r["hit3"])
            rows.append((m_h1, m_h3, sem_h1, sem_h3, model, key, pool_sum / len(v["rows"])))

    rows.sort(key=lambda x: (-x[0], -x[1]))
    print("%-9s %-22s %6s %6s %6s %6s %8s" % ("模型", "拼法|查询", "并h1", "并h3", "语h1", "语h3", "池均条数"))
    for m_h1, m_h3, s_h1, s_h3, model, key, pool in rows:
        print("%-9s %-22s %5d/26 %5d/26 %5d/26 %5d/26 %8.1f"
              % (model, key, m_h1, m_h3, s_h1, s_h3, pool))

    # 头部那一档的逐条明细：并集救回来的 / 并集也漏的
    top = rows[0]
    best = sweep["arms"][top[4]][top[5]]
    print()
    print("==== 最好档 %s %s 的逐条（并集 h1 %d/26） ====" % (top[4], top[5], top[0]))
    still_miss = []
    sem_only, lit_only = [], []
    for r in best["rows"]:
        tgt = r["target"].split(":")[-1]
        l = lit.get(r["query"], (0, []))
        l_ids = l[1] if l and isinstance(l[1], list) else []
        l_h1 = (l[0] == 1) if l else False
        s_h1 = bool(r["hit1"])
        s_ids = [x.split(":")[-1] for x in r["top3"]]
        if l_h1 and not s_h1:
            lit_only.append((r["group"], r["query"], tgt))
        if s_h1 and not l_h1:
            sem_only.append((r["group"], r["query"], tgt))
        if tgt not in l_ids and tgt not in s_ids:
            still_miss.append((r["group"], r["query"], tgt))
    print(" 只有字面能拿的 %d 条：" % len(lit_only))
    for g, q, t in lit_only:
        print("   [%s] %-26s → %s" % (g, q, t))
    print(" 只有语义能拿的 %d 条：" % len(sem_only))
    for g, q, t in sem_only:
        print("   [%s] %-26s → %s" % (g, q, t))
    print(" 并集也拿不到的 %d 条（= 真要另外想办法的）：" % len(still_miss))
    for g, q, t in still_miss:
        print("   [%s] %-26s → %s" % (g, q, t))


if __name__ == "__main__":
    main()
