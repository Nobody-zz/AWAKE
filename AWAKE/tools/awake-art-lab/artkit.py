#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""泛用绘图工具箱：形状、材质、图层合成。

设计原则（决定成图质量，改前先读）：
  1. **几何解析式画，不用滤镜腐蚀**：倒角矩形、圆章都是参数化多边形，
     逐层内缩就是描边/斜面，坐标精确、可复现。
  2. **超采样再缩**：所有图形在 ss 倍画布上画，最后 LANCZOS 缩小 ⇒ 斜边不锯齿。
     （palette 全是深色镶边，缩小时不产生可见暗边，见 note_lanczos）
  3. **噪声当作调制，不当颜色**：用 overlay 混合，保持底色不变、只加"石头的牙"。
"""
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageStat

try:
    import curve_shape as _curve_mod
except ImportError:                      # 独立跑 artkit 时不该死
    _curve_mod = None

# ---------------------------------------------------------------- 色板（实测原版）

CONTOUR = (8, 6, 5)              # 最外轮廓：近黑，像刀切的料边
BEVEL_LIGHT = (58, 52, 44)       # 斜面受光
BEVEL_DARK = (14, 11, 10)        # 斜面背光 / 分隔线

STONE_FIELD = (26, 21, 18)       # 碑面底（暖黑石）
STONE_FIELD_LIGHT = (34, 28, 24)

GOLD = (217, 169, 83)            # #D9A953 原版对话面板的金线
GOLD_BRIGHT = (231, 173, 115)    # #E7AD73 悬停金
GOLD_DIM = (150, 118, 62)        # 暗金（分隔线中段）
GOLD_UNDER = (18, 14, 10)        # 金线下的压深线

BTN_FIELD = (74, 66, 49)         # #4A4231 主按钮内衬（橄榄铜）
BTN_FIELD_HOVER = (94, 83, 64)   # 提亮
BTN_FIELD_PRESS = (58, 52, 40)   # 压深
BTN_SEC_FIELD = (60, 49, 38)     # #3C3126 次按钮内衬（锈褐）
BTN_SEC_HOVER = (76, 62, 48)
BTN_SEC_PRESS = (46, 37, 28)
BTN_TAB_FIELD = (42, 38, 32)     # Tab 未选中：偏冷的暗石
BTN_TAB_HOVER = (56, 51, 42)
BTN_TAB_PRESS = (34, 31, 26)
BTN_OFF = (58, 56, 53)           # 禁用：脱色

SEAL_INNER = (43, 20, 16)        # 蜡印内里（暗红棕）
SEAL_INNER_HOVER = (90, 34, 21)
SEAL_INNER_PRESS = (58, 26, 16)
SEAL_RIM = (57, 45, 41)          # 石圈
SEAL_RIM_LIGHT = (140, 132, 115)

SLAB = (60, 56, 42)              # 顶栏带 / 碑额（橄榄卡其）
SLAB_LIGHT = (76, 71, 54)
INSET_FIELD = (20, 16, 14)       # 输入区 / 凹槽（比碑面更深）


# ---------------------------------------------------------------- 几何

def chamfer_pts(w, h, cut, inset=0):
    """按钮/面板的**外轮廓**顶点，顺时针从左上起。

    ## 2026-09-14 12:2x 起：端头是**连续曲线**，不再是 45° 切角

    甲方对本族控件（主按钮 / 次按钮 / 页签）的定案：
      · 端头走**连续曲线**（`curve_shape`），三控件**完全同一轮廓**，只改长度；
      · 端头长度**跟绝对 16px**，不跟宽度比例。

    签名 `(w, h, cut, inset)` **保持不变**，但 `cut` 从"45° 切角长度"改语义为
    **"端头收束段长度"**：传 `cut<=0` 时退化成纯矩形（老行为的极限）。

    ## `inset` 的实现：按比例内缩

    内缩 = 先把轮廓缩到 `(w-2i, h-2i)` 再平移 `(i, i)`。
    实测（`out/study/shape/offset_probe.txt`）：端头/直边的**框带厚度比**
      · 八边形（旧）    bev=4 时 **2.11**、bev=7 时 **2.21**
      · 曲线按比例缩    bev=4 时 **1.54**、bev=7 时 **1.46**
    ⇒ 按比例缩在端头处**比原八边形更均匀**，不需要沿法线偏移那套复杂度。
    ⚠️ 厚度比不是 1.0 是因为端头上缘**斜着走**、按竖直方向量必然偏厚 —— 那是
       度量方式带来的斜率偏差，不是缺陷。八边形的 2.11 就是这条基线。
    """
    if inset <= 0:
        iw, ih = w, h
        ox, oy = 0.0, 0.0
    else:
        iw, ih = w - 2 * inset, h - 2 * inset
        ox, oy = float(inset), float(inset)

    if cut <= 0.01 or iw <= 3 or ih <= 3:
        x0, y0 = ox, oy
        x1, y1 = ox + iw - 1, oy + ih - 1
        return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]

    if _curve_mod is None:               # 兜底：退回八边形（不应发生）
        c = max(0.0, cut - 0.586 * inset)
        x0, y0, x1, y1 = ox, oy, ox + iw - 1, oy + ih - 1
        if c <= 0.01:
            return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]
        return [(x0 + c, y0), (x1 - c, y0), (x1, y0 + c), (x1, y1 - c),
                (x1 - c, y1), (x0 + c, y1), (x0, y1 - c), (x0, y0 + c)]

    # 端头按比例跟缩：缩得越少，端头越接近原长
    itl = max(2.0, cut * (iw / float(w)))
    pts = _curve_mod.tip_curve_pts(iw, ih, itl)
    return [(x + ox, y + oy) for x, y in pts]


def rect_pts(w, h, inset=0):
    return [(inset, inset), (w - 1 - inset, inset),
            (w - 1 - inset, h - 1 - inset), (inset, h - 1 - inset)]


def poly_mask(size, pts, ss=1):
    m = Image.new("L", (size[0] * ss, size[1] * ss), 0)
    ImageDraw.Draw(m).polygon([(x * ss, y * ss) for x, y in pts], fill=255)
    return m if ss == 1 else m.resize(size, Image.LANCZOS)


def disc_mask(size, inset=0, ss=1):
    w, h = size
    m = Image.new("L", (w * ss, h * ss), 0)
    ImageDraw.Draw(m).ellipse([inset * ss, inset * ss,
                               (w - 1 - inset) * ss, (h - 1 - inset) * ss], fill=255)
    return m if ss == 1 else m.resize(size, Image.LANCZOS)


def ring_mask(size, outer_pts, inner_pts, ss=1):
    return ImageChops.subtract(poly_mask(size, outer_pts, ss=ss),
                               poly_mask(size, inner_pts, ss=ss))


def disc_ring_mask(size, inset_o, inset_i, ss=1):
    return ImageChops.subtract(disc_mask(size, inset_o, ss=ss),
                               disc_mask(size, inset_i, ss=ss))


def edge_mask(size, mask, sides, thick):
    """取 mask 中靠某几条边的带状部分，用来做**有方向的**受光/背光。

    sides: "top" / "left" / "bottom" / "right"，可传多个（并集）。
    """
    w, h = size
    if isinstance(sides, str):
        sides = (sides,)
    out = None
    for s in sides:
        m = Image.new("L", size, 0)
        d = ImageDraw.Draw(m)
        if s == "top":
            d.rectangle([0, 0, w, thick], fill=255)
        elif s == "left":
            d.rectangle([0, 0, thick, h], fill=255)
        elif s == "bottom":
            d.rectangle([0, h - 1 - thick, w, h], fill=255)
        elif s == "right":
            d.rectangle([w - 1 - thick, 0, w, h], fill=255)
        band = ImageChops.multiply(mask, m)
        out = band if out is None else ImageChops.lighter(out, band)
    return out


# ---------------------------------------------------------------- 材质

def noise_field(size, sigma, blur, ss=1):
    """高斯噪声 → 模糊，得到平滑的值噪声场（L 模式，中心 128）。"""
    s = (size[0] * ss, size[1] * ss)
    n = Image.effect_noise(s, sigma).filter(ImageFilter.GaussianBlur(blur * ss))
    return n if ss == 1 else n.resize(size, Image.LANCZOS)


def textured(size, color, *, delta=14, fine=1.1, coarse=0.0, ss=1):
    """纯色底 + 石头的牙。**按目标标准差归一化**，不是拍脑袋乘系数。

    为什么要归一化（09-13 两次踩坑）：
      · 用 overlay：暗底（base≈26）上噪声被压成 ±1.5，等于没质感；
      · 用固定系数线性叠加：σ 随模糊半径剧烈变化，且暗部会撞到纯黑，出硬噪点。

    现在 delta ＝ 期望的**峰谷幅度（±N 灰阶）**，内部按实际 σ 反算系数，
    再把单侧幅度钳到 delta，暗底不会压出纯黑斑。

    fine   : 细颗粒模糊半径（≈0.8~1.4 ＝ 石面砂感；越大越糊）
    coarse : 低频斑驳模糊半径（>0 开启，像石头自然的深浅块；建议 25~40）
    """
    base = Image.new("RGB", size, color)
    n = noise_field(size, 44, fine, ss=ss)
    if coarse > 0:
        lo = noise_field(size, 60, coarse, ss=ss)
        n = Image.blend(n, lo, 0.5)
    sd = ImageStat.Stat(n).stddev[0] or 1.0
    k = (delta / 3.0) / sd          # 让 ±3σ ≈ ±delta
    pos = n.point(lambda v: min(delta, int(max(0, v - 128) * k))).convert("RGB")
    neg = n.point(lambda v: min(delta, int(max(0, 128 - v) * k))).convert("RGB")
    return ImageChops.subtract(ImageChops.add(base, pos), neg)


def solid(size, color):
    return Image.new("RGB", size, color)


def as_rgba(rgb, mask):
    out = rgb.convert("RGBA")
    out.putalpha(mask)
    return out


def over(base_rgba, rgb, mask):
    """把 rgb 按 mask 叠到 base 上。"""
    base_rgba.alpha_composite(as_rgba(rgb, mask))
    return base_rgba


# ---------------------------------------------------------------- 画布

class Canvas(object):
    """带超采样的画布：按目标坐标作画，出图时自动缩小。"""

    def __init__(self, w, h, ss=2, bg=(0, 0, 0, 0)):
        self.w, self.h, self.ss = w, h, ss
        self.img = Image.new("RGBA", (w * ss, h * ss), bg)
        self._d = ImageDraw.Draw(self.img)

    def poly(self, pts, color):
        if len(color) == 3:
            color = color + (255,)
        self._d.polygon([(x * self.ss, y * self.ss) for x, y in pts], fill=color)

    def ellipse(self, box, color):
        if len(color) == 3:
            color = color + (255,)
        self._d.ellipse([v * self.ss for v in box], fill=color)

    def line(self, pts, color, width=1):
        if len(color) == 3:
            color = color + (255,)
        self._d.line([(x * self.ss, y * self.ss) for x, y in pts],
                     fill=color, width=int(round(width * self.ss)), joint="curve")

    def rings(self, w, h, cut, stack):
        """从外到内逐层画（stack ＝ [(inset, color), ...]）。"""
        for inset, col in stack:
            self.poly(chamfer_pts(w, h, cut, inset), col)

    def rings_rect(self, w, h, stack):
        for inset, col in stack:
            self.poly(rect_pts(w, h, inset), col)

    def done(self):
        return self.img.resize((self.w, self.h), Image.LANCZOS)
