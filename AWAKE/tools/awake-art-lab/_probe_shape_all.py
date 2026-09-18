# -*- coding: utf-8 -*-
"""**形状总览**：把三个控件的"形"一次量全，作为改形状的依据。

分四层量（甲方 memory 规则 8 的四层里的"形"）：
  ① 长宽比
  ② 端头逐列高（跟原版比）
  ③ 端头占宽比例
  ④ 原生控件 vs 我们控件 的同尺寸对照
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REFDIR = os.path.normpath(os.path.join(HERE, "..", "awake-ui-lab", "out", "atlas", "sprites"))
OUT = os.path.join(HERE, "out", "study", "shape")
os.makedirs(OUT, exist_ok=True)
lines = []


def profile(img):
    """返回 (w, h, [逐列高])"""
    if img.mode != "RGBA":
        img = img.convert("RGBA")
    a = img.split()[3].point(lambda v: 255 if v > 128 else 0)
    bb = a.getbbox()
    body = img.crop(bb)
    w, h = body.size
    m = body.split()[3].point(lambda v: 255 if v > 128 else 0)
    cols = []
    for x in range(w):
        c = [y for y in range(h) if m.getpixel((x, y)) > 128]
        cols.append((max(c) - min(c) + 1) if c else 0)
    return w, h, cols


def describe(name, path):
    if not os.path.exists(path):
        lines.append("  [缺] %s" % path)
        return None
    w, h, cols = profile(Image.open(path))
    ar = w / float(h)
    full = max(cols)
    xf = next((x for x, c in enumerate(cols) if c >= h - 0.5), w)
    lines.append("  %-42s %3dx%-3d  长宽比 %5.2f  满高列@%d  端头占宽 %.1f%%"
                 % (name, w, h, ar, xf, xf / float(w) * 100))
    return w, h, cols


lines.append("① 原版（Bannerlord 自带）")
for f, n in [("General__Button__main_button_regular.png", "主按钮"),
             ("General__Button__secondary_button_regular.png", "次按钮"),
             ("General__Button__tab_button_regular.png", "页签")]:
    describe(n, os.path.join(REFDIR, f))
lines.append("")
lines.append("② 本项目现有产出（若已生成）")
for f, n in [("btn_primary_110.png", "主按钮 110x35"),
             ("btn_secondary_100.png", "次按钮 100x35"),
             ("btn_tab_105.png", "页签 105x35")]:
    describe(n, os.path.join(OUT, "..", "..", f) if "/" in f else os.path.join(HERE, "out", f))
lines.append("")
lines.append("③ 本会话约定尺寸")
for n, w, h in [("主按钮", 110, 35), ("次按钮", 100, 35), ("页签", 105, 35)]:
    lines.append("  %-8s %3dx%-3d  长宽比 %5.2f" % (n, w, h, w / float(h)))
lines.append("  ⚠️ 原版主按钮长宽比 5.52（232x42）；我们 3.14 —— **我们矮胖得多**")

txt = "\n".join(lines)
open(os.path.join(OUT, "shape_all.txt"), "w", encoding="utf-8").write(txt)
print(txt)
