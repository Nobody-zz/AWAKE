# -*- coding: utf-8 -*-
"""**形**的比较板 —— 把像素全部抽掉，只留**轮廓**，规整到同宽同高。

理由：甲方说「这个形状就很丑」。形状是形那一层，不是质感也不是色；
而且**形状类的东西描述无效，必须并排让他指**（同类物体同高、同底、同倍率放一行）。
⇒ 本板去掉所有材质与颜色，只填一个中灰。这样"丑"只可能来自轮廓本身。
"""
import os

from PIL import Image, ImageDraw, ImageFont

import make_button_primary as mb

OUT = os.path.join(mb.HERE, "out", "study", "shape")
os.makedirs(OUT, exist_ok=True)
BOX_W, BOX_H = 560, 150


def silhouette(path, crop_alpha=True):
    im = Image.open(path).convert("RGBA")
    a = im.split()[3].point(lambda v: 255 if v > 128 else 0)
    if crop_alpha:
        bb = a.getbbox()
        im = im.crop(bb)
        a = a.crop(bb)
    flat = Image.new("RGB", im.size, (255, 255, 255))
    flat.paste(Image.new("RGB", im.size, (105, 105, 108)), (0, 0), a)
    return flat


def fit(img, w, h):
    """**非等比**拉伸填满 —— 目的就是"去掉宽高比的干扰"，只看收束曲线的形状本身。"""
    return img.resize((w, h), Image.LANCZOS)


REF = os.path.normpath(os.path.join(mb.HERE, "..", "awake-ui-lab", "out", "atlas",
                                   "sprites", "General__Button__main_button_regular.png"))
items = [
    ("原版 main_button_regular  232x42  宽高比 5.52", REF),
    ("我们的主按钮  110x35  宽高比 3.14",
     os.path.join(mb.OUT_BTN, "btn_primary_110.png")),
    ("我们的次按钮  99x34  宽高比 2.91",
     os.path.join(mb.OUT_BTN, "btn_secondary_100.png")),
    ("我们的页签  104x34  宽高比 3.06",
     os.path.join(mb.OUT_BTN, "btn_tab_105.png")),
]

PAD = 26
LAB = 250
W = LAB + PAD + BOX_W + PAD
H = PAD + len(items) * (BOX_H + 30) + 60
canvas = Image.new("RGB", (W, H), (247, 246, 243))
d = ImageDraw.Draw(canvas)
try:
    f = ImageFont.truetype(r"C:\Windows\Fonts\msyh.ttc", 14)
    f2 = ImageFont.truetype(r"C:\Windows\Fonts\msyh.ttc", 13)
except Exception:
    f = f2 = None

y = PAD
for tag, p in items:
    sil = silhouette(p)
    canvas.paste(fit(sil, BOX_W, BOX_H), (LAB + PAD, y))
    d.text((PAD, y + BOX_H / 2 - 18), tag, fill=(45, 45, 45), font=f)
    y += BOX_H + 30

d.text((PAD, y + 6),
       "全部拉伸到同一个 560x150 框里 —— 宽高比被故意去掉，剩下的就是「收束曲线本身」。",
       fill=(120, 120, 120), font=f2)
d.text((PAD, y + 26),
       "看两端：原版是一条从窄到宽的连续曲线；我们是一条直线切角（就是那个八边形）。",
       fill=(120, 120, 120), font=f2)

out = os.path.join(OUT, "silhouette_board.png")
canvas.save(out)
print("-> %s  %dx%d" % (out, W, H))
