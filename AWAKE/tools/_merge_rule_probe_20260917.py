# -*- coding: utf-8 -*-
"""合并规则选型（2026-09-17）。

问题：字面臂（16/26 hit1）与语义臂（20/26 hit1）要怎么合，才能把「并集上限 22」尽量吃到嘴里？
本脚本**不重算任何一臂**，只读两份已落盘的实测：
  · 字面臂 top3：`_feed_sweep_20260917.json` 的顶层 `literal`
  · 语义臂 top3：同文件 `arms.small_f32["D|raw|small_f32"].rows[].top3`

三条候选规则（都能只用 top3 判出「合并后第 1 名是谁」）：
  S  语义优先：语义第 1 名压在前面，字面里没被语义提过的按原序接在后。
  L  字面优先：字面第 1 名压在前面，语义里没被字面提过的接在后。
  RRF 倒数排名融合（k=60）：两边各按第几名折分相加，只有 top3 时名次大于 3 的按缺失算。

输出每条的规则结果与三种规则的 hit1 合计，用来决定 C# 侧写哪一条。

运行：
  "C:/Users/26811/.workbuddy/binaries/python/envs/default/Scripts/python.exe" -u AWAKE/tools/_merge_rule_probe_20260917.py
"""
import io
import json
import os

ROOT = r"D:/AWAKE-Dev/AWAKE"
SWEEP = os.path.join(ROOT, "tools/_feed_sweep_20260917.json")
ARM = ("small_f32", "D|raw|small_f32")
K = 60


def short(x):
    return x.split(":")[-1]


def main():
    d = json.load(io.open(SWEEP, encoding="utf-8"))
    literal = d["literal"]
    rows = d["arms"][ARM[0]][ARM[1]]["rows"]

    stat = {"S": 0, "L": 0, "RRF": 0, "oracle": 0, "n": 0}
    rows_out = []
    for r in rows:
        q = r["query"]
        tgt = short(r["target"])
        sem = [short(x) for x in r["top3"]]
        lit = [short(x) for x in literal.get(q, {}).get("top3", [])]
        lit_h1 = bool(literal.get(q, {}).get("hit1"))
        sem_h1 = bool(r["hit1"])

        s_order = sem + [x for x in lit if x not in sem]
        l_order = lit + [x for x in sem if x not in lit]

        score = {}
        for i, x in enumerate(lit[:3]):
            score[x] = score.get(x, 0.0) + 1.0 / (K + i + 1)
        for i, x in enumerate(sem[:3]):
            score[x] = score.get(x, 0.0) + 1.0 / (K + i + 1)
        rrf_order = sorted(score.keys(), key=lambda x: (-score[x], x))
        # 平手时按「字面名次优先」，与 C# 侧保持一致
        rrf_order = sorted(score.keys(), key=lambda x: (-score[x],
                          lit.index(x) if x in lit else 99,
                          sem.index(x) if x in sem else 99, x))

        got = {"S": bool(s_order) and s_order[0] == tgt,
               "L": bool(l_order) and l_order[0] == tgt,
               "RRF": bool(rrf_order) and rrf_order[0] == tgt,
               "oracle": lit_h1 or sem_h1}
        for k in got:
            stat[k] += 1 if got[k] else 0
        stat["n"] += 1
        rows_out.append((r["group"], q, tgt, lit_h1, sem_h1, got, lit[:3], sem[:3], rrf_order[:3]))

    print("%-3s %-30s %-42s %-4s %-4s %-4s %-4s %-4s" % ("组", "问法", "目标", "字面", "语义", "S", "L", "RRF"))
    for g, q, tgt, lh, sh, got, lit, sem, rrf in rows_out:
        print("%-3s %-30s %-42s %-4s %-4s %-4s %-4s %-4s" % (
            g, q, tgt, "1" if lh else "0", "1" if sh else "0",
            "1" if got["S"] else "0", "1" if got["L"] else "0", "1" if got["RRF"] else "0"))

    print()
    n = stat["n"]
    print("字面臂 hit1        = %d/%d" % (sum(1 for r in rows_out if r[3]), n))
    print("语义臂 hit1        = %d/%d" % (sum(1 for r in rows_out if r[4]), n))
    print("上限（任一臂即算）= %d/%d" % (stat["oracle"], n))
    print("规则 S 语义优先   = %d/%d" % (stat["S"], n))
    print("规则 L 字面优先   = %d/%d" % (stat["L"], n))
    print("规则 RRF k=%d     = %d/%d" % (K, stat["RRF"], n))

    # 逐条看清：规则 S 拿到而字面拿不到的，和反过来丢掉的。
    gain_S = [r[1] for r in rows_out if r[5]["S"] and not r[3]]
    lose_S = [r[1] for r in rows_out if r[3] and not r[5]["S"]]
    gain_R = [r[1] for r in rows_out if r[5]["RRF"] and not r[3]]
    lose_R = [r[1] for r in rows_out if r[3] and not r[5]["RRF"]]
    print()
    print("S 比字面多拿 %d 条：%s" % (len(gain_S), " / ".join(gain_S)))
    print("S 比字面少拿 %d 条：%s" % (len(lose_S), " / ".join(lose_S)))
    print("RRF 比字面多拿 %d 条：%s" % (len(gain_R), " / ".join(gain_R)))
    print("RRF 比字面少拿 %d 条：%s" % (len(lose_R), " / ".join(lose_R)))

    # 规则 S 丢掉的那几条，语义臂到底吐没吐东西 —— 决定「语义空手才回落」这条有没有用
    print()
    for g, q, tgt, lh, sh, got, lit, sem, rrf in rows_out:
        if lh and not got["S"]:
            print("  S 丢掉 [%s] %s → 目标 %s；语义 top3=%s" % (g, q, tgt, sem))

    # ── 加权 RRF 扫描：找有没有「涨得比 RRF 多、且一条字面命中都不丢」的档 ──────────
    # score = w_l/(K+r_l) + w_s/(K+r_s)，缺该臂则该项为 0。
    # 判据两条：① hit1 不低于 RRF；② 「字面单独命中的条」一条都不许丢（不许回退）。
    print()
    print("加权 RRF 扫描（w_l=字面权重，w_s=语义权重，名次只用 top3）")
    print("%5s %5s %4s %8s %8s %8s" % ("w_l", "w_s", "k", "hit1", "较字面跌", "较字面涨"))
    best = None
    for kl in (1, 2, 5, 10, 30, 60, 100):
        for wl in (0.25, 0.5, 0.75, 1.0):
            for ws in (1.0,):
                hits = 0
                lost = 0
                gained = 0
                for r in rows:
                    q = r["query"]
                    tgt = short(r["target"])
                    sem = [short(x) for x in r["top3"]]
                    lit = [short(x) for x in literal.get(q, {}).get("top3", [])]
                    lit_h1 = bool(literal.get(q, {}).get("hit1"))
                    score = {}
                    for i, x in enumerate(lit[:3]):
                        score[x] = score.get(x, 0.0) + wl / (kl + i + 1)
                    for i, x in enumerate(sem[:3]):
                        score[x] = score.get(x, 0.0) + ws / (kl + i + 1)
                    order = sorted(score.keys(), key=lambda x: (-score[x],
                                   lit.index(x) if x in lit else 99,
                                   sem.index(x) if x in sem else 99, x))
                    ok = bool(order) and order[0] == tgt
                    hits += 1 if ok else 0
                    lost += 1 if (lit_h1 and not ok) else 0
                    gained += 1 if (ok and not lit_h1) else 0
                print("%5.2f %5.2f %4d %8d %8d %8d" % (wl, ws, kl, hits, lost, gained))
                if lost == 0 and (best is None or hits > best[3]):
                    best = (wl, ws, kl, hits, lost, gained)
    print()
    print("零回退档里最好的：w_l=%.2f w_s=%.2f k=%d ⇒ hit1=%d，较字面涨 %d、跌 %d"
          % (best[0], best[1], best[2], best[3], best[5], best[4]))


if __name__ == "__main__":
    main()
