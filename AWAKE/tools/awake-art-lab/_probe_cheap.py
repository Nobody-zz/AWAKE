# -*- coding: utf-8 -*-
"""并排看：原版（缩到同高 35）vs 我们现在的正式 sprite。

不复述参数，只为了**用眼睛找"廉价"是从哪来的**。三行：
  ① 原版 ×8　② 我们 ×8　③ 我们 ×8（只看形，灰底）
产物 out/study/cheap/vs_ref.png
"""
import os

from PIL import Image, ImageDraw, ImageFont

import make_button_primary as mb

HERE = mb.HERE
OUT = os.path.join(HERE, "out", "study", "cheap")
REF = os.path.normpath(os.path.join(HERE, "..", "awake-ui-lab", "out", "atlas",
                                    "sprites", "General__Button__main_button_regular.png"))
PANEL = (30, 27, 25)
PGC = (110, 110, 110)
Z = 8


def on_bg(im, bg):
    c = Image.new("RGBA", im.size, bg + (255,))
    c.alpha_composite(im.convert("RGBA"), (0, 0))
    return c.convert("RGB")


def ref_body(h=35):
    im = Image.open(REF).convert("RGBA")
    a = im.split()[3].point(lambda v: 255 if v > 128 else 0)
    body = im.crop(a.getbbox())
    w = max(1, int(round(body.width * h / float(body.height))))
    return body.resize((w, h), Image.LANCZOS)


def main():
    os.makedirs(OUT, exist_ok=True)
    ref = ref_body()
    ours = Image.open(os.path.join(mb.OUT_BTN, "btn_primary_110.png")).convert("RGBA")

    pad = 30
    cw = max(ref.width, ours.width) * Z
    ch = mb.H * Z
    Ht = pad * 2 + 30 + ch * 2 + 90 + 30 + mb.H * 2
    sh = Image.new("RGB", (pad * 2 + cw, Ht), (16, 14, 13))
    d = ImageDraw.Draw(sh)
    f_t = ImageFont.truetype(mb.FONT, 24)
    f_l = ImageFont.truetype(mb.FONT, 16)
    d.text((pad, pad - 6), "原版 h35 ／ 我们（正式 sprite）—— 同高、同底、×8", font=f_t,
           fill=(214, 196, 160))

    y = pad + 30
    d.text((pad, y), "原版实体 → h35", font=f_l, fill=(226, 208, 172))
    y += 24
    sh.paste(on_bg(ref.resize((ref.width * Z, mb.H * Z), Image.NEAREST), PANEL), (pad, y))

    y += ch + 26
    d.text((pad, y), "我们 btn_primary_110", font=f_l, fill=(226, 208, 172))
    y += 24
    sh.paste(on_bg(ours.resize((ours.width * Z, mb.H * Z), Image.NEAREST), PANEL), (pad, y))

    y += ch + 26
    d.text((pad, y), "两边 1× 实尺", font=f_l, fill=(226, 208, 172))
    y += 24
    sh.paste(on_bg(ref, PANEL), (pad, y))
    sh.paste(on_bg(ours, PANEL), (pad, y + mb.H + 8))

    p = os.path.join(OUT, "vs_ref.png")
    sh.save(p)
    print("->", p, sh.size)


if __name__ == "__main__":
    main()
