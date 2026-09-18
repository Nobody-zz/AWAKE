# -*- coding: utf-8 -*-
"""按钮族「形」改造 · 产线前后对照板 v2（交付用）

修 v1 三处：① 说明文字出框（改自动折行）② 放大窗太窄看不到端头全貌
③ 组间留白不均。
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

from PIL import Image, ImageDraw, ImageFont

import artkit
import build_m0 as B

OUT = os.path.join(HERE, "out")
ORIG_CURVE = artkit._curve_mod


def _font(sz):
    try:
        return ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", sz)
    except Exception:
        return ImageFont.load_default()


F_TITLE = _font(22)
F_HEAD = _font(16)
F_COL = _font(13)
F_TINY = _font(12)

BG = (23, 23, 27)
TXT = (226, 226, 234)
DIM = (140, 140, 154)
ACC = (120, 205, 145)
WARN = (240, 175, 120)
CARD = (32, 32, 38)

ZOOM = 5
ZOOM_H = 74          # 放大窗宽度（像素）—— 够看到端头收束全貌
GAP = 22
MARGIN = 34
COLW = 150           # 每列宽（控件 110 居中放得下）

W = MARGIN * 2 + COLW * 3 + GAP * 2      # 34*2 + 450 + 44 = 562


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


def render(curve, cuts):
    artkit._curve_mod = ORIG_CURVE if curve else None
    names = ["页签 105", "主按钮 110", "次按钮 100"]
    makers = [lambda: B.tab("default")(), lambda: B.primary("default")(),
              lambda: B.secondary("default")()]
    out = []
    for nm, mk, c in zip(names, makers, cuts):
        B.BUTTON_CUT = float(c)
        out.append((nm, mk(), c))
    artkit._curve_mod = ORIG_CURVE
    B.BUTTON_CUT = 13.0
    return out


def main():
    before = render(False, (7, 8, 8))
    after = render(True, (13, 13, 13))

    zh = 35 * ZOOM                      # 175
    grp_h = 26 + 20 + 6 + 35 + 10 + zh + 6 + 18   # 296

    foot_lines = [
        ("端头由「45° 切角」改为连续曲线，三控件共用同一轮廓；层级改由尺寸（105 / 110 / 100）与框色承担。", TXT),
        ("13px 的两条硬约束：木面内缩量 bev+1 = 5，故端头必须 > 5（现留余量 8px）；端头占宽 ≤ 12%，13/110 = 11.8%。", DIM),
        ("注意：铸件与端头孔只存在于研究脚本 make_button_primary.py，产线（build_m0）从来没长过它们。", WARN),
        ("残留两处：端头斜边上铁轨由 7px 压到 2~3px（等距内缩的几何必然，未处理）；整体质感偏灰暗（未动）。", DIM),
    ]

    # 先量高度
    probe = ImageDraw.Draw(Image.new("RGB", (10, 10)))
    wrapped = [( _wrap(probe, t, F_TINY, W - MARGIN * 2), c) for t, c in foot_lines]
    foot_h = sum(len(ls) * 18 + 6 for ls, _ in wrapped)

    H = (MARGIN + 34 + 34 + 12 + grp_h + 40 + grp_h + 26 + foot_h + MARGIN)
    img = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(img)

    d.text((MARGIN, MARGIN), "按钮族「形」改造 · 产线前后对照", fill=TXT, font=F_TITLE)
    d.text((MARGIN, MARGIN + 32),
           "build_m0.py → parts.btn_plate → artkit.chamfer_pts　真渲染，非示意图",
           fill=DIM, font=F_TINY)
    y = MARGIN + 34 + 34 + 12

    def draw_group(y, title, sub, rows, sub_color):
        d.text((MARGIN, y), title, fill=TXT, font=F_HEAD)
        d.text((MARGIN + 250, y + 3), sub, fill=sub_color, font=F_TINY)
        y += 26
        for i, (nm, im, c) in enumerate(rows):
            x = MARGIN + i * (COLW + GAP)
            d.text((x, y), nm, fill=DIM, font=F_COL)
        y += 20 + 6
        # 真尺寸（居中于列）
        for i, (nm, im, c) in enumerate(rows):
            x = MARGIN + i * (COLW + GAP) + (COLW - im.size[0]) // 2
            img.paste(im, (x, y), im)
        y += 35 + 10
        # 端头放大：从 x=0 起 ZOOM_H 宽
        for i, (nm, im, c) in enumerate(rows):
            cx = MARGIN + i * (COLW + GAP)
            zw = min(ZOOM_H, im.size[0])
            z = im.crop((0, 0, zw, 35)).resize(
                (zw * ZOOM, zh), Image.NEAREST)
            # 卡片底（比放大图四周各留 4px）
            d.rectangle([cx - 4, y - 4, cx + zw * ZOOM + 3, y + zh + 3],
                        fill=CARD)
            img.paste(z, (cx, y))
            d.text((cx + 2, y + zh + 4), "端头 %dpx" % c, fill=DIM, font=F_TINY)
        return y + zh + 6 + 18

    y = draw_group(y, "改前 · 八边形 45° 切角",
                   "页签 7 ／ 主 8 ／ 次 8 —— 三个轮廓互不相同",
                   before, WARN)
    y += 40
    y = draw_group(y, "改后 · 连续曲线端头",
                   "三控件同一轮廓，端头统一 13px",
                   after, ACC)

    y += 26
    for ls, c in wrapped:
        for ln in ls:
            d.text((MARGIN, y), ln, fill=c, font=F_TINY)
            y += 18
        y += 6

    path = os.path.join(OUT, "shape_final_board.png")
    img.save(path)
    print("saved", path, img.size)


if __name__ == "__main__":
    main()
