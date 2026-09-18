# -*- coding: utf-8 -*-
"""端头细看：三种铸件档 ＋ 原版，全部缩到 h=35、左端 ×10 同屏。只读研究。"""
import os
import glob

from PIL import Image, ImageDraw, ImageFont

import make_button_shape_taper as ts

OUT = ts.OUT


def main():
    z = 10
    cands = []
    ref = ts.ref_body()
    cands.append(("原版 h35", ref))
    for p in sorted(glob.glob(os.path.join(OUT, "*_shape_*.png"))):
        lbl = os.path.basename(p).split("_shape_", 1)[1][:-4]
        cands.append((lbl, Image.open(p).convert("RGBA")))

    w = 46
    ims = [(l, ts.zoom(ts.on_bg(i, ts.PANEL), z, (0, 0, min(w, i.width), i.height)))
           for l, i in cands]
    pad = 22
    total = pad * 2 + sum(i.width for _, i in ims) + 16 * (len(ims) - 1)
    Ht = pad + 30 + max(i.height for _, i in ims) + 40
    sh = Image.new("RGB", (total, Ht), (16, 14, 13))
    d = ImageDraw.Draw(sh)
    f = ImageFont.truetype(ts.FONT, 17)
    d.text((pad, pad - 4), "端头 ×10（左端 46px）—— 铸件三档 vs 原版", font=f, fill=(226, 208, 172))
    x = pad
    for l, i in ims:
        sh.paste(i, (x, pad + 30))
        d.text((x, pad + 32 + i.height), l, font=f, fill=(226, 208, 172))
        x += i.width + 16
    p = os.path.join(OUT, "_ends_compare.png")
    sh.save(p)
    print("->", p, sh.size)


if __name__ == "__main__":
    main()
