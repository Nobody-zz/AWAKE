# -*- coding: utf-8 -*-
"""按钮「质感/色调」诊断：明度域有多宽、有没有明暗结构。

诊断三件事（对应"灰暗"的三个可能成因）：
  ① 明度域宽度：值是挤在中间（闷），还是拉开到有黑有亮（通透）
  ② 垂直明暗结构：逐行中位明度曲线平不平（有没有受光/背光）
  ③ 分区对比：铁轨 vs 木面 各自占了多宽的值域

对象：我们的 btn_primary_110.png ／ 原版 main_button_regular.png（作基线，
      不是为了对齐它——是为了知道"原版有多窄"，我们比它窄多少）
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

from PIL import Image

OUT = os.path.join(HERE, "out")
STUDY = os.path.join(OUT, "study", "shape")
os.makedirs(STUDY, exist_ok=True)

REF = os.path.normpath(os.path.join(
    HERE, "..", "awake-ui-lab", "out", "atlas", "sprites",
    "General__Button__main_button_regular.png"))
OURS = os.path.join(OUT, "sprites", "btn_primary_110.png")


def lum(px):
    r, g, b = px[0], px[1], px[2]
    return 0.299 * r + 0.587 * g + 0.114 * b


def visible_pixels(im):
    """返回 [(x, y, L)]，只取不透明的。"""
    im = im.convert("RGBA")
    w, h = im.size
    px = im.load()
    out = []
    for y in range(h):
        for x in range(w):
            p = px[x, y]
            if p[3] > 128:
                out.append((x, y, lum(p)))
    return out


def quant(vals):
    vals = sorted(vals)
    n = len(vals)
    if n == 0:
        return {}
    def q(p):
        i = min(n - 1, max(0, int(round(p * (n - 1)))))
        return vals[i]
    return {"min": vals[0], "p05": q(.05), "p25": q(.25), "p50": q(.50),
            "p75": q(.75), "p95": q(.95), "max": vals[-1],
            "mean": sum(vals) / n}


def row_curve(pix, h):
    """逐行中位明度。"""
    buckets = [[] for _ in range(h)]
    for x, y, L in pix:
        buckets[y].append(L)
    out = []
    for b in buckets:
        out.append(sorted(b)[len(b) // 2] if b else None)
    return out


def col_curve(pix, w):
    buckets = [[] for _ in range(w)]
    for x, y, L in pix:
        buckets[x].append(L)
    out = []
    for b in buckets:
        out.append(sorted(b)[len(b) // 2] if b else None)
    return out


def report(tag, im):
    im = im.convert("RGBA")
    # 裁掉透明边距，只看内容
    bbox = im.getbbox()
    if bbox:
        im = im.crop(bbox)
    w, h = im.size
    pix = visible_pixels(im)
    lines = []
    lines.append("=" * 62)
    lines.append("%s   %dx%d（已裁透明边距）  有效像素 %d" % (tag, w, h, len(pix)))
    lines.append("=" * 62)

    q = quant([L for _, _, L in pix])
    lines.append("明度域: min %.0f  p05 %.0f  p25 %.0f  p50 %.0f  p75 %.0f  "
                 "p95 %.0f  max %.0f" % (q["min"], q["p05"], q["p25"], q["p50"],
                                         q["p75"], q["p95"], q["max"]))
    lines.append("  ⇒ 极差 %.0f，中段带宽(p25~p75) %.0f，mean %.0f"
                 % (q["max"] - q["min"], q["p75"] - q["p25"], q["mean"]))

    lines.append("")
    lines.append("逐行中位明度（垂直明暗结构，看有没有受光带/背光带）：")
    rc = row_curve(pix, h)
    for y, v in enumerate(rc):
        if v is None:
            continue
        bar = "#" * int(round(v / 4))
        lines.append("  y%2d  %5.1f  %s" % (y, v, bar))

    lines.append("")
    lines.append("逐列中位明度（水平结构，看端头）：")
    cc = col_curve(pix, w)
    step = max(1, w // 40)
    for x in range(0, w, step):
        v = cc[x]
        if v is None:
            continue
        bar = "#" * int(round(v / 4))
        lines.append("  x%3d  %5.1f  %s" % (x, v, bar))
    lines.append("")
    return "\n".join(lines)


def main():
    txt = []
    txt.append("按钮质感诊断 · 明度域与明暗结构")
    txt.append("（原版只作基线：看它有多窄，不是为了对齐它）")
    txt.append("")

    if os.path.exists(OURS):
        txt.append(report("我们 btn_primary_110", Image.open(OURS)))
    else:
        txt.append("!! 找不到 %s" % OURS)

    if os.path.exists(REF):
        txt.append(report("原版 main_button_regular", Image.open(REF)))
    else:
        txt.append("!! 找不到原版 %s" % REF)

    path = os.path.join(STUDY, "tone_diag.txt")
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(txt))
    print("saved", path)
    print("\n".join(txt[:60]))


if __name__ == "__main__":
    main()
