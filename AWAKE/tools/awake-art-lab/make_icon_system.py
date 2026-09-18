# -*- coding: utf-8 -*-
"""
恒形 · 图标系统与贴图生成器
================================================================
母题：CONSTANT FORM（恒形）—— 形体恒定，只有「质」是变量。
本脚本产出三组东西：

  1) 功能图标 14 枚（C5 / ui_awake_icon）
     基准网格 24×24、线宽 2.4、方端、纯白剪影；交付 48 / 72 两档。
  2) 无缝贴图 3 张（落 ui_awake_ornament）
     T-01 ground 256  低频底纹（面板底用；高频一律不进底面）
     T-02 weave  256  亚麻平织（只用于「有边界的件」）
     T-03 rule   24×240 竖规饰带（竖向无缝重复）
  3) 样张版一张（2400×1600）—— 图标系统的「标本页」。

运行：python make_icon_system.py
"""
import math
import os
from PIL import Image, ImageChops, ImageDraw, ImageFont

# ----------------------------------------------------------------------------
# 0. 常量
# ----------------------------------------------------------------------------
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SPRITE_PARTS = os.path.join(ROOT, "GUI", "SpriteParts")
DIR_ICON = os.path.join(SPRITE_PARTS, "ui_awake_icon")
DIR_ORN = os.path.join(SPRITE_PARTS, "ui_awake_ornament")
OUT_PLATE = os.path.join(os.path.dirname(__file__), "out", "icon-system")

FONT_CJK = r"C:\Windows\Fonts\simkai.ttf"
FONT_MONO = r"C:\Users\26811\.workbuddy\skills\canvas-design\canvas-fonts\DMMono-Regular.ttf"
FONT_SERIF = r"C:\Users\26811\.workbuddy\skills\canvas-design\canvas-fonts\InstrumentSerif-Regular.ttf"

# 恒形调色：地 / 构 / 墨 / 朱 —— 只四声，朱不越一根发丝
C_GROUND = (20, 16, 12)
C_GROUND2 = (11, 9, 7)
C_STRUCT = (94, 85, 72)
C_PIGMENT = (242, 236, 224)
C_ACCENT = (192, 69, 46)
WHITE_ICON = (248, 248, 248, 255)

SS = 12          # 超采样倍率（24 × 12 = 288 内画，再降采样）
U = 24.0         # 基准网格
STROKE = 2.4     # 基准线宽


# ----------------------------------------------------------------------------
# 1. 几何层：一切形状都在 24 格上用矩形/多边形拼，保证「方端」
# ----------------------------------------------------------------------------
def _box(cx, cy, r):
    return [cx - r, cy - r, cx + r, cy + r]


def _scale(pts, k):
    return [(int(round(x * k)), int(round(y * k))) for x, y in pts]


def _scale_box(b, k):
    return [int(round(v * k)) for v in b]


class Ink:
    """在超采样蒙版上作画。所有坐标都用 24 格单位传入。"""

    def __init__(self, w):
        w = int(round(w))
        self.w = w
        self.k = w / U
        self.mask = Image.new("L", (w, w), 0)

    # --- 内部 ---------------------------------------------------------------
    def _new(self):
        return Image.new("L", (self.w, self.w), 0)

    def _union(self, sub):
        self.mask = ImageChops.lighter(self.mask, sub)

    # --- 基本笔 -------------------------------------------------------------
    def seg(self, p0, p1, w=STROKE, cap="square"):
        """方端粗线段。"""
        x0, y0 = p0
        x1, y1 = p1
        dx, dy = x1 - x0, y1 - y0
        L = math.hypot(dx, dy)
        if L < 1e-9:
            return
        ux, uy = dx / L, dy / L
        if cap == "square":
            x0 -= ux * w / 2.0
            y0 -= uy * w / 2.0
            x1 += ux * w / 2.0
            y1 += uy * w / 2.0
        nx, ny = -uy * w / 2.0, ux * w / 2.0
        pts = [(x0 + nx, y0 + ny), (x1 + nx, y1 + ny),
               (x1 - nx, y1 - ny), (x0 - nx, y0 - ny)]
        sub = self._new()
        ImageDraw.Draw(sub).polygon(_scale(pts, self.k), fill=255)
        self._union(sub)

    def bar(self, x0, y0, x1, y1, w=STROKE):
        """轴对齐方条（等价于方端 seg，写出来更省心）。"""
        self.seg((x0, y0), (x1, y1), w)

    def poly(self, pts, w=STROKE, closed=True):
        """折线（closed 时首尾相接）。"""
        n = len(pts)
        for i in range(n - 1):
            self.seg(pts[i], pts[i + 1], w)
        if closed:
            self.seg(pts[-1], pts[0], w)

    def fill_poly(self, pts):
        """实心多边形（仅当描边会糊成一团时才用）。"""
        sub = self._new()
        ImageDraw.Draw(sub).polygon(_scale(pts, self.k), fill=255)
        self._union(sub)

    def disc(self, cx, cy, r):
        sub = self._new()
        ImageDraw.Draw(sub).ellipse(_scale_box(_box(cx, cy, r), self.k), fill=255)
        self._union(sub)

    def ring(self, cx, cy, r, w=STROKE):
        sub = self._new()
        d = ImageDraw.Draw(sub)
        d.ellipse(_scale_box(_box(cx, cy, r), self.k), fill=255)
        d.ellipse(_scale_box(_box(cx, cy, max(0.05, r - w)), self.k), fill=0)
        self._union(sub)

    def arc(self, cx, cy, r, a0, a1, w=STROKE):
        """圆环的一段（径向切断＝方端）。角度制，图像坐标（y 向下，顺时针增角）。"""
        ann = self._new()
        d = ImageDraw.Draw(ann)
        d.ellipse(_scale_box(_box(cx, cy, r), self.k), fill=255)
        d.ellipse(_scale_box(_box(cx, cy, max(0.05, r - w)), self.k), fill=0)

        if a1 <= a0:
            a1 += 360.0
        n = max(10, int((a1 - a0) / 4.0))
        pts = [(cx, cy)]
        for i in range(n + 1):
            a = math.radians(a0 + (a1 - a0) * i / n)
            pts.append((cx + (r + w) * math.cos(a), cy + (r + w) * math.sin(a)))
        sec = self._new()
        ImageDraw.Draw(sec).polygon(_scale(pts, self.k), fill=255)

        self._union(ImageChops.multiply(ann, sec))

    # --- 箭头 ---------------------------------------------------------------
    def vhead(self, tip, dvec, L=3.4, half=38.0):
        """方端 V 形箭头（两支）。tip＝尖端，dvec＝前进方向（单位向量）。"""
        dx, dy = dvec
        for s in (+1, -1):
            a = math.radians(s * half)
            bx = dx * math.cos(a) - dy * math.sin(a)
            by = dx * math.sin(a) + dy * math.cos(a)
            self.seg(tip, (tip[0] - L * bx, tip[1] - L * by), STROKE)

    def tri_head(self, tip, dvec, L=3.0, half=1.9):
        """实心三角箭头（仅用于弧线末端）。"""
        dx, dy = dvec
        nx, ny = -dy, dx
        base = (tip[0] - L * dx, tip[1] - L * dy)
        pts = [tip, (base[0] + half * nx, base[1] + half * ny),
               (base[0] - half * nx, base[1] - half * ny)]
        sub = self._new()
        ImageDraw.Draw(sub).polygon(_scale(pts, self.k), fill=255)
        self._union(sub)

    # --- 出图 ---------------------------------------------------------------
    def to_rgba(self, size_px, rgb):
        m = self.mask.resize((size_px, size_px), Image.Resampling.LANCZOS)
        img = Image.new("RGBA", (size_px, size_px), rgb + (255,))
        img.putalpha(m)
        return img

    # 便于谱面上直接画（返回同尺寸蒙版，供贴到大图）
    def to_mask(self, size_px):
        return self.mask.resize((size_px, size_px), Image.Resampling.LANCZOS)


# ----------------------------------------------------------------------------
# 2. 十四式
# ----------------------------------------------------------------------------
def i_close(k):
    k.seg((7.0, 7.0), (17.0, 17.0))
    k.seg((17.0, 7.0), (7.0, 17.0))


def i_send(k):
    # 折纸飞镖：实心。十八格里描边会把内折糊平，实心反而干净（spec 本就允许剪影）
    k.fill_poly([(20.8, 12.0), (3.4, 4.6), (11.4, 12.0), (3.4, 19.4)])


def i_write(k):
    # 封缄的信：信封 ＋ 折角
    k.poly([(4.2, 6.6), (19.8, 6.6), (19.8, 17.4), (4.2, 17.4)])
    k.seg((4.2, 6.6), (12.0, 13.2))
    k.seg((19.8, 6.6), (12.0, 13.2))


def i_reply(k):
    # ↩：竖臂 ＋ 横臂 ＋ 左向箭头
    k.seg((17.6, 6.4), (17.6, 14.0))
    k.seg((17.6, 14.0), (6.6, 14.0))
    k.vhead((6.6, 14.0), (-1.0, 0.0))


def i_back(k):
    k.seg((18.2, 12.0), (6.2, 12.0))
    k.vhead((6.2, 12.0), (-1.0, 0.0))


def i_pin(k):
    # 旗：杆 ＋ 三角旗
    k.seg((6.6, 4.0), (6.6, 20.4))
    k.seg((6.6, 5.0), (18.4, 9.0))
    k.seg((18.4, 9.0), (6.6, 13.0))


def i_hint(k):
    # 提示：环 ＋ 短杆 ＋ 点。点与杆必须留出气口，否则在 24 格里糊成一根粗柱
    k.ring(12.0, 12.2, 7.8)
    k.bar(12.0, 11.2, 12.0, 16.2, 2.2)
    k.disc(12.0, 8.0, 1.4)


def i_arrow_l(k):
    k.seg((14.4, 5.6), (7.6, 12.0))
    k.seg((7.6, 12.0), (14.4, 18.4))


def i_arrow_r(k):
    k.seg((9.6, 5.6), (16.4, 12.0))
    k.seg((16.4, 12.0), (9.6, 18.4))


def i_refresh(k):
    # 环缺口留在右上，箭头从缺口探出（顺时针）
    r = 6.6
    k.arc(12.0, 12.0, r, 300.0, 240.0)
    t = math.radians(240.0)
    px, py = 12.0 + r * math.cos(t), 12.0 + r * math.sin(t)
    k.tri_head((px + 0.7 * 0.866, py - 0.7 * 0.5), (0.866, -0.5), L=3.4, half=2.05)


def i_settings(k):
    # 齿轮：环要大、齿要短粗 —— 细长齿会读成星芒
    k.ring(12.0, 12.0, 5.6, 2.6)
    for i in range(8):
        a = math.radians(i * 45.0)
        k.seg((12.0 + 5.6 * math.cos(a), 12.0 + 5.6 * math.sin(a)),
              (12.0 + 8.8 * math.cos(a), 12.0 + 8.8 * math.sin(a)), 3.4)


def i_diagnostics(k):
    k.ring(10.6, 10.6, 6.2)
    k.seg((15.2, 15.2), (20.0, 20.0))


def i_logs(k):
    k.bar(4.6, 7.0, 19.4, 7.0)
    k.bar(4.6, 12.0, 19.4, 12.0)
    k.bar(4.6, 17.0, 14.0, 17.0)


def i_chronicle(k):
    # 册：摊开的书 —— 两页 ＋ 中缝（24 格下「摊开的书」比「卷轴」更易读）
    k.poly([(11.0, 6.4), (4.4, 8.0), (4.4, 17.2), (11.0, 18.8)])
    k.poly([(13.0, 6.4), (19.6, 8.0), (19.6, 17.2), (13.0, 18.8)])


ICONS = [
    ("N-01", "close",       "关闭",   i_close),
    ("N-02", "send",        "发送",   i_send),
    ("N-03", "write",       "写信",   i_write),
    ("N-04", "reply",       "回复",   i_reply),
    ("N-05", "back",        "返回",   i_back),
    ("N-06", "pin",         "标记",   i_pin),
    ("N-07", "hint",        "提示",   i_hint),
    ("N-08", "arrow_l",     "左翻",   i_arrow_l),
    ("N-09", "arrow_r",     "右翻",   i_arrow_r),
    ("N-10", "refresh",     "刷新",   i_refresh),
    ("N-11", "settings",    "设置",   i_settings),
    ("N-12", "diagnostics", "诊断",   i_diagnostics),
    ("N-13", "logs",        "日志",   i_logs),
    ("N-14", "chronicle",   "编年史", i_chronicle),
]


def render_icon(fn, size_px, rgb=None):
    k = Ink(U * SS)
    fn(k)
    return k, k.to_rgba(size_px, rgb or WHITE_ICON[:3])


# ----------------------------------------------------------------------------
# 3. 无缝贴图
# ----------------------------------------------------------------------------
def _seamless_field(n, comps):
    """整数频率正弦叠加 ⇒ 天然在 n×n 上无缝。返回 0..1 的二维列表。"""
    out = [[0.0] * n for _ in range(n)]
    amp_sum = sum(a for _, _, a, _ in comps)
    for y in range(n):
        ty = 2.0 * math.pi * y / n
        row = out[y]
        for x in range(n):
            tx = 2.0 * math.pi * x / n
            v = 0.0
            for fx, fy, a, ph in comps:
                v += a * math.sin(fx * tx + fy * ty + ph)
            row[x] = 0.5 + 0.5 * (v / amp_sum)
    return out


def make_ground_tile(n=256):
    """T-01 低频底纹：只留大尺度明暗，高频一律抹掉。"""
    comps = [
        (1, 0, 1.00, 0.4), (0, 1, 0.80, 1.9), (1, 1, 0.55, 3.1),
        (2, 1, 0.34, 0.9), (1, 2, 0.30, 2.4), (2, 2, 0.22, 5.0),
        (3, 1, 0.14, 1.2), (1, 3, 0.12, 4.4),
    ]
    f = _seamless_field(n, comps)
    lo, hi = 24.0, 46.0
    img = Image.new("RGBA", (n, n), (0, 0, 0, 255))
    px = img.load()
    for y in range(n):
        for x in range(n):
            v = lo + (hi - lo) * f[y][x]
            px[x, y] = (int(v * 1.06), int(v * 0.98), int(v * 0.86), 255)
    return img


def make_weave_tile(n=256, pitch=16):
    """T-02 亚麻平织：只用于有边界的件，不做整面底。"""
    base = 104.0
    span = 20.0
    img = Image.new("RGBA", (n, n), (0, 0, 0, 255))
    px = img.load()
    for y in range(n):
        for x in range(n):
            i, j = x // pitch, y // pitch
            if (i + j) % 2 == 0:          # 横线
                t = (y % pitch) / float(pitch)
            else:                          # 竖线
                t = (x % pitch) / float(pitch)
            u = (t - 0.5) * 2.0
            k = 0.5 + 0.5 * math.cos(u * math.pi)      # 0 边 1 中
            v = base - span * 0.5 + span * k
            # 每根线极轻的个体差异，避免机械感
            v += 2.2 * math.sin((i * 7 + j * 13) * 0.7)
            px[x, y] = (int(v * 1.06), int(v * 0.98), int(v * 0.86), 255)
    return img


def make_rule_strip(w=24, h=240, step=60):
    """T-03 竖规饰带：一根细规 ＋ 每 step 一枚朱点，竖向无缝。"""
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rectangle([w // 2 - 1, 0, w // 2, h - 1],
                fill=C_STRUCT + (255,))
    cx, cy = w / 2.0, step / 2.0
    while cy < h:
        s = 5.0
        d.polygon([(cx, cy - s), (cx + s, cy), (cx, cy + s), (cx - s, cy)],
                  fill=C_ACCENT + (255,))
        cy += step
    return img


def tile_repeat(tile, nx, ny):
    w, h = tile.size
    out = Image.new("RGBA", (w * nx, h * ny), (0, 0, 0, 0))
    for j in range(ny):
        for i in range(nx):
            out.paste(tile, (i * w, j * h))
    return out


# ----------------------------------------------------------------------------
# 4. 谱面工具
# ----------------------------------------------------------------------------
def font(path, size):
    return ImageFont.truetype(path, size)


def draw_ls(d, xy, s, f, fill, ls=0.0):
    """带字距的横排。"""
    x, y = xy
    for ch in s:
        d.text((x, y), ch, font=f, fill=fill)
        x += d.textlength(ch, font=f) + ls
    return x


def vgrad(size, c0, c1):
    w, h = size
    strip = Image.new("RGB", (1, h))
    p = strip.load()
    for y in range(h):
        t = y / float(max(1, h - 1))
        p[0, y] = tuple(int(c0[i] + (c1[i] - c0[i]) * t) for i in range(3))
    return strip.resize((w, h), Image.Resampling.BILINEAR).convert("RGBA")


def _rect(d, x0, y0, x1, y1, col):
    d.rectangle([min(x0, x1), min(y0, y1), max(x0, x1), max(y0, y1)], fill=col)


def register_marks(img, m=42, arm=26, col=C_ACCENT, w=2):
    d = ImageDraw.Draw(img)
    W, H = img.size
    for (x, y, sx, sy) in ((m, m, 1, 1), (W - m, m, -1, 1),
                           (m, H - m, 1, -1), (W - m, H - m, -1, -1)):
        _rect(d, x, y, x + sx * arm, y + sy * w, col + (210,))
        _rect(d, x, y, x + sx * w, y + sy * arm, col + (210,))


# ----------------------------------------------------------------------------
# 5. 样张版
# ----------------------------------------------------------------------------
def build_plate(path):
    W, H = 2400, 1600
    M = 110
    img = vgrad((W, H), C_GROUND, C_GROUND2)
    img = img.convert("RGBA")
    d = ImageDraw.Draw(img)

    f_hz = font(FONT_CJK, 128)
    f_sub = font(FONT_MONO, 23)
    f_spec = font(FONT_CJK, 20)
    f_idx = font(FONT_MONO, 17)
    f_name = font(FONT_CJK, 27)
    f_key = font(FONT_MONO, 15)
    f_tag = font(FONT_MONO, 16)

    # ---- 页眉 --------------------------------------------------------------
    d.text((M - 6, 92), "恒形", font=f_hz, fill=C_PIGMENT)
    draw_ls(d, (M + 4, 262), "CONSTANT FORM", f_sub, C_STRUCT + (255,), 13.0)

    d.rectangle([M, 300, W - M, 300], fill=C_STRUCT + (120,))
    draw_ls(d, (M + 2, 316), "二十四格 · 线宽二点四 · 方端 · 纯白剪影 · 引擎染色",
            f_spec, C_STRUCT + (255,), 1.0)

    # 右上：册号与规格
    right = W - M
    lines = [
        ("SPECIMEN SHEET 01", C_PIGMENT, f_sub, 7.0),
        ("ui_awake_icon", C_STRUCT, f_sub, 4.0),
        ("14 SIGNS  ·  48 / 72", C_STRUCT, f_tag, 3.0),
    ]
    y = 104
    for s, c, f, ls in lines:
        wdt = sum(d.textlength(ch, font=f) + ls for ch in s) - ls
        draw_ls(d, (right - wdt, y), s, f, c + (255,), ls)
        y += 34

    # 比例尺：1 格 = 4px ⇒ 24 格 = 96px
    sb_y, sb_x, unit = 232, right - 96, 4
    d.rectangle([sb_x, sb_y, sb_x + 96, sb_y + 1], fill=C_STRUCT + (190,))
    for i in range(0, 25, 6):
        d.rectangle([sb_x + i * unit, sb_y, sb_x + i * unit, sb_y + 7],
                    fill=C_STRUCT + (190,))
    d.text((sb_x - 34, sb_y - 9), "24", font=f_tag, fill=C_STRUCT + (255,))

    # ---- 标本格 ------------------------------------------------------------
    cols, rows = 7, 2
    cw = (W - 2 * M - (cols - 1) * 20) // cols
    box = 200
    cell_h = box + 18 + 62
    y0 = 356
    for n, (idx, key, zh, fn) in enumerate(ICONS):
        c, r = n % cols, n // cols
        cx0 = M + c * (cw + 20)
        cy0 = y0 + r * (cell_h + 26)

        # 格：淡格线 ＋ 外框
        gx0, gy0 = cx0 + (cw - box) // 2, cy0
        for i in range(1, 6):
            p = gx0 + int(box * i / 6.0)
            q = gy0 + int(box * i / 6.0)
            d.rectangle([p, gy0, p, gy0 + box], fill=C_STRUCT + (34,))
            d.rectangle([gx0, q, gx0 + box, q], fill=C_STRUCT + (34,))
        d.rectangle([gx0, gy0, gx0 + box - 1, gy0 + box - 1],
                    outline=C_STRUCT + (70,), width=1)

        # 图标
        k = Ink(U * SS)
        fn(k)
        GL = 164
        m = k.to_mask(GL)
        layer = Image.new("RGBA", (GL, GL), C_PIGMENT + (0,))
        layer.putalpha(m)
        img.alpha_composite(layer, (gx0 + (box - GL) // 2, gy0 + (box - GL) // 2))

        # 标注：编号与英文名同行（按实测字宽排开，不再压字），中文名下沉一行
        ly = gy0 + box + 18
        d.text((cx0, ly), idx, font=f_idx, fill=C_STRUCT + (255,))
        off = d.textlength(idx, font=f_idx) + 12
        d.text((cx0 + off, ly), key, font=f_idx, fill=C_STRUCT + (255,))
        d.text((cx0, ly + 26), zh, font=f_name, fill=C_PIGMENT)

    # ---- 贴图带 ------------------------------------------------------------
    sy = 1030
    d.rectangle([M, sy - 30, W - M, sy - 30], fill=C_STRUCT + (120,))
    draw_ls(d, (M + 2, sy - 22), "TILES  ·  SEAMLESS", f_tag, C_STRUCT + (255,), 3.0)

    ground = make_ground_tile(256)
    weave = make_weave_tile(256)
    rule = make_rule_strip(24, 240)

    specs = [
        ("T-01", "ground", "低频底纹", tile_repeat(ground, 2, 2).resize((200, 200), Image.Resampling.LANCZOS)),
        ("T-02", "weave", "亚麻平织", tile_repeat(weave, 2, 2).resize((200, 200), Image.Resampling.LANCZOS)),
    ]
    bx = M
    for tag, key, zh, im in specs:
        img.alpha_composite(im, (bx, sy))
        d.rectangle([bx, sy, bx + 199, sy + 199], outline=C_STRUCT + (70,), width=1)
        d.text((bx, sy + 210), tag + "  " + key, font=f_idx, fill=C_STRUCT + (255,))
        d.text((bx, sy + 234), zh, font=f_name, fill=C_PIGMENT)
        bx += 240

    # 竖向饰带：竖排重复 5 次展示连续性
    img.alpha_composite(rule.resize((24, 200), Image.Resampling.LANCZOS), (bx + 4, sy))
    d.rectangle([bx + 4, sy, bx + 27, sy + 199], outline=C_STRUCT + (70,), width=1)
    d.text((bx, sy + 210), "T-03  rule", font=f_idx, fill=C_STRUCT + (255,))
    d.text((bx, sy + 234), "竖规饰带", font=f_name, fill=C_PIGMENT)

    # 贴图说明：右对齐收在页边，左侧加一道细规把空档结构起来
    notes = [
        ("T-01  ground_tile_256.png", "只做面板底。低频单一 —— 高频一律不进底面。"),
        ("T-02  weave_tile_256.png", "只做有边界的件（标题带 / 键 / 槽），面积宜 ≤15%。"),
        ("T-03  rule_repeat_24x240.png", "竖向无缝。做侧栏与书眉的规线。"),
    ]
    d.rectangle([1150, sy - 30, 1150, sy + 206], fill=C_STRUCT + (70,))
    tx = W - M
    ny = sy + 4
    for a, b in notes:
        d.text((tx, ny), a, font=f_tag, fill=C_PIGMENT, anchor="ra")
        d.text((tx, ny + 24), b, font=f_key, fill=C_STRUCT + (255,), anchor="ra")
        ny += 68

    # ---- 实尺预览：图标在游戏里就是这么小，认不认得出看这一条 -------------
    d.rectangle([M, 1330, W - M, 1330], fill=C_STRUCT + (120,))
    draw_ls(d, (M + 2, 1338), "ACTUAL SIZE  ·  24 / 48", f_tag, C_STRUCT + (255,), 3.0)

    base_y = 1414
    for i, (idx, key, zh, fn) in enumerate(ICONS):
        kk = Ink(U * SS)
        fn(kk)
        for px, pitch, x0 in ((24, 36, M), (48, 60, M + 560)):
            m = kk.to_mask(px)
            lay = Image.new("RGBA", (px, px), C_PIGMENT + (0,))
            lay.putalpha(m)
            img.alpha_composite(lay, (x0 + i * pitch, base_y - px))
    d.text((M, 1420), "24", font=f_key, fill=C_STRUCT + (255,))
    d.text((M + 560, 1420), "48", font=f_key, fill=C_STRUCT + (255,))

    # ---- 页脚 --------------------------------------------------------------
    d.rectangle([M, 1462, W - M, 1462], fill=C_STRUCT + (120,))
    d.text((M + 2, 1479), "形恒，质殊。", font=font(FONT_CJK, 22), fill=C_STRUCT + (255,))
    tail = "AWAKE  ·  2026-09-13  ·  REV 01"
    wdt = sum(d.textlength(ch, font=f_tag) + 3.0 for ch in tail) - 3.0
    draw_ls(d, (W - M - wdt, 1483), tail, f_tag, C_STRUCT + (255,), 3.0)

    register_marks(img)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.convert("RGB").save(path, quality=95)
    return path


# ----------------------------------------------------------------------------
# 6. main
# ----------------------------------------------------------------------------
def build_zoom_sheet(path):
    """放大图鉴：逐枚复核用（UI 线看这个，不看整版）。"""
    W, H, M = 2280, 820, 90
    img = vgrad((W, H), C_GROUND, C_GROUND2).convert("RGBA")
    d = ImageDraw.Draw(img)
    f_idx = font(FONT_MONO, 20)
    f_name = font(FONT_CJK, 32)

    cols = 7
    cw = (W - 2 * M - (cols - 1) * 30) // cols
    box, GL = 260, 230
    for n, (idx, key, zh, fn) in enumerate(ICONS):
        c, r = n % cols, n // cols
        cx0 = M + c * (cw + 30)
        cy0 = 60 + r * 360
        k = Ink(U * SS)
        fn(k)
        m = k.to_mask(GL)
        lay = Image.new("RGBA", (GL, GL), C_PIGMENT + (0,))
        lay.putalpha(m)
        img.alpha_composite(lay, (cx0, cy0))
        d.text((cx0, cy0 + box + 6), idx + "   " + key, font=f_idx,
               fill=C_STRUCT + (255,))
        d.text((cx0, cy0 + box + 34), zh, font=f_name, fill=C_PIGMENT)

    register_marks(img, m=34, arm=20)
    img.convert("RGB").save(path, quality=95)
    return path


def main():
    os.makedirs(DIR_ICON, exist_ok=True)
    os.makedirs(DIR_ORN, exist_ok=True)
    os.makedirs(OUT_PLATE, exist_ok=True)

    n = 0
    for idx, key, zh, fn in ICONS:
        k48, im48 = render_icon(fn, 48)
        im48.save(os.path.join(DIR_ICON, "icon_%s_48.png" % key))
        k72, im72 = render_icon(fn, 72)
        im72.save(os.path.join(DIR_ICON, "icon_%s_72.png" % key))
        n += 2
    print("icons written:", n)

    for name, maker in (("ground_tile_256.png", make_ground_tile),
                        ("weave_tile_256.png", make_weave_tile),
                        ("rule_repeat_24x240.png", make_rule_strip)):
        maker().save(os.path.join(DIR_ORN, name))
        print("tile:", name)

    p = build_plate(os.path.join(OUT_PLATE, "plate_constant_form.png"))
    print("plate:", p)
    z = build_zoom_sheet(os.path.join(OUT_PLATE, "sheet_icons_zoom.png"))
    print("zoom:", z)


if __name__ == "__main__":
    main()
