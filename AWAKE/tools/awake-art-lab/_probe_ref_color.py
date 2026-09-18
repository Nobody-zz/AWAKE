# -*- coding: utf-8 -*-
"""把原版的「色」量成可抄的数字 —— 铁到底多冷多亮、木到底什么色相。

前几轮一直在对比"明度"(L)，但甲方说的是「颜色暗沉」⇒ 要的是**色相与饱和**。
本脚本只输出两件事：
  ① 铁面 / 木面 的 **RGB 均值**（＝"那块是什么颜色"，不是"多亮"）
  ② 色相分布（HSV 的 H 直方图）＋ 明度 → 给出可直接抄的**目标色**
输出 out/study/tone/ref_color.txt
"""
import os
import colorsys

from PIL import Image, ImageStat

import make_button_primary as mb

OUT = os.path.join(mb.HERE, "out", "study", "tone")
os.makedirs(OUT, exist_ok=True)
REF = os.path.normpath(os.path.join(mb.HERE, "..", "awake-ui-lab", "out", "atlas",
                                    "sprites", "General__Button__main_button_regular.png"))
A_MIN = 128
lines = []


def analyze(im, tag, pred=None):
    im = im.convert("RGBA")
    px = im.load()
    rs, gs, bs, hs, ss_, vs = [], [], [], [], [], []
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a <= A_MIN:
                continue
            if pred and not pred(r, g, b):
                continue
            rs.append(r)
            gs.append(g)
            bs.append(b)
            h, s, v = colorsys.rgb_to_hsv(r / 255.0, g / 255.0, b / 255.0)
            hs.append(h * 360)
            ss_.append(s * 100)
            vs.append(v * 100)
    if not rs:
        lines.append("%-28s (空)" % tag)
        return
    n = len(rs)
    mr, mg, mb_ = sum(rs) / n, sum(gs) / n, sum(bs) / n
    mh, ms, mv = sum(hs) / n, sum(ss_) / n, sum(vs) / n
    hs.sort()
    lines.append("%-28s n=%5d" % (tag, n))
    lines.append("   RGB 均值  #%02X%02X%02X   (%.0f, %.0f, %.0f)"
                 % (int(mr), int(mg), int(mb_), mr, mg, mb_))
    lines.append("   H %5.1f°  S %5.1f%%  V %5.1f%%   ｜ 色相中位 %5.1f°（R-B = %+.0f）"
                 % (mh, ms, mv, hs[n // 2], mr - mb_))


if os.path.exists(REF):
    im = Image.open(REF).convert("RGBA")
    a = im.split()[3].point(lambda v: 255 if v > 128 else 0)
    body = im.crop(a.getbbox())
    body = body.resize((max(1, round(body.width * 35.0 / body.height)), 35), Image.LANCZOS)
    analyze(body, "原版 全部")
    analyze(body, "原版 · 木面", lambda r, g, b: r - b >= 10)
    analyze(body, "原版 · 铁面", lambda r, g, b: r - b < 10)

for name in ("btn_primary_110", "btn_secondary_100"):
    p = os.path.join(mb.OUT_BTN, name + ".png")
    if os.path.exists(p):
        im = Image.open(p)
        analyze(im, "本项目 " + name)
        analyze(im, "  本项目 · 木面", lambda r, g, b: r - b >= 10)
        analyze(im, "  本项目 · 铁面", lambda r, g, b: r - b < 10)

txt = "\n".join(lines)
open(os.path.join(OUT, "ref_color.txt"), "w", encoding="utf-8").write(txt)
print(txt)
