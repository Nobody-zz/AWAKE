# -*- coding: utf-8 -*-
"""验收指标：1× sprite 的**左右镜像差** / **上下镜像差**（平均亮度差，越低越对称）。

对称是"算出来的"（end_taper_points 出 8 点、孔两端镜像），但栅格化还有半个像素的余量，
所以每次改形都要量一遍产出，别信"应该是对称的"。
"""
import os

from PIL import Image, ImageChops, ImageStat

import make_button_primary as mb

OUT = os.path.join(mb.AWAKE, "GUI", "SpriteParts", "ui_awake_button")


def main():
    for suf in ("", "_hover", "_pressed", "_disabled"):
        p = os.path.join(OUT, "btn_primary_110%s.png" % suf)
        im = Image.open(p).convert("RGBA")
        g = im.convert("L")
        lr = ImageStat.Stat(ImageChops.difference(g, ImageOpsMirror(g))).mean[0]
        ud = ImageStat.Stat(ImageChops.difference(g, g.transpose(Image.FLIP_TOP_BOTTOM))).mean[0]
        a = im.split()[3]
        bbox = a.getbbox()
        sa = ImageStat.Stat(ImageChops.difference(a, a.transpose(Image.FLIP_LEFT_RIGHT))).mean[0]
        print("%-28s 左右镜像差 %5.2f（形 %4.2f ／ 色 %5.2f）｜ 上下镜像差 %5.2f ｜ bbox %s"
              % (os.path.basename(p), lr, sa, lr, ud, bbox))


def ImageOpsMirror(im):
    return im.transpose(Image.FLIP_LEFT_RIGHT)


if __name__ == "__main__":
    main()
