# -*- coding: utf-8 -*-
"""配色第二版：底/字/金 一起调，并修掉亮棱白边

第一版的三个问题（out/study/tone/color_compare.txt 有数据）：
  ① 亮棱 mix(face, LIGHT, 0.86) 太亮 → 读成白描边
  ② 面上调亮后，金/面比从 4.6 掉到 4.07 → 金反而不跳了
  ③ 面变亮后，字色 (236,228,216) 对比度从 7.63 掉到 6.7

⇒ 本版规则：**底色一亮，字色与金同步「退一档」**（不是乘法，是重新定），
   让 面-框差 / 金-面比 / 字-面比 三个指标都落在阈值内。
   顺带把亮棱从 0.86 降到 0.62，底部反射光从 0.52 降到 0.40。
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

from PIL import Image, ImageDraw, ImageFont

import artkit as K
import parts as P

OUT = os.path.join(HERE, "out")
LIGHT = (238, 230, 214)
SHADOW = (9, 8, 7)
BEV = 4
INSET = BEV + 1


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def lum(c):
    return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]


def contrast(a, b):
    def rl(c):
        def f(v):
            v /= 255.0
            return v / 12.92 if v <= 0.03928 else ((v + 0.055) / 1.055) ** 2.4
        return 0.2126 * f(c[0]) + 0.7152 * f(c[1]) + 0.0722 * f(c[2])
    la, lb = rl(a), rl(b)
    if la < lb:
        la, lb = lb, la
    return (la + 0.05) / (lb + 0.05)


def make_btn(w, h, face, frame, *, cut=13.0, ss=6, accent=None,
             tex_delta=5, hi_t=0.62, bot_t=0.40, grad=-9,
             frame_lit_t=0.42, frame_dark_t=0.74):
    """凸＋硬边，亮棱与反射光都收过一档。"""
    frame_lit = mix(frame, LIGHT, frame_lit_t)
    frame_dark = mix(frame, SHADOW, frame_dark_t)

    img = P._frame(w, h, cut, ss, BEV, frame, lit_c=frame_lit, dark_c=frame_dark)
    fm = K.poly_mask((w, h), K.chamfer_pts(w, h, cut, INSET), ss=ss)
    img = K.over(img, K.textured((w, h), face, delta=tex_delta,
                                 fine=0.9, coarse=0.0, ss=ss), fm)

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

    hi = P._band((w, h), fm, top=INSET, thick=1)
    img = K.over(img, K.solid((w, h), mix(face, LIGHT, hi_t)), hi)
    lo = P._band((w, h), fm, bottom=h - 1 - INSET + 1 - 2,
                 thick=max(2, int(h * 0.16)))
    img = K.over(img, K.solid((w, h), mix(face, SHADOW, 0.80)),
                 lo.point(lambda v: int(v * 0.85)))
    bl = P._band((w, h), fm, bottom=h - INSET, thick=1)
    img = K.over(img, K.solid((w, h), mix(face, LIGHT, bot_t)), bl)

    if accent:
        color, thick, gap = accent
        img = K.over(img, K.solid((w, h), color),
                     P._band((w, h), fm, bottom=h - 3 - gap, thick=thick))
    return img


# 底色 / 框色 / 字色 / 金色 —— 四处一起定
SETS = [
    ("净石 · 暖灰", "洗过的石头",
     dict(face=(72, 67, 61), frame=(50, 46, 42), h=(94, 88, 81), p=(53, 49, 45),
          sec=(58, 54, 49), tab=(40, 37, 34), tabsel=(72, 67, 61),
          txt=(248, 242, 230), gold=(226, 176, 88))),
    ("冷灰岩", "骑士甲与城堡的灰",
     dict(face=(67, 71, 76), frame=(44, 48, 52), h=(89, 93, 98), p=(49, 52, 56),
          sec=(53, 57, 61), tab=(37, 40, 43), tabsel=(67, 71, 76),
          txt=(240, 244, 250), gold=(232, 182, 92))),
    ("铜锈", "教堂铜顶的锈",
     dict(face=(56, 70, 67), frame=(38, 48, 46), h=(76, 92, 88), p=(41, 51, 49),
          sec=(44, 55, 53), tab=(31, 39, 37), tabsel=(56, 70, 67),
          txt=(238, 246, 242), gold=(228, 178, 90))),
    ("现状 · 橄榄铜", "作对照",
     dict(face=K.BTN_FIELD, frame=(66, 58, 47), h=K.BTN_FIELD_HOVER,
          p=K.BTN_FIELD_PRESS, sec=K.BTN_SEC_FIELD, tab=K.BTN_TAB_FIELD,
          tabsel=K.BTN_FIELD, txt=(236, 228, 216), gold=K.GOLD)),
]


def _font(sz):
    try:
        return ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", sz)
    except Exception:
        return ImageFont.load_default()


F_TITLE = _font(20)
F_HEAD = _font(15)
F_TINY = _font(11)

BG = (23, 23, 27)
TXT = (226, 226, 234)
DIM = (140, 140, 154)

MARGIN = 30
GAP = 22
COLW = 124


def main():
    cols = []
    for nm, feel, s in SETS:
        btns = [
            ("主按钮", make_btn(110, 35, s["face"], s["frame"])),
            ("hover", make_btn(110, 35, s["h"], s["frame"])),
            ("pressed", make_btn(110, 35, s["p"], s["frame"])),
            ("次按钮", make_btn(100, 35, s["sec"], s["frame"])),
            ("页签 选中", make_btn(105, 35, s["tabsel"], s["frame"],
                                accent=(s["gold"], 2, 4))),
        ]
        # 白字压在面上，看可读性
        demo = make_btn(110, 35, s["face"], s["frame"])
        dd = ImageDraw.Draw(demo)
        dd.text((26, 9), "确 定", fill=s["txt"], font=_font(16))
        btns.insert(0, ("白字压面", demo))

        m = dict(
            gap=lum(s["face"]) - lum(s["frame"]),
            gold_ratio=contrast(s["gold"], s["face"]),
            txt_ratio=contrast(s["txt"], s["face"]),
            hover=lum(s["h"]) - lum(s["face"]),
            press=lum(s["face"]) - lum(s["p"]),
            lev=lum(s["face"]) - lum(s["sec"]),
        )
        cols.append((nm, feel, btns, s, m))

    rowh = 16 + 35 + 8
    W_BOARD = MARGIN * 2 + COLW * 4 + GAP * 3
    # 每列内容：6 行控件 + 色卡(22) + 脚注(2 行 × 14) + 24 缓冲
    COL_H = rowh * 6 + 22 + 28 + 24
    TABLE_H = 18 + 15 * 6 + 16
    H_BOARD = (MARGIN + 30 + 26 + 26 + COL_H + TABLE_H + 44 + MARGIN)

    img = Image.new("RGB", (W_BOARD, H_BOARD), BG)
    d = ImageDraw.Draw(img)

    d.text((MARGIN, MARGIN), "按钮配色 · 第二版（底／字／金 一起调）", fill=TXT, font=F_TITLE)
    d.text((MARGIN, MARGIN + 26),
           "亮棱从 0.86 收到 0.62、反射光 0.52→0.40；底色一亮，字色与金同步退一档，"
           "让三个指标都回到阈值内。", fill=DIM, font=F_TINY)

    y = MARGIN + 30 + 26
    for i, (nm, feel, btns, s, m) in enumerate(cols):
        x = MARGIN + i * (COLW + GAP)
        d.text((x, y), nm, fill=TXT, font=F_HEAD)
    y += 22

    for i, (nm, feel, btns, s, m) in enumerate(cols):
        x = MARGIN + i * (COLW + GAP)
        yy = y
        for tag, im in btns:
            d.text((x, yy), tag, fill=DIM, font=F_TINY)
            yy += 16
            img.paste(im, (x, yy), im)
            yy += 35 + 8
        # 色卡：面/框/字/金
        for j, c in enumerate([s["face"], s["frame"], s["txt"], s["gold"]]):
            bx = x + j * 30
            d.rectangle([bx, yy, bx + 26, yy + 16], fill=c)
            d.rectangle([bx, yy, bx + 26, yy + 16], outline=(70, 70, 80))
        yy += 22
        for j, ln in enumerate(_wrap(d, feel, F_TINY, COLW)):
            d.text((x, yy + j * 14), ln, fill=(116, 116, 128), font=F_TINY)

    y += COL_H
    # 指标表
    heads = ["面-框", "金/面", "字/面", "hover", "pressed", "主-次"]
    keys = ["gap", "gold_ratio", "txt_ratio", "hover", "press", "lev"]
    rh = 15
    d.text((MARGIN, y), "指标（阈值：面-框≥8，金/面≥3，字/面≥4.5，hover≥12，press≥10，主-次≥6）",
           fill=(150, 150, 164), font=F_TINY)
    y += 18
    for r, (hd, k) in enumerate(zip(heads, keys)):
        d.text((MARGIN, y + r * rh), hd, fill=DIM, font=F_TINY)
        for i, (nm, feel, btns, s, m) in enumerate(cols):
            x = MARGIN + 62 + i * (COLW + GAP)
            v = m[k]
            ok = _ok(k, v)
            d.text((x, y + r * rh), "%.2f" % v if k != "gap" and k != "hover"
                   and k != "press" and k != "lev" else "%+.1f" % v,
                   fill=(130, 210, 150) if ok else (235, 150, 120), font=F_TINY)
    y += rh * len(heads) + 12

    note = ("两处仍由你定：① 面-框差现状只有 7.3，是「发闷」的直接成因，"
            "后三套都到了 17~20，代价是框更黑更硬。② 冷灰岩的金/面比最高 —— "
            "冷底最托金，但整块会偏「制式」，离骑砍的浊土最远。")
    for j, ln in enumerate(_wrap(d, note, F_TINY, W_BOARD - MARGIN * 2)):
        d.text((MARGIN, y + j * 14), ln, fill=DIM, font=F_TINY)

    path = os.path.join(OUT, "color_dir_board2.png")
    img.save(path)
    print("saved", path, img.size)
    for nm, feel, btns, s, m in cols:
        print("  %-12s 面-框 %+5.1f  金/面 %.2f  字/面 %.2f  hover %+5.1f  press %+5.1f  主-次 %+5.1f"
              % (nm, m["gap"], m["gold_ratio"], m["txt_ratio"],
                 m["hover"], m["press"], m["lev"]))


def _ok(k, v):
    return {"gap": v >= 8, "gold_ratio": v >= 3.0, "txt_ratio": v >= 4.5,
            "hover": v >= 12, "press": v >= 10, "lev": v >= 6}[k]


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
