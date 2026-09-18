# -*- coding: utf-8 -*-
"""形状对照板 v2：**真尺寸**并排，不用放大图。

放大图会骗人（插值把台阶糊成斜线，旧注释就是这么写错的）。
改用：
  · 每个控件**真尺寸**一行，上下叠放，同一左边界对齐；
  · 端头区另出一列**真尺寸 ×3**（浅放大，只看轮廓不看锯齿）；
  · 逐列高以数字并排。
"""
import os
import sys
from PIL import Image, ImageDraw, ImageFont

BASE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, BASE)
import curve_shape as CS

OUT = os.path.join(BASE, "out", "study", "shape")
os.makedirs(OUT, exist_ok=True)

W, H, SS = 110, 35, 4
FILL = (74, 66, 49, 255)
EDGE = (8, 6, 5, 255)
TL = CS.TIP_LEN_PX
ZOOM = 3


def render(pts, w, h, zoom=1):
    big = Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))
    ImageDraw.Draw(big).polygon([(x * SS, y * SS) for x, y in pts],
                                fill=FILL, outline=EDGE)
    im = big.resize((w, h), Image.LANCZOS)
    if zoom > 1:
        im = im.resize((w * zoom, h * zoom), Image.LANCZOS)
    return im


def old_bez(w, h, tip_len, tip_h, seg=4):
    cy = h * 0.5
    th = max(0.5, tip_h * h * 0.5)
    tl = max(0.0, min(tip_len, w * 0.5 - 1.0))
    n = max(2, seg)
    mid = w * 0.5

    def bz(p0, c, p1, k):
        o = []
        for i in range(k + 1):
            t = i / float(k)
            mt = 1 - t
            o.append((mt * mt * p0[0] + 2 * mt * t * c[0] + t * t * p1[0],
                      mt * mt * p0[1] + 2 * mt * t * c[1] + t * t * p1[1]))
        return o
    chain = [((0.0, cy - th), (0.0, 0.0), (tl, 0.0), n),
             ((tl, 0.0), (mid, 0.0), (w - tl, 0.0), 1),
             ((w - tl, 0.0), (w, 0.0), (w, cy - th), n),
             ((w, cy - th), (w, cy), (w, cy + th), 1),
             ((w, cy + th), (w, h), (w - tl, h), n),
             ((w - tl, h), (mid, h), (tl, h), 1),
             ((tl, h), (0.0, h), (0.0, cy + th), n),
             ((0.0, cy + th), (0.0, cy), (0.0, cy - th), 1)]
    pts = []
    for i, (p0, c, p1, k) in enumerate(chain):
        s = bz(p0, c, p1, k)
        pts += s if i == 0 else s[1:]
    if pts and abs(pts[-1][0] - pts[0][0]) < 1e-12 and abs(pts[-1][1] - pts[0][1]) < 1e-12:
        pts.pop()
    return pts


def old_cham(w, h, cut, inset=0):
    x0, y0, x1, y1 = inset, inset, w - 1 - inset, h - 1 - inset
    c = max(0.0, cut - 0.586 * inset)
    if c <= 0.01:
        return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]
    return [(x0 + c, y0), (x1 - c, y0), (x1, y0 + c), (x1, y1 - c),
            (x1 - c, y1), (x0 + c, y1), (x0, y1 - c), (x0, y0 + c)]


rows = [
    ("旧 · 主按钮（贝塞尔半圆头）", W, old_bez(W, H, TL, 0.16)),
    ("新 · 主按钮（统一曲线）",     W, CS.tip_curve_pts(W, H, TL)),
    ("旧 · 次按钮（八边形 cut=8）", 100, old_cham(100, H, 8, 0)),
    ("新 · 次按钮（统一曲线）",     100, CS.tip_curve_pts(100, H, TL)),
    ("新 · 页签（统一曲线）",       105, CS.tip_curve_pts(105, H, TL)),
]

PADL, PADT = 20, 18
LBL = 24
GAP = 12
ZW = 26 * ZOOM            # 端头放大区宽
ZW_H = H * ZOOM
left_w = W + 40
right_w = ZW + 210
bw = PADL * 2 + left_w + right_w
bh = PADT * 2 + len(rows) * (LBL + max(H, ZW_H) + GAP)

board = Image.new("RGB", (bw, bh), (24, 24, 28))
d = ImageDraw.Draw(board)
try:
    f1 = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 13)
    f2 = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 11)
except Exception:
    f1 = f2 = ImageFont.load_default()

y = PADT
for label, w, pts in rows:
    d.text((PADL, y + 3), label, fill=(205, 205, 215), font=f1)
    ty = y + LBL

    # 真尺寸
    im = render(pts, w, H)
    board.paste(im, (PADL, ty), im)
    d.text((PADL, ty + H + 2), "%dx%d 真尺寸" % (w, H),
           fill=(110, 110, 120), font=f2)

    # 端头 ×3（真尺寸的邻域放大，只用来看轮廓）
    z = render(pts, w, H, zoom=ZOOM).crop((0, 0, ZW, ZW_H))
    zy = ty - (ZW_H - H) // 2
    board.paste(z, (PADL + left_w, zy), z)
    d.rectangle([PADL + left_w - 1, zy - 1, PADL + left_w + ZW, zy + ZW_H],
                outline=(80, 80, 92))
    d.text((PADL + left_w, zy - 15), "端头 ×%d（浅放大，只看走向）" % ZOOM,
           fill=(110, 110, 120), font=f2)

    # 逐列高
    px = im.load()
    hs = []
    for x in range(min(22, w)):
        col = [yy for yy in range(H) if px[x, yy][3] > 127]
        hs.append((max(col) - min(col) + 1) if col else 0)
    tx = PADL + left_w + ZW + 12
    d.text((tx, ty - 2), "端头逐列高", fill=(150, 150, 162), font=f2)
    d.text((tx, ty + 16), " ".join("%2d" % v for v in hs),
           fill=(120, 205, 145), font=f2)

    y += LBL + max(H, ZW_H) + GAP

p = os.path.join(OUT, "shape_pipeline_board2.png")
board.save(p)
print("saved:", p, board.size)
