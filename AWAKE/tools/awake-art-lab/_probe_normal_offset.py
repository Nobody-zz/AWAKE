# -*- coding: utf-8 -*-
"""把 `chamfer_pts` 的 `inset` 从"按比例缩"改成**沿法线等距内缩**。

上一版我做法线偏移时**方向写反**（把轮廓推出去），`ring_mask` 相减得空，
于是放弃了、改用按比例缩。按比例缩的问题是：端头处缩得比竖直方向慢
⇒ `_frame` 的环带在端头**更宽**（实测比值 1.54），放大图里就是端头铁轨畸变。

这次做对：
  · 逐顶点求两条邻边的单位法线，取角平分方向；
  · 沿角平分线内缩 `inset / sin(夹角半角)`（**不是** `inset`）——
    否则在折点处内缩量会短一截（凸角偏内、凹角偏外）。
  · 多边形是顺时针，内法线 = 边方向**顺时针转 90°**。

验证：环带厚度在端头与直边处是否接近相等。
"""
import math
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import curve_shape as CS


def offset_pts(pts, inset):
    """沿法线等距内缩（顺时针多边形的内侧）。"""
    if inset <= 0:
        return list(pts)
    n = len(pts)
    out = []
    for i in range(n):
        px, py = pts[(i - 1) % n]
        cx, cy = pts[i]
        nx_, ny_ = pts[(i + 1) % n]

        e1 = (cx - px, cy - py)
        e2 = (nx_ - cx, ny_ - cy)
        l1 = math.hypot(*e1)
        l2 = math.hypot(*e2)
        if l1 < 1e-9 or l2 < 1e-9:
            continue
        e1 = (e1[0] / l1, e1[1] / l1)
        e2 = (e2[0] / l2, e2[1] / l2)

        # 屏幕坐标（y 向下）里，顺时针多边形的内侧在**行进方向的左边**
        # = 逆时针转 90° = (-dy, dx)。
        # ⚠️ 上一版我写的 (dy, -dx) 是**把轮廓推出去**，ring_mask 相减得空 —— 方向反了。
        n1 = (-e1[1], e1[0])
        n2 = (-e2[1], e2[0])

        bx, by = n1[0] + n2[0], n1[1] + n2[1]
        bl = math.hypot(bx, by)
        if bl < 1e-9:
            # 两条边反向（回折）—— 直接沿 e 的法线走
            out.append((cx + n1[0] * inset, cy + n1[1] * inset))
            continue
        bx, by = bx / bl, by / bl

        # 角平分线内缩量要除以 cos(夹半角) = |(n1+n2)/2| 的模
        half_cos = math.hypot((n1[0] + n2[0]) / 2.0, (n1[1] + n2[1]) / 2.0)
        k = inset / max(0.25, half_cos)
        out.append((cx + bx * k, cy + by * k))
    return out


def vert_thickness(pts_o, pts_i, w, h, ss=8):
    """逐列量上缘带竖直厚度（1× 像素）。"""
    from PIL import Image, ImageDraw, ImageChops
    def mask(pts):
        m = Image.new("L", (w * ss, h * ss), 0)
        ImageDraw.Draw(m).polygon([(x * ss, y * ss) for x, y in pts], fill=255)
        return m.resize((w, h), Image.LANCZOS)
    band = ImageChops.subtract(mask(pts_o), mask(pts_i))
    px = band.load()
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


W, H = 110, 35
TL = 9.0

print("=== 沿法线等距内缩：环带厚度 ===")
for bev in (1, 4, 7):
    o = CS.tip_curve_pts(W, H, TL)
    i = offset_pts(o, bev)
    th = vert_thickness(o, i, W, H)
    tip = [t for t in th[1: int(TL)] if t > 0]
    mid = [t for t in th[W // 2 - 8: W // 2 + 8] if t > 0]
    print("  inset 0→%d：端头 avg %.2f (min %d max %d) | 直边 avg %.2f | 比值 %.2f"
          % (bev, sum(tip) / float(len(tip)), min(tip), max(tip),
             sum(mid) / float(len(mid)),
             (sum(tip) / float(len(tip))) / (sum(mid) / float(len(mid)))))
    print("     逐列: %s" % " ".join("%d" % th[x] for x in range(min(20, W))))

print()
print("=== 对照：按比例缩（现状）===")
def scaled(w, h, tip_len, inset):
    iw, ih = w - 2 * inset, h - 2 * inset
    itl = max(2.0, tip_len * (iw / float(w)))
    return [(x + inset, y + inset) for x, y in CS.tip_curve_pts(iw, ih, itl)]

for bev in (1, 4, 7):
    o = CS.tip_curve_pts(W, H, TL)
    i = scaled(W, H, TL, bev)
    th = vert_thickness(o, i, W, H)
    tip = [t for t in th[1: int(TL)] if t > 0]
    mid = [t for t in th[W // 2 - 8: W // 2 + 8] if t > 0]
    print("  inset 0→%d：端头 avg %.2f (min %d max %d) | 直边 avg %.2f | 比值 %.2f"
          % (bev, sum(tip) / float(len(tip)), min(tip), max(tip),
             sum(mid) / float(len(mid)),
             (sum(tip) / float(len(tip))) / (sum(mid) / float(len(mid)))))

# 镜像与闭合检查
print()
print("=== 法线内缩后的轮廓自检 ===")
o = CS.tip_curve_pts(W, H, TL)
i = offset_pts(o, 4)
print("  顶点数 %d → %d" % (len(o), len(i)))
print("  x 范围 %.2f..%.2f（原 %.2f..%.2f）" % (min(p[0] for p in i), max(p[0] for p in i),
                                              min(p[0] for p in o), max(p[0] for p in o)))
print("  左右镜像(前5列高 vs 后5列高): %s" % (
    "见下方逐列"))
th = vert_thickness(o, i, W, H)
print("    左端头 %s" % " ".join("%d" % th[x] for x in range(10)))
print("    右端头 %s" % " ".join("%d" % th[W - 1 - x] for x in range(10)))
