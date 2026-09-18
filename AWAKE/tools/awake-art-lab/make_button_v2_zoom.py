# -*- coding: utf-8 -*-
"""v2 放大细看：四件并排，同一区域 ×10 —— 看「箍的剖面」和「面的起伏」到底出没出来。"""
import os

from PIL import Image, ImageDraw, ImageFont

import make_button_primary as mb
from make_button_v2_study import fit_h, on_bg, zoom, REF, PGC, PANEL, V2, PREV

FONT = mb.FONT
S = 10


def main():
    V = V2
    items = [("原版 → h35", fit_h(Image.open(REF).convert("RGBA"), mb.H)),
             ("① 当前落盘（旧）", Image.open(os.path.join(PREV, "btn_primary_110.png")).convert("RGBA")),
             ("② v2 B 亮铁缓坡", Image.open(os.path.join(V, "btn_primary_110_v2B.png")).convert("RGBA")),
             ("③ v2 A 黑边陡棱（交付）", Image.open(os.path.join(V, "btn_primary_110_v2A.png")).convert("RGBA"))]

    boxes = [("左上角（箍的剖面）", (0, 0, 26, 18)),
             ("上边中段", (40, 0, 66, 14)),
             ("左下角", (0, 17, 26, 35)),
             ("左端整条", (0, 0, 14, 35))]

    f_t = ImageFont.truetype(FONT, 24)
    f_l = ImageFont.truetype(FONT, 16)
    tiles = []
    for lbl, box in boxes:
        w, h = box[2] - box[0], box[3] - box[1]
        tiles.append((lbl, w * S, h * S))

    pad, gap = 30, 22
    colw = max(t[1] for t in tiles)
    total_w = pad * 2 + colw * 4 + gap * 3
    total_h = pad + 44 + sum(t[2] + 44 for t in tiles) + 20
    sh = Image.new("RGB", (total_w, total_h), (16, 14, 13))
    d = ImageDraw.Draw(sh)
    d.text((pad, pad), "v2 放大 ×10 · 箍的剖面与面的起伏", font=f_t, fill=(214, 196, 160))

    x0 = pad
    for i, (lbl, im) in enumerate(items):
        d.text((x0 + i * (colw + gap), pad + 34), lbl, font=f_l, fill=(226, 208, 172))

    y = pad + 62
    for lbl, tw, th in tiles:
        d.text((pad, y), lbl, font=f_l, fill=(190, 172, 142))
        y += 26
        for i, (_, im) in enumerate(items):
            box = boxes[[b[0] for b in boxes].index(lbl)][1]
            sh.paste(on_bg(zoom(im, S, box), PANEL), (pad + i * (colw + gap), y))
        y += th + 18

    p = os.path.join(V, "zoom_v2.png")
    sh.save(p)
    print("->", p, sh.size)


if __name__ == "__main__":
    main()
