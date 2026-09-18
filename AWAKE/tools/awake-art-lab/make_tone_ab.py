# -*- coding: utf-8 -*-
"""按钮"暗沉"改版 —— 对照页。

Max：「不好看。你的质感和颜色太暗沉了」。
量出来的病根（`_probe_tone.py` / `_probe_palette.py`）：
  · 饱和：原版按钮 **27.1**，本项目主按钮 **13.5** —— 灰了一半。
    其中铁面：底图取块本来就只有 **9.0**（木是 39.7）⇒ 铁**从一开始就没色**。
    木面：`WOOD_TINT (0.90,1.05,0.94)` 名义"只动色相"，实际把饱和 43.9 → **36.2**（去色管道）。
  · 明度其实**没问题**（木面 68.2 vs 原版 68.5）。

本脚本在**不改主生成器默认值**的前提下，用参数覆盖试几档色，出一张并排页给甲方挑。
输出 out/study/tone/tone_ab.png（并排，同底同倍率）

⚠️ 口径：这是**对照研究**，不落交付目录（`out/` 不在 git）。
   选中哪档，再把值写回 make_button_primary.py。
"""
import os

from PIL import Image, ImageDraw, ImageFont

import make_button_primary as mb

OUT = os.path.join(mb.HERE, "out", "study", "tone")
os.makedirs(OUT, exist_ok=True)

FONT = mb.FONT


def sat_of(im):
    from PIL import ImageStat
    return ImageStat.Stat(im.convert("RGB").convert("HSV")).mean[1] * 100.0 / 255.0


# 待试的档位：(名字, 说明, wood_sat, wood_tint, iron_sat, iron_lift, iron_hi_tint, hi_mix)
CANDIDATES = [
    ("A 现状", "现用值（灰铁＋橄榄木）", 0.55, (0.90, 1.05, 0.94), 0.34, 1.20, (188, 192, 198), 0.14),
    ("B 铁回暖", "只把铁还成暖铁（木不动）", 0.55, (0.90, 1.05, 0.94), 0.30, 0.82, (176, 168, 152), 0.16),
    ("C 同 B＋木还色", "铁回暖 ＋ WOOD_TINT 降档", 0.55, (0.97, 1.03, 0.96), 0.30, 0.82, (176, 168, 152), 0.16),
    ("D 再加饱和", "同 C ＋ 木/铁 各再提一档", 0.72, (0.99, 1.02, 0.97), 0.44, 0.82, (176, 168, 152), 0.16),
]


def render(name, note, wsat, wtint, isat, ilift, ihit, himix):
    """临时改模块级常量 → 出一态 → 立刻还原。不落交付目录。"""
    saved = (mb.WOOD_SAT, mb.WOOD_TINT, mb.IRON_SAT, mb.IRON_LIFT,
             mb.IRON_HI_TINT, mb.IRON_HI_MIX)
    mb.WOOD_SAT, mb.WOOD_TINT = wsat, wtint
    mb.IRON_SAT, mb.IRON_LIFT = isat, ilift
    mb.IRON_HI_TINT, mb.IRON_HI_MIX = ihit, himix
    try:
        gold = mb.sample_gold(mb.load_plaque(mb.IRON_SRC))
        states = mb.build_states(gold)
        im = states["soft"][""]
    finally:
        (mb.WOOD_SAT, mb.WOOD_TINT, mb.IRON_SAT, mb.IRON_LIFT,
         mb.IRON_HI_TINT, mb.IRON_HI_MIX) = saved
    return im


print("重渲染中（每档约十几秒）…")
rows = []
for name, note, *args in CANDIDATES:
    im = render(name, note, *args)
    rows.append((name, note, im))
    print("  %-14s 饱和 %5.1f" % (name, sat_of(im)))

# —— 并排页 ——
ZOOM = 4
PAD, GAP = 44, 30
cw, ch = mb.W * ZOOM, mb.H * ZOOM
LABEL_H, NOTE_H = 46, 34
Wp = PAD * 2 + cw
Hp = PAD * 2 + (LABEL_H + ch + NOTE_H + GAP) * len(rows)
page = Image.new("RGB", (Wp, Hp), (26, 24, 23))
d = ImageDraw.Draw(page)
f_lab = ImageFont.truetype(FONT, 30)
f_note = ImageFont.truetype(FONT, 22)

y = PAD
for name, note, im in rows:
    d.text((PAD, y), name, font=f_lab, fill=(238, 232, 222))
    d.text((PAD + 260, y + 6), note, font=f_note, fill=(168, 158, 146))
    d.text((Wp - PAD - 240, y + 6), "饱和 %.1f" % sat_of(im), font=f_note, fill=(168, 158, 146))
    y += LABEL_H
    big = im.resize((cw, ch), Image.NEAREST)
    page.paste(big.convert("RGB"), (PAD, y))
    y += ch + NOTE_H + GAP

p = os.path.join(OUT, "tone_ab.png")
page.save(p)
print("-> %s  %dx%d" % (p, page.width, page.height))
