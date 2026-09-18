#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""AWAKE UI · 按钮族「统一风格」收敛案 v1（提案，不动交付目录）

## 解决什么

《UI 控件契约》§4 记的问题：四个按钮族的**面**各走一路 ——
主按钮橄榄、次按钮锈褐、页签冷灰、关闭键红圆 ⇒ 单看都能用，摆一排读成**四套件**。

## 收敛口径：**一块料，三档火**

1. **一块料** —— 全族同一个切角与端头曲线（cut=13）、同一套框色与受光方向、
   同一金线线重（2px）、同一材质颗粒。框**不再按族压暗**。
2. **三档火** —— 差异只允许出现在「面」的**明度**上，**色相比不变**：
   重 74（主动作 / 选中页签）、常 62（次动作）、静 52（未选中页签）。
   读起来＝同一块石头在面板里凸得深浅不同，而不是三种材料。
3. **关闭键保留圆印** —— 给 §4 质疑的"红"一个语义出处：**火漆印**（AWAKE 是写信与对话）。
   但石圈收到与牌面框同档、金叉同线重、红只留在最里层且面积收小。
4. **选中页签** —— 面升到"重"档 ＋ 底部 2px 金线 ＋ 其下 1px 压深线
   （与面板金规线同一语汇），不再只靠一根亮条。

用法：
    python make_style_convergence.py            # 出提案 sprite + 对照板
产出（都不碰 GUI/SpriteParts，交付仍属美术线）：
    out/style-convergence/proposed/<名>.png
    out/style-convergence/board_style_v1.png
    out/style-convergence/board_real_size.png
"""
import os
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import artkit as K          # noqa: E402
import parts as P           # noqa: E402
from artkit import (BEVEL_DARK, CONTOUR, GOLD, GOLD_DIM, GOLD_UNDER)  # noqa: E402

REPO = os.path.normpath(os.path.join(HERE, "..", ".."))
DELIVER = os.path.join(REPO, "GUI", "SpriteParts", "ui_awake_button")
OUT = os.path.join(HERE, "out", "style-convergence")
PROP = os.path.join(OUT, "proposed")

# ============================================================ 统一参数表
CUT = 13.0                      # 端头收束段长度（2026-09-14 定案，全族共用）
BEV = 4                         # 内缩 5px；tip 13 > 5 有余量

FRAME_C = (58, 51, 42)          # 框底：全族同一个
FRAME_LIT = (100, 89, 74)       # 框受光：全族同一个（光一律打上/左）

# 「三档火」：一块料，从"刚出炉的暖金"到"凉下来的石灰"，只沿一个方向走。
#   · 明度单调下降（重 → 常 → 静）
#   · 饱和度同步下降（暖 → 冷）
# 这样四族是同一块料的不同火候，不是四种材料。
HOT = (1.000, 0.868, 0.605)     # 暖端（金调）
COLD = (1.000, 0.920, 0.780)    # 冷端（石调）


def _v(r, t=1.0):
    """取一个明度 r、火候 t（1=最暖，0=最冷）的面色。"""
    ratio = tuple(COLD[k] + (HOT[k] - COLD[k]) * t for k in range(3))
    return tuple(int(round(r * k)) for k in ratio)


LV_HEAVY = (76, 1.00)           # 重 → 主按钮 / 选中页签
LV_NORM = (64, 0.50)            # 常 → 次按钮
LV_QUIET = (50, 0.00)           # 静 → 未选中页签

# 逐状态：只在同档内升降明度，火候不动
FIELDS = {
    "primary":   {"default": (76, 1.00), "hover": (94, 1.00),
                  "pressed": (58, 1.00), "disabled": (64, 0.35)},
    "secondary": {"default": (64, 0.50), "hover": (79, 0.50), "pressed": (51, 0.50)},
    "tab":       {"default": (50, 0.00), "hover": (63, 0.10),
                  "pressed": (42, 0.00), "selected": (76, 1.00)},
}
DISABLED_DESAT = 0.10           # 禁用：往中灰拉的比例


def _disabled(c):
    g = sum(c) / 3.0
    return tuple(int(round(v + (g - v) * DISABLED_DESAT)) for v in c)


# ============================================================ 牌（plates）

def plate(w, h, field, *, disabled=False, selected=False):
    """统一牌面：切角端头 + 同框 + 同受光；选中另加金规线。"""
    img = P.btn_plate(w, h, cut=CUT, field=field, bev=BEV,
                      frame_c=FRAME_C, frame_lit=FRAME_LIT, disabled=disabled)
    if selected:
        fm = K.poly_mask((w, h), K.chamfer_pts(w, h, CUT, BEV + 1), ss=6)
        # 2px 金线 + 其下 1px 压深线 —— 与碑面的金规线同一语汇
        img = K.over(img, K.solid((w, h), GOLD),
                     P._band((w, h), fm, bottom=h - 3 - 5, thick=2))
        img = K.over(img, K.solid((w, h), GOLD_UNDER),
                     P._band((w, h), fm, bottom=h - 3 - 3, thick=1))
    return img


def mk_plates(w, h, family):
    out = {}
    for state, (r, t) in FIELDS[family].items():
        field = _v(r, t)
        dis = state == "disabled"
        out[state] = plate(w, h, _disabled(field) if dis else field,
                           disabled=dis, selected=state == "selected")
    return out


# ============================================================ 印（seal）

WAX = {"default": (56, 27, 19), "hover": (98, 39, 24), "pressed": (62, 29, 19)}
RIM_LIT = (126, 114, 95)        # 石圈受光（与框同一个偏暖的灰）
RIM_MID = (58, 51, 42)          # ＝ FRAME_C


def seal(size, state="default"):
    """火漆印：石圈（同牌面框料）+ 内里火漆 + 金叉（同金线）。"""
    s = size
    c = K.Canvas(s, s, 8)
    c.ellipse([0, 0, s - 1, s - 1], CONTOUR)
    c.ellipse([1, 1, s - 2, s - 2], RIM_LIT)
    c.ellipse([3, 3, s - 4, s - 4], RIM_MID)
    c.ellipse([4, 4, s - 5, s - 5], BEVEL_DARK)
    img = c.done()

    inner_c = WAX[state]
    inner = K.disc_mask((s, s), 5, ss=8)
    img = K.over(img, K.textured((s, s), inner_c, delta=10, fine=0.9, coarse=0.0, ss=8), inner)

    hl = K.edge_mask((s, s), K.disc_ring_mask((s, s), 5, 8, ss=8), "top", int(s * 0.5))
    img = K.over(img, K.solid((s, s), tuple(min(255, v + 34) for v in inner_c)),
                 hl.point(lambda v: int(v * 0.5)))
    dk = K.edge_mask((s, s), K.disc_ring_mask((s, s), 5, 8, ss=8), "bottom", int(s * 0.35))
    img = K.over(img, K.solid((s, s), (12, 6, 4)), dk.point(lambda v: int(v * 0.5)))

    xm = ImageChops.multiply(P._cross_mask((s, s), int(s * 0.30), 2, ss=8),
                             K.disc_mask((s, s), 6, ss=8))
    img = K.over(img, K.solid((s, s), GOLD), xm)
    return img


# ============================================================ 提案清单
PROPOSAL = [
    ("btn_close_40", (40, 40), lambda: seal(40, "default")),
    ("btn_close_40_hover", (40, 40), lambda: seal(40, "hover")),
    ("btn_close_40_pressed", (40, 40), lambda: seal(40, "pressed")),
]


def build_proposal():
    os.makedirs(PROP, exist_ok=True)
    made = []
    for name, size, f in PROPOSAL:
        im = f()
        assert im.size == size, (name, im.size, size)
        im.save(os.path.join(PROP, name + ".png"))
        made.append(name)
    for name, size, fam in [("btn_primary_110", (110, 35), "primary"),
                            ("btn_secondary_100", (100, 35), "secondary"),
                            ("btn_tab_105", (105, 35), "tab")]:
        for state, im in mk_plates(size[0], size[1], fam).items():
            nm = name if state == "default" else "%s_%s" % (name, state)
            im.save(os.path.join(PROP, nm + ".png"))
            made.append(nm)
    return made


# ============================================================ 对照板

def _font(sz):
    try:
        return ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", sz)
    except Exception:  # noqa: BLE001
        return ImageFont.load_default()


FIELD = (26, 21, 18)
PANEL = (40, 34, 29)
INK = (238, 230, 218)
DIM = (150, 140, 128)
GOLD_T = (217, 169, 83)

FAM = [
    ("关闭键 40x40", ["btn_close_40", "btn_close_40_hover", "btn_close_40_pressed"]),
    ("主按钮 110x35", ["btn_primary_110", "btn_primary_110_hover",
                       "btn_primary_110_pressed", "btn_primary_110_disabled"]),
    ("次按钮 100x35", ["btn_secondary_100", "btn_secondary_100_hover",
                       "btn_secondary_100_pressed"]),
    ("页签 105x35", ["btn_tab_105", "btn_tab_105_hover",
                     "btn_tab_105_pressed", "btn_tab_105_selected"]),
]


def _load(root, n):
    return Image.open(os.path.join(root, n + ".png")).convert("RGBA")


def board_style(z=4):
    """现状（上块）| 统一案（下块），纵向堆叠、同倍率、同底。"""
    PADX, GAP, LBL = 34, 22, 236
    maxw = 0
    for _, names in FAM:
        w = sum(_load(PROP, n).width * z + GAP for n in names) - GAP
        maxw = max(maxw, w)
    W = PADX + LBL + maxw + PADX
    row_h = [max(_load(PROP, n).height for n in names) * z + 58 for _, names in FAM]
    H = 150 + 2 * (46 + sum(row_h)) + 24
    sheet = Image.new("RGB", (W, H), FIELD)
    d = ImageDraw.Draw(sheet)

    d.text((PADX, 24), "按钮族 · 统一风格案 v1  ·  一块料，三档火", font=_font(30), fill=INK)
    d.text((PADX, 66), "同一套框与受光 + 同一端头曲线 + 同一线重；差异只落在「面」的明度与火候"
                       "（重 76 / 常 64 / 静 50，越重越暖）", font=_font(16), fill=DIM)

    y = 112
    for tag, root, col in (("现状 · 交付 sprite", DELIVER, INK),
                           ("统一案 · 提案（未进交付目录）", PROP, GOLD_T)):
        d.text((PADX, y), tag, font=_font(20), fill=col)
        d.line([(PADX, y + 30), (W - PADX, y + 30)], fill=(72, 62, 52))
        y += 46
        for (label, names), rh in zip(FAM, row_h):
            d.text((PADX, y + 10), label, font=_font(16), fill=DIM)
            x = PADX + LBL
            for n in names:
                im = _load(root, n)
                show = im.resize((im.width * z, im.height * z), Image.LANCZOS)
                bg = Image.new("RGB", show.size, PANEL)
                bg.paste(show, (0, 0), show)
                sheet.paste(bg, (x, y))
                d.rectangle([x - 1, y - 1, x + show.width, y + show.height],
                            outline=(78, 68, 58))
                x += show.width + GAP
            y += rh
        y += 10
    p = os.path.join(OUT, "board_style_v1.png")
    sheet.save(p)
    return p


def board_real():
    """1x 真尺寸 · 摆在真面板的底上：关闭键 / 页签排 / 按钮对。上＝现状，下＝统一案。"""
    PADX, LBL = 34, 190
    panel = Image.open(os.path.join(REPO, "GUI", "SpriteParts", "ui_awake_frame",
                                    "panel_main_960.png")).convert("RGBA")
    VW, VH = 960, 250
    crop = panel.crop((0, 120, VW, 120 + VH))

    def stage(root):
        c = crop.copy()
        # 关闭键（右上）
        cl = _load(root, "btn_close_40")
        c.paste(cl, (VW - 70, 26), cl)
        # 页签 ×3，间距 5（Prefab 真值）
        x = 40
        for s in ("", "", "_selected"):
            im = _load(root, "btn_tab_105" + s)
            c.paste(im, (x, 92), im)
            x += im.width + 5
        # 按钮对：主 110 + 次 100 + 主 110，间距 10
        x = 40
        for n in ("btn_primary_110", "btn_secondary_100", "btn_primary_110_hover"):
            im = _load(root, n)
            c.paste(im, (x, 165), im)
            x += im.width + 10
        return c

    a, b = stage(DELIVER), stage(PROP)
    W = PADX + LBL + VW + PADX
    H = 150 + 2 * (VH + 52) + 20
    sheet = Image.new("RGB", (W, H), FIELD)
    d = ImageDraw.Draw(sheet)
    d.text((PADX, 24), "1x 真尺寸 · 摆在真面板底上", font=_font(28), fill=INK)
    d.text((PADX, 64), "同一排里放关闭键 / 三个页签 / 一主一次一悬停。左标是 Prefab 里的真间距（页签 5、按钮 10）。",
           font=_font(15), fill=DIM)
    y = 110
    for tag, im, col in (("现状", a, INK), ("统一案", b, GOLD_T)):
        d.text((PADX, y + VH // 2 - 10), tag, font=_font(20), fill=col)
        sheet.paste(im.convert("RGB"), (PADX + LBL, y))
        d.rectangle([PADX + LBL - 1, y - 1, PADX + LBL + VW, y + VH], outline=(78, 68, 58))
        y += VH + 52
    p = os.path.join(OUT, "board_real_size.png")
    sheet.save(p)
    return p


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    names = build_proposal()
    print("提案 sprite: %d 个 → %s" % (len(names), PROP))
    print(board_style())
    print(board_real())
