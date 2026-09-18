# -*- coding: utf-8 -*-
"""量**原版按钮族**的形状 —— 关键问题：同一族里端头语言有几种？
若有多种，说明"多语言"是原版自己就有的，不是我们的错；
若只有一种，那我们就该统一成那一种。
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REFDIR = os.path.normpath(os.path.join(HERE, "..", "awake-ui-lab", "out", "atlas", "sprites"))
OUT = os.path.join(HERE, "out", "study", "shape")

FILES = [
    ("主按钮",       "General__Button__main_button_regular.png"),
    ("主按钮 hover", "General__Button__main_button_regular_hover.png"),
    ("主按钮 big",   "General__Button__main_button_regular_big.png"),
    ("完成按钮",     "General__Button__main_button_done.png"),
    ("取消按钮",     "General__Button__button_cancel.png"),
    ("页中键",       "StdAssets__page_button_center.png"),
    ("关闭键",       "StdAssets__close_button.png"),
]

lines = []
print("%-14s %10s %6s %8s %9s   %s" % ("名字", "尺寸", "长宽比", "满高列", "端头占宽", "端头逐列高（前16列）"))
lines.append("%-14s %10s %6s %8s %9s   %s" % ("名字", "尺寸", "长宽比", "满高列", "端头占宽", "端头逐列高（前16列）"))

for name, f in FILES:
    p = os.path.join(REFDIR, f)
    if not os.path.exists(p):
        print("%-14s [缺]" % name)
        continue
    im = Image.open(p).convert("RGBA")
    a = im.split()[3].point(lambda v: 255 if v > 128 else 0)
    bb = a.getbbox()
    if bb is None:
        print("%-14s [全透明]" % name); continue
    body = im.crop(bb)
    w, h = body.size
    m = body.split()[3].point(lambda v: 255 if v > 128 else 0)
    cols = []
    for x in range(w):
        c = [y for y in range(h) if m.getpixel((x, y)) > 128]
        cols.append((max(c) - min(c) + 1) if c else 0)
    xf = next((x for x, c in enumerate(cols) if c >= h - 0.5), w)
    head = ",".join(str(v) for v in cols[:16])
    print("%-14s %4dx%-4d %6.2f %8d %8.1f%%   %s"
          % (name, w, h, w / float(h), xf, xf / float(w) * 100, head))
    lines.append("%-14s %4dx%-4d %6.2f %8d %8.1f%%   %s"
                 % (name, w, h, w / float(h), xf, xf / float(w) * 100, head))

open(os.path.join(OUT, "family_probe.txt"), "w", encoding="utf-8").write("\n".join(lines))
