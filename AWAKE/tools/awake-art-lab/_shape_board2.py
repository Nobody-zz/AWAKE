# -*- coding: utf-8 -*-
"""形状对照板 v2 —— **用真函数**画"现在"（不再手搓近似）。

主按钮当前轮廓：`make_button_primary.end_taper_points`
次/页签当前轮廓：`artkit.chamfer_pts`
建议：`curve_shape.tip_curve_pts`
"""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image, ImageDraw

import make_button_primary as MB
from artkit import chamfer_pts
from curve_shape import tip_curve_pts

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "out", "study", "shape")
CW, BG = 1820, (26, 26, 28)
SS, Z = 8, 3

SPECS = [
    ("主按钮 110×35", 110, 35, "primary"),
    ("次按钮 100×35", 100, 35, "chamfer8"),
    ("页签 105×35",   105, 35, "chamfer7"),
]


def now_pts(kind, w, h):
    if kind == "primary":
        return MB.end_taper_points(float(w), float(h), MB.TIP_LEN, MB.TIP_H)
    cut = 8 if kind == "chamfer8" else 7
    return chamfer_pts(w, h, cut, 2)


def render(pts, w, h):
    m = Image.new("L", (w * SS, h * SS), 0)
    if len(pts) >= 3:
        ImageDraw.Draw(m).polygon([(x * SS, y * SS) for x, y in pts], fill=255)
    return m.resize((w, h), Image.LANCZOS)


def tint(mask, color):
    im = Image.new("RGB", mask.size, BG)
    im.paste(Image.new("RGB", mask.size, color), (0, 0), mask)
    return im


PAD, TOP = 30, 74
ROW_H = 35 * Z + 56
H = TOP + ROW_H * len(SPECS) + PAD
board = Image.new("RGB", (CW, H), BG)
d = ImageDraw.Draw(board)
d.text((PAD, 20), "按钮形状对照（真轮廓）—— 只画形，不画材质／颜色", fill=(225, 225, 228))
d.text((PAD, 40), "左＝现在　　右＝建议（三控件统一为同一条连续斜线）", fill=(145, 145, 150))

x_now = PAD + 130
x_new = x_now + 115 * Z + 80

for i, (name, w, h, kind) in enumerate(SPECS):
    y = TOP + i * ROW_H
    d.text((PAD, y + h * Z // 2 - 8), name, fill=(225, 225, 228))

    a = tint(render(now_pts(kind, w, h), w, h), (140, 138, 135))
    b = tint(render(tip_curve_pts(w, h), w, h), (208, 202, 190))

    board.paste(a.resize((w * Z, h * Z), Image.NEAREST), (x_now, y))
    board.paste(b.resize((w * Z, h * Z), Image.NEAREST), (x_new, y))
    d.text((x_now, y - 16), "现在", fill=(200, 110, 100))
    d.text((x_new, y - 16), "建议", fill=(140, 205, 150))

    sx = x_new + w * Z + 50
    d.text((sx, y + h * Z // 2 - 22), "真尺寸 1×", fill=(145, 145, 150))
    board.paste(a, (sx, y + h * Z // 2 - 2))
    board.paste(b, (sx, y + h * Z // 2 + h + 8))

board.save(os.path.join(OUT, "shape_board2.png"))
print("saved", board.size)
