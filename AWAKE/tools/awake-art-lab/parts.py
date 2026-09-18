#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""AWAKE UI 各部件的画法。

母题＝**碑刻 / 铭牌**：一块碑，不同的人读到不同层次。
  · 碑面（面板底）＝切角石框 + 暖黑石面 + 边内一道 2px 金规线
  · 铭牌（按钮）  ＝切角石框 + 凹进内衬（逐状态）
  · 蜡印（关闭键）＝深红棕圆章 + 石圈 + 金叉
  · 鎏金端饰横牌（顶栏带）＝橄榄卡其长条 + 底缘金线
四禁忌（不许出现）：拟物（木纹/皮革/金属反光）、科技感、奇幻魔法、大面积金。

**受光规则（这条是"像不像凿出来的料"的关键）**
  凸起 → 光打在上/左；凹进 → 光打在下/右。反了立刻变贴纸。
"""
import artkit as K
from artkit import (BEVEL_DARK, BEVEL_LIGHT, BTN_FIELD, CONTOUR, GOLD, GOLD_BRIGHT,
                    GOLD_DIM, GOLD_UNDER, INSET_FIELD, SEAL_INNER, SEAL_INNER_HOVER,
                    SEAL_INNER_PRESS, SEAL_RIM, SEAL_RIM_LIGHT, SLAB, STONE_FIELD)
from PIL import Image, ImageChops, ImageDraw


def _lighten(c, d):
    return tuple(min(255, v + d) for v in c)


# ---------------------------------------------------------------- 内部工具

def _band(size, fmask, *, top=None, bottom=None, thick=2):
    """fmask 内的横带（距顶/距底）。"""
    m = Image.new("L", size, 0)
    d = ImageDraw.Draw(m)
    if top is not None:
        d.rectangle([0, top, size[0], top + thick], fill=255)
    if bottom is not None:
        d.rectangle([0, bottom - thick, size[0], bottom], fill=255)
    return ImageChops.multiply(fmask, m)


def _frame(w, h, cut, ss, bev, frame_c, lit_c, dark_c,
           lit_sides=("top", "left")):
    """切角石框：轮廓 + 框带（有方向受光）+ 内侧分隔线。"""
    c = K.Canvas(w, h, ss)
    c.rings(w, h, cut, [(0, CONTOUR), (1, frame_c)])
    img = c.done()

    band = K.ring_mask((w, h), K.chamfer_pts(w, h, cut, 1),
                       K.chamfer_pts(w, h, cut, bev), ss=ss)
    inv = tuple(s for s in ("top", "left", "bottom", "right") if s not in lit_sides)
    lit = K.edge_mask((w, h), band, lit_sides, bev + 1)
    drk = K.edge_mask((w, h), band, inv, bev + 1)
    img = K.over(img, K.solid((w, h), lit_c), lit.point(lambda v: int(v * 0.9)))
    img = K.over(img, K.solid((w, h), dark_c), drk.point(lambda v: int(v * 0.9)))

    sep = K.ring_mask((w, h), K.chamfer_pts(w, h, cut, bev),
                      K.chamfer_pts(w, h, cut, bev + 1), ss=ss)
    img = K.over(img, K.solid((w, h), BEVEL_DARK), sep)
    return img


# ---------------------------------------------------------------- 碑面（面板底）

def plaque(w, h, *, cut, base=STONE_FIELD, ss=2, rule_at=None, rule_w=2,
           texture=(15, 1.1, 34.0), bev=7,
           frame_c=(50, 44, 37), lit_c=(90, 80, 67), dark_c=(26, 22, 19)):
    img = _frame(w, h, cut, ss, bev, frame_c, lit_c, dark_c)
    field = K.poly_mask((w, h), K.chamfer_pts(w, h, cut, bev + 1), ss=ss)
    delta, fine, coarse = texture
    img = K.over(img, K.textured((w, h), base, delta=delta, fine=fine,
                                 coarse=coarse, ss=ss), field)

    if rule_at:
        m = K.ring_mask((w, h), K.chamfer_pts(w, h, cut, rule_at),
                        K.chamfer_pts(w, h, cut, rule_at + rule_w), ss=ss)
        img = K.over(img, K.solid((w, h), GOLD), m)
        ms = K.ring_mask((w, h), K.chamfer_pts(w, h, cut, rule_at + rule_w),
                         K.chamfer_pts(w, h, cut, rule_at + rule_w + 1), ss=ss)
        img = K.over(img, K.solid((w, h), GOLD_UNDER), ms)
    return img


# ---------------------------------------------------------------- 凹槽（输入区 / 状态条）

def recess(w, h, *, cut, base=INSET_FIELD, ss=4, accent=None, bev=4,
           texture=(9, 1.0, 0.0), frame_c=(44, 38, 32)):
    """凹进去的料：受光打在下/右（与凸起相反），读起来是"凿掉一层"。"""
    img = _frame(w, h, cut, ss, bev, frame_c, lit_c=(88, 78, 66),
                 dark_c=(22, 19, 16), lit_sides=("bottom", "right"))
    field = K.poly_mask((w, h), K.chamfer_pts(w, h, cut, bev + 1), ss=ss)
    delta, fine, coarse = texture
    img = K.over(img, K.textured((w, h), base, delta=delta, fine=fine,
                                 coarse=coarse, ss=ss), field)

    # 内里上沿压深：凹口在暗处
    sh = _band((w, h), field, top=bev + 1, thick=max(2, int(h * 0.16)))
    img = K.over(img, K.solid((w, h), (8, 6, 5)), sh.point(lambda v: int(v * 0.6)))

    if accent:
        color, thick, gap = accent
        img = K.over(img, K.solid((w, h), color),
                     _band((w, h), field, bottom=h - 3 - gap, thick=thick))
    return img


# ---------------------------------------------------------------- 铭牌（按钮）

def btn_plate(w, h, *, cut, field, ss=6, disabled=False, accent=None, bev=4,
              frame_c=(54, 47, 39), frame_lit=(92, 82, 68)):
    img = _frame(w, h, cut, ss, bev, frame_c,
                 lit_c=frame_lit if not disabled else (74, 71, 68),
                 dark_c=(28, 24, 21) if not disabled else (34, 33, 31))
    fm = K.poly_mask((w, h), K.chamfer_pts(w, h, cut, bev + 1), ss=ss)
    img = K.over(img, K.textured((w, h), field, delta=9 if not disabled else 3,
                                 fine=0.9, coarse=0.0, ss=ss), fm)

    # 内衬上沿一道细高光 + 下沿一道压深：凹进去的料有厚薄
    hi = _band((w, h), fm, top=bev + 1, thick=1)
    img = K.over(img, K.solid((w, h), _lighten(field, 34 if not disabled else 14)),
                 hi.point(lambda v: int(v * 0.75)))
    lo = _band((w, h), fm, bottom=h - 3 - bev, thick=max(2, int(h * 0.16)))
    img = K.over(img, K.solid((w, h), (10, 8, 7)), lo.point(lambda v: int(v * 0.45)))

    if accent:
        color, thick, gap = accent
        img = K.over(img, K.solid((w, h), color),
                     _band((w, h), fm, bottom=h - 3 - gap, thick=thick))
    return img


SEAL_STATES = {
    #           石圈亮              石圈         内里                 金叉
    "default": (SEAL_RIM_LIGHT, SEAL_RIM, SEAL_INNER, GOLD_DIM),
    "hover":   ((176, 165, 146), (78, 63, 57), SEAL_INNER_HOVER, GOLD_BRIGHT),
    "pressed": ((112, 105, 93), (42, 34, 30), SEAL_INNER_PRESS, GOLD_DIM),
}


def _cross_mask(size, inset, width, ss=1):
    m = Image.new("L", (size[0] * ss, size[1] * ss), 0)
    d = ImageDraw.Draw(m)
    a, b = inset * ss, (size[0] - 1 - inset) * ss
    d.line([(a, a), (b, b)], fill=255, width=int(round(width * ss)))
    d.line([(b, a), (a, b)], fill=255, width=int(round(width * ss)))
    return m.resize(size, Image.LANCZOS) if ss > 1 else m


# ---------------------------------------------------------------- 蜡印（关闭键）

def seal(size, state="default", ss=8):
    """蜡印圆章：石圈 + 暗红棕内里 + 金叉。"""
    rim_light, rim, inner_c, xc = SEAL_STATES[state]
    s = size
    c = K.Canvas(s, s, ss)
    c.ellipse([0, 0, s - 1, s - 1], CONTOUR)
    c.ellipse([1, 1, s - 2, s - 2], rim_light)
    c.ellipse([3, 3, s - 4, s - 4], rim)
    c.ellipse([4, 4, s - 5, s - 5], BEVEL_DARK)
    img = c.done()

    inner = K.disc_mask((s, s), 5, ss=ss)
    img = K.over(img, K.textured((s, s), inner_c, delta=10, fine=0.9,
                                 coarse=0.0, ss=ss), inner)

    # 内里上缘受光（蜡面不平）+ 下缘压深
    hl = K.edge_mask((s, s), K.disc_ring_mask((s, s), 5, 8, ss=ss), "top", int(s * 0.5))
    img = K.over(img, K.solid((s, s), _lighten(inner_c, 40)),
                 hl.point(lambda v: int(v * 0.55)))
    dk = K.edge_mask((s, s), K.disc_ring_mask((s, s), 5, 8, ss=ss), "bottom", int(s * 0.35))
    img = K.over(img, K.solid((s, s), (12, 6, 4)), dk.point(lambda v: int(v * 0.5)))

    xm = ImageChops.multiply(_cross_mask((s, s), int(s * 0.29), max(2, s // 12), ss=ss),
                             K.disc_mask((s, s), 6, ss=ss))
    img = K.over(img, K.solid((s, s), xc), xm)
    return img


# ---------------------------------------------------------------- 横牌（顶栏带）

def header_band(w, h, *, cut, ss=4, base=SLAB, rule_gap=9, rule_w=2, bev=5):
    """鎏金端饰横牌：切角长条 + 底缘金线（上下不拉伸，顶底不做切角）。"""
    img = _frame(w, h, cut, ss, bev, frame_c=(52, 48, 36),
                 lit_c=(96, 90, 70), dark_c=(28, 25, 20))
    field = K.poly_mask((w, h), K.chamfer_pts(w, h, cut, bev + 1), ss=ss)
    img = K.over(img, K.textured((w, h), base, delta=10, fine=1.3,
                                 coarse=32.0, ss=ss), field)

    gold = _band((w, h), field, bottom=h - 3 - rule_gap, thick=rule_w)
    img = K.over(img, K.solid((w, h), GOLD), gold)
    return img


# ---------------------------------------------------------------- 金分隔线

def divider(w, h, *, ss=4, ends=10):
    """整条拉伸的金线：1px 暗轮廓 + 金芯；两端压暗，拉伸时两端不炸。"""
    c = K.Canvas(w, h, ss)
    c.poly(K.rect_pts(w, h, 0), CONTOUR)
    c.poly(K.rect_pts(w, h, 1), GOLD)
    img = c.done()
    if ends > 0:
        core = K.poly_mask((w, h), K.rect_pts(w, h, 1))
        for x0, x1 in ((0, ends), (w - ends, w)):
            m = Image.new("L", (w, h), 0)
            ImageDraw.Draw(m).rectangle([x0, 0, x1, h], fill=255)
            img = K.over(img, K.solid((w, h), GOLD_DIM), ImageChops.multiply(m, core))
    return img
