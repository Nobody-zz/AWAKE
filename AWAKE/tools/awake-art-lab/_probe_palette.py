# -*- coding: utf-8 -*-
"""看底图到底能给多少色 —— 我怀疑"灰"是**取块把色抽掉了**，不是底图本来就没色。

对每张底图：
  · 整图饱和度 / 主色相
  · 按 material() 的取块逻辑（CROP_ZOOM 2.0），统计那块的实际饱和度
  · 把取块缩到 110x35，再按 material 的流水线跑一遍（sat / contrast / tint_gain），
    看**最终交付尺寸**上的饱和度还剩多少
不写任何东西进交付目录。输出 out/study/tone/palette.txt
"""
import os
import colorsys

from PIL import Image, ImageStat, ImageEnhance, ImageFilter, ImageChops

import make_button_primary as mb

OUT = os.path.join(mb.HERE, "out", "study", "tone")
os.makedirs(OUT, exist_ok=True)
lines = []


def sat_of(im):
    hsv = im.convert("RGB").convert("HSV")
    return ImageStat.Stat(hsv).mean[1] * 100.0 / 255.0


def hue_hist(im, bins=12):
    hsv = im.convert("RGB").convert("HSV")
    h = hsv.split()[0].histogram()
    tot = sum(h)
    return [100.0 * sum(h[i::bins]) / max(1, tot) for i in range(bins)]


for tag, src, box in (("木 WOOD_SRC", mb.WOOD_SRC, (0.28, 0.28, 0.72, 0.72)),
                      ("铁 IRON_SRC", mb.IRON_SRC, (0.36, 0.46, 0.64, 0.74))):
    lines.append("=" * 70)
    lines.append("%s   %s" % (tag, os.path.basename(src)))
    im = Image.open(src).convert("RGB")
    lines.append("  源图 %dx%d   饱和 %.1f   均值 %s"
                 % (im.width, im.height, sat_of(im),
                    "#%02X%02X%02X" % tuple(int(v) for v in ImageStat.Stat(im).mean[:3])))
    hs = hue_hist(im)
    top = sorted(range(12), key=lambda i: -hs[i])[:3]
    lines.append("  主色相（0=红 30=黄 60=绿 85=青 128=蓝）："
                 + "  ".join("%.0f°%%%4.1f" % (i * 30, hs[i]) for i in top))

    # —— 按 material() 取块
    pw, ph = im.size
    cw, ch = min(pw, mb.CROP[0]), min(ph, mb.CROP[1])
    cx = (box[0] + box[2]) * 0.5 * pw
    cy = (box[1] + box[3]) * 0.5 * ph
    x0 = int(max(0, min(pw - cw, cx - cw * 0.5)))
    y0 = int(max(0, min(ph - ch, cy - ch * 0.5)))
    c = im.crop((x0, y0, x0 + cw, y0 + ch))
    lines.append("  ── 取块（material 口径）%dx%d @(%d,%d)" % (cw, ch, x0, y0))
    lines.append("     取块饱和 %.1f   均值 %s"
                 % (sat_of(c), "#%02X%02X%02X" % tuple(int(v) for v in ImageStat.Stat(c).mean[:3])))
    m = c.resize((110, 35), Image.LANCZOS)
    lines.append("     缩到 110x35 饱和 %.1f" % sat_of(m))

    # —— 再走流水线的后三步（sat / contrast / tint）看剩多少
    for tag2, sat, contrast, tint in (
            ("现用 sat=1.06/无tint", 1.06, 1.08, None),
            ("原参数 sat=1.06/无tint（同）", 1.06, 1.08, None)):
        mm = ImageEnhance.Contrast(m).enhance(contrast)
        mm = ImageEnhance.Color(mm).enhance(sat)
        lines.append("     %s ⇒ 饱和 %.1f" % (tag2, sat_of(mm)))
        break
    # 逐通道增益的作用
    if tag.startswith("木"):
        tg = mb.WOOD_TINT
        chs = list(m.split())
        for i, g in enumerate(tg):
            chs[i] = chs[i].point(lambda v, g=g: min(255, int(round(v * g))))
        mt = Image.merge("RGB", chs)
        lines.append("     再叠 WOOD_TINT%s ⇒ 饱和 %.1f（**逐通道增益就是去饱和**）"
                     % (str(tg), sat_of(mt)))

txt = "\n".join(lines)
open(os.path.join(OUT, "palette.txt"), "w", encoding="utf-8").write(txt)
print(txt)
