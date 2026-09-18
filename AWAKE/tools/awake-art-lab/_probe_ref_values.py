# -*- coding: utf-8 -*-
"""量原版（缩到 h=35）：**各块各占多少面积、各自什么亮度**。

不猜了。上一次是靠眼睛调，把一个方向调反了（体色 96 → 38，结果整块铁变黑）。
这一页要回答三个问题：
  ① 铁（长边轨／端头件）和木，各占多少面积？
  ② 各自亮度区间是多少（min / 均值 / max）？
  ③ 亮的线有多粗（几像素）、占铁面积的多少？

产物 out/study/cheap/ref_values.txt
"""
import os

from PIL import Image

import make_button_primary as mb

HERE = mb.HERE
OUT = os.path.join(HERE, "out", "study", "cheap")
REF = os.path.normpath(os.path.join(HERE, "..", "awake-ui-lab", "out", "atlas",
                                    "sprites", "General__Button__main_button_regular.png"))


def lum(px):
    r, g, b = px[0], px[1], px[2]
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


def ref_body(h=35):
    im = Image.open(REF).convert("RGBA")
    a = im.split()[3].point(lambda v: 255 if v > 128 else 0)
    body = im.crop(a.getbbox())
    w = max(1, int(round(body.width * h / float(body.height))))
    return body.resize((w, h), Image.LANCZOS)


def stats(im, box, name, lines):
    c = im.crop(box)
    vals = sorted(lum(p) for p in c.convert("RGB").getdata())
    n = len(vals)
    if not n:
        return
    lines.append("%-26s 均值 %6.1f ｜ 中位 %6.1f ｜ min %5.1f ｜ max %5.1f ｜ >100 的占 %4.1f%%"
                 % (name, sum(vals) / n, vals[n // 2], vals[0], vals[-1],
                    100.0 * sum(1 for v in vals if v > 100) / n))


def main():
    os.makedirs(OUT, exist_ok=True)
    im = ref_body()
    W, H = im.size
    lines = ["原版实体 → h=%d  ⇒  %dx%d" % (H, W, H), ""]

    # 逐行剖面（全宽均值）——看"哪几行是亮的"
    lines.append("逐行（全宽均值）：")
    px = im.convert("RGB").load()
    for y in range(H):
        row = [lum(px[x, y]) for x in range(W)]
        bar = "#" * int(sum(row) / len(row) / 4)
        lines.append("  y%02d  %6.1f  %s" % (y, sum(row) / len(row), bar))

    lines.append("")
    lines.append("分块：")
    stats(im, (0, 0, 24, H), "端头件 左 x0..23", lines)
    stats(im, (W - 24, 0, W, H), "端头件 右 xW-24..W", lines)
    stats(im, (40, 0, W - 40, 8), "长边轨 上 y0..7", lines)
    stats(im, (40, H - 8, W - 40, H), "长边轨 下 yH-8..H", lines)
    stats(im, (40, 10, W - 40, H - 10), "木面 中段", lines)

    # 亮线的粗细：在 x = W/2 那一列，数 >100 的行数
    col = [lum(px[W // 2, y]) for y in range(H)]
    lines.append("")
    lines.append("中列 x=%d 的逐行亮度：%s" % (W // 2, " ".join("%d" % v for v in col)))
    lines.append("  中列 >100 的行数 = %d（上）／%d（下）"
                 % (sum(1 for y in range(H // 2) if col[y] > 100),
                    sum(1 for y in range(H // 2, H) if col[y] > 100)))

    p = os.path.join(OUT, "ref_values.txt")
    open(p, "w", encoding="utf-8").write("\n".join(lines) + "\n")
    print("\n".join(lines))
    print("->", p)


if __name__ == "__main__":
    main()
