# -*- coding: utf-8 -*-
"""最终交付板：按钮族「形」改造前后对照（**真尺寸**，不放大）。

分两块：
  A 块  改造前 / 改造后  三控件轮廓并排（虚构旧图现场重算 + 真实新图）
  B 块  真实新图逐态（从交付目录读 png）

判读要点写在图侧（不是写给我自己看的，是写给甲方看的）。
"""
import os
import sys
import glob
from PIL import Image, ImageDraw, ImageFont

BASE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, BASE)
import curve_shape as CS

BTN = os.path.join(BASE, "..", "..", "GUI", "SpriteParts", "ui_awake_button")
OUT = os.path.join(BASE, "out", "study", "shape")
os.makedirs(OUT, exist_ok=True)

W, H, SS = 110, 35, 4
TL = CS.TIP_LEN_PX


def render(pts, w, h, fill=(84, 74, 55, 255), edge=(10, 8, 6, 255)):
    big = Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))
    ImageDraw.Draw(big).polygon([(x * SS, y * SS) for x, y in pts],
                                fill=fill, outline=edge)
    return big.resize((w, h), Image.LANCZOS)


def old_bez(w, h, tip_len, tip_h, seg=4):
    cy, th = h * 0.5, max(0.5, tip_h * h * 0.5)
    tl = max(0.0, min(tip_len, w * 0.5 - 1.0))
    n, mid = max(2, seg), w * 0.5

    def bz(p0, c, p1, k):
        o = []
        for i in range(k + 1):
            t = i / float(k)
            mt = 1 - t
            o.append((mt * mt * p0[0] + 2 * mt * t * c[0] + t * t * p1[0],
                      mt * mt * p0[1] + 2 * mt * t * c[1] + t * t * p1[1]))
        return o
    ch = [((0.0, cy - th), (0.0, 0.0), (tl, 0.0), n),
          ((tl, 0.0), (mid, 0.0), (w - tl, 0.0), 1),
          ((w - tl, 0.0), (w, 0.0), (w, cy - th), n),
          ((w, cy - th), (w, cy), (w, cy + th), 1),
          ((w, cy + th), (w, h), (w - tl, h), n),
          ((w - tl, h), (mid, h), (tl, h), 1),
          ((tl, h), (0.0, h), (0.0, cy + th), n),
          ((0.0, cy + th), (0.0, cy), (0.0, cy - th), 1)]
    pts = []
    for i, (p0, c, p1, k) in enumerate(ch):
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


BG = (22, 22, 26)
PAD = 24
LBL = 26

board_w = 1000
rows = []

# ============ A 块：前后对照 ============
rows.append(("H", "A · 改造前 / 改造后（真尺寸，端头都是 16px 长）", "", None))
rows.append(("R", "旧 · 主按钮", "二次贝塞尔半圆头", old_bez(W, H, TL, 0.16)))
rows.append(("R", "新 · 主按钮", "统一曲线（本族三控件共用）", CS.tip_curve_pts(W, H, TL)))
rows.append(("R", "旧 · 次按钮 / 页签", "45° 直斜切（八边形 cut=8）", old_cham(100, H, 8, 0)))
rows.append(("R", "新 · 次按钮", "统一曲线", CS.tip_curve_pts(100, H, TL)))
rows.append(("R", "新 · 页签", "统一曲线（与上面两条同形，仅长度差）", CS.tip_curve_pts(105, H, TL)))

# ============ B 块：真实交付图逐态 ============
rows.append(("H", "B · 真实交付图（从 GUI/SpriteParts/ui_awake_button 读）", "", None))
for name in ("btn_primary_110", "btn_primary_110_hover",
             "btn_primary_110_pressed", "btn_primary_110_disabled",
             "btn_secondary_100", "btn_secondary_100_hover",
             "btn_secondary_100_pressed",
             "btn_tab_105", "btn_tab_105_hover",
             "btn_tab_105_pressed", "btn_tab_105_selected"):
    p = os.path.join(BTN, name + ".png")
    rows.append(("P", name, "", p))

# 计算高度
hA = PAD
for kind, a, b, c in rows:
    if kind == "H":
        hA += LBL + 14
    else:
        hA += H + 14

board = Image.new("RGB", (board_w, hA + PAD), BG)
d = ImageDraw.Draw(board)
try:
    fh = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 15)
    fr = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 12)
    fs = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 11)
except Exception:
    fh = fr = fs = ImageFont.load_default()

y = PAD
for kind, a, b, c in rows:
    if kind == "H":
        d.line([(PAD, y + 8), (board_w - PAD, y + 8)], fill=(70, 70, 80))
        d.text((PAD, y - 2), a, fill=(232, 200, 130), font=fh)
        y += LBL + 14
        continue
    d.text((PAD, y + 8), a, fill=(200, 200, 210), font=fr)
    if kind == "R":
        im = render(c, a.endswith("页签") and 105 or (100 if "次按钮" in a else W), H)
        board.paste(im, (300, y), im)
        if b:
            d.text((300 + im.size[0] + 16, y + 10), b, fill=(120, 120, 132), font=fs)
    else:
        im = Image.open(c).convert("RGBA")
        board.paste(im, (300, y), im)
    y += H + 14

p = os.path.join(OUT, "shape_delivery_board.png")
board.save(p)
print("saved:", p, board.size)
