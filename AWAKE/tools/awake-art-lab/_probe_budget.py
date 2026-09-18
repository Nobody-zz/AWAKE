# -*- coding: utf-8 -*-
"""
把「面 / 框 / 线」的宽度预算量准 —— 这是上一轮诊断缺的那一步。

动机（2026-09-14 02:2x）：
  Max 拍板「按原版语汇重做」。重做前必须先知道**原版在 h=35 时各段各占几 px**，
  否则又变成凭感觉调参（前面十一轮就是这么烧掉的）。

量什么：
  A. 纵向剖面（中央 60% 宽的平均）—— 上框带几行、面几行、下框带几行
  B. 横向剖面（垂直正中一行带）—— 左框带几列、面几列、右框带几列
  C. 判「铁 / 木」靠**暖度**（R-B），不是亮度：铁是中性冷灰、木是暖褐
  D. 每一行/列打印 均值RGB / 亮度 / 暖度，让人（和后续脚本）能直接读出台阶

产物：out/study/budget_probe.txt（纯文本，不画图）
"""
import os

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SPRITES = os.path.normpath(os.path.join(HERE, "..", "awake-ui-lab", "out", "atlas", "sprites"))
PARTS = os.path.normpath(os.path.join(HERE, "..", "..", "GUI", "SpriteParts", "ui_awake_button"))
OUT = os.path.join(HERE, "out", "study")

REF = os.path.join(SPRITES, "General__Button__main_button_regular.png")
MINE = os.path.join(PARTS, "btn_primary_110.png")


def lum(px):
    r, g, b, a = px
    if a == 0:
        return None
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


def warm(px):
    r, g, b, a = px
    return r - b


def line(tag, y_or_x, px):
    r, g, b, a = px
    L = lum(px)
    if L is None:
        return "%s %3d | ----  (alpha 0)" % (tag, y_or_x)
    return "%s %3d | %3d %3d %3d a%3d | L %5.1f | warm %+4d" % (tag, y_or_x, r, g, b, a, L, warm(px))


def fit_h(im, h):
    w = max(1, int(round(im.width * h / float(im.height))))
    return im.resize((w, h), Image.LANCZOS)


def avg_col(im, y, x0, x1):
    """y 行、x0..x1 的平均颜色（只算 alpha>0）"""
    acc = [0, 0, 0]
    n = 0
    for x in range(x0, x1):
        p = im.getpixel((x, y))
        if p[3] > 0:
            acc[0] += p[0]
            acc[1] += p[1]
            acc[2] += p[2]
            n += 1
    if n == 0:
        return (0, 0, 0, 0)
    return (acc[0] // n, acc[1] // n, acc[2] // n, 255)


def avg_row(im, x, y0, y1):
    acc = [0, 0, 0]
    n = 0
    for y in range(y0, y1):
        p = im.getpixel((x, y))
        if p[3] > 0:
            acc[0] += p[0]
            acc[1] += p[1]
            acc[2] += p[2]
            n += 1
    if n == 0:
        return (0, 0, 0, 0)
    return (acc[0] // n, acc[1] // n, acc[2] // n, 255)


def section(out, name, im):
    w, h = im.size
    out.append("=" * 78)
    out.append("%s  %dx%d" % (name, w, h))
    out.append("=" * 78)

    x0 = int(w * 0.20)
    x1 = int(w * 0.80)
    out.append("-- A 纵向剖面（x %d..%d 平均）" % (x0, x1))
    for y in range(h):
        out.append(line("y", y, avg_col(im, y, x0, x1)))

    yc0 = int(h * 0.35)
    yc1 = int(h * 0.65) + 1
    out.append("-- B 横向剖面（y %d..%d 平均）" % (yc0, yc1))
    for x in range(w):
        out.append(line("x", x, avg_row(im, x, yc0, yc1)))

    # C 端点台阶：左右各 24 列，看是否 2~3 行台阶
    out.append("-- C 左端 0..23 每列（判框带台阶）")
    for x in range(min(24, w)):
        out.append(line("x", x, avg_row(im, x, yc0, yc1)))
    out.append("")


def main():
    os.makedirs(OUT, exist_ok=True)
    out = []
    if os.path.exists(REF):
        section(out, "原版 main_button_regular（按高缩到 35）", fit_h(Image.open(REF).convert("RGBA"), 35))
    else:
        out.append("!! 缺 %s" % REF)
    if os.path.exists(MINE):
        section(out, "本项目 btn_primary_110", Image.open(MINE).convert("RGBA"))
    else:
        out.append("!! 缺 %s" % MINE)

    p = os.path.join(OUT, "budget_probe.txt")
    with open(p, "w", encoding="utf-8") as f:
        f.write("\n".join(out))
    print("->", p, len(out), "lines")


if __name__ == "__main__":
    main()
