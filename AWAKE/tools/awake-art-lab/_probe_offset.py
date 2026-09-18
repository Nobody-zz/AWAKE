# -*- coding: utf-8 -*-
"""量两件事，决定 `chamfer_pts` 曲线版怎么实现 `inset`。

Q：曲线轮廓内缩时，端头处的**框带厚度**会不会比直边处厚/薄？

度量方式：取轮廓**上缘那一条带**（从最上面一个不透明像素往下数连续带内像素），
逐列量其厚度。直边处 vs 端头处 一比就知道。

对照组：① 八边形（内置基线）② 曲线按比例缩 ③ 曲线沿法线缩
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import artkit as K
import curve_shape as CS

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out", "study", "shape")
os.makedirs(OUT, exist_ok=True)

W, H = 110, 35
SS = 4
TL = 16.0


def top_band_thickness(pts_o, pts_i):
    """逐列量上缘带的厚度（像素，1× 单位）。"""
    m = K.ring_mask((W, H), pts_o, pts_i, ss=SS)
    px = m.load()
    thick = []
    for x in range(W):
        # 该列内上缘带：从最上像素往下数连续 >0
        ys = [y for y in range(H) if px[x, y] > 127]
        if not ys:
            thick.append(-1)
            continue
        top = ys[0]
        n = 0
        y = top
        while y < H and px[x, y] > 127:
            n += 1
            y += 1
        thick.append(n)
    return thick


def report(thick, label):
    L = []
    mid = [t for t in thick[int(W * 0.40): int(W * 0.60)] if t > 0]
    tip = [t for t in thick[1: int(TL) - 1] if t > 0]
    L.append("== %s ==" % label)
    if mid:
        L.append("   直边处厚度  min/max/avg = %d / %d / %.2f"
                 % (min(mid), max(mid), sum(mid) / float(len(mid))))
    else:
        L.append("   直边处厚度：无有效列")
    if tip:
        L.append("   端头处厚度  min/max/avg = %d / %d / %.2f"
                 % (min(tip), max(tip), sum(tip) / float(len(tip))))
        if mid:
            L.append("   ⇒ 端头/直边 平均厚度比 = %.2f"
                     % ((sum(tip) / float(len(tip))) / (sum(mid) / float(len(mid)))))
    else:
        L.append("   端头处厚度：无有效列")
    L.append("")
    return L


# ---------------- ① 八边形基线 ----------------
lines = []
for bev in (1, 4, 7):
    o = K.chamfer_pts(W, H, 8, 0)
    i = K.chamfer_pts(W, H, 8, bev)
    # 八边形 cut=8 < bev=7 有效；inset 0 时 cut 被削减
    t = top_band_thickness(o, i)
    lines += report(t, "① 八边形 cut=8  inset 0 → %d" % bev)

# ---------------- ② 曲线，按比例缩 ----------------
def curve_scaled(w, h, tip_len, inset):
    iw, ih = w - 2 * inset, h - 2 * inset
    if iw <= 3 or ih <= 3:
        return [(inset, inset), (w - 1 - inset, inset),
                (w - 1 - inset, h - 1 - inset), (inset, h - 1 - inset)]
    itl = max(2.0, tip_len * (iw / float(w)))
    pts = CS.tip_curve_pts(iw, ih, itl)
    return [(x + inset, y + inset) for x, y in pts]


for bev in (1, 4, 7):
    o = curve_scaled(W, H, TL, 0)
    i = curve_scaled(W, H, TL, bev)
    t = top_band_thickness(o, i)
    lines += report(t, "② 曲线 tip=%.0f  按比例缩  0 → %d" % (TL, bev))

# ---------------- ③ 曲线，沿法线缩 ----------------
def curve_offset(w, h, tip_len, inset):
    if inset <= 0:
        return CS.tip_curve_pts(w, h, tip_len)
    pts = CS.tip_curve_pts(w, h, tip_len)
    n = len(pts)
    out = []
    for idx in range(n):
        x, y = pts[idx]
        xp, yp = pts[(idx - 1) % n]
        xn, yn = pts[(idx + 1) % n]
        e1 = (x - xp, y - yp)
        e2 = (xn - x, yn - y)
        l1 = math.hypot(*e1) or 1.0
        l2 = math.hypot(*e2) or 1.0
        e1 = (e1[0] / l1, e1[1] / l1)
        e2 = (e2[0] / l2, e2[1] / l2)
        # 顺时针多边形 → 内法线 = 边方向顺时针转 90°
        n1 = (e1[1], -e1[0])
        n2 = (e2[1], -e2[0])
        bx, by = n1[0] + n2[0], n1[1] + n2[1]
        bl = math.hypot(bx, by) or 1.0
        out.append((x + bx / bl * inset, y + by / bl * inset))
    return out


for bev in (1, 4, 7):
    o = curve_offset(W, H, TL, 0)
    i = curve_offset(W, H, TL, bev)
    t = top_band_thickness(o, i)
    lines += report(t, "③ 曲线 tip=%.0f  沿法线缩  0 → %d" % (TL, bev))

txt = "\n".join(lines)
with open(os.path.join(OUT, "offset_probe.txt"), "w", encoding="utf-8") as f:
    f.write(txt)
print(txt)
