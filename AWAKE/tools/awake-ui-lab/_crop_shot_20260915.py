# -*- coding: utf-8 -*-
"""把 Lab 截图按矩形裁开放大，用于肉眼核对细节。一次性脚本。

用法：python _crop_shot_20260915.py <in.png> <out.png> x0 y0 x1 y1 [scale]
"""
import sys

from PIL import Image


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    args = sys.argv[1:]
    if len(args) < 6:
        print(__doc__)
        return
    src, dst = args[0], args[1]
    x0, y0, x1, y1 = (int(float(v)) for v in args[2:6])
    scale = float(args[6]) if len(args) > 6 else 3.0

    im = Image.open(src).convert("RGBA")
    print("原图", im.size)
    box = (max(0, x0), max(0, y0), min(im.size[0], x1), min(im.size[1], y1))
    crop = im.crop(box)
    print("裁切", box, "→", crop.size)
    w = max(1, int(crop.size[0] * scale))
    h = max(1, int(crop.size[1] * scale))
    big = crop.resize((w, h), Image.NEAREST)
    big.save(dst)
    print("写出", dst, big.size)


if __name__ == "__main__":
    main()
