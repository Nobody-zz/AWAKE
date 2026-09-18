# -*- coding: utf-8 -*-
"""
原版**小控件**长什么样 —— 用来定"110×35 该有多少细节"。

动机：参照物 `General/Button/main_button_regular` 是 **271×84**（面积是本项目的 6 倍），
拿它当"细节标准"会把我们带偏。真正该看的是**同一语境下尺寸相近**的原版控件。

发现：原版对话选项底 `dialog_option_canvas_9` 是 **48×44**（九宫格），
比本项目 110×35（3850 px）面积还小（2112 px）—— 它才是合适的标尺。

产物：out/study/ref_small_controls.png
"""
import os

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.normpath(os.path.join(HERE, "..", "awake-ui-lab", "out", "atlas", "sprites"))
OUT = os.path.join(HERE, "out", "study")

PGC = (128, 128, 128, 255)
DARK = (26, 22, 20, 255)
FONT = r"C:\Windows\Fonts\simkai.ttf"

ITEMS = [
    ("dialog_option_canvas_9", "对话选项底（九宫格）"),
    ("__tint_dialog_option_canvas_white_9__tffc8a468", "同一件的**可染色**版（游戏染成暖琥珀 ffc8a468）"),
    ("StdAssets__checkmark", "勾"),
    ("General__Slider__slider_knob", "滑钮"),
    ("StdAssets__arrow_pointing_left", "左箭头"),
]
Z = 9


def on_bg(im, bg):
    c = Image.new("RGBA", im.size, bg)
    c.alpha_composite(im.convert("RGBA"), (0, 0))
    return c.convert("RGB")


def main():
    os.makedirs(OUT, exist_ok=True)
    f_t = ImageFont.truetype(FONT, 26)
    f_l = ImageFont.truetype(FONT, 17)
    f_s = ImageFont.truetype(FONT, 13)

    loaded = []
    for name, note in ITEMS:
        p = os.path.join(SRC, name + ".png")
        if os.path.exists(p):
            loaded.append((name, note, Image.open(p).convert("RGBA")))

    pad, gap = 40, 34
    tiles = []
    for name, note, im in loaded:
        big = im.resize((im.width * Z, im.height * Z), Image.NEAREST)
        small = im.resize((im.width * 3, im.height * 3), Image.NEAREST)
        band = Image.new("RGB", (big.width, big.height + small.height * 2 + 16), (18, 17, 16))
        band.paste(on_bg(big, PGC), (0, 0))
        band.paste(on_bg(small, PGC), (0, big.height + 8))
        band.paste(on_bg(small, DARK), (0, big.height + 8 + small.height + 8))
        tiles.append((name, note, im, band))

    tw = sum(t[3].width for t in tiles) + gap * (len(tiles) - 1)
    th = max(t[3].height for t in tiles)
    sh = Image.new("RGB", (pad * 2 + tw, pad + 92 + th + 48 + 150), (18, 17, 16))
    d = ImageDraw.Draw(sh)
    d.text((pad, pad), "原版小控件怎么处理细节（这才是 110×35 的标尺）", font=f_t,
           fill=(214, 196, 160))
    d.text((pad, pad + 40),
           "参照物 main_button_regular 是 271×84，面积是本项目的 6 倍 —— 不能拿它当细节标准。"
           "同一语境、尺寸相近的，是下面这几件。",
           font=f_s, fill=(140, 124, 104))
    d.text((pad, pad + 62),
           "每件给三个视角：×9 灰底／×3 灰底／×3 暗底 —— 深色面板上一件控件立不立得住，"
           "取决于它自己的明暗关系，不取决于细节多少。",
           font=f_s, fill=(120, 106, 90))

    x = pad
    y = pad + 92
    for name, note, im, band in tiles:
        d.text((x, y), "%dx%d" % (im.size), font=f_l, fill=(226, 208, 172))
        d.text((x, y + 24), note.replace("**", ""), font=f_s, fill=(146, 130, 108))
        sh.paste(band, (x, y + 48))
        x += band.width + gap

    yf = y + 48 + th + 34
    d.text((pad, yf), "判读提示", font=f_l, fill=(226, 208, 172))
    for j, ln in enumerate([
        "看的是：这么小的面积上，原版留下了几级明暗、几道可辨的线。",
        "若它在 48×44 上都只留 1~2 级明暗 ⇒ 「控件越小、细节越少」是原版自己的口径，",
        "那么本项目 110×35 就不该去承载 271×84 那套四段剖面。",
    ]):
        d.text((pad, yf + 34 + j * 22), ln, font=f_s, fill=(150, 134, 112))

    p = os.path.join(OUT, "ref_small_controls.png")
    sh.save(p)
    print("->", p, sh.size)


if __name__ == "__main__":
    main()

