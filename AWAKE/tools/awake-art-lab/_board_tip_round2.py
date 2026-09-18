# -*- coding: utf-8 -*-
"""端头长度第 2 轮对照：**带完整铁轨 + 木面**（上一轮只画纯轮廓，漏了内缩吃端头）。

关键约束（上一轮没算）：
    端头长度 TIP  必须 > 木面内缩量 (bev+1)
    否则木面的端头会被内缩**吃平** —— 端头在外轮廓上收成了斜楔，
    但木面在端头处接近竖直 ⇒ 斜楔里只剩很窄一条铁轨，读起来像"被切了一刀"。
    实测看 `zoom6_btn_primary_110.png`（TIP=9, bev=4 ⇒ 只剩 4px 可用）。

本板把四档 TIP 用**同一套 parts.btn_plate 参数**画出来（只换 cut），
并标出"端头可用余量 = TIP − (bev+1)"。
"""
import os
import sys
from PIL import Image, ImageDraw, ImageFont

BASE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, BASE)
import parts as P

OUT = os.path.join(BASE, "out", "study", "shape")
os.makedirs(OUT, exist_ok=True)

W, H = 110, 35
BEV = 4
FIELD = (74, 66, 49)

LANES = [7.0, 9.0, 11.0, 13.0, 16.0]
ZOOM = 4

PADL, PADT = 20, 18
LBL = 22
GAP = 14

bw = PADL * 2 + W * ZOOM + 260
bh = PADT * 2 + len(LANES) * (LBL + H * ZOOM + GAP) + 30

board = Image.new("RGB", (bw, bh), (22, 22, 26))
d = ImageDraw.Draw(board)
try:
    f1 = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 13)
    f2 = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 11)
except Exception:
    f1 = f2 = ImageFont.load_default()

y = PADT
for tip in LANES:
    room = tip - (BEV + 1)
    note = ("余量 %.1f px %s" % (room,
            "← 够" if room >= 3 else "← 端头被内缩吃平"))
    d.text((PADL, y + 4), "端头 %.0fpx（内缩 %.0f ⇒ %s）" % (tip, BEV + 1, note),
           fill=(210, 210, 220) if room >= 3 else (240, 170, 130), font=f1)
    ty = y + LBL
    im = P.btn_plate(W, H, cut=tip, field=FIELD, bev=BEV)
    z = im.resize((W * ZOOM, H * ZOOM), Image.NEAREST)
    board.paste(z, (PADL, ty))
    d.rectangle([PADL - 1, ty - 1, PADL + W * ZOOM, ty + H * ZOOM],
                outline=(80, 80, 92))

    # 右侧：端头 1× 原尺寸 ×1（看真实观感）
    im1 = im.resize((W, H), Image.LANCZOS)
    board.paste(im1, (PADL + W * ZOOM + 24, ty + 4))
    # 端头逐列高
    px = im1.load()
    hs = []
    for x in range(14):
        col = [yy for yy in range(H) if px[x, yy][3] > 127]
        hs.append((max(col) - min(col) + 1) if col else 0)
    d.text((PADL + W * ZOOM + 24, ty + H + 10), "端头逐列高：", fill=(150, 150, 162), font=f2)
    d.text((PADL + W * ZOOM + 24, ty + H + 26), " ".join("%d" % v for v in hs),
           fill=(120, 205, 145), font=f2)
    y += LBL + H * ZOOM + GAP

p = os.path.join(OUT, "tip_len_round2.png")
board.save(p)
print("saved:", p, board.size)
