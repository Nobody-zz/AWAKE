# -*- coding: utf-8 -*-
"""用**真正的 232x42 原版剪影逐列数据**（不是抽样 5 点）拟合端头。

上一版错在拿"手抄的 5 个归一化点"当数据 —— 点太少，拟合器随便怎么拐都"贴"。
这次逐列全用上。
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REF = os.path.normpath(os.path.join(HERE, "..", "awake-ui-lab", "out", "atlas",
                                    "sprites", "General__Button__main_button_regular.png"))
im = Image.open(REF).convert("RGBA")
a = im.split()[3].point(lambda v: 255 if v > 128 else 0)
body = im.crop(a.getbbox())
W0, H0 = body.size
print("原版 body:", W0, "x", H0)

mask = body.split()[3].point(lambda v: 255 if v > 128 else 0)
cols = []
for x in range(W0):
    c = [y for y in range(H0) if mask.getpixel((x, y)) > 128]
    cols.append((max(c) - min(c) + 1) if c else 0)

# 找到满高的列
xf = next(x for x, c in enumerate(cols) if c >= H0 - 0.5)
print("首次满高列 x =", xf, " 该列高", cols[xf])
print("\n端头逐列（x, 高, 半高比）：")
for x in range(0, xf + 2):
    print("   %3d   %3d   %.4f" % (x, cols[x], cols[x] / float(H0)))

# 本项目尺度：端头长按宽度比例 16/232*110 = 7.59
TL = 16.0 / W0 * 110.0
print("\n本项目端头长 %.2f px（按 16/232*110）" % TL)

# 把原版端头按 x∈[0,xf] 归一化为 t，半高比归一化为 d（d(0)=首列, d(1)=1）
ref = []
for x in range(0, xf + 1):
    t = x / float(xf)
    d = cols[x] / float(H0)
    ref.append((t, d))
d0 = ref[0][1]
print("原版 d0（端点起始半高比）= %.4f" % d0)


def model(t, dd0, p):
    return dd0 + (1.0 - dd0) * (1.0 - (1.0 - t) ** p)


best = []
for dd0x in range(2, 25):
    dd0 = dd0x / 100.0
    for p100 in range(80, 260, 2):
        p = p100 / 100.0
        errs = [abs(model(t, dd0, p) - d) for t, d in ref]
        # 用 RMS + 最大偏差的混合，避免个别列主导
        rms = (sum(e * e for e in errs) / len(errs)) ** 0.5
        mx = max(errs)
        best.append((rms + mx, rms, mx, dd0, p))
best.sort()
print("\n最优 (RMS+max, RMS, max, d0, p)：")
for tot, rms, mx, dd0, p in best[:12]:
    print("   %.4f  %.4f  %.4f   d0=%.2f p=%.2f" % (tot, rms, mx, dd0, p))

# 打印最优那条的逐列残差
_, _, _, bd0, bp = best[0]
print("\n最优 d0=%.2f p=%.2f 的逐列残差：" % (bd0, bp))
for t, d in ref:
    m = model(t, bd0, bp)
    print("   t=%.3f  实测 %.4f  模型 %.4f  %+.4f" % (t, d, m, m - d))
