# -*- coding: utf-8 -*-
"""三方对照板：原版 / 改动前 / 改动后 —— **并排、同高、同倍率**。

甲方看形的东西必须先并排（同类物体同高、同底、同倍率放一行），描述无效。
本板只做一件事：把三件东西放大到同一个尺度摆在一行，让他指。
输出 out/study/tone/ab_board.png
"""
import os

from PIL import Image, ImageDraw, ImageFont

import make_button_primary as mb

OUT = os.path.join(mb.HERE, "out", "study", "tone")
os.makedirs(OUT, exist_ok=True)

# 原版
REF = os.path.normpath(os.path.join(mb.HERE, "..", "awake-ui-lab", "out", "atlas",
                                    "sprites", "General__Button__main_button_regular.png"))
im = Image.open(REF).convert("RGBA")
a = im.split()[3].point(lambda v: 255 if v > 128 else 0)
ref = im.crop(a.getbbox())

# 改动后（＋ 改动前的一份备份）
new = Image.open(os.path.join(mb.OUT_BTN, "btn_primary_110.png")).convert("RGBA")
old_p = os.path.join(OUT, "_prev_btn_primary_110.png")
old = Image.open(old_p).convert("RGBA") if os.path.exists(old_p) else None
sec = Image.open(os.path.join(mb.OUT_BTN, "btn_secondary_100.png")).convert("RGBA")

TARGET_H = 35 * 6
rows = [("原版 main_button_regular", ref),
        ("改动前", old),
        ("改动后", new),
        ("次按钮（改动后）", sec)]
rows = [(t, i) for t, i in rows if i is not None]

PAD = 24
LAB = 210
W = LAB + PAD + max(int(i.width * TARGET_H / i.height) for _, i in rows) + PAD
H = PAD + len(rows) * (TARGET_H + 26)
canvas = Image.new("RGB", (W, H), (246, 245, 242))
d = ImageDraw.Draw(canvas)
try:
    f = ImageFont.truetype(r"C:\Windows\Fonts\msyh.ttc", 14)
except Exception:
    f = None

y = PAD
for tag, img in rows:
    k = TARGET_H / float(img.height)
    big = img.resize((max(1, int(img.width * k)), TARGET_H), Image.NEAREST)
    # 贴到一个纯白底上，透明区才看得出来
    cell = Image.new("RGB", big.size, (255, 255, 255))
    cell.paste(big, (0, 0), big)
    canvas.paste(cell, (LAB + PAD, y))
    d.text((PAD, y + TARGET_H / 2 - 9), tag, fill=(50, 50, 50), font=f)
    d.text((LAB + PAD, y + TARGET_H + 4), "%dx%d   ×%.1f" % (img.width, img.height, k),
           fill=(140, 140, 140), font=f)
    y += TARGET_H + 26

out = os.path.join(OUT, "ab_board.png")
canvas.save(out)
print("-> %s  %dx%d" % (out, W, H))
