# -*- coding: utf-8 -*-
"""**按钮形状对照板** —— 现在 vs 建议，三个控件，真尺寸 ＋ 放大。

只画**形状**（纯剪影灰），不画材质与颜色 —— 甲方要求先定形。

出：out/study/shape/shape_board.png（宽 1820）
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image, ImageDraw

from curve_shape import tip_curve_pts, tip_len_for
from artkit import chamfer_pts

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "out", "study", "shape")
os.makedirs(OUT, exist_ok=True)

CW = 1820
BG = (26, 26, 28)
FG = (225, 225, 228)
DIM = (145, 145, 150)
SHAPE = (150, 148, 145)          # 纯形灰
SHAPE_HI = (205, 200, 190)
BAD = (150, 95, 90)
SS = 8
Z = 4                            # 放大倍数

SPECS = [
    ("主按钮", 110, 35, "old_semicircle"),
    ("次按钮", 100, 35, "old_octagon"),
    ("页签",   105, 35, "old_octagon"),
]


def current_pts(kind, w, h):
    if kind == "old_octagon":
        cut = 8 if w == 100 else 7
        return chamfer_pts(w, h, cut, 2)
    # 主按钮：近半圆二次贝塞尔（简化重画，形状等价 —— 只为对照）
    cy = h * 0.5
    th = 0.16 * h * 0.5
    tl = 16.0
    pts = []
    # 用圆角矩形近似它现在的"胖"端头
    from artkit import rect_pts
    r = min(tl, h * 0.5)
    # 直接借用圆角：把端头做成半径 = 半高的大圆角
    steps = 12
    import math
    for i in range(steps + 1):
        a = math.pi / 2 * i / steps
        pts.append((r - r * math.cos(a), cy - r * math.sin(a)))
    pts.append((w - r, 0.0))
    for i in range(steps + 1):
        a = math.pi / 2 * i / steps
        pts.append((w - r + r * math.sin(a), cy - r * math.cos(a)))
    pts.append((w - r, h))
    for i in range(steps + 1):
        a = math.pi / 2 * i / steps
        pts.append((w - r + r * math.cos(a), cy + r * math.sin(a)))
    pts.append((r, h))
    for i in range(steps + 1):
        a = math.pi / 2 * i / steps
        pts.append((r - r * math.sin(a), cy + r * math.cos(a)))
    return pts


def proposed_pts(kind, w, h):
    return tip_curve_pts(w, h)          # 本轮曲线，三个控件共用


def render(pts, w, h):
    m = Image.new("L", (w * SS, h * SS), 0)
    if len(pts) >= 3:
        ImageDraw.Draw(m).polygon([(x * SS, y * SS) for x, y in pts], fill=255)
    return m.resize((w, h), Image.LANCZOS)


PAD = 30
TOP = 76
# 布局：每个控件一行，左边"现在"，右边"建议"
ROW_H = 35 * Z + 62
H = TOP + ROW_H * len(SPECS) + PAD
board = Image.new("RGB", (CW, H), BG)
d = ImageDraw.Draw(board)

d.text((PAD, 22), "按钮形状对照 —— 只画「形」，不画材质与颜色", fill=FG)
d.text((PAD, 42), "左＝现在　　右＝建议（三控件统一为同一条连续斜线）　　真尺寸见每行末尾小图", fill=DIM)

LABW = 120
x_now = PAD + LABW
x_new = PAD + LABW + 110 * Z + 90

for i, (name, w, h, kind) in enumerate(SPECS):
    y = TOP + i * ROW_H
    d.text((PAD, y + 35 * Z // 2 - 14), name, fill=FG)

    cur = render(current_pts(kind, w, h), w, h)
    new = render(proposed_pts(kind, w, h), w, h)

    board.paste(cur.convert("RGB").point(lambda v: 0), (0, 0)) if False else None
    cb = Image.new("RGB", cur.size, BG)
    cb.paste(Image.new("RGB", cur.size, SHAPE), (0, 0), cur)
    nb = Image.new("RGB", new.size, BG)
    nb.paste(Image.new("RGB", new.size, SHAPE_HI), (0, 0), new)

    board.paste(cb.resize((w * Z, h * Z), Image.NEAREST), (x_now, y))
    board.paste(nb.resize((w * Z, h * Z), Image.NEAREST), (x_new, y))

    d.text((x_now, y - 18), "现在", fill=BAD)
    d.text((x_new, y - 18), "建议", fill=(150, 210, 160))

    # 真尺寸小图
    sx = x_new + w * Z + 40
    d.text((sx, y + h * Z // 2 - 20), "真尺寸 1×：", fill=DIM)
    board.paste(cb, (sx + 92, y + h * Z // 2 - h // 2))
    board.paste(nb, (sx + 92 + w + 16, y + h * Z // 2 - h // 2))

board.save(os.path.join(OUT, "shape_board.png"))
print("saved", board.size)
