#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""放大侦察：把交付的 4 个族的代表态，同高并排放大看形状与线脚。"""
import os

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", ".."))
BTN = os.path.join(REPO, "GUI", "SpriteParts", "ui_awake_button")
OUT = os.path.join(HERE, "out", "style-convergence")
FIELD = (26, 21, 18)
PANEL = (40, 34, 29)
INK = (238, 230, 218)
DIM = (150, 140, 128)

NAMES = ["btn_close_40", "btn_primary_110", "btn_secondary_100",
         "btn_tab_105", "btn_tab_105_selected"]


def _font(sz):
    try:
        return ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", sz)
    except Exception:  # noqa: BLE001
        return ImageFont.load_default()


def main():
    os.makedirs(OUT, exist_ok=True)
    Z = 6
    imgs = []
    for n in NAMES:
        im = Image.open(os.path.join(BTN, n + ".png")).convert("RGBA")
        imgs.append((n, im.resize((im.width * Z, im.height * Z), Image.LANCZOS)))

    GAP, PADX = 40, 34
    W = PADX + sum(i.width + GAP for _, i in imgs) + PADX
    H = 110 + max(i.height for _, i in imgs) + 60
    sheet = Image.new("RGB", (W, H), FIELD)
    d = ImageDraw.Draw(sheet)
    d.text((PADX, 26), "交付 sprite 放大 ×%d（元数据 = 真像素 %s）" % (Z, " / ".join(
        "%s %dx%d" % (n.replace("btn_", ""), Image.open(os.path.join(BTN, n + ".png")).width,
                      Image.open(os.path.join(BTN, n + ".png")).height) for n in NAMES)),
        font=_font(20), fill=INK)
    x = PADX
    for n, im in imgs:
        bg = Image.new("RGB", im.size, PANEL)
        bg.paste(im, (0, 0), im)
        sheet.paste(bg, (x, 84))
        d.rectangle([x - 1, 83, x + im.width, 84 + im.height], outline=(78, 68, 58))
        d.text((x, 84 + im.height + 8), n.replace("btn_", ""), font=_font(14), fill=DIM)
        x += im.width + GAP
    p = os.path.join(OUT, "recon_zoom.png")
    sheet.save(p)
    print(p, sheet.size)


if __name__ == "__main__":
    main()
