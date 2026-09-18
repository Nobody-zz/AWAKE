# -*- coding: utf-8 -*-
"""端头**放大对照板**：左＝原版实拍端头，中＝本曲线，右＝旧八边形。
同一高度、同一底、同一倍率并排 —— 形状类的东西只能这么比。
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image, ImageDraw
from curve_shape import tip_curve_pts, tip_len_for
from artkit import poly_mask, chamfer_pts

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "out", "study", "shape")
W, H, SS, Z = 110, 35, 8, 6          # Z = 放大倍数

# ── 原版：从实拍图上裁端头区，缩放到本项目高度
ref = Image.open(os.path.join(OUT, "ref_silhouette.png")).convert("L")
bb = ref.getbbox()
ref = ref.crop(bb)                    # 232×42 左右
# 缩放到 H 高，保持比例
rw = int(round(ref.width * H / ref.height))
ref = ref.resize((rw, H), Image.LANCZOS)
# 只取左端 22px（本项目端头 7.6 约当原版 16）
refcap = ref.crop((0, 0, 22, H))


def render(pts):
    m = Image.new("L", (W * SS, H * SS), 0)
    ImageDraw.Draw(m).polygon([(x * SS, y * SS) for x, y in pts], fill=255)
    return m.resize((W, H), Image.LANCZOS)


curve = render(tip_curve_pts(W, H))
octa = render(chamfer_pts(W, H, 8, 2))

CAPW = 24
panels = [("原版", refcap), ("本曲线", curve.crop((0, 0, CAPW, H))),
          ("旧八边形", octa.crop((0, 0, CAPW, H)))]

GAP, PAD, TOP = 18, 20, 46
total_w = PAD * 2 + sum(p.width for _, p in panels) * Z + GAP * (len(panels) - 1)
total_h = TOP + H * Z + PAD
board = Image.new("RGB", (total_w, total_h), (26, 26, 28))
d = ImageDraw.Draw(board)

x = PAD
for name, p in panels:
    big = p.resize((p.width * Z, p.height * Z), Image.NEAREST).convert("RGB")
    board.paste(big, (x, TOP))
    d.text((x, TOP - 22), name, fill=(230, 230, 230))
    x += p.width * Z + GAP

board.save(os.path.join(OUT, "tip_board.png"))
print("saved", os.path.join(OUT, "tip_board.png"), board.size)
