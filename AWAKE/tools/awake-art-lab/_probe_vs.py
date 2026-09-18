# -*- coding: utf-8 -*-
"""把"廉价感"变成一张能对数的表：原版 h35 vs 我们，同一套分块定义。

只做一件事——**同底、同高、同口径**地量两边，输出到 out/study/cheap/vs_values.txt。
分块定义（1× 像素）：
  端头件   : x ∈ [0, TIPW)   与 [W-TIPW, W)
  长边轨   : y ∈ [0, 7)      与 [H-7, H)  ，x 取中段 1/3，避开端头
  木面中段 : x 中段 1/3、y ∈ [8, H-8)
另附：全宽逐行均值剖面、铁木**面积占比**（按"该块里有多少是木色"粗估）。
"""
import os

from PIL import Image

import make_button_primary as mb

HERE = mb.HERE
OUT = os.path.join(HERE, "out", "study", "cheap")
REF = os.path.normpath(os.path.join(HERE, "..", "awake-ui-lab", "out", "atlas",
                                    "sprites", "General__Button__main_button_regular.png"))
TIPW = 24          # 端头件区宽（1×）——原版按同高折算约 24，我们也定 24
RAIL = 7


def lum(px):
    return 0.2126 * px[0] + 0.7152 * px[1] + 0.0722 * px[2]


def ref_body():
    im = Image.open(REF).convert("RGBA")
    a = im.split()[3].point(lambda v: 255 if v > 128 else 0)
    body = im.crop(a.getbbox())
    w = max(1, int(round(body.width * 35 / float(body.height))))
    return body.resize((w, 35), Image.LANCZOS)


# ⛔ 10:3x 修的一个**测量坑**：第一版把 RGBA 直接 convert("RGB") 再统计，
#    于是**全透明像素的 RGB（≈0）被当成"很暗的铁"**算进了均值和逐行剖面 ——
#    端头那一块本来就有大量透视背景（收尖），均值被凭空压低一大截。
#    ⇒ 一切统计只计 **alpha > 128** 的像素；逐行剖面也改成"该行实心像素的均值"。
A_MIN = 128


def stats(im, x0, x1, y0, y1, tag, lines):
    px = im.load()
    vals = []
    for y in range(max(0, y0), min(im.height, y1)):
        for x in range(max(0, x0), min(im.width, x1)):
            r, g, b, a = px[x, y]
            if a > A_MIN:
                vals.append(lum((r, g, b)))
    if not vals:
        lines.append("%-14s  (空)" % tag)
        return
    vals.sort()
    n = len(vals)
    mean = sum(vals) / n
    med = vals[n // 2]
    hi = sum(1 for v in vals if v > 100)
    lines.append("%-14s 均值 %6.1f ｜ 中位 %6.1f ｜ min %5.1f ｜ max %6.1f ｜ >100 占 %5.1f%% ｜ n=%d"
                 % (tag, mean, med, vals[0], vals[-1], 100.0 * hi / n, n))


def report(im, name, lines):
    im = im.convert("RGBA")
    W, H = im.size
    px = im.load()
    lines.append("=" * 96)
    lines.append("%s   %dx%d" % (name, W, H))
    lines.append("-" * 96)
    lines.append("逐行均值（**只计实心像素**，alpha>%d）：" % A_MIN)
    for y in range(H):
        vs = [lum(px[x, y][:3]) for x in range(W) if px[x, y][3] > A_MIN]
        if not vs:
            lines.append("  y%02d       -  (全透明)" % y)
            continue
        s = sum(vs) / float(len(vs))
        bar = "#" * int(s / 4.0)
        lines.append("  y%02d %7.1f  %s  (%d/%d 实心)" % (y, s, bar, len(vs), W))
    lines.append("-" * 96)
    mid0, mid1 = W // 3, W - W // 3
    stats(im, 0, TIPW, 0, H, "端头件 左", lines)
    stats(im, W - TIPW, W, 0, H, "端头件 右", lines)
    stats(im, mid0, mid1, 0, RAIL, "长边轨 上", lines)
    stats(im, mid0, mid1, H - RAIL, H, "长边轨 下", lines)
    stats(im, mid0, mid1, RAIL + 1, H - RAIL - 1, "木面 中段", lines)
    # 铁 / 木 面积比：按"实心且偏暖（黄）"粗判木面
    wood = 0
    solid = 0
    for y in range(H):
        for x in range(W):
            r, g, b, a = px[x, y]
            if a <= A_MIN:
                continue
            solid += 1
            if r > b + 8 and g > b + 6:      # 暖/黄 ⇒ 木
                wood += 1
    lines.append("-" * 96)
    lines.append("实心像素 %d ｜ 其中偏暖(木) %d  ⇒ 木面占实心面积 %.1f%%"
                 % (solid, wood, 100.0 * wood / max(1, solid)))
    lines.append("")


def main():
    os.makedirs(OUT, exist_ok=True)
    lines = []
    ref = ref_body()
    ours = Image.open(os.path.join(mb.OUT_BTN, "btn_primary_110.png")).convert("RGB")
    report(ref, "原版（缩到 h35）", lines)
    report(ours, "我们 btn_primary_110", lines)
    p = os.path.join(OUT, "vs_values.txt")
    with open(p, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    print("->", p)


if __name__ == "__main__":
    main()
