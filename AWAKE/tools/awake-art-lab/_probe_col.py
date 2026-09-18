# -*- coding: utf-8 -*-
"""中列剖面：原版 vs 我们（长边轨与水面的分界，都在这一列上）。产物 out/study/cheap/col.txt"""
import os

from PIL import Image

import make_button_primary as mb

HERE = mb.HERE
OUT = os.path.join(HERE, "out", "study", "cheap")
REF = os.path.normpath(os.path.join(HERE, "..", "awake-ui-lab", "out", "atlas",
                                    "sprites", "General__Button__main_button_regular.png"))


def lum(p):
    return 0.2126 * p[0] + 0.7152 * p[1] + 0.0722 * p[2]


def ref_body():
    im = Image.open(REF).convert("RGBA")
    a = im.split()[3].point(lambda v: 255 if v > 128 else 0)
    body = im.crop(a.getbbox())
    w = max(1, int(round(body.width * 35 / float(body.height))))
    return body.resize((w, 35), Image.LANCZOS)


def col(im, x, tag, lines):
    px = im.load()
    lines.append("%s  x=%d" % (tag, x))
    for y in range(im.height):
        r, g, b, a = px[x, y]
        if a <= 128:
            lines.append("   y%02d      -   (透明)" % y)
        else:
            mark = ""
            if lum((r, g, b)) > 100:
                mark = "  <<< 亮"
            lines.append("   y%02d %6.1f   rgb(%3d,%3d,%3d)%s" % (y, lum((r, g, b)), r, g, b, mark))
    lines.append("")


def main():
    os.makedirs(OUT, exist_ok=True)
    ref = ref_body()
    ours = Image.open(os.path.join(mb.OUT_BTN, "btn_primary_110.png")).convert("RGBA")
    lines = []
    col(ref, ref.width // 2, "原版 h35 · 中列", lines)
    col(ours, 55, "我们 · 中列 x=55", lines)
    p = os.path.join(OUT, "col.txt")
    with open(p, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    print("->", p)


if __name__ == "__main__":
    main()
