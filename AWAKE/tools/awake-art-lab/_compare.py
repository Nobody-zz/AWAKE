#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""出「原版 vs AWAKE 皮肤」对比图。

前置：out/before/ 与 out/after/ 各有同名截图（由 build_m0.py 前后各跑一次 UI Lab 得到）。
用法：python _compare.py
"""
import os

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "out")
PICK = ["NpcDialogue", "WorldEventInbox", "AwakeMessenger"]


def _font(sz):
    try:
        return ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", sz)
    except Exception:  # noqa: BLE001
        return ImageFont.load_default()


def main():
    rows = []
    for n in PICK:
        b = os.path.join(OUT, "before", n + ".png")
        a = os.path.join(OUT, "after", n + ".png")
        if os.path.isfile(b) and os.path.isfile(a):
            rows.append((n, Image.open(b).convert("RGB"), Image.open(a).convert("RGB")))
    if not rows:
        print("没有可对比的截图")
        return
    colw = 620
    tiles = []
    for n, b, a in rows:
        sc = min(1.0, (colw - 20) / b.width)
        bb = b.resize((round(b.width * sc), round(b.height * sc)), Image.LANCZOS)
        aa = a.resize((round(a.width * sc), round(a.height * sc)), Image.LANCZOS)
        tiles.append((n, bb, aa))
    W = 40 + 2 * colw
    H = 110 + sum(max(t[1].height, t[2].height) + 62 for t in tiles) + 20
    sheet = Image.new("RGB", (W, H), (26, 22, 20))
    d = ImageDraw.Draw(sheet)
    d.text((24, 24), "AWAKE UI · 原版（左） vs AWAKE 皮肤（右）", font=_font(28),
           fill=(242, 234, 222))
    d.text((24, 62), "预览是离线几何推导，不执行数据绑定、字体为近似；未换装的浅色块／褐色条是尚未接管的原版元素（M1）。",
           font=_font(15), fill=(152, 142, 128))
    y = 108
    for n, bb, aa in tiles:
        d.text((24, y), n, font=_font(20), fill=(226, 175, 84))
        d.text((40, y + 24), "原版", font=_font(14), fill=(170, 158, 142))
        d.text((40 + colw, y + 24), "AWAKE 皮肤", font=_font(14), fill=(170, 158, 142))
        sheet.paste(bb, (24, y + 46))
        sheet.paste(aa, (24 + colw, y + 46))
        d.rectangle([23, y + 45, 24 + bb.width, y + 47 + bb.height], outline=(64, 56, 48))
        d.rectangle([23 + colw, y + 45, 24 + colw + aa.width, y + 47 + aa.height],
                    outline=(64, 56, 48))
        y += max(bb.height, aa.height) + 62
    p = os.path.join(OUT, "compare_before_after.png")
    sheet.save(p)
    print("写出:", p, sheet.size)


if __name__ == "__main__":
    main()
