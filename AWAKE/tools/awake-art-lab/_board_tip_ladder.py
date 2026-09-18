# -*- coding: utf-8 -*-
"""端头长度并排对照（**真尺寸**，无铸件无孔）。

目的：定"端头到底该多长"。判据不是数字，是它在 110×35 上读起来
是"一块板"还是"一片梭子"。

一并回答一个更激进的问题：**短到某个程度之后，它跟圆角矩形还有区别吗？**
⇒ 所以最后一档放一个纯圆角矩形（r=3）当参照物。
"""
import os
import sys
from PIL import Image, ImageDraw, ImageFont

BASE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, BASE)
import curve_shape as CS

OUT = os.path.join(BASE, "out", "study", "shape")
os.makedirs(OUT, exist_ok=True)

W, H, SS = 110, 35, 6
FILL = (86, 76, 57, 255)
EDGE = (10, 8, 6, 255)


def render_pts(pts, w, h):
    big = Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))
    ImageDraw.Draw(big).polygon([(x * SS, y * SS) for x, y in pts],
                                fill=FILL, outline=EDGE)
    return big.resize((w, h), Image.LANCZOS)


def render_round(w, h, r):
    big = Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))
    ImageDraw.Draw(big).rounded_rectangle([0, 0, w * SS - 1, h * SS - 1],
                                          radius=r * SS, fill=FILL,
                                          outline=EDGE, width=SS)
    return big.resize((w, h), Image.LANCZOS)


LANES = [("6px", 6.0), ("8px", 8.0), ("9px", 9.0),
         ("10px", 10.0), ("12px", 12.0), ("16px（当前）", 16.0)]

PADL, PADT = 22, 20
LBL = 22
RGAP = 16

# 每行：一个长度 × 三控件（110/100/105）
rows = []
for name, tl in LANES:
    rows.append((name, tl))
rows.append(("圆角矩形 r=3（参照）", None))

bw = PADL * 2 + 110 + 30 + 100 + 30 + 105 + 120
bh = PADT * 2 + len(rows) * (LBL + H + RGAP)

board = Image.new("RGB", (bw, bh), (22, 22, 26))
d = ImageDraw.Draw(board)
try:
    f1 = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 13)
    f2 = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 11)
except Exception:
    f1 = f2 = ImageFont.load_default()

# 表头
d.text((PADL, 4), "端头长度", fill=(150, 150, 162), font=f2)
d.text((PADL + 40, 4), "主 110", fill=(150, 150, 162), font=f2)
d.text((PADL + 40 + 110 + 30, 4), "次 100", fill=(150, 150, 162), font=f2)
d.text((PADL + 40 + 110 + 30 + 100 + 30, 4), "页签 105", fill=(150, 150, 162), font=f2)
d.text((PADL + 40 + 110 + 30 + 100 + 30 + 105 + 20, 4), "端头占宽", fill=(150, 150, 162), font=f2)

y = PADT
for name, tl in rows:
    d.text((PADL, y + 8), name, fill=(205, 205, 215), font=f1)
    x = PADL + 40
    for w in (110, 100, 105):
        if tl is None:
            im = render_round(w, H, 3)
        else:
            im = render_pts(CS.tip_curve_pts(w, H, tl), w, H)
        board.paste(im, (x, y), im)
        x += w + 30
    if tl is not None:
        d.text((x, y + 8), "%.1f%%" % (100 * tl / 110.0),
               fill=(120, 205, 145), font=f1)
    y += LBL + H + RGAP

p = os.path.join(OUT, "tip_len_ladder.png")
board.save(p)
print("saved:", p, board.size)
