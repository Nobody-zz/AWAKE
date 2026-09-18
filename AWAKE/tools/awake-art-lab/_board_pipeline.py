# -*- coding: utf-8 -*-
"""形状对照板：**改造前 vs 改造后**，三控件一行。

旧版现场重算（仓库里没留旧 png）：
  · 主按钮：二次贝塞尔"半圆头"（旧 end_taper_points 的实现，此处独立复刻）
  · 次按钮/页签：八边形 45° 切角（旧 chamfer_pts）

新：三控件同一曲线（curve_shape）。

版式：
  第 1 行  旧 · 主按钮
  第 2 行  新 · 主按钮
  第 3 行  旧 · 次按钮 / 页签
  第 4 行  新 · 次按钮 / 页签
  —— 每行左端各配一张**端头放大 8×**（NEAREST，看台阶不糊）
"""
import os
import sys
from PIL import Image, ImageDraw, ImageFont

BASE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, BASE)

import artkit as K
import curve_shape as CS

OUT = os.path.join(BASE, "out", "study", "shape")
os.makedirs(OUT, exist_ok=True)

W, H = 110, 35
SS = 4
ZOOM = 8

FILL = (74, 66, 49, 255)          # 只做形状，单色填充（**不碰颜色**）
EDGE = (8, 6, 5, 255)


def render_poly(pts, w, h, ss=SS, zoom=1):
    """按多边形渲染一张 1× RGBA（单色），可选放大。"""
    big = Image.new("RGBA", (w * ss, h * ss), (0, 0, 0, 0))
    ImageDraw.Draw(big).polygon([(x * ss, y * ss) for x, y in pts],
                                fill=FILL, outline=EDGE)
    im = big.resize((w, h), Image.LANCZOS)
    if zoom > 1:
        im = im.resize((w * zoom, h * zoom), Image.NEAREST)
    return im


# ---------- 旧版：贝塞尔半圆头（复刻改造前实现）----------
def old_taper_besier(w, h, tip_len, tip_h, seg=4):
    """改造前的 end_taper_points（8 段二次贝塞尔）。"""
    cy = h * 0.5
    th = max(0.5, tip_h * h * 0.5)
    tl = max(0.0, min(tip_len, w * 0.5 - 1.0))
    if tl <= 0:
        return [(0.0, 0.0), (float(w), 0.0), (float(w), float(h)), (0.0, float(h))]
    n = max(2, seg)
    mid = w * 0.5

    def bez(p0, c, p1, k):
        out = []
        for i in range(k + 1):
            t = i / float(k)
            mt = 1.0 - t
            out.append((mt * mt * p0[0] + 2 * mt * t * c[0] + t * t * p1[0],
                        mt * mt * p0[1] + 2 * mt * t * c[1] + t * t * p1[1]))
        return out

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
        s = bez(p0, c, p1, k)
        pts += s if i == 0 else s[1:]
    if pts and abs(pts[-1][0] - pts[0][0]) < 1e-12 and abs(pts[-1][1] - pts[0][1]) < 1e-12:
        pts.pop()
    return pts


def old_chamfer(w, h, cut, inset=0):
    """改造前的 chamfer_pts（八边形）。"""
    x0, y0, x1, y1 = inset, inset, w - 1 - inset, h - 1 - inset
    c = max(0.0, cut - 0.586 * inset)
    if c <= 0.01:
        return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]
    return [(x0 + c, y0), (x1 - c, y0), (x1, y0 + c), (x1, y1 - c),
            (x1 - c, y1), (x0 + c, y1), (x0, y1 - c), (x0, y0 + c)]


TL = CS.TIP_LEN_PX
TIP_H_OLD = 0.16

rows = [
    ("旧 · 主按钮 110x35（贝塞尔半圆头）", W, H, old_taper_besier(W, H, TL, TIP_H_OLD)),
    ("新 · 主按钮 110x35（统一曲线）",     W, H, CS.tip_curve_pts(W, H, TL)),
    ("旧 · 次按钮 100x35 / 页签 105x35（八边形 cut=8）",
     100, H, old_chamfer(100, H, 8, 0)),
    ("新 · 次按钮 100x35（统一曲线）",     100, H, CS.tip_curve_pts(100, H, TL)),
    ("新 · 页签 105x35（统一曲线）",       105, H, CS.tip_curve_pts(105, H, TL)),
]

PAD = 16
LBL_H = 22
ZOOM_H = H * ZOOM
ZOOM_W = 30 * ZOOM

colw = max(max(r[1] for r in rows) + ZOOM_W + 220, 700)
rowh = max(H + LBL_H + PAD, ZOOM_H + LBL_H + PAD)
board_w = PAD * 2 + colw
board_h = PAD + len(rows) * rowh + PAD

board = Image.new("RGB", (board_w, board_h), (26, 26, 30))
d = ImageDraw.Draw(board)

try:
    font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 13)
    font_s = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 11)
except Exception:
    font = ImageFont.load_default()
    font_s = font

y = PAD
for label, w, h, pts in rows:
    d.text((PAD, y), label, fill=(200, 200, 210), font=font)
    ty = y + LBL_H
    im = render_poly(pts, w, h)
    board.paste(im, (PAD, ty), im)

    # 端头放大
    zx = PAD + w + 30
    zoom = render_poly(pts, w, h, zoom=ZOOM)
    z = zoom.crop((0, 0, ZOOM_W, ZOOM_H))
    # 放大块贴到行内垂直居中
    zy = ty + max(0, (H - ZOOM_H) // 2)
    board.paste(z, (zx, zy), z)
    d.rectangle([zx - 1, zy - 1, zx + ZOOM_W, zy + ZOOM_H], outline=(90, 90, 100))

    # 端头逐列高标注
    px = im.load()
    hs = []
    for x in range(min(20, w)):
        col = [yy for yy in range(h) if px[x, yy][3] > 127]
        hs.append((max(col) - min(col) + 1) if col else 0)
    d.text((zx + ZOOM_W + 8, ty), "端头逐列高", fill=(160, 160, 170), font=font_s)
    d.text((zx + ZOOM_W + 8, ty + 18),
           " ".join("%d" % v for v in hs), fill=(120, 200, 140), font=font_s)

    y += rowh

p = os.path.join(OUT, "shape_pipeline_board.png")
board.save(p)
print("saved:", p, board.size)
