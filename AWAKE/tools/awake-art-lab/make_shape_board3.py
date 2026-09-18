# -*- coding: utf-8 -*-
"""**最终形状对照板** —— 四列：原版实拍 / 现在(主) / 现在(次) / 建议。

版式要点（这次学乖了）：
  · 每一列都**缩放到同一高度**再并排 —— 原版 232×42，我们 110×35，不缩没法比。
  · **并排的是"端头"局部**：把左端 30px 放大，才是看"线条流不流畅"的地方。
  · 另配一行**整条按钮真尺寸**，用来看比例（长宽比、端头占比）。
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image, ImageDraw

import make_button_primary as MB
from artkit import chamfer_pts
from curve_shape import tip_curve_pts

HERE = os.path.dirname(os.path.abspath(__file__))
REFDIR = os.path.normpath(os.path.join(HERE, "..", "awake-ui-lab", "out", "atlas", "sprites"))
OUT = os.path.join(HERE, "out", "study", "shape")
CW, BG = 1820, (26, 26, 28)
SS = 8
H = 35

# ── 造"原版"剪影（缩到 H 高）
ref = Image.open(os.path.join(REFDIR, "General__Button__main_button_regular.png")).convert("RGBA")
a = ref.split()[3].point(lambda v: 255 if v > 128 else 0)
body = ref.crop(a.getbbox())
rw = int(round(body.width * H / float(body.height)))
ref_sil = body.split()[3].point(lambda v: 255 if v > 128 else 0)
ref_sil = ref_sil.resize((rw, H), Image.LANCZOS)


def render(pts, W):
    m = Image.new("L", (W * SS, H * SS), 0)
    ImageDraw.Draw(m).polygon([(x * SS, y * SS) for x, y in pts], fill=255)
    return m.resize((W, H), Image.LANCZOS)


now_p = render(MB.end_taper_points(110.0, float(H), MB.TIP_LEN, MB.TIP_H), 110)
now_o = render(chamfer_pts(100, H, 8, 2), 100)
new_p = render(tip_curve_pts(110, H), 110)


def tint(mask, color):
    im = Image.new("RGB", mask.size, BG)
    im.paste(Image.new("RGB", mask.size, color), (0, 0), mask)
    return im


COLS = [
    ("原版实拍", tint(ref_sil, (172, 168, 160)), 0),
    ("现在 · 主按钮", tint(now_p, (140, 138, 135)), 12),
    ("现在 · 次按钮", tint(now_o, (140, 138, 135)), 8),
    ("建议 · 统一曲线", tint(new_p, (206, 200, 188)), 15),
]

PAD, TOP = 30, 84
CAPW_1X = 26          # 并排取左端 26px
Zc = 5                # 端头放大
COLW = CAPW_1X * Zc + 40
ROW1 = TOP
ROW2 = TOP + H * Zc + 74

board = Image.new("RGB", (CW, ROW2 + H * 3 + 70), BG)
d = ImageDraw.Draw(board)
d.text((PAD, 20), "按钮形状对照 —— 只论「形」", fill=(228, 228, 230))
d.text((PAD, 40), "上行＝端头放大 5×（看「线条流不流畅」）；下行＝整条真尺寸（看比例）",
       fill=(145, 145, 150))

for i, (name, im, fullx) in enumerate(COLS):
    x = PAD + i * (COLW + 20)
    d.text((x, TOP - 20), name, fill=(228, 228, 230))
    cap = im.crop((0, 0, CAPW_1X, H))
    board.paste(cap.resize((CAPW_1X * Zc, H * Zc), Image.NEAREST), (x, ROW1))
    # 满高列标一条竖线
    if 0 < fullx < CAPW_1X:
        lx = x + fullx * Zc
        d.line([(lx, ROW1), (lx, ROW1 + H * Zc)], fill=(235, 190, 100))
    board.paste(im, (x, ROW2))

d.text((PAD, ROW2 + H + 16),
       "橙线＝满高列。原版 x=14/232（6.0%）；现在主按钮 x=12/110（10.9%）；建议 x=15/110（13.6%，甲方定案跟绝对长度 16px）",
       fill=(235, 190, 100))
board.save(os.path.join(OUT, "shape_board3.png"))
print("saved", board.size)
