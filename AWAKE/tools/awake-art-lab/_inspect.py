#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""放大看 UI Lab 出的截图：裁一块、放大、拼成一页，用于判细节。

只读截图。用法：
    python _inspect.py NpcDialogue 0,0,420,300 2
    python _inspect.py AwakeMessenger 0,0,520,220 2
参数：<截图名> <x,y,w,h> [放大倍数] [第二块 x,y,w,h] ...
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFont

SHOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                     "..", "awake-ui-lab", "out", "shot"))


def _font(sz):
    try:
        return ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", sz)
    except Exception:  # noqa: BLE001
        return ImageFont.load_default()


def main():
    name = sys.argv[1]
    zoom = 3.0
    crops = []
    args = sys.argv[2:]
    if args and args[0].replace(".", "").isdigit():
        zoom = float(args[0])
        args = args[1:]
    for a in args:
        x, y, w, h = [int(v) for v in a.split(",")]
        crops.append((x, y, x + w, y + h))
    if not crops:
        crops = [(0, 0, 420, 300)]
    src = os.path.join(SHOT, name + ".png")
    im = Image.open(src).convert("RGB")
    print("源:", src, im.size)
    tiles = []
    for c in crops:
        sub = im.crop(c)
        tiles.append((c, sub.resize((round(sub.width * zoom), round(sub.height * zoom)),
                                    Image.NEAREST)))
    W = max(t[1].width for t in tiles) + 40
    H = 70 + sum(t[1].height + 46 for t in tiles)
    sheet = Image.new("RGB", (W, H), (30, 26, 24))
    d = ImageDraw.Draw(sheet)
    d.text((20, 18), "%s  ×%.1f" % (name, zoom), font=_font(22), fill=(240, 232, 220))
    y = 62
    for c, t in tiles:
        d.text((20, y), "裁 %d,%d %dx%d" % c, font=_font(14), fill=(170, 158, 142))
        sheet.paste(t, (20, y + 20))
        y += t.height + 46
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out", "study",
                       "inspect_%s.png" % name)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    sheet.save(out)
    print("写出:", out, sheet.size)


if __name__ == "__main__":
    main()
