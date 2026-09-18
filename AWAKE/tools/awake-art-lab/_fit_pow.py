# -*- coding: utf-8 -*-
"""用**原版逐列实测**拟合端头曲线。数据源＝shape_probe.txt 的真实列，不是我手抄的表。

磨法：把端头曲线族取作
    d(t) = d0 + (1-d0) * (1 - (1-t)^p)，   t∈[0,1] 是收束段内的水平比例
d0 = 端点起始半高比（原版 0.143，**不是 0**）。
扫 (d0, p) 找最小二乘/最大偏差最优。
"""
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
txt = open(os.path.join(HERE, "out", "study", "shape", "shape_probe.txt"),
           encoding="utf-8").read()

# 解析逐列块：i/W  x  ytop ybot 高 ...
rows = []
started = False
for ln in txt.splitlines():
    if "i/W" in ln:
        started = True
        continue
    if not started:
        continue
    m = re.match(r"\s*([\d.]+)\s+(\d+)\s+(-?\d+)\s+(-?\d+)\s+(\d+)", ln)
    if not m:
        if rows:
            break
        continue
    frac = float(m.group(1))
    h = int(m.group(5))
    rows.append((frac, h))

FULL = max(h for _, h in rows)
# 端头＝从 0 起到第一次到满高的那一段
tip = []
for frac, h in rows:
    tip.append((frac, h / FULL))
    if h >= FULL and len(tip) > 1:
        break

print("端点区实测（比例, 半高比）：")
for f, d in tip:
    print("   %.3f  %.3f" % (f, d))

# 把"收束段"归一化成 t∈[0,1]：从尖端列到满高列
t0f = tip[0][0]
t1f = tip[-1][0]
norm = [((f - t0f) / (t1f - t0f), d) for f, d in tip]
print("\n归一化到 t∈[0,1]：")
for t, d in norm:
    print("   %.3f  %.3f" % (t, d))

best = []
for d0x in range(0, 40):
    d0 = d0x / 100.0
    for p10 in range(10, 121):
        p = p10 / 10.0
        worst = 0.0
        for t, d in norm:
            model = d0 + (1.0 - d0) * (1.0 - (1.0 - t) ** p)
            worst = max(worst, abs(model - d))
        best.append((worst, d0, p))
best.sort()
print("\n最优 (最大偏差, d0, p)：")
for w, d0, p in best[:10]:
    print("   偏差 %.4f   d0=%.2f  p=%.1f" % (w, d0, p))
