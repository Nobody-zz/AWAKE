# -*- coding: utf-8 -*-
"""定案后的形状自检：三个尺寸各查一遍闭合／方向／逐列高／与原版对照。"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image, ImageDraw
from curve_shape import tip_curve_pts, tip_len_for, CURVE_D0, CURVE_POW, TIP_LEN_PX

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out", "study", "shape")
SS = 8
lines = []
lines.append("定案形状自检  CURVE_D0=%.2f  CURVE_POW=%.2f  TIP_LEN=%.0fpx" % (CURVE_D0, CURVE_POW, TIP_LEN_PX))

for name, W, H in [("主按钮", 110, 35), ("次按钮", 100, 35), ("页签", 105, 35)]:
    pts = tip_curve_pts(W, H)
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    # 闭合性：首尾是否分别在两侧钝口
    first, last = pts[0], pts[-1]
    m = Image.new("L", (W * SS, H * SS), 0)
    ImageDraw.Draw(m).polygon([(x * SS, y * SS) for x, y in pts], fill=255)
    m = m.resize((W, H), Image.LANCZOS)
    cols = []
    for x in range(W):
        c = [y for y in range(H) if m.getpixel((x, y)) > 127]
        cols.append((max(c) - min(c) + 1) if c else 0)
    xf = next((x for x, c in enumerate(cols) if c >= H - 0.5), W)
    filled = sum(1 for v in cols if v > 0)
    lines.append("")
    lines.append("【%s %dx%d】%d 顶点  x∈[%.1f,%.1f] y∈[%.1f,%.1f]"
                 % (name, W, H, len(pts), min(xs), max(xs), min(ys), max(ys)))
    lines.append("   首点 %s   尾点 %s" % (str(first), str(last)))
    lines.append("   有内容的列 %d/%d   首次满高 @x=%d（%d%%）"
                 % (filled, W, xf, round(xf / float(W) * 100)))
    lines.append("   端头逐列高: %s" % ",".join(str(cols[i]) for i in range(min(18, W))))
    # 镜像自检
    ok = all(abs(cols[i] - cols[W - 1 - i]) <= 1 for i in range(W // 2))
    lines.append("   左右镜像 %s" % ("OK" if ok else "FAIL"))
    # 连通性（每列都有内容，无断列）
    gaps = [i for i, v in enumerate(cols) if v == 0]
    lines.append("   断列 %s" % ("无" if not gaps else str(gaps[:10])))

txt = "\n".join(lines)
open(os.path.join(OUT, "final_shape_check.txt"), "w", encoding="utf-8").write(txt)
print(txt)
