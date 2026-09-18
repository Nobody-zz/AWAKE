# -*- coding: utf-8 -*-
"""冷灰岩 ＋ 使用痕迹：三档并排

我上一轮说冷灰岩的"代价是偏制式"。**这句话我说错了。**
真正的风险不是"制式"，是**"干净"** —— 一块没有使用痕迹的冷灰，
读起来是现代 UI 的灰按钮，不是中世纪的物件。

中世纪的手工制品跟野外的自然材质，区别在于它是**加工过的**：
刨过、锻过、凿过、被人拿过。加工过的东西更冷更硬 —— 这是它该有的样子。
但要让它可信，必须有**用过的痕迹**：
  ① 亮棱不连续（磨损）—— 棱线被摸断，不是一条完美的线
  ② 金有深浅（氧化）—— 不是均匀的亮金
  ③ 纹理不均匀（手作）—— 低频起伏，不是机器压的
"""
import os
import sys
import random

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

from PIL import Image, ImageChops, ImageDraw, ImageFont

import artkit as K
import parts as P

OUT = os.path.join(HERE, "out")
LIGHT = (238, 230, 214)
SHADOW = (9, 8, 7)
BEV = 4
INSET = BEV + 1

# 冷灰岩（第二版定案值）
FACE = (67, 71, 76)
FACE_H = (89, 93, 98)
FACE_P = (49, 52, 56)
SEC = (53, 57, 61)
TAB = (37, 40, 43)
FRAME = (44, 48, 52)
GOLD = (232, 182, 92)
TXT = (240, 244, 250)


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def lowfreq(size, cells_x, seed):
    """低频噪声：先画小图再放大，得到平滑起伏。"""
    w, h = size
    cw = max(2, cells_x)
    ch = max(2, int(round(cells_x * h / float(w))))
    r = random.Random(seed)
    small = Image.new("L", (cw, ch))
    small.putdata([r.randint(0, 255) for _ in range(cw * ch)])
    return small.resize((w, h), Image.BICUBIC)


def wear_mask(mask, wear, seed, size):
    """按噪声削弱 mask —— 磨损后的棱线是断的。"""
    if wear <= 0:
        return mask
    n = lowfreq(size, 22, seed)
    n = n.point(lambda v: int(255 * ((1.0 - wear) + wear * (v / 255.0))))
    return ImageChops.multiply(mask, n)


def make_btn(w, h, face, frame, *, cut=13.0, ss=6, accent=None,
             hi_wear=0.0, gold_wear=0.0, coarse=0.0, tex_delta=14,
             seed=11, grad=-9):
    frame_lit = mix(frame, LIGHT, 0.42)
    frame_dark = mix(frame, SHADOW, 0.74)
    size = (w, h)

    img = P._frame(w, h, cut, ss, BEV, frame, lit_c=frame_lit, dark_c=frame_dark)
    fm = K.poly_mask(size, K.chamfer_pts(w, h, cut, INSET), ss=ss)
    # delta 是**峰谷灰阶**（artkit.textured 的语义），石头要 12~16 才看得见；
    # coarse 是低频斑驳的模糊半径，设计区间 25~40（不是 7/15）。
    img = K.over(img, K.textured(size, face, delta=tex_delta, fine=0.9,
                                 coarse=coarse, ss=ss), fm)

    top = mix(face, LIGHT, abs(grad) / 100.0)
    bot = mix(face, SHADOW, abs(grad) / 100.0)
    g = Image.new("RGB", size)
    gd = ImageDraw.Draw(g)
    for y in range(h):
        t = y / float(max(1, h - 1))
        gd.line([(0, y), (w, y)],
                fill=tuple(int(top[i] + (bot[i] - top[i]) * t) for i in range(3)))
    img = K.over(img, g, fm)

    # ① 顶部亮棱 —— 带磨损
    hi = P._band(size, fm, top=INSET, thick=1)
    img = K.over(img, K.solid(size, mix(face, LIGHT, 0.62)),
                 wear_mask(hi, hi_wear, seed, size))

    lo = P._band(size, fm, bottom=h - 1 - INSET + 1 - 2,
                 thick=max(2, int(h * 0.16)))
    img = K.over(img, K.solid(size, mix(face, SHADOW, 0.80)),
                 lo.point(lambda v: int(v * 0.85)))

    # 底部反射亮棱 —— 也带磨损（比顶部轻）
    bl = P._band(size, fm, bottom=h - INSET, thick=1)
    img = K.over(img, K.solid(size, mix(face, LIGHT, 0.40)),
                 wear_mask(bl, hi_wear * 0.7, seed + 5, size))

    # ② 金条：亮金打底，**暗斑**按噪声稀疏浮现（氧化是斑，不是整体压暗）
    if accent:
        color, thick, gap = accent
        am = P._band(size, fm, bottom=h - 3 - gap, thick=thick)
        img = K.over(img, K.solid(size, color), am)
        if gold_wear > 0:
            n = lowfreq(size, 16, seed + 3)
            # 只有最暗的一部分露面 ⇒ 稀疏的暗斑
            thr = int(255 * (1.0 - gold_wear))
            spots = n.point(lambda v: 255 if v < thr else 0)
            dark = ImageChops.multiply(am, spots)
            img = K.over(img, K.solid(size, tuple(int(v * 0.55) for v in color)),
                         dark)
    return img


def _font(sz):
    try:
        return ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", sz)
    except Exception:
        return ImageFont.load_default()


F_TITLE = _font(20)
F_HEAD = _font(15)
F_TINY = _font(11)

BG = (23, 23, 27)
FG = (226, 226, 234)
DIM = (140, 140, 154)
CARD = (32, 32, 38)

MARGIN = 30
GAP = 22
COLW = 132

WEAR = [
    ("无磨损", "棱线完美、金均匀", dict(hi_wear=0.0, gold_wear=0.0, coarse=0.0)),
    ("轻磨损", "棱线偶断、金有深浅", dict(hi_wear=0.38, gold_wear=0.30, coarse=7.0)),
    ("中磨损", "棱线断续、金明显氧化", dict(hi_wear=0.62, gold_wear=0.50, coarse=15.0)),
]


def main():
    cols = []
    for nm, feel, kw in WEAR:
        b1 = make_btn(110, 35, FACE, FRAME, **kw)
        b2 = make_btn(105, 35, FACE, FRAME, accent=(GOLD, 2, 4), **kw)
        cols.append((nm, feel, b1, b2))

    rowh = 16 + 35 + 10
    ZOOM = 4
    ZW = 46
    ZH = 35 * ZOOM
    COL_H = rowh * 2 + 16 + ZH + 8 + 22
    W_BOARD = MARGIN * 2 + COLW * 3 + GAP * 2
    H_BOARD = MARGIN + 30 + 26 + 24 + COL_H + 56 + MARGIN

    img = Image.new("RGB", (W_BOARD, H_BOARD), BG)
    d = ImageDraw.Draw(img)

    d.text((MARGIN, MARGIN), "冷灰岩 ＋ 使用痕迹 · 三档", fill=FG, font=F_TITLE)
    d.text((MARGIN, MARGIN + 26),
           "底色同为冷灰岩。差别只在三件事：棱线断不断、金匀不匀、纹理平不平。"
           "放大窗看左端 46px。", fill=DIM, font=F_TINY)

    y = MARGIN + 30 + 26
    for i, (nm, feel, b1, b2) in enumerate(cols):
        x = MARGIN + i * (COLW + GAP)
        d.text((x, y), nm, fill=FG, font=F_HEAD)
    y += 24

    for i, (nm, feel, b1, b2) in enumerate(cols):
        x = MARGIN + i * (COLW + GAP)
        yy = y
        for tag, im in [("主按钮", b1), ("页签 选中", b2)]:
            d.text((x, yy), tag, fill=DIM, font=F_TINY)
            yy += 16
            img.paste(im, (x, yy), im)
            yy += 35 + 10
        d.text((x, yy), "放大 4×", fill=DIM, font=F_TINY)
        yy += 16
        z = b1.crop((0, 0, ZW, 35)).resize((ZW * ZOOM, ZH), Image.NEAREST)
        d.rectangle([x - 4, yy - 4, x + ZW * ZOOM + 3, yy + ZH + 3], fill=CARD)
        img.paste(z, (x, yy))
        yy += ZH + 8
        for j, ln in enumerate(_wrap(d, feel, F_TINY, COLW)):
            d.text((x, yy + j * 14), ln, fill=(116, 116, 128), font=F_TINY)

    y += COL_H + 14
    note = ("我的判断：中磨损过火，无磨损是「现代灰按钮」。取轻磨损 —— "
            "在 1× 下它只是「这块料有点旧」，放大了才看出棱线是断的。"
            "痕迹应该是余光里感觉到的，不是盯着看的。")
    for j, ln in enumerate(_wrap(d, note, F_TINY, W_BOARD - MARGIN * 2)):
        d.text((MARGIN, y + j * 14), ln, fill=DIM, font=F_TINY)

    path = os.path.join(OUT, "wear_board.png")
    img.save(path)
    print("saved", path, img.size)


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
