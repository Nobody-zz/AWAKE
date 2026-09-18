# -*- coding: utf-8 -*-
"""铁的色偏到底在哪一层丢的？—— 底图 R-B vs 去色 R-B。

铁面现在的 R-B 只有 -1（中性灰）。原版是 +5（略暖）。两条可能的解释：
  (a) 底图本来就是偏冷/偏褐 ⇒ 只能靠 sat 提回来；
  (b) 底图是暖的，是 `IRON_SAT=0.34` 的去色把它抹平了 ⇒ 解法是调整"去多少"。
本脚本把每一层的 **R-B / G-B** 打出来，直接看是哪一层丢的。
输出 out/study/tone/iron_chain.txt
"""
import os

from PIL import Image
import make_button_primary as mb

OUT = os.path.join(mb.HERE, "out", "study", "tone")
os.makedirs(OUT, exist_ok=True)
lines = []


def stat(im, tag):
    im = im.convert("RGB")
    px = im.load()
    rs, gs, bs = [], [], []
    for y in range(im.height):
        for x in range(im.width):
            r, g, b = px[x, y]
            rs.append(r)
            gs.append(g)
            bs.append(b)
    n = len(rs)
    mr, mg, mbb = sum(rs) / n, sum(gs) / n, sum(bs) / n
    lines.append("%-34s  RGB #%02X%02X%02X  R-B %+6.1f  G-B %+6.1f  均 %5.1f"
                 % (tag, int(mr), int(mg), int(mbb), mr - mbb, mg - mbb,
                    (mr + mg + mbb) / 3.0))


plaque_i = mb.load_plaque(mb.IRON_SRC)
stat(plaque_i, "铁 底图 全幅")
box = (0.36, 0.46, 0.64, 0.74)
pw, ph = plaque_i.size
x0, y0 = int(pw * box[0]), int(ph * box[1])
cw, ch = mb.CROP[0], mb.CROP[1]
cx, cy = (box[0] + box[2]) * 0.5 * pw, (box[1] + box[3]) * 0.5 * ph
x0 = int(max(0, min(pw - cw, cx - cw * 0.5)))
y0 = int(max(0, min(ph - ch, cy - ch * 0.5)))
crop = plaque_i.crop((x0, y0, x0 + cw, y0 + ch))
stat(crop, "铁 取块 %dx%d" % (cw, ch))
stat(crop.resize((mb.W * mb.SS, mb.H * mb.SS), Image.LANCZOS), "铁 缩到 SS 画布")

FULL = (mb.W * mb.SS, mb.H * mb.SS)
for sat in (1.0, 0.85, 0.70, 0.55, 0.48, 0.40, 0.34):
    m = mb.material(plaque_i, box, FULL, mb.TARGET_IRON,
                    grain=mb.IRON_GRAIN, grain_dark=mb.IRON_GRAIN_DARK,
                    contrast=1.00, sat=sat, blur_f=0.13, crop_px=mb.CROP)
    stat(m, "铁 material(sat=%.2f)" % sat)

lines.append("")
plaque_w = mb.load_plaque(mb.WOOD_SRC)
stat(plaque_w, "木 底图 全幅")
for sat in (0.55, 0.70, 0.85, 1.00):
    for tg, lab in ((mb.WOOD_TINT, "现 tint"), ((0.96, 1.00, 0.90), "暖 tint")):
        m = mb.material(plaque_w, (0.28, 0.28, 0.72, 0.72), FULL, mb.TARGET_WOOD,
                        grain=mb.WOOD_GRAIN, crop_px=mb.CROP,
                        structure=mb.WOOD_STRUCT, sat=sat, tint_gain=tg)
        stat(m, "木 material(sat=%.2f, %s)" % (sat, lab))

txt = "\n".join(lines)
open(os.path.join(OUT, "iron_chain.txt"), "w", encoding="utf-8").write(txt)
print(txt)
