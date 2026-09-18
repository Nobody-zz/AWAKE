# -*- coding: utf-8 -*-
"""定方向 ① 的**目标色**：原版木面的「亮起来那半」到底是多少。

背景：原版木面整体 `#4F4733`（R-B +28），本项目 `#484739`（R-B +15）。
均值不好用 —— 原版木面**有斑驳**，均值被暗斑拉低。真正被眼睛读成"木头的颜色"
的是**亮的那一半**。所以量 p50 / p75 / p90 的 RGB。
输出 out/study/tone/ref_target.txt
"""
import os

from PIL import Image
import make_button_primary as mb

OUT = os.path.join(mb.HERE, "out", "study", "tone")
REF = os.path.normpath(os.path.join(mb.HERE, "..", "awake-ui-lab", "out", "atlas",
                                    "sprites", "General__Button__main_button_regular.png"))
lines = []


def quant(im, tag, y0f, y1f):
    im = im.convert("RGB")
    W, H = im.size
    px = im.load()
    rs, gs, bs = [], [], []
    for y in range(int(H * y0f), int(H * y1f)):
        for x in range(int(W * 0.10), int(W * 0.90)):
            r, g, b = px[x, y]
            rs.append(r)
            gs.append(g)
            bs.append(b)
    if not rs:
        lines.append("%s (空)" % tag)
        return
    rs.sort()
    gs.sort()
    bs.sort()
    n = len(rs)
    lines.append("%-22s n=%4d" % (tag, n))
    for q, lab in ((0.50, "p50"), (0.75, "p75"), (0.90, "p90")):
        i = min(n - 1, int(n * q))
        r, g, b = rs[i], gs[i], bs[i]
        lines.append("   %s  #%02X%02X%02X  (R %3d  G %3d  B %3d)  R-B %+3d  均 %5.1f"
                     % (lab, r, g, b, r, g, b, r - b, (r + g + b) / 3.0))


im = Image.open(REF).convert("RGBA")
a = im.split()[3].point(lambda v: 255 if v > 128 else 0)
body = im.crop(a.getbbox())
body = body.resize((max(1, round(body.width * 35.0 / body.height)), 35), Image.LANCZOS)
lines.append("== 原版木面（按行比例取中段；x 收 10% 避开端头）==")
for i in range(4):
    quant(body, "  行 %.2f~%.2f" % (0.22 + 0.12 * i, 0.22 + 0.12 * (i + 1)),
          0.22 + 0.12 * i, 0.22 + 0.12 * (i + 1))

p = os.path.join(mb.OUT_BTN, "btn_primary_110.png")
if os.path.exists(p):
    im2 = Image.open(p)
    lines.append("")
    lines.append("== 本项目主按钮木面 ==")
    for i in range(4):
        quant(im2, "  行 %.2f~%.2f" % (0.22 + 0.12 * i, 0.22 + 0.12 * (i + 1)),
              0.22 + 0.12 * i, 0.22 + 0.12 * (i + 1))

txt = "\n".join(lines)
open(os.path.join(OUT, "ref_target.txt"), "w", encoding="utf-8").write(txt)
print(txt)
