# -*- coding: utf-8 -*-
"""原版铁到底冷不冷？—— 用"位置"而不是"颜色阈值"切木/铁，避免自证。

上一版 _probe_ref_color.py 用 `r-b>=10` 切木面，于是"铁面"的定义变成了
"r-b<10 的像素"。这有自证之嫌：如果原版铁本来就有一点点暖，就会被切进木面里，
剩下的铁面自然显得中性。⇒ 本脚本改用**位置**切：
  木面 = 中间那条横带（去掉上下铁轨）；铁轨 = 最上/最下若干行。
输出 out/study/tone/ref_zone.txt
"""
import os

from PIL import Image

import make_button_primary as mb

OUT = os.path.join(mb.HERE, "out", "study", "tone")
os.makedirs(OUT, exist_ok=True)
REF = os.path.normpath(os.path.join(mb.HERE, "..", "awake-ui-lab", "out", "atlas",
                                    "sprites", "General__Button__main_button_regular.png"))
lines = []


def rows(im, tag, y0f, y1f, x0f=0.06, x1f=0.94):
    """按**行比例**取一条横带（x 也收一点，避开端头装饰）。"""
    im = im.convert("RGBA")
    W, H = im.size
    px = im.load()
    rs, gs, bs = [], [], []
    y0, y1 = int(H * y0f), max(int(H * y1f), int(H * y0f) + 1)
    x0, x1 = int(W * x0f), int(W * x1f)
    for y in range(y0, y1):
        for x in range(x0, x1):
            r, g, b, a = px[x, y]
            if a <= 128:
                continue
            rs.append(r)
            gs.append(g)
            bs.append(b)
    if not rs:
        lines.append("%-30s (空)" % tag)
        return
    n = len(rs)
    mr, mg, mb_ = sum(rs) / n, sum(gs) / n, sum(bs) / n
    v = max(mr, mg, mb_)
    lines.append("%-30s n=%5d  RGB #%02X%02X%02X  R-B %+5.1f  G-B %+5.1f  峰V %5.1f"
                 % (tag, n, int(mr), int(mg), int(mb_), mr - mb_, mg - mb_, v))


im = Image.open(REF).convert("RGBA")
a = im.split()[3].point(lambda v: 255 if v > 128 else 0)
body = im.crop(a.getbbox())
body = body.resize((max(1, round(body.width * 35.0 / body.height)), 35), Image.LANCZOS)
lines.append("== 原版 main_button_regular（缩到 h=35）按行比例切片 ==")
for i in range(9):
    rows(body, "  行 %.2f~%.2f" % (i / 9.0, (i + 1) / 9.0), i / 9.0, (i + 1) / 9.0)

lines.append("")
lines.append("== 对照：本项目主按钮 ==")
p = os.path.join(mb.OUT_BTN, "btn_primary_110.png")
if os.path.exists(p):
    im2 = Image.open(p)
    for i in range(9):
        rows(im2, "  行 %.2f~%.2f" % (i / 9.0, (i + 1) / 9.0), i / 9.0, (i + 1) / 9.0)

txt = "\n".join(lines)
open(os.path.join(OUT, "ref_zone.txt"), "w", encoding="utf-8").write(txt)
print(txt)
