# -*- coding: utf-8 -*-
"""按钮配色方向板：四套配色并排（真尺寸）

用户重点＝**颜色** 与 **外形质感**。这里两个变量一起给：
  · 颜色：四套（现状橄榄铜 / 净石暖灰 / 冷灰岩 / 铜锈）
  · 质感：统一走"凸＋硬边"（上亮下暗、窄锐亮棱、深净暗部）

关键设计：**光源色全局统一** LIGHT=(238,230,214) 暖白。
四套只换材质色，高光一律由材质色向 LIGHT 混合而来 —— 这样它们才像
同一个世界里的东西，而不是四套各自打光的贴图。

每列给四个图：主按钮 default / hover / 次按钮 / 页签 selected(带金条)，
用来看：颜色、状态可辨性、层级、金跟底色的关系。
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

from PIL import Image, ImageDraw, ImageFont

import artkit as K
import parts as P

OUT = os.path.join(HERE, "out")

LIGHT = (238, 230, 214)      # 光源色（暖白）—— 全局唯一，四套共用
SHADOW = (9, 8, 7)           # 遮蔽色
BEV = 4
INSET = BEV + 1


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def make_btn(w, h, face, frame, *, cut=13.0, ss=6, accent=None,
             tex_delta=5, hi_t=0.86, bot_t=0.52, lo_t=0.80, grad=-9):
    """凸＋硬边：上亮下暗、窄锐亮棱、深净暗部。不动产线，独立实验函数。"""
    frame_lit = mix(frame, LIGHT, 0.42)
    frame_dark = mix(frame, SHADOW, 0.74)

    img = P._frame(w, h, cut, ss, BEV, frame,
                   lit_c=frame_lit, dark_c=frame_dark)
    fm = K.poly_mask((w, h), K.chamfer_pts(w, h, cut, INSET), ss=ss)
    img = K.over(img, K.textured((w, h), face, delta=tex_delta,
                                 fine=0.9, coarse=0.0, ss=ss), fm)

    # 木面：上亮下暗的连续渐变（负 grad = 上亮）
    top = mix(face, LIGHT, abs(grad) / 100.0)
    bot = mix(face, SHADOW, abs(grad) / 100.0)
    if grad > 0:
        top, bot = bot, top
    g = Image.new("RGB", (w, h))
    gd = ImageDraw.Draw(g)
    for y in range(h):
        t = y / float(max(1, h - 1))
        gd.line([(0, y), (w, y)],
                fill=tuple(int(top[i] + (bot[i] - top[i]) * t) for i in range(3)))
    img = K.over(img, g, fm)

    # 顶部窄锐亮棱（1px，硬边）
    hi = P._band((w, h), fm, top=INSET, thick=1)
    img = K.over(img, K.solid((w, h), mix(face, LIGHT, hi_t)), hi)

    # 下沿压深
    lo = P._band((w, h), fm, bottom=h - 1 - INSET + 1 - 2,
                 thick=max(2, int(h * 0.16)))
    img = K.over(img, K.solid((w, h), mix(face, SHADOW, lo_t)),
                 lo.point(lambda v: int(v * 0.85)))

    # 底部反射亮棱（木面最下沿，1px）
    bl = P._band((w, h), fm, bottom=h - INSET, thick=1)
    img = K.over(img, K.solid((w, h), mix(face, LIGHT, bot_t)), bl)

    if accent:
        color, thick, gap = accent
        img = K.over(img, K.solid((w, h), color),
                     P._band((w, h), fm, bottom=h - 3 - gap, thick=thick))
    return img


# ------------------------------------------------------------------ 四套配色

PALETTES = [
    ("现状 · 橄榄铜", "H41 S34 V29", None),          # None = 用产线原样
    ("净石 · 暖灰", "H35 S14 V31",
     dict(face=(79, 74, 68), face_h=(101, 95, 87),
          sec=(64, 60, 55), tab=(46, 43, 39), tab_sel=(79, 74, 68),
          frame=(58, 54, 49))),
    ("冷灰岩", "H210 S10 V31",
     dict(face=(71, 75, 79), face_h=(93, 97, 102),
          sec=(57, 61, 64), tab=(41, 44, 47), tab_sel=(71, 75, 79),
          frame=(51, 55, 59))),
    ("铜锈", "H165 S18 V28",
     dict(face=(58, 71, 68), face_h=(78, 93, 89),
          sec=(47, 58, 55), tab=(33, 42, 40), tab_sel=(58, 71, 68),
          frame=(43, 53, 51))),
]


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
GOLD = K.GOLD

MARGIN = 30
GAP = 22
COLW = 118


def main():
    cols = []
    for nm, hsv, pal in PALETTES:
        if pal is None:                       # 现状：走产线原样
            imgs = [
                ("主按钮", P.btn_plate(110, 35, cut=13.0, field=K.BTN_FIELD,
                                       frame_c=(66, 58, 47), frame_lit=(108, 96, 80))),
                ("hover", P.btn_plate(110, 35, cut=13.0, field=K.BTN_FIELD_HOVER,
                                      frame_c=(66, 58, 47), frame_lit=(108, 96, 80))),
                ("次按钮", P.btn_plate(100, 35, cut=13.0, field=K.BTN_SEC_FIELD,
                                       frame_c=(48, 42, 35), frame_lit=(82, 73, 61))),
                ("页签 选中", P.btn_plate(105, 35, cut=13.0, field=K.BTN_FIELD,
                                          accent=(GOLD, 2, 4))),
            ]
            sw = [K.BTN_FIELD, (66, 58, 47), GOLD]
        else:
            imgs = [
                ("主按钮", make_btn(110, 35, pal["face"], pal["frame"])),
                ("hover", make_btn(110, 35, pal["face_h"], pal["frame"])),
                ("次按钮", make_btn(100, 35, pal["sec"], pal["frame"])),
                ("页签 选中", make_btn(105, 35, pal["tab_sel"], pal["frame"],
                                       accent=(GOLD, 2, 4))),
            ]
            sw = [pal["face"], pal["frame"], GOLD]
        cols.append((nm, hsv, imgs, sw))

    tag_h = 14
    sw_h = 18
    feel_h = 30
    row_h = tag_h + 35 + 10
    W_BOARD = MARGIN * 2 + COLW * 4 + GAP * 3
    body_h = row_h * 4 + sw_h + 12 + feel_h
    H_BOARD = MARGIN + 30 + 26 + 24 + body_h + 44 + MARGIN

    img = Image.new("RGB", (W_BOARD, H_BOARD), BG)
    d = ImageDraw.Draw(img)

    d.text((MARGIN, MARGIN), "按钮配色方向板 · 四套材质色", fill=TXT, font=F_TITLE)
    d.text((MARGIN, MARGIN + 26),
           "光源色全局统一为暖白 (238,230,214)，只有材质色在变 —— 四套共用一盏灯。"
           "后三套同为「凸＋硬边」质感。", fill=DIM, font=F_TINY)

    y = MARGIN + 30 + 26
    feels = ["（产线现样，作对照）",
             "洗过的石头：浊土退成净灰，金开始跳",
             "骑士甲与城堡的灰：冷、肃、有分量",
             "教堂铜顶的锈：有年头，跟金是对比色"]

    for i, (nm, hsv, imgs, sw) in enumerate(cols):
        x = MARGIN + i * (COLW + GAP)
        d.text((x, y), nm, fill=TXT, font=F_HEAD)
        d.text((x, y + 18), hsv, fill=DIM, font=F_TINY)
    y += 24 + 6

    for i, (nm, hsv, imgs, sw) in enumerate(cols):
        x = MARGIN + i * (COLW + GAP)
        yy = y
        for tag, im in imgs:
            d.text((x, yy), tag, fill=DIM, font=F_TINY)
            yy += tag_h
            img.paste(im, (x, yy), im)
            yy += 35 + 10
        # 色卡：面 / 框 / 金
        for j, c in enumerate(sw):
            bx = x + j * 34
            d.rectangle([bx, yy, bx + 30, yy + sw_h], fill=c)
            d.rectangle([bx, yy, bx + 30, yy + sw_h], outline=(70, 70, 80))
        yy += sw_h + 8
        for j, ln in enumerate(_wrap(d, feels[i], F_TINY, COLW)):
            d.text((x, yy + j * 14), ln, fill=(116, 116, 128), font=F_TINY)

    y += body_h + 16
    note = ("注意页签那张：金条在暖底上偏融、在冷灰与铜锈上会跳出来 —— "
            "换配色最先变的是「金还压不压得住」，其次才是木头还是石头。")
    for j, ln in enumerate(_wrap(d, note, F_TINY, W_BOARD - MARGIN * 2)):
        d.text((MARGIN, y + j * 14), ln, fill=DIM, font=F_TINY)

    path = os.path.join(OUT, "color_dir_board.png")
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
