# -*- coding: utf-8 -*-
"""重拟合：**判据换成"和一条直线的偏离"**，不再只看半高误差。

上一版错在哪：只用"半高最大偏差"当目标 ⇒ 拟合器把曲线往"更鼓"的方向推，
渲出来是一根**桶形**（比原版圆得多）。半高误差小 ≠ 形状对。

原版端头看着像什么：**一条几乎直的斜线，只在两端各圆一下**。
⇒ 正确的目标是「离直线近」，也就是**曲率集中在两端、中段几乎直**。
"""
import os, math

HERE = os.path.dirname(os.path.abspath(__file__))
REF = ((0.000, 0.143), (0.246, 0.333), (0.507, 0.667), (0.754, 0.905), (1.000, 1.000))

# 参考直线：过首末两点
def line_d(t):
    t0, d0 = REF[0]
    t1, d1 = REF[-1]
    return d0 + (d1 - d0) * (t - t0) / (t1 - t0)


def model(t, d0, p):
    return d0 + (1.0 - d0) * (1.0 - (1.0 - t) ** p)


print("原版 vs 直线：")
for t, d in REF:
    print("   t=%.3f  实测 %.3f  直线 %.3f  偏离 %+.3f" % (t, d, line_d(t), d - line_d(t)))

best = []
for d0x in range(0, 30):
    d0 = d0x / 100.0
    for p10 in range(10, 61):
        p = p10 / 10.0
        # 目标①：贴实测点（权重 1）
        e_fit = max(abs(model(t, d0, p) - d) for t, d in REF)
        # 目标②：贴直线（权重 1）—— 惩罚"鼓出去"
        e_lin = max(abs(model(t, d0, p) - line_d(t)) for t in
                    [i / 40.0 for i in range(41)])
        best.append((e_fit + e_lin, e_fit, e_lin, d0, p))
best.sort()
print("\n最优 (合计, 贴点, 贴线, d0, p)：")
for tot, ef, el, d0, p in best[:12]:
    print("   %.4f  %.4f  %.4f   d0=%.2f p=%.1f" % (tot, ef, el, d0, p))
