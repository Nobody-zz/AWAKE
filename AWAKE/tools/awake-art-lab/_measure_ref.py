#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""参照实测：把原版关键 sprite 的几何与配色量成数字（只读）。

量什么：
  - 尺寸 / 不透明包围盒
  - 水平中线、垂直中线的「颜色分段」（能反推倒角宽度、描边宽度、内衬范围）
  - 主要色（出现频次 top N）
  - 材质噪声强度（标准差）

用法：python _measure_ref.py
"""
import collections
import os

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.normpath(os.path.join(HERE, "..", "awake-ui-lab", "out", "atlas", "sprites"))


def load(fn):
    p = os.path.join(SRC, fn)
    return Image.open(p).convert("RGBA") if os.path.isfile(p) else None


def bbox(im):
    return im.split()[3].getbbox()


def runs(im, axis, idx):
    """沿 axis（'x' 横扫描 / 'y' 纵扫描）取一条线，返回连续同色段。"""
    w, h = im.size
    if axis == "x":
        seq = [im.getpixel((x, idx)) for x in range(w)]
    else:
        seq = [im.getpixel((idx, y)) for y in range(h)]
    out = []
    for i, c in enumerate(seq):
        if out and _near(out[-1][2], c):
            out[-1][1] = i
        else:
            out.append([i, i, c])
    return [(a, b, b - a + 1, c) for a, b, c in out]


def _near(c1, c2, tol=6):
    return all(abs(int(a) - int(b)) <= tol for a, b in zip(c1, c2))


def fmt(c):
    return "#%02X%02X%02X%s" % (c[0], c[1], c[2], "" if c[3] == 255 else "@%d" % c[3])


def top_colors(im, n=10):
    cnt = collections.Counter(im.getdata())
    return [(c, k) for c, k in cnt.most_common(n)]


def report(fn, label):
    im = load(fn)
    print("=" * 78)
    if im is None:
        print("[缺] %s (%s)" % (fn, label))
        return
    w, h = im.size
    print("%s   %dx%d   %s" % (label, w, h, fn))
    bb = bbox(im)
    print("  不透明包围盒:", bb)
    print("  主要色 top10:")
    for c, k in top_colors(im, 10):
        print("     %-16s %6d  (%.1f%%)" % (fmt(c), k, 100.0 * k / (w * h)))
    print("  水平中线色段（x 从 0 起）：")
    for a, b, ln, c in runs(im, "x", h // 2):
        if ln >= 2:
            print("     x %4d-%-4d len %-4d %s" % (a, b, ln, fmt(c)))
    print("  垂直中线色段（y 从 0 起）：")
    for a, b, ln, c in runs(im, "y", w // 2):
        if ln >= 2:
            print("     y %4d-%-4d len %-4d %s" % (a, b, ln, fmt(c)))


def noise(fn, label):
    """材质噪声：只看不透明区域，算灰度标准差（越大越粗粝）。"""
    im = load(fn)
    if im is None:
        return
    px = [p for p in im.getdata() if p[3] > 200]
    if not px:
        print("%s: 全透明" % label)
        return
    lum = [0.299 * r + 0.587 * g + 0.114 * b for r, g, b, _ in px]
    mean = sum(lum) / len(lum)
    var = sum((v - mean) ** 2 for v in lum) / len(lum)
    print("  %-46s 均值 %6.1f  标准差 %6.2f  n=%d" % (label, mean, var ** 0.5, len(px)))


if __name__ == "__main__":
    report("General__Button__main_button_regular.png", "主按钮 Default")
    report("General__Button__button_cancel.png", "取消按钮 Default")
    report("StdAssets__close_button.png", "关闭键 Default")
    report("StdAssets__close_button_hover.png", "关闭键 Hover")

    print()
    print("#" * 78)
    print("面板类（只看尺寸 / 角落 / 噪声）")
    print("#" * 78)
    for fn, label in [
        ("npc_dialogue_panel_9.png", "对话面板底"),
        ("StdAssets__Popup__canvas.png", "弹窗底"),
        ("stone_texture_overlay.png", "石材 overlay"),
        ("General__CharacterCreation__name_input_area.png", "输入区"),
        ("General__CharacterCreation__character_creation_background_gradient.png", "顶栏带"),
        ("GradientDivider_9.png", "金分隔线"),
        ("TitleHeader.png", "标题碑额"),
    ]:
        im = load(fn)
        if im is None:
            print("[缺]", fn)
            continue
        print("  %-28s %4dx%-5d bbox=%s" % (label, im.width, im.height, bbox(im)))

    print()
    print("#" * 78)
    print("材质噪声强度")
    print("#" * 78)
    for fn, label in [
        ("StdAssets__Popup__canvas.png", "弹窗底"),
        ("stone_texture_overlay.png", "石材 overlay"),
        ("stone_texture_continuous.png", "石材 continuous"),
        ("npc_dialogue_panel_9.png", "对话面板底"),
    ]:
        noise(fn, label)

    print()
    print("#" * 78)
    print("对话面板底 · 水平/垂直中线（看金线的位置与宽度）")
    print("#" * 78)
    report("npc_dialogue_panel_9.png", "对话面板底")
