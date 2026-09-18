# -*- coding: utf-8 -*-
"""诊断：shade 图到底长什么样（以及 material 的曝光有没有被 clamp 卡住）。"""
import os
import sys

from PIL import Image, ImageDraw, ImageStat, ImageChops, ImageFilter

import make_button_primary as mb

mb.build_states  # noqa

ww, hh = mb.W * mb.SS, mb.H * mb.SS
shape = Image.new("L", (ww, hh), 0)
ImageDraw.Draw(shape).rounded_rectangle((0, 0, ww - 1, hh - 1),
                                        radius=mb.RADIUS * mb.SS, fill=255)
frame = mb.rounded_ring((ww, hh), 0, mb.FRAME * mb.SS, mb.RADIUS * mb.SS)
seam = mb.rounded_ring((ww, hh), mb.GOLD_INSET * mb.SS, mb.SEAM * mb.SS, 1)
dist = mb.inward_field(shape, int((mb.FRAME + mb.SEAM) * mb.SS) + 1)
shade = mb.budget_shade((ww, hh), mb.SS, dist, seam)

print("SS=%d  FRAME*SS=%g  maxd=%d" % (mb.SS, mb.FRAME * mb.SS, int((mb.FRAME + mb.SS) * mb.SS) + 1))
print("dist 中心列 y0..48:", [dist.getpixel((ww // 2, y)) for y in range(0, 49, 2)])
print("shade中心列 y0..48:", [shade.getpixel((ww // 2, y)) for y in range(0, 49, 2)])
print("shade中心列 末8行 :", [shade.getpixel((ww // 2, hh - 1 - y)) for y in range(8)])
print("shade 1x 缩回后的纵向均值:")
s1 = shade.resize((mb.W, mb.H), Image.LANCZOS)
for y in range(mb.H):
    row = [s1.getpixel((x, y)) for x in range(22, 88)]
    print("  y %2d  mean %5.1f" % (y, sum(row) / len(row)))
