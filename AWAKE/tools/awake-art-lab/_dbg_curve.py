# -*- coding: utf-8 -*-
"""快速诊断：母线到底生成了什么点。别猜，打出来看。"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from curve_shape import CURVE_POW, _d_of, _t_of, _tip_curve_a  # noqa: E402

print("CURVE_POW =", CURVE_POW)
print()
print("半高比 d  ->  参数 t  ->  w = 1-t")
for i in range(9):
    d = i / 8.0
    t = _t_of(d)
    print("  d=%.3f  t=%.4f  w=%.4f   _d_of(t)=%.4f" % (d, t, 1 - t, _d_of(t)))
print()
pts = _tip_curve_a(16.0, 17.5)
print("_tip_curve_a 原始输出 %d 点：" % len(pts))
for w, d in pts:
    print("   w=%.4f  d=%.4f  ->  x=%.2f  y=%.2f" % (w, d, w * 16.0, d * 17.5))
