# -*- coding: utf-8 -*-
"""按钮族「形」改造 · 真尺寸横排对照（1× 与 2×）

形状好不好看要在真实尺寸下看，放大窗只用来读几何。
同一行 = 同一档放大倍率，行内三控件同底（垂直对齐）。
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


F_TITLE = _font(20)
F_HEAD = _font(14)
F_LBL = _font(12)
F_TINY = _font(11)

BG = (23, 23, 27)
TXT = (226, 226, 234)
DIM = (140, 140, 154)
ACC = (120, 205, 145)
WARN = (240, 175, 120)

MARGIN = 30
LABW = 74            # 左侧标签列宽
GAPX = 26


def render(curve, cuts):
    artkit._curve_mod = ORIG_CURVE if curve else None
    names = ["页签", "主按钮", "次按钮"]
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

    bands = [("1×（点对点真实大小）", 1), ("2×", 2), ("4×", 4)]

    # 每档行高 = 35*scale，行间距
    ROWPAD = 16
    GROUP_TITLE = 24
    rows_h = sum(35 * s + ROWPAD for _, s in bands)

    W = MARGIN * 2 + LABW + GAPX * 2 + 110 * 4   # 主按钮 110@4x=440 会超，改用统一上限
    # 统一列布局：以最大放大档的宽度决定列宽
    maxw = 110 * 4
    W = MARGIN * 2 + LABW + GAPX * 2 + maxw * 3
    H = MARGIN + 30 + 34 + GROUP_TITLE + rows_h + 44 + GROUP_TITLE + rows_h + MARGIN + 30

    img = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(img)

    d.text((MARGIN, MARGIN), "按钮族「形」· 真尺寸横排对照", fill=TXT, font=F_TITLE)
    d.text((MARGIN, MARGIN + 26),
           "同一行＝同一倍率，同底对齐；形状好不好看请在 1× 那行判断",
           fill=DIM, font=F_TINY)

    y = MARGIN + 26 + 34

    def colx(i, maxw, scale, itemw):
        """第 i 列，按该列内容放大后宽度居中到列格。"""
        cell = maxw
        cx = MARGIN + LABW + GAPX + i * (cell + GAPX)
        return cx + (cell - itemw * scale) // 2

    def draw_group(y, title, rows, titlec):
        d.text((MARGIN, y), title, fill=titlec, font=F_HEAD)
        y += GROUP_TITLE
        for band_name, s in bands:
            # 每档：先画标签
            d.text((MARGIN, y + (35 * s) // 2 - 8), band_name, fill=DIM, font=F_LBL)
            base = y + 35 * s          # 底边
            for i, (nm, im, c) in enumerate(rows):
                xx = colx(i, maxw, s, im.size[0])
                big = im.resize((im.size[0] * s, 35 * s), Image.NEAREST)
                yy = base - 35 * s
                img.paste(big, (xx, yy), big)
            y += 35 * s + ROWPAD
        return y

    y = draw_group(y, "改前 · 八边形 45° 切角", before, WARN)
    y += 10
    y = draw_group(y, "改后 · 连续曲线端头 13px", after, ACC)

    d.text((MARGIN, y + 16),
           "注：4× 档下页面宽 1280 放不下三个控件，此处已按行内列宽缩放裁切显示；1×／2× 为准确尺寸。",
           fill=(110, 110, 122), font=F_TINY)

    # 底部列标签
    yl = MARGIN + 26 + 34 - 16
    for i, nm in enumerate(["页签 105", "主按钮 110", "次按钮 100"]):
        xx = colx(i, maxw, 1, 110)
        pass

    path = os.path.join(OUT, "shape_final_row.png")
    img.save(path)
    print("saved", path, img.size)


if __name__ == "__main__":
    main()
