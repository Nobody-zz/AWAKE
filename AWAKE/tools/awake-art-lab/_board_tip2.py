# -*- coding: utf-8 -*-
"""端头对照板 v2：把**原版也做成纯剪影**，保证三方是同一套基准（同高、同底、同倍率）。
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image, ImageDraw
from curve_shape import tip_curve_pts, tip_len_for, CURVE_D0, CURVE_POW
from artkit import chamfer_pts

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "out", "study", "shape")
W, H, SS, Z = 110, 35, 8, 7

# 原版：用真彩原图取 alpha/亮度做剪影
SRC = None
for c in ("../awake-ui-lab/out/atlas/sprites/General__Button__main_button_regular.png",):
    p = os.path.join(HERE, c)
    if os.path.exists(p):
        SRC = p
        break
print("ref src:", SRC)
ref = Image.open(SRC)
if ref.mode in ("RGBA", "LA"):
    a = ref.convert("RGBA").split()[-1]
    sil = a.point(lambda v: 255 if v > 128 else 0)
else:
    g = ref.convert("L")
    bbx = g.getbbox()
    # 假设背景比按钮亮/暗，二值化
    lo, hi = g.getextrema()
    th = (lo + hi) / 2.0
    sil = g.point(lambda v: 255 if v > th else 0)
sil = sil.crop(sil.getbbox())
print("ref bbox size:", sil.size)
# 缩放到 H 高
r2 = sil.resize((int(round(sil.width * H / sil.height)), H), Image.LANCZOS)


def render(pts):
    m = Image.new("L", (W * SS, H * SS), 0)
    ImageDraw.Draw(m).polygon([(x * SS, y * SS) for x, y in pts], fill=255)
    return m.resize((W, H), Image.LANCZOS)


curve = render(tip_curve_pts(W, H))
octa = render(chamfer_pts(W, H, 8, 2))

CAPW = 22
panels = [("原版剪影", r2.crop((0, 0, CAPW, H))),
          ("本曲线 p=%.1f" % CURVE_POW, curve.crop((0, 0, CAPW, H))),
          ("旧八边形", octa.crop((0, 0, CAPW, H)))]

GAP, PAD, TOP = 20, 22, 30
total_w = PAD * 2 + sum(p.width for _, p in panels) * Z + GAP * (len(panels) - 1)
total_h = TOP + H * Z + PAD
board = Image.new("RGB", (total_w, total_h), (30, 30, 32))
x = PAD
for name, p in panels:
    board.paste(p.resize((p.width * Z, p.height * Z), Image.NEAREST).convert("RGB"), (x, TOP))
    x += p.width * Z + GAP
board.save(os.path.join(OUT, "tip_board2.png"))

# 数值：逐列高度三方对照
def cols(img):
    out = []
    for xx in range(img.width):
        c = [yy for yy in range(img.height) if img.getpixel((xx, yy)) > 127]
        out.append((max(c) - min(c) + 1) if c else 0)
    return out

def norm(c, full):
    return [round(v / full * H, 1) for v in c]

# 原版按宽度比例折算到本项目宽度
rc = cols(r2)
scale = W / float(r2.width)
print("\n列高对照（都折算到本项目 %dpx 宽、%dpx 高）" % (W, H))
print("  x   原版   本曲线   八边形")
for x in range(0, 14):
    rx = int(round(x / scale))
    rx = min(rx, len(rc) - 1)
    print("  %2d   %5.1f   %6.1f   %6.1f" % (x, rc[rx] / float(H) * H, cols(curve)[x], cols(octa)[x]))
print("\nsaved", os.path.join(OUT, "tip_board2.png"), board.size)
