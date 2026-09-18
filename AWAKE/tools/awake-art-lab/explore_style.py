#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""风格探索：同一部件 × 三个方向，看哪条路跟原版真正拉得开。

背景（Max 2026-09-13 批）：现有皮肤＝换皮——把原版最显眼的三个符号
（暖黑石 / 金色细线 / 切角框）整份继承，只加了石纹颗粒。区分度不足。
根因：artkit 色板注释写着"实测原版"，等于把原版当基线。
本脚本探索三个**动语言层**的方向（换色域 / 换结构 / 换体量）：

  A 阴刻  —— 金色退场。界限不靠亮线，靠"凿沟 + 沟下缘微高光"。冷青灰石。
  B 碑额  —— 面板起头（额）收脚（座），形制本身说话。砂黄石 + 暗刻纹带。
  C 厚凿  —— 厚料：右侧/下侧露出可见厚度面，表面粗凿。中灰石。

只出到 out/explore/，**不动交付目录**。
"""
import os
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import artkit as K  # noqa: E402

OUT = os.path.join(HERE, "out", "explore")

# ---------------------------------------------------------------- 色板

# A 冷石阴刻：色相从暖黑整体转到冷青灰；无金
A_P = dict(base=(43, 49, 54), trench=(17, 21, 25), trench_lit=(84, 93, 100),
           edge=(9, 11, 13), field_bev=(54, 61, 67), field_dark=(22, 26, 30))
# B 碑额砂石：暖但**亮**（明度关系跟原版的"暗底"拉开）；装饰暗刻不带金
B_P = dict(base=(64, 57, 45), edge=(26, 22, 17), cap=(78, 70, 55),
           cap_lit=(112, 102, 82), cap_dark=(38, 33, 26), foot=(46, 41, 32))
# C 厚凿铁灰：中灰 + 实体厚度面
C_P = dict(base=(56, 58, 56), edge=(20, 21, 20), side=(33, 34, 33),
           side_lit=(78, 80, 78), face_lit=(84, 86, 83))


def _cham(w, h, cut, inset=0):
    return K.chamfer_pts(w, h, cut, inset)


# ---------------------------------------------------------------- A 阴刻

def panel_A(w, h, cut=14, rule=20, ss=2):
    """一圈**凿沟**代替金规线：沟体压深、沟下/右缘一道微高光。"""
    c = K.Canvas(w, h, ss)
    c.rings(w, h, cut, [(0, A_P["edge"]), (2, A_P["field_bev"])])
    img = c.done()

    fm = K.poly_mask((w, h), _cham(w, h, cut, 2), ss=ss)
    img = K.over(img, K.textured((w, h), A_P["base"], delta=12, fine=1.2,
                                 coarse=30.0, ss=ss), fm)

    # 凿沟（比底面更深）
    outer = _cham(w, h, cut, rule)
    inner = _cham(w, h, cut, rule + 3)
    img = K.over(img, K.solid((w, h), A_P["trench"]),
                 K.ring_mask((w, h), outer, inner, ss=ss))
    # 沟的下缘 + 右缘：凹进的受光（光在下/右）
    lit = K.ring_mask((w, h), inner, _cham(w, h, cut, rule + 4), ss=ss)
    lit = K.edge_mask((w, h), lit, ("bottom", "right"), 3)
    img = K.over(img, K.solid((w, h), A_P["trench_lit"]),
                 lit.point(lambda v: int(v * 0.7)))
    # 沟的上缘 + 左缘：落影
    sh = K.ring_mask((w, h), outer, _cham(w, h, cut, rule + 1), ss=ss)
    sh = K.edge_mask((w, h), sh, ("top", "left"), 3)
    img = K.over(img, K.solid((w, h), (6, 8, 10)), sh.point(lambda v: int(v * 0.55)))
    return img


def btn_A(w, h, cut=8, ss=6, pressed=False):
    c = K.Canvas(w, h, ss)
    c.rings(w, h, cut, [(0, A_P["edge"]), (2, A_P["field_bev"])])
    img = c.done()
    fm = K.poly_mask((w, h), _cham(w, h, cut, 2), ss=ss)
    base = (38, 44, 48) if not pressed else (30, 35, 39)
    img = K.over(img, K.textured((w, h), base, delta=8, fine=0.9, ss=ss), fm)
    out = _cham(w, h, cut, 4)
    inn = _cham(w, h, cut, 6)
    img = K.over(img, K.solid((w, h), A_P["trench"]),
                 K.ring_mask((w, h), out, inn, ss=ss))
    return img


# ---------------------------------------------------------------- B 碑额

def panel_B(w, h, cut=10, ss=2, cap_h=34, foot_h=18, inset=18):
    """顶起『额』、底收『座』；四周一圈暗刻回纹带。"""
    c = K.Canvas(w, h, ss)
    c.rings(w, h, cut, [(0, B_P["edge"]), (2, B_P["cap"])])
    img = c.done()
    fm = K.poly_mask((w, h), _cham(w, h, cut, 2), ss=ss)
    img = K.over(img, K.textured((w, h), B_P["base"], delta=13, fine=1.15,
                                 coarse=34.0, ss=ss), fm)

    # 额：顶部横带，比正面亮 → 上缘受光、下缘压深
    capm = Image.new("L", (w, h), 0)
    ImageDraw.Draw(capm).rectangle([2, 2, w - 3, 2 + cap_h], fill=255)
    capm = ImageChops.multiply(capm, fm)
    img = K.over(img, K.textured((w, h), B_P["cap"], delta=11, fine=1.2,
                                 coarse=0.0, ss=ss), capm)
    top = K.edge_mask((w, h), capm, "top", 3)
    img = K.over(img, K.solid((w, h), B_P["cap_lit"]),
                 top.point(lambda v: int(v * 0.8)))
    bot = K.edge_mask((w, h), capm, "bottom", 3)
    img = K.over(img, K.solid((w, h), B_P["cap_dark"]),
                 bot.point(lambda v: int(v * 0.85)))

    # 座：底部横带，比正面暗 → 上缘一道高光（台阶转折）
    ftm = Image.new("L", (w, h), 0)
    ImageDraw.Draw(ftm).rectangle([2, h - 3 - foot_h, w - 3, h - 3], fill=255)
    ftm = ImageChops.multiply(ftm, fm)
    img = K.over(img, K.textured((w, h), B_P["foot"], delta=9, fine=1.2, ss=ss), ftm)
    topf = K.edge_mask((w, h), ftm, "top", 2)
    img = K.over(img, K.solid((w, h), B_P["cap_lit"]),
                 topf.point(lambda v: int(v * 0.62)))

    # 暗刻回纹带：额下沿 + 座下沿各一条断续短刻
    def tickrun(y, n, tw=10, gap=8, thick=2, x0=None):
        m = Image.new("L", (w, h), 0)
        d = ImageDraw.Draw(m)
        x0 = inset if x0 is None else x0
        x = x0
        while x + tw < w - x0:
            d.rectangle([x, y, x + tw, y + thick], fill=255)
            x += tw + gap
        return ImageChops.multiply(m, fm)

    img = K.over(img, K.solid((w, h), B_P["edge"]),
                 tickrun(cap_h + 8, 0, tw=12, gap=9, thick=2))
    img = K.over(img, K.solid((w, h), B_P["edge"]),
                 tickrun(h - foot_h - 9, 0, tw=12, gap=9, thick=2))
    return img


def btn_B(w, h, cut=8, ss=6, pressed=False):
    c = K.Canvas(w, h, ss)
    c.rings(w, h, cut, [(0, B_P["edge"]), (2, B_P["cap"])])
    img = c.done()
    fm = K.poly_mask((w, h), _cham(w, h, cut, 2), ss=ss)
    base = B_P["cap"] if not pressed else (60, 54, 42)
    img = K.over(img, K.textured((w, h), base, delta=9, fine=0.9, ss=ss), fm)
    hi = K.edge_mask((w, h), fm, "top", 2)
    img = K.over(img, K.solid((w, h), B_P["cap_lit"]),
                 hi.point(lambda v: int(v * 0.55)))
    lo = K.edge_mask((w, h), fm, "bottom", max(3, h // 6))
    img = K.over(img, K.solid((w, h), B_P["cap_dark"]),
                 lo.point(lambda v: int(v * 0.5)))
    return img


# ---------------------------------------------------------------- C 厚凿

def panel_C(w, h, cut=12, t=8, ss=2):
    """厚料：正面 (w-t, h-t) 铺在右上；右/下露出可见厚度面。无描边线。"""
    fw, fh = w - t, h - t
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))

    # 厚度面先铺满整块（切角形），暗一档
    thick = K.poly_mask((w, h), _cham(w, h, cut, 0), ss=ss)
    out.alpha_composite(K.as_rgba(K.textured((w, h), C_P["side"], delta=7,
                                             fine=1.1, ss=ss), thick), (0, 0))

    # 正面压在左上
    fshape = K.poly_mask((fw, fh), _cham(fw, fh, cut, 0), ss=ss)
    face = K.as_rgba(K.textured((fw, fh), C_P["base"], delta=10, fine=1.0,
                                coarse=22.0, ss=ss), fshape)
    hi = K.edge_mask((fw, fh), fshape, ("top", "left"), 3)
    face.alpha_composite(K.as_rgba(K.solid((fw, fh), C_P["face_lit"]),
                                   hi.point(lambda v: int(v * 0.55))))
    out.alpha_composite(face, (0, 0))

    # 正/厚转折：正面右下缘一道窄高光
    seam = K.edge_mask((fw, fh), fshape, ("bottom", "right"), 2)
    out.alpha_composite(K.as_rgba(K.solid((fw, fh), C_P["side_lit"]),
                                  seam.point(lambda v: int(v * 0.45))))
    return out


def btn_C(w, h, cut=8, t=4, ss=6, pressed=False):
    fw, fh = w - t, h - t
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    thick = K.poly_mask((w, h), _cham(w, h, cut, 0), ss=ss)
    out.alpha_composite(K.as_rgba(K.textured((w, h), C_P["side"], delta=6,
                                             fine=1.1, ss=ss), thick), (0, 0))
    base = (50, 52, 50) if not pressed else (40, 42, 40)
    fshape = K.poly_mask((fw, fh), _cham(fw, fh, cut, 0), ss=ss)
    face = K.as_rgba(K.textured((fw, fh), base, delta=8, fine=0.9, ss=ss), fshape)
    hi = K.edge_mask((fw, fh), fshape, ("top", "left"), 2)
    face.alpha_composite(K.as_rgba(K.solid((fw, fh), C_P["face_lit"]),
                                   hi.point(lambda v: int(v * 0.5))))
    out.alpha_composite(face, (0, 0))
    return out


# ---------------------------------------------------------------- 栏头

def band_A(w, h, cut=6, ss=4):
    c = K.Canvas(w, h, ss)
    c.rings(w, h, cut, [(0, A_P["edge"]), (2, A_P["field_bev"])])
    img = c.done()
    img = K.over(img, K.textured((w, h), A_P["base"], delta=10, fine=1.3, coarse=30.0, ss=ss),
                 K.poly_mask((w, h), _cham(w, h, cut, 2), ss=ss))
    img = K.over(img, K.solid((w, h), A_P["trench"]),
                 K.ring_mask((w, h), _cham(w, h, cut, 9), _cham(w, h, cut, 11), ss=ss))
    return img


def band_B(w, h, cut=6, ss=4):
    c = K.Canvas(w, h, ss)
    c.rings(w, h, cut, [(0, B_P["edge"]), (2, B_P["cap"])])
    img = c.done()
    fm = K.poly_mask((w, h), _cham(w, h, cut, 2), ss=ss)
    img = K.over(img, K.textured((w, h), B_P["cap"], delta=10, fine=1.3, coarse=32.0, ss=ss), fm)
    top = K.edge_mask((w, h), fm, "top", 2)
    img = K.over(img, K.solid((w, h), B_P["cap_lit"]), top.point(lambda v: int(v * 0.6)))
    bot = K.edge_mask((w, h), fm, "bottom", 4)
    img = K.over(img, K.solid((w, h), B_P["cap_dark"]), bot.point(lambda v: int(v * 0.55)))
    return img


def band_C(w, h, cut=6, t=5, ss=4):
    fw, fh = w - t, h - t
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    thick = K.poly_mask((w, h), _cham(w, h, cut, 0), ss=ss)
    out.alpha_composite(K.as_rgba(K.textured((w, h), C_P["side"], delta=6,
                                             fine=1.1, ss=ss), thick), (0, 0))
    fshape = K.poly_mask((fw, fh), _cham(fw, fh, cut, 0), ss=ss)
    face = K.as_rgba(K.textured((fw, fh), C_P["base"], delta=9, fine=1.2,
                                coarse=20.0, ss=ss), fshape)
    hi = K.edge_mask((fw, fh), fshape, ("top", "left"), 2)
    face.alpha_composite(K.as_rgba(K.solid((fw, fh), C_P["face_lit"]),
                                   hi.point(lambda v: int(v * 0.5))))
    out.alpha_composite(face, (0, 0))
    return out


# ---------------------------------------------------------------- 拼图

FONT = "C:/Windows/Fonts/msyh.ttc"


def _font(sz):
    try:
        return ImageFont.truetype(FONT, sz)
    except Exception:  # noqa: BLE001
        return ImageFont.load_default()


def main():
    os.makedirs(OUT, exist_ok=True)
    PW, PH = 620, 360
    BW, BH = 110, 35
    HW, HH = 620, 52

    rows = [
        ("A 阴刻 · 冷青灰石／金色退场／界限靠凿沟",
         [panel_A(PW, PH), btn_A(BW, BH), band_A(HW, HH)]),
        ("B 碑额 · 砂黄石／起头收脚／暗刻回纹",
         [panel_B(PW, PH), btn_B(BW, BH), band_B(HW, HH)]),
        ("C 厚凿 · 中灰石／可见厚度侧边／粗凿面",
         [panel_C(PW, PH), btn_C(BW, BH), band_C(HW, HH)]),
    ]

    cols = [PW, 150, HW]
    PAD, GAP = 26, 22
    W = PAD * 2 + sum(cols) + GAP * 2
    rowh = PH + 58 + 34
    H = 92 + rowh * len(rows) + PAD

    sheet = Image.new("RGB", (W, H), (24, 24, 26))
    d = ImageDraw.Draw(sheet)
    d.text((PAD, 24), "AWAKE UI · 风格探索（三方向，示意尺寸）", font=_font(25), fill=(240, 236, 228))
    d.text((PAD, 56), "同一部件横向比：面板底 / 主按钮(default) / 栏头。看哪条路跟原版（暖黑+金细线）拉得开。",
           font=_font(14), fill=(150, 150, 158))

    y = 92
    for label, (pan, btn, band) in rows:
        d.text((PAD, y), label, font=_font(17), fill=(226, 175, 84))
        yy = y + 30
        # 面板
        _paste(sheet, pan, PAD, yy)
        d.text((PAD, yy + pan.height + 6), "" , font=_font(12), fill=(150, 150, 158))
        # 按钮（放大 2x 展示）
        bx = PAD + cols[0] + GAP
        b2 = btn.resize((btn.width * 2, btn.height * 2), Image.NEAREST)
        _paste(sheet, b2, bx, yy + 12)
        # 栏头
        hx = bx + cols[1] + GAP
        _paste(sheet, band, hx, yy)
        y += rowh

    p = os.path.join(OUT, "style_compare.png")
    sheet.save(p)
    print("wrote", p, sheet.size)

    for name, im in (("A_panel", panel_A(PW, PH)), ("A_btn", btn_A(BW, BH)), ("A_band", band_A(HW, HH)),
                     ("B_panel", panel_B(PW, PH)), ("B_btn", btn_B(BW, BH)), ("B_band", band_B(HW, HH)),
                     ("C_panel", panel_C(PW, PH)), ("C_btn", btn_C(BW, BH)), ("C_band", band_C(HW, HH))):
        im.save(os.path.join(OUT, name + ".png"))


def _paste(sheet, im, x, y):
    bg = Image.new("RGB", im.size, (16, 15, 14))
    bg.paste(im.convert("RGBA"), (0, 0), im.convert("RGBA"))
    sheet.paste(bg, (x, y))
    ImageDraw.Draw(sheet).rectangle([x - 1, y - 1, x + im.width, y + im.height],
                                    outline=(70, 70, 74))


if __name__ == "__main__":
    main()
