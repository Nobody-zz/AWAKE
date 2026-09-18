# -*- coding: utf-8 -*-
"""一张卡：证明「暗沉」到底差几级 —— 原版木面的色 vs 我们木面的色。

中间那条 9 级渐变条是我从**我们木面的亮度**爬到**原版木面的亮度**，
每一格是 1 级。用来回答"9 级到底看得见吗"——顺带说明
"均值一样"为什么不是证据（均值一样，分布不同，眼睛就不同）。
"""
import os

from PIL import Image, ImageDraw

import make_button_primary as mb

OUT = os.path.join(mb.HERE, "out", "study", "tone")
os.makedirs(OUT, exist_ok=True)

# ── 原版木面（取中段）────────────────────────────────────────────────
REF = os.path.normpath(os.path.join(mb.HERE, "..", "awake-ui-lab", "out", "atlas",
                                    "sprites", "General__Button__main_button_regular.png"))
im = Image.open(REF).convert("RGBA")
a = im.split()[3].point(lambda v: 255 if v > 128 else 0)
body = im.crop(a.getbbox())
body = body.resize((max(1, round(body.width * 35.0 / body.height)), 35), Image.LANCZOS)
ref_wood = body.crop((10, 11, 40, 24)).convert("RGB")

# ── 我们的木面 ───────────────────────────────────────────────────────
cur = Image.open(os.path.join(mb.OUT_BTN, "btn_primary_110.png")).convert("RGB")
now_wood = cur.crop((24, 11, 86, 24))

# ── 版式 ─────────────────────────────────────────────────────────────
K = 7                                    # 放大倍数（像素级看，只能 NEAREST）
CW, CH = 350, 91
PAD, GAP = 22, 26
W = PAD * 2 + CW * 2 + GAP + 60
H = PAD * 2 + 26 + CH * 2 + 26 + 26
canvas = Image.new("RGB", (W, H), (250, 249, 246))
d = ImageDraw.Draw(canvas)
f = None
try:
    from PIL import ImageFont
    f = ImageFont.truetype(r"C:\Windows\Fonts\msyh.ttc", 15)
    f2 = ImageFont.truetype(r"C:\Windows\Fonts\msyh.ttc", 13)
except Exception:
    f2 = f


def up(img):
    return img.resize((img.width * K, img.height * K), Image.NEAREST)


def mean_rgb(img):
    px = img.load()
    n = img.width * img.height
    r = sum(px[x, y][0] for y in range(img.height) for x in range(img.width)) / n
    g = sum(px[x, y][1] for y in range(img.height) for x in range(img.width)) / n
    b = sum(px[x, y][2] for y in range(img.height) for x in range(img.width)) / n
    return r, g, b


r1 = mean_rgb(ref_wood)
r0 = mean_rgb(now_wood)
steps = int(round((sum(r1) - sum(r0)) / 3.0))

x1 = PAD
x0 = PAD + CW + GAP
canvas.paste(up(ref_wood), (x1, PAD + 26))
canvas.paste(up(now_wood), (x0, PAD + 26))
d.text((x1, PAD + 2), "原版木面   #4F4733   R-B +29", fill=(40, 40, 40), font=f)
d.text((x0, PAD + 2), "我们的木面  #484739   R-B +14   （R=G）", fill=(40, 40, 40), font=f)

# ── 中间那条 9 级阶梯：从我们的色爬到原版的色 ─────────────────────────
yb = PAD + 26 + CH + 26
d.text((x1, yb - 20), "同一条色阶：左＝我们的木面亮度，右＝原版木面亮度，每格 1 级（共 %d 级）"
       % steps, fill=(70, 70, 70), font=f2)
seg = (W - PAD * 2) / float(max(steps, 1))
for i in range(steps):
    t = i / float(max(steps - 1, 1))
    c = tuple(int(round(r0[k] + (r1[k] - r0[k]) * t)) for k in range(3))
    d.rectangle((PAD + i * seg, yb, PAD + (i + 1) * seg, yb + 30), fill=c)

out = os.path.join(OUT, "wood_gap.png")
canvas.save(out)
print("-> %s  %dx%d  差 %d 级" % (out, W, H, steps))
