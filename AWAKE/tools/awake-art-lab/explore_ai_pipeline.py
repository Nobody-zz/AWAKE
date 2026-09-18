#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""AI 材质 → 可用 sprite 的装配演示。

分工论点（本脚本只演示它的可行性与边界）：
  · AI 出**肌理**（石头的层理、风化、凿痕）—— 代码画不出"有性格的石头"
  · 代码出**骨架**（几何、九宫格、受光方向、状态族）—— AI 保证不了这些

做法：取 AI 生成的无缝石板纹理，压暗到 UI 明度后，套上**阴刻框**
（界限于凿沟，不用亮线），输出一块可直接进 SpriteParts 的面板底。

产出 out/explore/：ai_panel_960x720.png、ai_compare.png（原版 / 现皮肤 / AI 混合）
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFont, ImageStat

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import artkit as K  # noqa: E402

EXP = os.path.join(HERE, "out", "explore")
AI_TEX = os.path.join(EXP, "ai_tex_slate.jpg")
LAB = os.path.normpath(os.path.join(HERE, "..", "awake-ui-lab", "out", "atlas", "sprites"))
DELIVER = os.path.normpath(os.path.join(HERE, "..", "..", "GUI", "SpriteParts"))
FONT = "C:/Windows/Fonts/msyh.ttc"

W, H = 960, 720


def _font(sz):
    try:
        return ImageFont.truetype(FONT, sz)
    except Exception:  # noqa: BLE001
        return ImageFont.load_default()


def crop_fill(im, w, h):
    """按 cover 裁一块 (w,h)。"""
    if im.width / im.height > w / h:
        nw = int(im.height * w / h)
        x = (im.width - nw) // 2
        box = (x, 0, x + nw, im.height)
    else:
        nh = int(im.width * h / w)
        y = (im.height - nh) // 2
        box = (0, y, im.width, y + nh)
    return im.crop(box).resize((w, h), Image.LANCZOS)


def darken(im, tmean):
    m = ImageStat.Stat(im.convert("L")).mean[0] or 1.0
    k = tmean / m
    return im.point(lambda v: max(0, min(255, int(v * k))))


def panel_from_ai(w, h, *, cut=16, rule=22, ss=2, mean=54):
    """AI 石纹 + 代码骨架（切角轮廓 / 阴刻框 / 受光）。"""
    base = darken(crop_fill(Image.open(AI_TEX).convert("RGB"), w, h), mean)
    img = base.convert("RGBA")

    shape = K.poly_mask((w, h), K.chamfer_pts(w, h, cut, 0), ss=ss)
    img.putalpha(shape)

    # 料到边：外缘压深
    edge = K.ring_mask((w, h), K.chamfer_pts(w, h, cut, 0),
                       K.chamfer_pts(w, h, cut, 3), ss=ss)
    img = K.over(img, K.solid((w, h), (11, 14, 17)), edge.point(lambda v: int(v * 0.85)))

    # 阴刻沟：界限于"陷进去的槽"，不是亮线
    outer = K.chamfer_pts(w, h, cut, rule)
    inner = K.chamfer_pts(w, h, cut, rule + 3)
    groove = K.ring_mask((w, h), outer, inner, ss=ss)
    img = K.over(img, K.solid((w, h), (12, 16, 20)), groove)

    # 沟的下/右缘：凹进的受光（光在下右）
    lit = K.ring_mask((w, h), inner, K.chamfer_pts(w, h, cut, rule + 5), ss=ss)
    lit = K.edge_mask((w, h), lit, ("bottom", "right"), 3)
    img = K.over(img, K.solid((w, h), (96, 106, 114)), lit.point(lambda v: int(v * 0.55)))

    # 沟的上/左缘：落影
    sh = K.ring_mask((w, h), outer, K.chamfer_pts(w, h, cut, rule + 2), ss=ss)
    sh = K.edge_mask((w, h), sh, ("top", "left"), 3)
    img = K.over(img, K.solid((w, h), (5, 7, 9)), sh.point(lambda v: int(v * 0.5)))
    return img


def main():
    os.makedirs(EXP, exist_ok=True)
    ai = panel_from_ai(W, H)
    ai.save(os.path.join(EXP, "ai_panel_960x720.png"))

    cols = []
    p = os.path.join(LAB, "npc_dialogue_panel_9.png")
    if os.path.isfile(p):
        cols.append(("原版（官方）", Image.open(p).convert("RGBA")))
    p2 = os.path.join(DELIVER, "ui_awake_frame", "panel_main_960.png")
    if os.path.isfile(p2):
        cols.append(("现皮肤（纯代码）", Image.open(p2).convert("RGBA")))
    cols.append(("AI 材质 + 代码骨架", ai))

    sc = 0.46
    tw, th = int(W * sc), int(H * sc)
    PAD, GAP, TOP = 26, 22, 96
    Wt = PAD * 2 + tw * len(cols) + GAP * (len(cols) - 1)
    Ht = TOP + th + 46 + PAD

    sheet = Image.new("RGB", (Wt, Ht), (22, 22, 24))
    d = ImageDraw.Draw(sheet)
    d.text((PAD, 22), "AWAKE UI · 三条路的同一块界面底（960×720）",
           font=_font(25), fill=(240, 236, 228))
    d.text((PAD, 56), "看区分度：左＝官方，中＝我上一版（纯代码：暖黑 + 金细线），右＝AI 材质 + 阴刻骨架。",
           font=_font(14), fill=(152, 152, 160))

    x = PAD
    for label, im in cols:
        show = im.resize((tw, th), Image.LANCZOS)
        bg = Image.new("RGB", (tw, th), (16, 15, 14))
        bg.paste(show, (0, 0), show)
        sheet.paste(bg, (x, TOP))
        d.rectangle([x - 1, TOP - 1, x + tw, TOP + th], outline=(74, 74, 78))
        d.text((x, TOP + th + 10), label, font=_font(16), fill=(232, 224, 212))
        x += tw + GAP

    out = os.path.join(EXP, "ai_compare.png")
    sheet.save(out)
    print("wrote", out, sheet.size)
    print("wrote", os.path.join(EXP, "ai_panel_960x720.png"))


if __name__ == "__main__":
    main()
