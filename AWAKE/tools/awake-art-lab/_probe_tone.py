# -*- coding: utf-8 -*-
"""量"暗沉"。
Max 说「你的质感和颜色太暗沉了」——先分清是**整体明度低**（值）还是**对比不足**（质）
还是**色相发灰/发冷**（色）。

只量四个数：
  · 实心像素的整体均值 / 中位（alpha>128）
  · **面的中位**（木面 = 偏暖的那些像素；铁 = 其余）—— 分开算，"哪个部分暗"才看得出来
  · 饱和度（HSV 的 S）均值 —— "灰"的量化口径
  · 对比度 = p90 - p10

对照：原版 main_button_regular 缩到同高。
输出 out/study/tone/tone.txt
"""
import os
from PIL import Image, ImageStat

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(HERE, "out", "study", "tone")
os.makedirs(OUT, exist_ok=True)
A_MIN = 128


def lum(r, g, b):
    return 0.299 * r + 0.587 * g + 0.114 * b


def stats(im, tag, lines):
    im = im.convert("RGBA")
    w, h = im.size
    px = im.load()
    vals, sats, warms, colds = [], [], [], []
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a <= A_MIN:
                continue
            L = lum(r, g, b)
            vals.append(L)
            mx, mn = max(r, g, b), min(r, g, b)
            sats.append(0.0 if mx == 0 else (mx - mn) * 100.0 / mx)
            # 偏暖 = 木（R 明显大于 B）；其余归铁
            if r - b >= 10:
                warms.append(L)
            else:
                colds.append(L)
    if not vals:
        lines.append("%-22s 无实心像素" % tag)
        return
    vals.sort()
    n = len(vals)

    def pct(p):
        return vals[min(n - 1, int(n * p))]

    lines.append("=" * 70)
    lines.append("%s   %dx%d" % (tag, w, h))
    lines.append("  实心 %d px" % n)
    lines.append("  全局   均值 %5.1f ｜ 中位 %5.1f ｜ p10 %5.1f ｜ p90 %5.1f ｜ 对比 %5.1f"
                 % (sum(vals) / n, pct(0.5), pct(0.10), pct(0.90), pct(0.90) - pct(0.10)))
    lines.append("  饱和 S 均值 %5.1f" % (sum(sats) / len(sats)))
    if warms:
        warms.sort()
        lines.append("  木面   均值 %5.1f ｜ 中位 %5.1f ｜ n=%d  (%.0f%%)"
                     % (sum(warms) / len(warms), warms[len(warms) // 2], len(warms),
                        100.0 * len(warms) / n))
    if colds:
        colds.sort()
        lines.append("  铁面   均值 %5.1f ｜ 中位 %5.1f ｜ n=%d  (%.0f%%)"
                     % (sum(colds) / len(colds), colds[len(colds) // 2], len(colds),
                        100.0 * len(colds) / n))


lines = []
REF = os.path.normpath(os.path.join(HERE, "..", "awake-ui-lab", "out", "atlas",
                                    "sprites", "General__Button__main_button_regular.png"))
REF2 = os.path.normpath(os.path.join(HERE, "..", "awake-ui-lab", "out", "atlas",
                                     "sprites", "General__Button__dialog_option_canvas_9.png"))
for tag, p in (("原版 main_button_regular", REF),
               ("原版 dialog_option_canvas_9", REF2)):
    if os.path.exists(p):
        im = Image.open(p).convert("RGBA")
        a = im.split()[3].point(lambda v: 255 if v > 128 else 0)
        body = im.crop(a.getbbox())
        if body.height > 40:
            body = body.resize((max(1, round(body.width * 35.0 / body.height)), 35),
                               Image.LANCZOS)
        stats(body, tag, lines)
    else:
        lines.append("%s  未找到: %s" % (tag, p))

for name in ("btn_primary_110", "btn_primary_110_hover",
             "btn_secondary_100", "btn_tab_105"):
    p = os.path.join(ROOT, "GUI", "SpriteParts", "ui_awake_button", name + ".png")
    if os.path.exists(p):
        stats(Image.open(p), "本项目 " + name, lines)

txt = "\n".join(lines)
open(os.path.join(OUT, "tone.txt"), "w", encoding="utf-8").write(txt)
print(txt)
