# -*- coding: utf-8 -*-
"""核对原版参考图：真实尺寸、内容边界、逐列高（含透明边距）。"""
import os
from PIL import Image

BASE = os.path.dirname(os.path.abspath(__file__))
P = os.path.join(BASE, "..", "awake-ui-lab", "out", "atlas", "sprites",
                 "General__Button__main_button_regular.png")

im = Image.open(P)
print("mode=%s  size=%s" % (im.mode, im.size))
im = im.convert("RGBA")
w, h = im.size
a = im.split()[-1]
px = a.load()

# 内容包围盒（alpha>8）
xs = [x for x in range(w) if any(px[x, y] > 8 for y in range(h))]
ys = [y for y in range(h) if any(px[x, y] > 8 for x in range(w))]
print("内容 bbox: x %d..%d  y %d..%d  → %dx%d"
      % (min(xs), max(xs), min(ys), max(ys),
         max(xs) - min(xs) + 1, max(ys) - min(ys) + 1))

x0, y0, x1, y1 = min(xs), min(ys), max(xs), max(ys)

# 逐列高（相对内容）
print("\n逐列高（从内容左边界起，前 30 列）:")
line = []
for i in range(30):
    x = x0 + i
    if x > x1:
        break
    col = [y for y in range(h) if px[x, y] > 127]
    line.append("%d" % (len(col) if col else 0))
print("  " + " ".join(line))

print("\n逐列 [最高,最低] 像素（前 20 列）:")
for i in range(20):
    x = x0 + i
    if x > x1:
        break
    col = [y for y in range(h) if px[x, y] > 127]
    if col:
        print("  rel x=%2d  abs x=%3d  y %3d..%3d  高 %2d"
              % (i, x, min(col), max(col), max(col) - min(col) + 1))

# 满高（内容高）在哪列达到
ch = y1 - y0 + 1
tf = None
for i in range(x1 - x0 + 1):
    col = [y for y in range(h) if px[x0 + i, y] > 127]
    if col and (max(col) - min(col) + 1) >= ch:
        tf = i
        break
print("\n内容高 = %d，满高列 rel x = %s" % (ch, tf))
