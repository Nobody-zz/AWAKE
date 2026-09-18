# -*- coding: utf-8 -*-
"""对着 1× 逐列量三种轮廓，看建议版到底哪里不对。"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image, ImageDraw

import make_button_primary as MB
from artkit import chamfer_pts
from curve_shape import tip_curve_pts

SS = 8
W, H = 110, 35


def render(pts):
    m = Image.new("L", (W * SS, H * SS), 0)
    ImageDraw.Draw(m).polygon([(x * SS, y * SS) for x, y in pts], fill=255)
    return m.resize((W, H), Image.LANCZOS)


def cols(m):
    out = []
    for x in range(W):
        c = [y for y in range(H) if m.getpixel((x, y)) > 127]
        out.append((max(c) - min(c) + 1) if c else 0)
    return out


a = cols(render(MB.end_taper_points(float(W), float(H), MB.TIP_LEN, MB.TIP_H)))
b = cols(render(chamfer_pts(W, H, 8, 2)))
c = cols(render(tip_curve_pts(W, H)))

print("x    现在(主)  现在(八边)  建议")
for x in range(0, 24):
    print("%3d   %6d   %8d   %6d" % (x, a[x], b[x], c[x]))
print("...")
for x in range(W - 24, W):
    print("%3d   %6d   %8d   %6d" % (x, a[x], b[x], c[x]))

# 关键指标
def first_full(col):
    return next((i for i, v in enumerate(col) if v >= H), W)

print("\n满高列：现在(主)=%d  现在(八边)=%d  建议=%d" % (first_full(a), first_full(b), first_full(c)))
print("中段列高：现在(主)=%d  现在(八边)=%d  建议=%d" % (a[55], b[55], c[55]))
