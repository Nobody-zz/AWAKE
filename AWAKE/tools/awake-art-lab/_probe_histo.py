# -*- coding: utf-8 -*-
"""明度分布直方图对比 —— 均值一样也能差很远。

Max：「质感和颜色太暗沉」。
前两轮量了均值（木 68.2 vs 原版 68.5，一样）和全局饱和（13.5 vs 27.1），
但**眼睛说还是暗**。⇒ 得看**分布**，不是均值。

只输出两组数：直方图百分位 ＋ 高光段占比。
输出 out/study/tone/histo.txt
"""
import os

from PIL import Image

import make_button_primary as mb

OUT = os.path.join(mb.HERE, "out", "study", "tone")
os.makedirs(OUT, exist_ok=True)
A_MIN = 128
REF = os.path.normpath(os.path.join(mb.HERE, "..", "awake-ui-lab", "out", "atlas",
                                    "sprites", "General__Button__main_button_regular.png"))


def lum(r, g, b):
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


def collect(im, mask_pred=None):
    im = im.convert("RGBA")
    px = im.load()
    vals = []
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a <= A_MIN:
                continue
            if mask_pred and not mask_pred(r, g, b):
                continue
            vals.append(lum(r, g, b))
    return vals


def report(vals, tag, lines):
    if not vals:
        lines.append("%-28s (空)" % tag)
        return
    vals.sort()
    n = len(vals)

    def p(q):
        return vals[min(n - 1, int(n * q))]

    # 分箱
    bins = [0] * 8
    for v in vals:
        bins[min(7, int(v // 32))] += 1
    lines.append("%-28s n=%5d" % (tag, n))
    lines.append("   p05 %5.1f  p25 %5.1f  p50 %5.1f  p75 %5.1f  p95 %5.1f ｜ 均值 %5.1f"
                 % (p(.05), p(.25), p(.50), p(.75), p(.95), sum(vals) / n))
    lines.append("   高光 >100 占 %5.1f%% ｜ >85 占 %5.1f%% ｜ >70 占 %5.1f%%"
                 % (100.0 * sum(1 for v in vals if v > 100) / n,
                    100.0 * sum(1 for v in vals if v > 85) / n,
                    100.0 * sum(1 for v in vals if v > 70) / n))
    lines.append("   暗部 <30 占 %5.1f%% ｜ 分箱 %s"
                 % (100.0 * sum(1 for v in vals if v < 30) / n,
                    " ".join("%d:%d%%" % (i * 32, round(100.0 * b / n)) for i, b in enumerate(bins))))


lines = []
if os.path.exists(REF):
    im = Image.open(REF).convert("RGBA")
    a = im.split()[3].point(lambda v: 255 if v > 128 else 0)
    body = im.crop(a.getbbox())
    body = body.resize((max(1, round(body.width * 35.0 / body.height)), 35), Image.LANCZOS)
    report(collect(body), "原版 main_button_regular", lines)
    report(collect(body, lambda r, g, b: r - b >= 10), "  原版 · 木面", lines)
    report(collect(body, lambda r, g, b: r - b < 10), "  原版 · 铁面", lines)

for name in ("btn_primary_110", "btn_primary_110_hover", "btn_secondary_100", "btn_tab_105"):
    pth = os.path.join(mb.OUT_BTN, name + ".png")
    if not os.path.exists(pth):
        continue
    im = Image.open(pth)
    report(collect(im), "本项目 " + name, lines)
    report(collect(im, lambda r, g, b: r - b >= 10), "  本项目 · 木面", lines)
    report(collect(im, lambda r, g, b: r - b < 10), "  本项目 · 铁面", lines)

txt = "\n".join(lines)
open(os.path.join(OUT, "histo.txt"), "w", encoding="utf-8").write(txt)
print(txt)
