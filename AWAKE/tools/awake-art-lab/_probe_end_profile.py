# -*- coding: utf-8 -*-
"""端头的**径向结构**逐点对：原版 vs 我们。

问题：端头从"木面"往外到"外形"，亮度是怎么排的？
  · 第 1 条：过端头**垂直中线**那一行（y=H/2），x 从 0 到 TIPW
  · 第 2 条：过**铸件实心**那一竖列（x=END_HOLE_X 附近），y 从 0 到 H
只打数，不画图。产物 out/study/cheap/end_profile.txt
"""
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


def row(im, y, x0, x1, tag, lines):
    px = im.load()
    lines.append("%s  y=%d  x=%d..%d" % (tag, y, x0, x1 - 1))
    for x in range(x0, x1):
        r, g, b, a = px[x, y]
        if a <= 128:
            lines.append("   x%02d     -   (透明)" % x)
        else:
            lines.append("   x%02d %5.1f   rgb(%3d,%3d,%3d)" % (x, lum((r, g, b)), r, g, b))
    lines.append("")


def col(im, x, y0, y1, tag, lines):
    px = im.load()
    lines.append("%s  x=%d  y=%d..%d" % (tag, x, y0, y1 - 1))
    for y in range(y0, y1):
        r, g, b, a = px[x, y]
        if a <= 128:
            lines.append("   y%02d     -   (透明)" % y)
        else:
            lines.append("   y%02d %5.1f   rgb(%3d,%3d,%3d)" % (y, lum((r, g, b)), r, g, b))
    lines.append("")


def main():
    os.makedirs(OUT, exist_ok=True)
    ref = ref_body()
    ours = Image.open(os.path.join(mb.OUT_BTN, "btn_primary_110.png")).convert("RGBA")
    lines = []
    lines.append("# 端头径向结构（只打数）\n")
    row(ref, 17, 0, 40, "原版 h35 · 中线那一行", lines)
    col(ref, 6, 0, 35, "原版 h35 · 铸件实心那一竖列 (x=6)", lines)
    row(ours, 17, 0, 40, "我们 · 中线那一行", lines)
    col(ours, 6, 0, 35, "我们 · 铸件实心那一竖列 (x=6)", lines)
    p = os.path.join(OUT, "end_profile.txt")
    with open(p, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    print("->", p)


if __name__ == "__main__":
    main()
