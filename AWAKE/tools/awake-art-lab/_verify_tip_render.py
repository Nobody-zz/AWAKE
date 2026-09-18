# -*- coding: utf-8 -*-
"""**光栅化**验证端头：把本曲线渲成遮罩，逐列量半高，跟原版实测比。

⚠️ 为什么不再用"折角"当判据：
   本项目按钮端头只有 **7.6px 宽 × 17.5px 高**，整段曲线在 ~11 个顶点里画完。
   任何多边形逼近在这个尺度上都会有十几度的弦夹角 —— 那个数**不是缺陷**，
   是分辨率。真正决定"看不看得见折"的是**光栅化后的轮廓**。
   ⇒ 判据换成：遮罩逐列半高 vs 原版逐列半高，最大差几像素。
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image, ImageDraw

from curve_shape import tip_curve_pts, tip_len_for

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "out", "study", "shape")
os.makedirs(OUT, exist_ok=True)

W, H, SS = 110, 35, 8
lines = []

# 原版实测（232×42 → 比例）：x 从端头起每 4px 一列，半高/全高
REF_COLS_PX = [(0, 6), (4, 14), (8, 28), (12, 38), (16, 42)]
REF_FULL = 42.0
REF_TIP_W = 16.0
REF_W = 232.0


def draw(mode, tl, label):
    pts = tip_curve_pts(W, H, tl)
    m = Image.new("L", (W * SS, H * SS), 0)
    ImageDraw.Draw(m).polygon([(x * SS, y * SS) for x, y in pts], fill=255)
    m = m.resize((W, H), Image.LANCZOS)

    # 逐列量
    lines.append("=== %s   端头长 %.1fpx   点数 %d" % (label, tl, len(pts)))
    cols = []
    for x in range(W):
        col = [y for y in range(H) if m.getpixel((x, y)) > 127]
        cols.append((max(col) - min(col) + 1) if col else 0)

    # 找到"第一次到满高"的列
    first_full = next((x for x, c in enumerate(cols) if c >= H - 0.5), W)
    lines.append("      首次满高 @x=%d（原版按宽度比例折算 ≈%.1f）"
                 % (first_full, REF_TIP_W / REF_W * W))

    # 折算：原版每 4px 一列，换算到本项目宽度
    scale = W / REF_W
    lines.append("      列半高对比（原版折算 vs 本曲线，满高=%d）" % H)
    lines.append("        原x  折算x   原版高   本曲线高   差(px)")
    worst = 0.0
    for rx, rh in REF_COLS_PX:
        x = int(round(rx * scale))
        x = min(x, W - 1)
        got = cols[x]
        exp = rh / REF_FULL * H
        d = got - exp
        worst = max(worst, abs(d))
        lines.append("       %4d  %5d   %6.1f   %7d   %+6.1f" % (rx, x, exp, got, d))
    lines.append("      · 最大列高差 %.1f px   %s"
                 % (worst, "OK(<1.5px)" if worst < 1.5 else "偏大"))
    lines.append("")
    return m, cols


m1, c1 = draw("curve", tip_len_for(W), "曲线端头（本轮）")
m2, c2 = draw("refscale", 16.0 * (W / REF_W), "曲线端头（按原版比例 6.9%）")

# 并排图：上=本曲线，中=原版端头区放大，下=八边形（旧）
from artkit import poly_mask, chamfer_pts
old = poly_mask((W, H), chamfer_pts(W, H, 8, 2), ss=SS)

board = Image.new("RGB", (W * 6, H * 6 * 3 + 40), (24, 24, 26))
for i, (mm, yy) in enumerate([(m1, 0), (old, H * 6 + 20)]):
    big = mm.resize((W * 6, H * 6), Image.NEAREST).convert("RGB")
    board.paste(big, (0, yy))
board.save(os.path.join(OUT, "tip_curve_vs_octagon.png"))

# 叠图：本曲线(红边) vs 八边形(绿边)
ov = Image.new("RGB", (W * 6, H * 6), (0, 0, 0))
A = m1.resize((W * 6, H * 6), Image.NEAREST).load()
B = old.resize((W * 6, H * 6), Image.NEAREST).load()
ov = Image.new("RGB", (W * 6, H * 6), (0, 0, 0))
po = ov.load()
for yy in range(H * 6):
    for xx in range(W * 6):
        a = A[xx, yy] > 127
        b = B[xx, yy] > 127
        if a and b:
            po[xx, yy] = (220, 220, 220)
        elif a:
            po[xx, yy] = (200, 60, 60)
        elif b:
            po[xx, yy] = (60, 200, 90)
ov.save(os.path.join(OUT, "tip_overlay.png"))

lines.append("已出图：tip_curve_vs_octagon.png（上=曲线 / 下=旧八边形）、tip_overlay.png")
txt = "\n".join(lines)
open(os.path.join(OUT, "tip_render_check.txt"), "w", encoding="utf-8").write(txt)
print(txt)
