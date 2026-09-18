# -*- coding: utf-8 -*-
"""量化"端头尖不尖"——用斜边倾角，不用感觉。

倾角 = atan(半高差 / 水平长度)
  · 原版：端头 14px 内，半高从 3（d=0.143）涨到 21 ⇒ 倾角
  · 我们旧（八边形 cut=8）：8px 内，半高从 9.5 涨到 17.5
  · 我们新（曲线 16px、d0=0.10）：16px 内，半高从 1.75 涨到 17.5

另外算"端头占宽的视觉权重" = 端头长 / 按钮宽。
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import curve_shape as CS

H = 35.0
half = H / 2

cases = [
    ("原版 main_button_regular", 232.0, 42.0, 14.0, 0.143),
    ("原版（缩放比喻：同 16px 端头、232 宽）", 232.0, 42.0, 16.0, 0.143),
    ("我们·旧 次按钮 八边形 cut=8", 100.0, H, 8.0, (half - 8.0) / half),
    ("我们·新 三控件 曲线 16px", 110.0, H, 16.0, CS.CURVE_D0),
]

print("端头倾角与视觉权重")
print("%-38s %7s %7s %8s %9s %9s" %
      ("对象", "端头px", "宽px", "起始半高", "倾角°", "端头/宽"))
for name, w, h, tl, d0 in cases:
    hh = h / 2
    rise = hh * (1 - d0)
    ang = math.degrees(math.atan2(rise, tl))
    print("%-38s %7.1f %7.0f %8.2f %9.1f %8.1f%%"
          % (name, tl, w, hh * d0, ang, 100 * tl / w))

print()
print("⇒ 倾角越大＝越尖。")
print("   原版 %.1f°  → 我们旧 %.1f°   →  我们新 %.1f°"
      % (math.degrees(math.atan2(21 * (1 - 0.143), 14)),
         math.degrees(math.atan2(half * (1 - (half - 8) / half), 8)),
         math.degrees(math.atan2(half * (1 - CS.CURVE_D0), 16))))

print()
print("如果要把新版倾角拉回接近原版的感觉，反推需要的 d0（起始半高比）：")
tgt = math.degrees(math.atan2(21 * (1 - 0.143), 14))
for tl in (10.0, 12.0, 16.0):
    for d0 in (0.10, 0.20, 0.30, 0.40):
        ang = math.degrees(math.atan2(half * (1 - d0), tl))
        print("   端头 %4.0fpx  d0=%.2f → 倾角 %.1f°   %s"
              % (tl, d0, ang, "← 接近原版" if abs(ang - tgt) < 3 else ""))
