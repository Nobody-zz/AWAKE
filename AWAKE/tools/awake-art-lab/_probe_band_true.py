# -*- coding: utf-8 -*-
"""沿**轮廓法线**量 `_frame` 的真实环带宽度（不是竖直量）。

竖直量在斜边上必然偏厚 —— 那是度量偏差，不是缺陷。
判据：带宽 = 环带面积 / 环带中线的长度，或者逐点沿法线量。
这里用**逐点沿法线**：对轮廓上每个点，向内外各走，看它是不是落在环带里。
简化：算"环带面积 ÷ 外轮廓周长"，跟 inset 比。
"""
import math
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image, ImageDraw, ImageChops
import curve_shape as CS
import artkit as K

W, H = 110, 35
TL = 9.0
SS = 8


def area_of(pts, w, h):
    m = Image.new("L", (w * SS, h * SS), 0)
    ImageDraw.Draw(m).polygon([(x * SS, y * SS) for x, y in pts], fill=255)
    m = m.resize((w, h), Image.LANCZOS)
    return sum(1 for v in m.getdata() if v > 127)


def perimeter(pts):
    n = len(pts)
    s = 0.0
    for i in range(n):
        x0, y0 = pts[i]
        x1, y1 = pts[(i + 1) % n]
        s += math.hypot(x1 - x0, y1 - y0)
    return s


print("环带真实宽度（面积 ÷ 外周长，对照 inset）")
print("%8s %10s %10s %10s" % ("inset", "环带面积", "外周长", "折算带宽"))
for bev in (1, 2, 4, 7):
    o = K.chamfer_pts(W, H, TL, 0)
    i = K.chamfer_pts(W, H, TL, bev)
    a = area_of(o, W, H) - area_of(i, W, H)
    p = perimeter(o)
    print("%8d %10d %10.1f %10.2f" % (bev, a, p, a / p))

print()
print("⇒ 折算带宽若 ≈ inset，说明环带沿法线等宽（正确）；")
print("  若明显 > inset，说明端头处偏宽。")
print()
print("对照：八边形（旧）")
for bev in (1, 2, 4, 7):
    def cham_old(w, h, cut, inset):
        x0, y0, x1, y1 = inset, inset, w - 1 - inset, h - 1 - inset
        c = max(0.0, cut - 0.586 * inset)
        if c <= 0.01:
            return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]
        return [(x0 + c, y0), (x1 - c, y0), (x1, y0 + c), (x1, y1 - c),
                (x1 - c, y1), (x0 + c, y1), (x0, y1 - c), (x0, y0 + c)]
    o = cham_old(W, H, 8, 0)
    i = cham_old(W, H, 8, bev)
    a = area_of(o, W, H) - area_of(i, W, H)
    p = perimeter(o)
    print("%8d %10d %10.1f %10.2f" % (bev, a, p, a / p))
