# -*- coding: utf-8 -*-
from curve_shape import tip_curve, tip_curve_pts, tip_len_for
W, H = 110.0, 35.0
tl = tip_len_for(W)
print("tip_len =", tl)
print("\n--- tip_curve(reverse=True) 左端头 ---")
L = tip_curve(tl, H/2, reverse=True)
print("  首", L[0], " 尾", L[-1])
print("\n--- tip_curve(reverse=False) ---")
R = tip_curve(tl, H/2, reverse=False)
print("  首", R[0], " 尾", R[-1])
print("\n--- tip_curve_pts 整条 ---")
P = tip_curve_pts(W, H, tl)
print("  共", len(P), "点")
print("  前 6:", [(round(x,2), round(y,2)) for x, y in P[:6]])
print("  中 2:", [(round(x,2), round(y,2)) for x, y in P[len(P)//2-1:len(P)//2+1]])
print("  后 6:", [(round(x,2), round(y,2)) for x, y in P[-6:]])
xs = [x for x, y in P]
print("  x 范围", min(xs), max(xs), " 单调?", all(xs[i] <= xs[i+1]+1e-9 for i in range(len(xs)-1)))
