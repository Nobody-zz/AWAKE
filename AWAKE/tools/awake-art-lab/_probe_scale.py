# -*- coding: utf-8 -*-
"""定案：原版那条「缓—陡—缓」的三段式，缩到我们 35 高的按钮上，
差别到底看不看得见？

做法：把原版 14 列归一化曲线**直接映射**到我们的 16px 端头 × 35 高，
逐列算出应有的像素高，再跟我们的 p=1.12 曲线逐列比。
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import curve_shape as CS

RAW = [6, 8, 10, 12, 14, 18, 20, 24, 28, 32, 34, 36, 38, 40, 42]
FULL = 42.0
REF_TL = 14.0            # 原版端头 14 列

HALF = CS.tip_len_for(110)   # 16.0
H = 35.0

print("我们：端头 %.0fpx，按钮高 %.0f → 半高 %.1f" % (HALF, H, H / 2))
print("原版：端头 14px，按钮高 42 → 半高 21")
print()

# 原版归一化后，映射到我们的端头上
print("把原版逐列高映射到我们的端头（x 从最外端往内，每列 %.2fpx）:"
      % (HALF / REF_TL))
print("  原版列  原版高  归一d    我们的x(distance from tip)  我们的半高")
rows = []
for i, v in enumerate(RAW):
    d = v / FULL
    x_tip = i * (HALF / REF_TL)          # 距最外端的距离
    our_half = d * (H / 2)
    rows.append((i, v, d, x_tip, our_half))
    print("   %2d      %2d    %.3f    %5.2f px                 %5.2f px"
          % (i, v, d, x_tip, our_half))

# 我们的曲线 p=1.12 在同一批 x 处的半高
print()
print("对照：我们 CURVE_POW=1.12 曲线在同样 x 处的半高")
p_now = CS.CURVE_POW
d0_now = CS.CURVE_D0
print("  x(距最外)  原版映射   p=1.12   p=1.40   p=1.70   差(当前-原版)")
for pi in (1.12, 1.40, 1.70):
    pass

for i, v, d, x_tip, our_half in rows:
    t = x_tip / HALF
    vals = []
    for p in (1.12, 1.40, 1.70, 2.20):
        dd = d0_now + (1 - d0_now) * (1 - (1 - t) ** p)
        vals.append(dd * H / 2)
    print("   %5.2f      %5.2f    %5.2f   %5.2f   %5.2f   %+5.2f"
          % (x_tip, our_half, vals[0], vals[1], vals[2], vals[0] - our_half))

# 换算成像素列（我们的端头 16px，实际只有 16 列）
print()
print("真正的答案——**逐 1px 列**（我们的端头只有 16 列）:")
print("  列x   原版映射高     p=1.12高     差   | 原版四舍五入  p=1.12四舍五入")
for x in range(0, 17):
    t = x / HALF
    # 原版映射：找 RAW 里的位置（线性插值）
    pos = t * REF_TL
    i0 = int(math.floor(pos))
    frac = pos - i0
    if i0 >= len(RAW) - 1:
        ref_d = 1.0
    else:
        ref_d = (RAW[i0] / FULL) * (1 - frac) + (RAW[i0 + 1] / FULL) * frac
    cur_d = d0_now + (1 - d0_now) * (1 - (1 - t) ** p_now)
    rh = ref_d * H
    ch = cur_d * H
    print("  %3d     %5.2f        %5.2f     %+5.2f |    %2d          %2d"
          % (x, rh, ch, ch - rh, round(rh), round(ch)))
