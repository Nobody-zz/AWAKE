# -*- coding: utf-8 -*-
"""按原版**内容坐标**的 14 列全数据，重定 CURVE_POW。

原版（内容坐标，满高 42，端头 14 列）：
    x=0..13  高 6,8,10,12,14,18,20,24,28,32,34,36,38,40   (x=14 起 42 满高)
归一化：t = x/14, d = 高/42
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

RAW = [6, 8, 10, 12, 14, 18, 20, 24, 28, 32, 34, 36, 38, 40, 42]
FULL = 42.0
TL = len(RAW) - 1                      # 14 = 满高列相对端头的偏移

# 数据点：(t, d)  —— t = (x+0.5)/TL 取半格，更贴"列中心"的物理含义
DATA = [((i + 0.5) / TL, v / FULL) for i, v in enumerate(RAW)]


def d_of(t, p, d0):
    t = max(0.0, min(1.0, t))
    return d0 + (1.0 - d0) * (1.0 - (1.0 - t) ** p)


print("原版数据（t, d）:")
for t, d in DATA:
    print("   t=%.3f  d=%.3f" % (t, d))
print()

best = None
for pi in range(60, 300):
    p = pi / 100.0
    for d0i in range(0, 40):
        d0 = d0i / 100.0
        if d0 >= 0.5:
            continue
        err = 0.0
        for t, d in DATA:
            err += (d_of(t, p, d0) - d) ** 2
        err = math.sqrt(err / len(DATA))
        if best is None or err < best[0]:
            best = (err, p, d0)

print("全 15 点最优拟合：rmse=%.4f  p=%.2f  d0=%.2f" % best)

# 逐点误差
err, p, d0 = best
print("\n逐点:")
for t, d in DATA:
    print("   t=%.3f  实测 %.3f  拟合 %.3f  差 %+.3f"
          % (t, d, d_of(t, p, d0), d_of(t, p, d0) - d))

# 中段陡度：原版 14→18 那一步（+4，而其余 +2）
print("\n中段陡度检查（逐列增量 ÷2 看是否均匀）:")
for i in range(1, len(RAW)):
    print("   x=%2d→%2d  +%d  %s" % (i - 1, i, RAW[i] - RAW[i - 1],
                                     "  ← 双倍步长" if RAW[i] - RAW[i - 1] >= 4 else ""))
