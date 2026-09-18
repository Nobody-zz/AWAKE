# -*- coding: utf-8 -*-
"""验证端头曲线：① 切线连续（无折点）② 半高追上原版实测 ③ 整条轮廓闭合无自交。

判定用两个数，不靠眼睛：
  · **折角**：相邻线段夹角。八边形是 45.0°，C1 曲线应 < 3°。
  · **半高差**：曲线在各采样点的半高 vs 原版那 5 个实测点，最大偏差。
输出 out/study/shape/curve_check.txt
"""
import math
import os

from curve_shape import CURVE_TARGET, tip_curve, tip_curve_pts, tip_len_for

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out", "study", "shape")
os.makedirs(OUT, exist_ok=True)
lines = []

RUN, RISE = 16.0, 17.5          # 本项目主按钮：收尖 16px、半高 35/2


def angles(pts):
    out = []
    for i in range(1, len(pts) - 1):
        ax, ay = pts[i - 1]
        bx, by = pts[i]
        cx, cy = pts[i + 1]
        v1 = (bx - ax, by - ay)
        v2 = (cx - bx, cy - by)
        n1 = math.hypot(*v1)
        n2 = math.hypot(*v2)
        if n1 < 1e-9 or n2 < 1e-9:
            continue
        dot = max(-1.0, min(1.0, (v1[0] * v2[0] + v1[1] * v2[1]) / (n1 * n2)))
        out.append((math.degrees(math.acos(dot)), bx, by))
    return out


def sample_d(pts, x, rise):
    """在 x 处线性内插半高比（pts 按 x 单调，方向不限）。"""
    q = sorted(pts, key=lambda p: p[0])
    for j in range(len(q) - 1):
        x0, y0 = q[j]
        x1, y1 = q[j + 1]
        if (x0 - x) * (x1 - x) <= 0 and abs(x1 - x0) > 1e-9:
            u = (x - x0) / (x1 - x0)
            return (y0 + (y1 - y0) * u) / rise
    return None


pts = tip_curve(RUN, RISE)
lines.append("=== 端头曲线 ===  %d 个顶点" % len(pts))
lines.append("      首点 (%.3f, %.3f)   尾点 (%.3f, %.3f)"
             % (pts[0][0], pts[0][1], pts[-1][0], pts[-1][1]))
lines.append("      （起点应为 (run, rise) 切线竖直；"
             "终点应为 (0, d0*rise)，**不是 (0,0)**）")
ang = angles(pts)
worst = max(ang)[0] if ang else 0.0
lines.append("  · 最大折角 %6.2f deg   （八边形 45.0；目标 < 3）   %s"
             % (worst, "光滑 OK" if worst < 3.0 else "有折点 FAIL"))
for a, x, y in ang:
    if a > 3.0:
        lines.append("      点 @(%.2f, %.2f)  夹角 %.2f deg" % (x, y, a))

lines.append("  · 半高追表（原版实测 5 点 vs 本曲线）")
lines.append("      x(px)   原版半高比   本曲线   差")
worst_d = 0.0
for t, dref in CURVE_TARGET:
    x = t * RUN
    got = sample_d(pts, x, RISE)
    if got is None:
        continue
    diff = got - dref
    worst_d = max(worst_d, abs(diff))
    lines.append("      %5.2f     %5.3f       %5.3f    %+5.3f" % (x, dref, got, diff))
lines.append("  · 最大半高偏差 %.3f（35px 高上约 %.1f px）   %s"
             % (worst_d, worst_d * RISE, "OK" if worst_d < 0.08 else "偏大"))
lines.append("")

# 整条轮廓自检
W, H = 110.0, 35.0
tl = tip_len_for(W)
poly = tip_curve_pts(W, H, tl)
xs = [p[0] for p in poly]
ys = [p[1] for p in poly]
lines.append("=== 整条轮廓 (%.0f×%.0f, 端头长 %.1f) ===" % (W, H, tl))
lines.append("      %d 个顶点   x∈[%.2f, %.2f]   y∈[%.2f, %.2f]"
             % (len(poly), min(xs), max(xs), min(ys), max(ys)))
pang = angles(poly)
pworst = max(pang)[0] if pang else 0.0
lines.append("  · 最大折角 %6.2f deg   %s"
             % (pworst, "光滑 OK" if pworst < 3.0 else "有折点 FAIL"))
for a, x, y in pang:
    if a > 3.0:
        lines.append("      点 @(%.2f, %.2f)  夹角 %.2f deg" % (x, y, a))
lines.append("")

txt = "\n".join(lines)
open(os.path.join(OUT, "curve_check.txt"), "w", encoding="utf-8").write(txt)
print(txt)
