# -*- coding: utf-8 -*-
"""三控件新轮廓三重自检：逐列高 / 左右镜像 / 断列。

对象：btn_primary_110 / btn_secondary_100 / btn_tab_105（各 110/100/105 × 35）
读法：直接从交付目录读 png，逐列量不透明区高度。
"""
import os
import sys
from PIL import Image

BASE = os.path.dirname(os.path.abspath(__file__))
BTN = os.path.join(BASE, "..", "..", "GUI", "SpriteParts", "ui_awake_button")

CASES = [("btn_primary_110", 110, 35),
         ("btn_secondary_100", 100, 35),
         ("btn_tab_105", 105, 35)]

lines = []
for name, w, h in CASES:
    p = os.path.join(BTN, name + ".png")
    if not os.path.exists(p):
        lines.append("!! 缺文件 %s" % p)
        continue
    im = Image.open(p).convert("RGBA")
    W, H = im.size
    px = im.load()

    def colh(x):
        col = [y for y in range(H) if px[x, y][3] > 127]
        return (max(col) - min(col) + 1) if col else 0

    hs = [colh(x) for x in range(W)]
    lines.append("== %s.png  %dx%d ==" % (name, W, H))

    # ① 断列
    gaps = [x for x, v in enumerate(hs) if v == 0]
    lines.append("   ① 断列: %s" % ("无（全列有料）" if not gaps else str(gaps)))

    # ② 左右镜像（列高）
    mir = []
    for x in range(W):
        if hs[x] != hs[W - 1 - x]:
            mir.append((x, hs[x], hs[W - 1 - x]))
    lines.append("   ② 左右镜像: %s"
                 % ("OK（逐列高完全对称）" if not mir else
                    "%d 列不齐，前 8: %s" % (len(mir), mir[:8])))

    # ③ 上下镜像（行宽）
    def roww(y):
        row = [x for x in range(W) if px[x, y][3] > 127]
        return (max(row) - min(row) + 1) if row else 0

    rw = [roww(y) for y in range(H)]
    vm = []
    for y in range(H):
        if rw[y] != rw[H - 1 - y]:
            vm.append((y, rw[y], rw[H - 1 - y]))
    lines.append("   ③ 上下镜像: %s"
                 % ("OK（逐行宽完全对称）" if not vm else
                    "%d 行不齐，前 8: %s" % (len(vm), vm[:8])))

    # ④ 端头逐列高（左右各 20 列）
    lines.append("   ④ 左端头逐列高 x=0..19: %s"
                 % " ".join("%d" % hs[x] for x in range(min(20, W))))
    lines.append("   ⑤ 中心列高 x=%d: %d（应 = %d）" % (W // 2, hs[W // 2], H))
    lines.append("")

txt = "\n".join(lines)
out = os.path.join(BASE, "out", "study", "shape", "verify_three.txt")
with open(out, "w", encoding="utf-8") as f:
    f.write(txt)
print(txt)
