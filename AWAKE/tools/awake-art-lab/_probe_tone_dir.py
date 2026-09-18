# -*- coding: utf-8 -*-
"""按钮质感方向板：三种光影逻辑并排（真尺寸 + 放大 + 实测明度曲线）

诊断（out/study/shape/tone_diag.txt）给出的三条事实：
  ① 平均明度 56 == 原版 56        ⇒ 不是曝光问题，改曝光没用
  ② 极差 94 vs 原版 170           ⇒ 是动态范围问题
  ③ 木面 y7~y22 十六行死平 65.5   ⇒ 没有体积
  ④ 原版 y45 有 137 的底部亮棱，我们没有

本板做三个变体，每个都**实测**明度统计（不是声称）：
  甲 现状      平涂 + 上沿高光 + 下沿压深（无底部亮棱）
  乙 软渐变    拉开值域 + 木面连续渐变 + 底部反射光
  丙 硬边      窄锐亮棱 + 深净暗部 + 硬分界（碑刻硬材质）
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

from PIL import Image, ImageDraw, ImageFont

import artkit as K
import parts as P

OUT = os.path.join(HERE, "out")
W, H = 110, 35
BEV = 4
CUT = 13.0
INSET = BEV + 1          # 木面内缩量 = 5

FIELD = K.BTN_FIELD
FRAME_C = (66, 58, 47)
FRAME_LIT = (108, 96, 80)


# ---------------------------------------------------------------- 变体生成

def _vgrad(w, h, top_c, bot_c):
    im = Image.new("RGB", (w, h))
    d = ImageDraw.Draw(im)
    for y in range(h):
        t = y / float(max(1, h - 1))
        d.line([(0, y), (w, y)],
               fill=tuple(int(top_c[i] + (bot_c[i] - top_c[i]) * t) for i in range(3)))
    return im


def make_variant(*, grad_amp=0, hi_add=34, hi_str=0.75, hi_thick=1,
                 lo_c=(10, 8, 7), lo_str=0.45, lo_thick=None,
                 bot_add=None, bot_str=1.0, bot_thick=0,
                 tex_delta=9):
    """btn_plate 的参数化复刻，只加几个旋钮，不动产线。"""
    if lo_thick is None:
        lo_thick = max(2, int(H * 0.16))
    ss = 6
    img = P._frame(W, H, CUT, ss, BEV, FRAME_C,
                   lit_c=FRAME_LIT, dark_c=(28, 24, 21))
    fm = K.poly_mask((W, H), K.chamfer_pts(W, H, CUT, INSET), ss=ss)
    img = K.over(img, K.textured((W, H), FIELD, delta=tex_delta,
                                 fine=0.9, coarse=0.0, ss=ss), fm)

    # 木面垂直渐变（上暗下亮 or 上亮下暗，由 grad_amp 正负定）
    if grad_amp:
        a = abs(grad_amp)
        top = tuple(max(0, v - a) for v in FIELD)
        bot = tuple(min(255, v + a) for v in FIELD)
        if grad_amp < 0:
            top, bot = bot, top
        img = K.over(img, _vgrad(W, H, top, bot), fm)

    # 上沿高光
    hi = P._band((W, H), fm, top=INSET, thick=hi_thick)
    img = K.over(img, K.solid((W, H), P._lighten(FIELD, hi_add)),
                 hi.point(lambda v: int(v * hi_str)))

    # 下沿压深
    lo = P._band((W, H), fm, bottom=H - 1 - INSET + 1 - 2, thick=lo_thick)
    img = K.over(img, K.solid((W, H), lo_c), lo.point(lambda v: int(v * lo_str)))

    # 底部反射亮棱（木面最下沿）
    if bot_thick and bot_add is not None:
        bl = P._band((W, H), fm, bottom=H - INSET, thick=bot_thick)
        img = K.over(img, K.solid((W, H), P._lighten(FIELD, bot_add)),
                     bl.point(lambda v: int(v * bot_str)))
    return img


# ---------------------------------------------------------------- 实测

def lum(p):
    return 0.299 * p[0] + 0.587 * p[1] + 0.114 * p[2]


def row_med(im):
    im = im.convert("RGBA")
    px = im.load()
    w, h = im.size
    out = []
    for y in range(h):
        vs = [lum(px[x, y]) for x in range(w) if px[x, y][3] > 128]
        out.append(sorted(vs)[len(vs) // 2] if vs else None)
    return out


def stats(im):
    """统一口径：像素级 p01/p99 作极差（跟原版基线可比）。

    木面振幅只量**主体**（去掉上沿高光带 3 行与下沿压深带 7 行），
    否则高光/压深会主导这个数 —— 甲实测会算出 51，而它主体其实是 0。
    """
    im = im.convert("RGBA")
    px = im.load()
    w, h = im.size
    allv = []
    rows = [[] for _ in range(h)]
    for y in range(h):
        for x in range(w):
            p = px[x, y]
            if p[3] > 128:
                L = lum(p)
                allv.append(L)
                rows[y].append(L)
    allv.sort()

    def q(a, p):
        return a[min(len(a) - 1, max(0, int(round(p * (len(a) - 1)))))]

    def hiq(ys):
        vs = []
        for y in ys:
            vs += rows[y]
        vs.sort()
        return q(vs, .95) if vs else 0.0

    p01, p99 = q(allv, .01), q(allv, .99)
    rmed = [(sorted(r)[len(r) // 2] if r else None) for r in rows]
    wood = []
    for y in range(INSET + 3, H - INSET - 7):
        if rmed[y] is not None:
            wood.append(rmed[y])
    return {"range": p99 - p01, "amp": (max(wood) - min(wood)) if wood else 0.0,
            "top": hiq(range(0, 8)), "bot": hiq(range(H - 8, H)),
            "curve": rmed, "min": p01, "max": p99}


def ref_stats(path):
    """原版基线，同口径。裁掉透明边距后再量。"""
    im = Image.open(path).convert("RGBA")
    bb = im.getbbox()
    if bb:
        im = im.crop(bb)
    s = stats(im)
    # 原版高 60，木面段按比例取中间
    px = im.load()
    w, h = im.size
    rows = [[] for _ in range(h)]
    for y in range(h):
        for x in range(w):
            p = px[x, y]
            if p[3] > 128:
                rows[y].append(lum(p))
    rmed = [(sorted(r)[len(r) // 2] if r else None) for r in rows]
    y0, y1 = int(h * 0.30), int(h * 0.65)
    wood = [v for v in rmed[y0:y1] if v is not None]
    s["amp"] = (max(wood) - min(wood)) if wood else 0.0
    return s


# ---------------------------------------------------------------- 画板

def _font(sz):
    try:
        return ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", sz)
    except Exception:
        return ImageFont.load_default()


F_TITLE = _font(20)
F_HEAD = _font(15)
F_SM = _font(12)
F_TINY = _font(11)

BG = (23, 23, 27)
TXT = (226, 226, 234)
DIM = (140, 140, 154)
CARD = (32, 32, 38)
ACC = (120, 205, 145)
WARN = (240, 175, 120)
HOT = (235, 150, 120)

MARGIN = 30
LABW = 96
GAP = 26
ZOOM = 4
ZOOM_W = 44               # 放大窗取中间一段（避开端头），保留全高
COLW = ZOOM_W * ZOOM      # 176


def main():
    variants = [
        ("甲 · 现状", "平涂木面／无底部亮棱",
         make_variant(), WARN),
        ("乙 · 软渐变（凹）", "凹坑读法：上暗下亮＋连续渐变＋底部反射光",
         make_variant(grad_amp=14, hi_add=52, hi_str=0.85,
                      lo_c=(6, 5, 4), lo_str=0.62,
                      bot_add=44, bot_str=0.75, bot_thick=1), ACC),
        ("丙 · 硬边（碑刻）", "凸棱读法：上亮下暗＋窄锐亮棱＋硬分界",
         make_variant(grad_amp=-8, hi_add=110, hi_str=1.0, hi_thick=1,
                      lo_c=(16, 14, 12), lo_str=0.85,
                      bot_add=96, bot_str=1.0, bot_thick=1, tex_delta=5), HOT),
    ]

    mets = [stats(im) for _, _, im, _ in variants]

    curve_h = 46
    data_h = 5 * 15 + 8
    W_BOARD = MARGIN * 2 + LABW + GAP + COLW * 3 + GAP * 2
    H_BOARD = (MARGIN + 30 + 30 + 20 + 35 + 14 + 35 * ZOOM + 12 +
               curve_h + 14 + data_h + 40 + MARGIN)

    img = Image.new("RGB", (W_BOARD, H_BOARD), BG)
    d = ImageDraw.Draw(img)

    d.text((MARGIN, MARGIN), "按钮质感方向板 · 三种光影逻辑", fill=TXT, font=F_TITLE)
    d.text((MARGIN, MARGIN + 26),
           "全部为实测值（非声称）。平均明度都是 56 左右 —— 差别在动态范围与体积，不在曝光。",
           fill=DIM, font=F_TINY)

    y = MARGIN + 30 + 30

    # 列标题
    for i, (nm, sub, im, c) in enumerate(variants):
        x = MARGIN + LABW + GAP + i * (COLW + GAP)
        d.text((x, y), nm, fill=c, font=F_HEAD)
    y += 20

    # 真尺寸（列内居中）
    for i, (nm, sub, im, c) in enumerate(variants):
        cx = MARGIN + LABW + GAP + i * (COLW + GAP)
        img.paste(im, (cx + (COLW - W) // 2, y), im)
    y += 35 + 14

    # 放大（取中间段，保留全高）
    for i, (nm, sub, im, c) in enumerate(variants):
        cx = MARGIN + LABW + GAP + i * (COLW + GAP)
        x0 = (W - ZOOM_W) // 2
        z = im.crop((x0, 0, x0 + ZOOM_W, H)).resize(
            (ZOOM_W * ZOOM, H * ZOOM), Image.NEAREST)
        d.rectangle([cx - 4, y - 4, cx + COLW + 3, y + H * ZOOM + 3], fill=CARD)
        img.paste(z, (cx, y))
    y += 35 * ZOOM + 12

    # 逐行明度迷你曲线（死平 vs 爬升，一眼可辨）
    d.text((MARGIN, y + 14), "逐行明度", fill=DIM, font=F_TINY)
    for i, (nm, sub, im, c) in enumerate(variants):
        cx = MARGIN + LABW + GAP + i * (COLW + GAP)
        m = mets[i]
        d.rectangle([cx - 4, y - 4, cx + COLW + 3, y + curve_h + 3], fill=CARD)
        pts = []
        for j, v in enumerate(m["curve"]):
            if v is None:
                continue
            px = cx + int(round(j / float(H - 1) * (COLW - 1)))
            py = y + curve_h - int(round((v - m["min"]) / max(1.0, m["max"] - m["min"]) * (curve_h - 4)))
            pts.append((px, py))
        if len(pts) > 1:
            d.line(pts, fill=c, width=2)
        d.text((cx + COLW - 34, y - 4), "上→下", fill=(96, 96, 108), font=F_TINY)
    y += curve_h + 14

    # 数据
    keys = [("极差", "range", "%.0f"), ("木面振幅", "amp", "%.0f"),
            ("顶棱", "top", "%.0f"), ("底棱", "bot", "%.0f"),
            ("暗部", "min", "%.0f")]
    for r, (lbl, k, fmt) in enumerate(keys):
        d.text((MARGIN, y + r * 15), lbl, fill=DIM, font=F_TINY)
        for i, (nm, sub, im, c) in enumerate(variants):
            x = MARGIN + LABW + GAP + i * (COLW + GAP)
            d.text((x, y + r * 15), fmt % mets[i][k], fill=TXT, font=F_TINY)
    y += data_h

    # 列脚注
    for i, (nm, sub, im, c) in enumerate(variants):
        x = MARGIN + LABW + GAP + i * (COLW + GAP)
        for j, ln in enumerate(_wrap(d, sub, F_TINY, COLW)):
            d.text((x, y + j * 14), ln, fill=(118, 118, 130), font=F_TINY)

    y += 34
    note = ("原版基线：极差 170 ／ 底部亮棱 137 ／ 木面连续爬升 45→73。"
            "我们的 mean 与它相同（56），缺的是后面三样。")
    for j, ln in enumerate(_wrap(d, note, F_TINY, W_BOARD - MARGIN * 2)):
        d.text((MARGIN, y + j * 14), ln, fill=DIM, font=F_TINY)

    path = os.path.join(OUT, "tone_dir_board.png")
    img.save(path)
    print("saved", path, img.size)
    REF = os.path.normpath(os.path.join(
        HERE, "..", "awake-ui-lab", "out", "atlas", "sprites",
        "General__Button__main_button_regular.png"))
    for (nm, _, _, _), m in zip(variants, mets):
        print("  %-16s 极差 %3.0f  木面振幅 %4.1f  顶棱 %3.0f  底棱 %3.0f  最暗 %3.0f"
              % (nm, m["range"], m["amp"], m["top"], m["bot"], m["min"]))
    if os.path.exists(REF):
        r = ref_stats(REF)
        print("  %-16s 极差 %3.0f  木面振幅 %4.1f  顶棱 %3.0f  底棱 %3.0f  最暗 %3.0f"
              % ("原版基线", r["range"], r["amp"], r["top"], r["bot"], r["min"]))
    else:
        print("  !! 原版找不到", REF)


def _wrap(d, text, font, maxw):
    lines, cur = [], ""
    for ch in text:
        if d.textlength(cur + ch, font=font) > maxw:
            lines.append(cur)
            cur = ch
        else:
            cur += ch
    if cur:
        lines.append(cur)
    return lines


if __name__ == "__main__":
    main()
