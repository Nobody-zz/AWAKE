# -*- coding: utf-8 -*-
"""原版**端头**放大细看：轮廓的收尖，与端头那块铁件的关系。

只读研究，不改交付。产物 out/study/shape/_ref_end.png
"""
import os

from PIL import Image, ImageDraw, ImageFont

import make_button_shape_taper as ts

OUT = ts.OUT
REF = ts.REF


def main():
    os.makedirs(OUT, exist_ok=True)
    im = Image.open(REF).convert("RGBA")
    a = im.split()[3].point(lambda v: 255 if v > 128 else 0)
    body = im.crop(a.getbbox())                       # 232x42
    print("实体 %dx%d" % body.size)

    # 左端 60px（原大）×8、右端 60px ×8、整条缩到 h=35 后左端 40px ×8
    z = 8
    left = ts.zoom(ts.on_bg(body, ts.PANEL), z, (0, 0, 60, body.height))
    right = ts.zoom(ts.on_bg(body, ts.PANEL), z,
                    (body.width - 60, 0, body.width, body.height))
    small = ts.ref_body()
    sl = ts.zoom(ts.on_bg(small, ts.PANEL), z, (0, 0, 40, small.height))

    pad = 24
    W = pad * 2 + max(left.width, right.width, sl.width)
    Ht = pad * 2 + left.height + right.height + sl.height + 120
    sh = Image.new("RGB", (W, Ht), (16, 14, 13))
    d = ImageDraw.Draw(sh)
    f = ImageFont.truetype(ts.FONT, 20)
    y = pad
    for lbl, c in (("原版实体 左端 ×8  —— 收尖 + 端头铁件 + 圆孔", left),
                   ("原版实体 右端 ×8", right),
                   ("缩到 h=35 后 左端 ×8（本项目实际要对照的样子）", sl)):
        d.text((pad, y), lbl, font=f, fill=(226, 208, 172))
        y += 26
        sh.paste(c, (pad, y))
        y += c.height + 14
    p = os.path.join(OUT, "_ref_end.png")
    sh.save(p)
    print("->", p, sh.size)


if __name__ == "__main__":
    main()
