# -*- coding: utf-8 -*-
"""验证：铁轨/木面**都按"同一曲线内缩"定义**时，端头处的铁轨是否等宽。

对照两种定义：
  甲（现状）两者都按 `dist`（离外形垂距）切 ⇒ 端头塌陷
  乙（候选）木面 = 同一曲线内缩 (FRAME+SEAM)；铁轨 = 外形 − 木面

判据：量端头处与直边处的**铁轨竖直厚度**，看是否接近。
"""
import os
import sys
from PIL import Image, ImageChops

BASE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, BASE)
import artkit as K
import curve_shape as CS
import make_button_primary as M

W, H, SS = 110, 35, 6
FRAME, SEAM = M.FRAME, M.SEAM
GOLD_INSET = M.GOLD_INSET
TL = M.TIP_LEN

print("FRAME=%.1f  SEAM=%.1f  GOLD_INSET=%.2f  TIP_LEN=%.1f" % (FRAME, SEAM, GOLD_INSET, TL))
print()


def frame_thickness(mask, w, h):
    """逐列量上缘带厚度（1× 像素）。"""
    px = mask.load()
    out = []
    for x in range(w):
        ys = [y for y in range(h) if px[x, y] > 127]
        if not ys:
            out.append(-1)
            continue
        n, y = 0, ys[0]
        while y < h and px[x, y] > 127:
            n += 1
            y += 1
        out.append(n)
    return out


def stat(th, lo, hi, label):
    v = [t for t in th[lo:hi] if t > 0]
    if not v:
        return "%s 无有效列" % label
    return "%s min/max/avg = %d/%d/%.2f" % (label, min(v), max(v), sum(v) / float(len(v)))


# ---------- 甲：现状（dist 切）----------
pts = M.end_taper_points(W, H, TL, M.TIP_H)
shape = K.poly_mask((W, H), pts, ss=SS)
f_ss = int(round(FRAME * SS))
s1_ss = int(round((GOLD_INSET + SEAM) * SS))

dist = M.band_field(pts, (W * SS, H * SS), SS, int((FRAME + SEAM) * SS) + 1)
frameA = dist.point(lambda v: 255 if v < f_ss else 0)
seamA = ImageChops.subtract(dist.point(lambda v: 255 if v < s1_ss else 0), frameA)


thA = frame_thickness(frameA, W * SS, H * SS)
print("甲（现状，按 dist 切）")
print("  " + stat(thA, 2, int(TL * SS), "端头带"))
print("  " + stat(thA, W * SS // 2 - 40, W * SS // 2 + 40, "直边带"))

# ---------- 乙：候选（同一曲线内缩）----------
# 木面 = 曲线内缩 (FRAME+SEAM)；铁轨 = 外形 − 木面
from PIL import ImageChops
inner_pts = K.chamfer_pts(W, H, TL, FRAME + SEAM)
wood = K.poly_mask((W, H), inner_pts, ss=SS)
shape1 = K.poly_mask((W, H), K.chamfer_pts(W, H, TL, 0), ss=SS)
frameB = ImageChops.subtract(shape1, wood)

thB = frame_thickness(frameB, W, H)
print()
print("乙（候选，按同一曲线内缩）")
print("  " + stat(thB, 2, int(TL), "端头带"))
print("  " + stat(thB, W // 2 - 8, W // 2 + 8, "直边带"))

# 甲也换算到 1× 便于同尺度比
thA1 = frame_thickness(frameA.resize((W, H), Image.NEAREST), W, H)
print()
print("甲（1× 尺度）")
print("  " + stat(thA1, 2, int(TL), "端头带"))
print("  " + stat(thA1, W // 2 - 8, W // 2 + 8, "直边带"))

# 逐列打印端头那一段
print()
print("逐列（1× 列）上缘带厚度：")
print("  列   甲(现状)  乙(候选)")
for x1 in range(0, int(TL) + 8):
    a = thA1[x1] if x1 < len(thA1) else -1
    b = thB[x1] if x1 < len(thB) else -1
    print("  %3d    %5d     %5d" % (x1, a, b))
