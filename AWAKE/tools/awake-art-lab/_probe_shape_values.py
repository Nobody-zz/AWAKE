# -*- coding: utf-8 -*-
"""量**产出**，不量输入：端头铸件区 / 长边轨 / 木面 / 孔，各自的实测亮度。

踩过太多次"参数写对了但没生效"（Brightness 被 min 钳死、MinFilter 边界钳位），
所以这里一律从落盘的 PNG 反读。
"""
import os
import sys

from PIL import Image

import make_button_primary as mb

OUT = os.path.join(mb.HERE, "out", "study", "shape")


def region_mean(im, box):
    c = im.convert("RGB").crop(box)
    n = c.width * c.height
    s = 0.0
    for r, g, b in c.getdata():
        s += 0.2126 * r + 0.7152 * g + 0.0722 * b
    return s / n


def main():
    path = sys.argv[1] if len(sys.argv) > 1 else os.path.join(OUT, "btn_primary_110_shape_刃.png")
    for name in sorted(os.listdir(OUT)):
        if not name.startswith("btn_primary_110_shape_") or name.endswith("刃.png"):
            continue
        p = os.path.join(OUT, name)
        im = Image.open(p).convert("RGBA")
        print("== %s" % name)
        # 端头铸件的**体**：x 4..9（避开外缘 0..3 的暗轮廓、也避开 x=8.5 那个孔）
        #   孔在 x 8.5 y 11.5/23.5 ⇒ 避开 y 9..14 / 21..26 ⇒ 取 y 15..20
        print("   铸件体  x4..9  y15..20        : %5.1f" % region_mean(im, (4, 15, 10, 21)))
        print("   铸件体  x4..9  y4..8（上侧）  : %5.1f" % region_mean(im, (4, 4, 10, 9)))
        print("   长边轨  x45..65 y2..7         : %5.1f" % region_mean(im, (45, 2, 66, 8)))
        print("   木面    x45..65 y14..21       : %5.1f" % region_mean(im, (45, 14, 66, 22)))
        print("   最外一行 y0                   : %5.1f" % region_mean(im, (0, 0, 110, 1)))


if __name__ == "__main__":
    main()
