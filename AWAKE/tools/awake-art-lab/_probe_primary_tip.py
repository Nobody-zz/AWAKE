# -*- coding: utf-8 -*-
"""主按钮端头：旧（二次贝塞尔半圆头）vs 新（curve_shape 曲线）→ 逐列高对照。

同时量：平头高度（最外列的实心高度），与原版 232×42 的对照（缩放后）。
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from PIL import Image, ImageDraw
import curve_shape as CS
import make_button_primary as M

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out", "study", "shape")
os.makedirs(OUT, exist_ok=True)

W, H = M.W, M.H
SS = 8
TL = CS.TIP_LEN_PX


def col_heights(pts, w, h, ss=SS):
    """在 ss 倍画布上填充，缩回 1×，逐列量实心高度。"""
    m = Image.new("L", (w * ss, h * ss), 0)
    ImageDraw.Draw(m).polygon([(x * ss, y * ss) for x, y in pts], fill=255)
    m = m.resize((w, h), Image.LANCZOS)
    px = m.load()
    out = []
    for x in range(w):
        col = [y for y in range(h) if px[x, y] > 127]
        out.append((max(col) - min(col) + 1) if col else 0)
    return out


# ---- 旧：二次贝塞尔半圆头 ----
old = M.end_taper_points(W, H, TL, M.TIP_H)
oh = col_heights(old, W, H)

# ---- 新：curve_shape 曲线（坐标语义是 0..w-1 像素格，而 end_taper 是 0..w 连续）----
# 用 tip_curve_pts 的连续版语义对齐：它按 (width, height) 的像素格算，x∈[0,w-1]
new = CS.tip_curve_pts(W, H, TL)
nh = col_heights(new, W, H)

# ---- 原版参考（232×42，缩到 H 高）----
import glob
ref = None
for cand in ("out/atlas/sprites/General__Button__main_button_regular.png",
             "../awake-ui-lab/out/atlas/sprites/General__Button__main_button_regular.png"):
    p = os.path.join(os.path.dirname(os.path.abspath(__file__)), cand)
    if os.path.exists(p):
        ref = p
        break

lines = []
lines.append("主按钮 %dx%d  端头长 %.0fpx" % (W, H, TL))
lines.append("")
lines.append("左端头逐列高（x=0 起，到 2·TL=%.0f）" % (2 * TL))
lines.append("  x   旧(贝塞尔半圆)  新(曲线)")
for x in range(0, int(2 * TL) + 1):
    lines.append("  %2d      %2d            %2d" % (x, oh[x], nh[x]))
lines.append("")
lines.append("平头（x=0 列）高度：旧 %d / 新 %d" % (oh[0], nh[0]))

# 满高列位置（首次达到 H 的列）
def first_full(hs):
    for i, v in enumerate(hs):
        if v >= H - 1:
            return i
    return -1

lines.append("满高列位置：旧 x=%d / 新 x=%d" % (first_full(oh), first_full(nh)))
lines.append("中段直边列高：旧 %d / 新 %d（应均为 %d）"
             % (oh[W // 2], nh[W // 2], H))

if ref:
    im = Image.open(ref).convert("RGBA")
    rw, rh = im.size
    alpha = im.split()[-1]
    rpx = alpha.load()
    # 逐列高（原尺寸）
    raw = []
    for x in range(rw):
        col = [y for y in range(rh) if rpx[x, y] > 127]
        raw.append((max(col) - min(col) + 1) if col else 0)
    lines.append("")
    lines.append("原版参考 %dx%d，端头逐列高（x=0..24）" % (rw, rh))
    lines.append("  " + " ".join("%d" % raw[x] for x in range(min(25, rw))))
    # 缩放到 H 高后的等效列高（原版满高 rh → 我们 H）
    k = H / float(rh)
    lines.append("  缩到 %d 高后（×%.3f）：" % (H, k))
    lines.append("  " + " ".join("%.1f" % (raw[x] * k) for x in range(min(25, rw))))
    tf = None
    for i, v in enumerate(raw):
        if v >= rh * 0.98:
            tf = i
            break
    if tf is None:
        lines.append("  满高列：未找到（阈值 98%% 内无列）")
    else:
        lines.append("  满高列 x=%s（占宽 %.1f%%）" % (tf, 100.0 * tf / rw))

txt = "\n".join(lines)
with open(os.path.join(OUT, "primary_tip_probe.txt"), "w", encoding="utf-8") as f:
    f.write(txt)
print(txt)
