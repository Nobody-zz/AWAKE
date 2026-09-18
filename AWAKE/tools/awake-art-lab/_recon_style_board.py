#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""侦察板：把交付的按钮族摆到面板底上，看「四个族是否像一套」。

只读 GUI/SpriteParts/ui_awake_button/*.png，不改任何东西。
产出 out/style-convergence/recon_current.png
"""
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

FAMILIES = [
    ("关闭键 seal 40x40", ["btn_close_40", "btn_close_40_hover", "btn_close_40_pressed"]),
    ("主按钮 plate 110x35", ["btn_primary_110", "btn_primary_110_hover",
                             "btn_primary_110_pressed", "btn_primary_110_disabled"]),
    ("次按钮 plate 100x35", ["btn_secondary_100", "btn_secondary_100_hover",
                             "btn_secondary_100_pressed"]),
    ("页签 plate 105x35", ["btn_tab_105", "btn_tab_105_hover",
                           "btn_tab_105_pressed", "btn_tab_105_selected"]),
]


def _font(sz):
    try:
        return ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", sz)
    except Exception:  # noqa: BLE001
        return ImageFont.load_default()


def load(name):
    return Image.open(os.path.join(BTN, name + ".png")).convert("RGBA")


def main():
    os.makedirs(OUT, exist_ok=True)
    Z = 4
    PADX, GAP = 34, 26
    LEFT = 250

    rows = []
    for label, names in FAMILIES:
        imgs = [load(n) for n in names]
        rows.append((label, names, imgs))

    # 行内还加一条「真用得上的排法」：3 页签间距 5 / 4 主按钮间距 10
    row_w = LEFT + PADX
    for _, _, imgs in rows:
        row_w = max(row_w, LEFT + PADX + sum(i.width * Z + GAP for i in imgs))

    W = row_w + PADX
    line_h = [max(i.height for i in imgs) * Z + 64 for _, _, imgs in rows]
    H = 130 + sum(line_h) + 46
    sheet = Image.new("RGB", (W, H), FIELD)
    d = ImageDraw.Draw(sheet)

    d.text((34, 26), "按钮族现状 · 交付 sprite 铺在面板底上（×%d）" % Z, font=_font(26), fill=INK)
    d.text((34, 62), "判据：四个族摆在一起，像不像同一套料做出来的？"
                     "（形状 / 受光方向 / 金线线重 / 切角 / 材质颗粒）",
           font=_font(15), fill=DIM)

    y = 118
    for (label, names, imgs), lh in zip(rows, line_h):
        d.text((34, y + 6), label, font=_font(17), fill=INK)
        x = LEFT
        for n, im in zip(names, imgs):
            show = im.resize((im.width * Z, im.height * Z), Image.NEAREST)
            bg = Image.new("RGB", show.size, PANEL)
            bg.paste(show, (0, 0), show)
            sheet.paste(bg, (x, y))
            d.rectangle([x - 1, y - 1, x + show.width, y + show.height],
                        outline=(78, 68, 58))
            d.text((x, y + show.height + 6), n.replace("btn_", ""), font=_font(12), fill=DIM)
            x += show.width + GAP
        y += lh

    p = os.path.join(OUT, "recon_current.png")
    sheet.save(p)
    print(p, sheet.size)


if __name__ == "__main__":
    main()
