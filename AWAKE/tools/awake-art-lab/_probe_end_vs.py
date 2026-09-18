# -*- coding: utf-8 -*-
"""只看端头的形：原版左端 vs 我们左端，×10，同高同底。产物 out/study/cheap/end_vs.png。"""
import os

from PIL import Image, ImageDraw, ImageFont

import make_button_primary as mb

HERE = mb.HERE
OUT = os.path.join(HERE, "out", "study", "cheap")
REF = os.path.normpath(os.path.join(HERE, "..", "awake-ui-lab", "out", "atlas",
                                    "sprites", "General__Button__main_button_regular.png"))
PANEL = (30, 27, 25)
Z = 10
CROP = 40          # 左端看多少 1× 像素


def on_bg(im, bg):
    c = Image.new("RGBA", im.size, bg + (255,))
    c.alpha_composite(im.convert("RGBA"), (0, 0))
    return c.convert("RGB")


def ref_body():
    im = Image.open(REF).convert("RGBA")
    a = im.split()[3].point(lambda v: 255 if v > 128 else 0)
    body = im.crop(a.getbbox())
    w = max(1, int(round(body.width * 35 / float(body.height))))
    return body.resize((w, 35), Image.LANCZOS)


def zoom(im, z, box):
    c = im.crop(box)
    return c.resize((c.width * z, c.height * z), Image.NEAREST)


def main():
    os.makedirs(OUT, exist_ok=True)
    ref = ref_body()
    ours = Image.open(os.path.join(mb.OUT_BTN, "btn_primary_110.png")).convert("RGBA")
    a = zoom(on_bg(ref, PANEL), Z, (0, 0, min(CROP, ref.width), ref.height))
    b = zoom(on_bg(ours, PANEL), Z, (0, 0, min(CROP, ours.width), ours.height))
    pad = 22
    W = pad * 2 + a.width + b.width + 24
    Ht = pad + 30 + a.height + 40
    sh = Image.new("RGB", (W, Ht), (16, 14, 13))
    d = ImageDraw.Draw(sh)
    f = ImageFont.truetype(mb.FONT, 18)
    d.text((pad, pad - 4), "端头 ×10（左端 40px）—— 左：原版　右：我们", font=f, fill=(226, 208, 172))
    sh.paste(a, (pad, pad + 30))
    sh.paste(b, (pad + a.width + 24, pad + 30))
    d.text((pad, pad + 32 + a.height), "原版 h35", font=f, fill=(226, 208, 172))
    d.text((pad + a.width + 24, pad + 32 + b.height), "我们 btn_primary_110", font=f, fill=(226, 208, 172))
    p = os.path.join(OUT, "end_vs.png")
    sh.save(p)
    print("->", p, sh.size)


if __name__ == "__main__":
    main()
