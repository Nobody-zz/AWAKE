#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""参照研究：把原版 UI 贴图放大出来看清楚（只读，不改任何源文件）。

素材来源＝UI Lab 已裁切的 sprite 缓存（读，不写）。
产物：out/study/ref_full.png（全貌+尺寸）、ref_corner.png（左上角特写）

用法：python _study_ref.py
"""
import os

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.normpath(os.path.join(HERE, "..", "awake-ui-lab", "out", "atlas", "sprites"))
OUT = os.path.join(HERE, "out", "study")

BG = (128, 128, 128, 255)          # 中性灰：深色和浅色元素都能看清
PANEL_DARK = (24, 16, 16, 255)     # AWAKE 面板暖黑，用来看"放到真实底色上什么样"

# (标题, 文件名, 说明)
GROUPS = [
    ("面板底 · 候选落点", [
        ("npc_dialogue_panel_9.png", "对话面板 960x720 的底"),
        ("StdAssets__Popup__canvas.png", "三个弹窗共用的底"),
        ("stone_texture_overlay.png", "信使面板曾误用（只作 overlay）"),
        ("stone_texture_continuous.png", "石材连续纹"),
    ]),
    ("按钮 · D/H/P 三态", [
        ("General__Button__main_button_regular.png", "主按钮 Default"),
        ("General__Button__main_button_regular_hover.png", "主按钮 Hover"),
        ("General__Button__main_button_done.png", "确认 Default"),
        ("General__Button__main_button_done_hover.png", "确认 Hover"),
        ("General__Button__button_cancel.png", "取消 Default"),
        ("General__Button__button_cancel_hover.png", "取消 Hover"),
    ]),
    ("关闭键 · D/H/P", [
        ("StdAssets__close_button.png", "Default"),
        ("StdAssets__close_button_hover.png", "Hover"),
        ("StdAssets__close_button_pressed.png", "Pressed"),
    ]),
    ("装饰", [
        ("TitleHeader.png", "标题碑额"),
        ("GradientDivider_9.png", "金分隔线"),
        ("General__CharacterCreation__character_creation_background_gradient.png", "顶栏带"),
        ("StdAssets__page_button_center.png", "翻页横杠"),
    ]),
    ("输入 / 其它", [
        ("General__CharacterCreation__name_input_area.png", "输入区"),
        ("General__Slider__slider_knob.png", "滑钮"),
        ("StdAssets__checkmark.png", "勾"),
        ("StdAssets__arrow_pointing_left.png", "箭头"),
    ]),
]


def font(sz):
    try:
        return ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", sz)
    except Exception:  # noqa: BLE001
        return ImageFont.load_default()


def flat(img, bg):
    canvas = Image.new("RGBA", img.size, bg)
    canvas.alpha_composite(img.convert("RGBA"))
    return canvas.convert("RGB")


def build_full():
    Z = 3          # 全貌放大倍率
    CELL_W = 460
    PAD = 14
    rows = []
    for title, items in GROUPS:
        for fn, note in items:
            p = os.path.join(SRC, fn)
            if not os.path.isfile(p):
                rows.append((title, note, fn, None))
                continue
            im = Image.open(p).convert("RGBA")
            rows.append((title, note, fn, im))

    # 先算高度
    heights = []
    for _, _, _, im in rows:
        h = (im.height * Z) if im else 60
        heights.append(min(h, 300) + PAD)

    W = 1180
    H = 90 + sum(heights) + 40
    sheet = Image.new("RGB", (W, H), (34, 34, 38))
    d = ImageDraw.Draw(sheet)
    f_t = font(26)
    f_n = font(17)
    f_s = font(15)

    d.text((24, 22), "AWAKE UI · 原版参照全貌（放大 3x，中性灰底）", font=f_t, fill=(240, 236, 228))
    y = 72
    last_title = None
    for (title, note, fn, im), hh in zip(rows, heights):
        if title != last_title:
            d.text((24, y), "── %s ──" % title, font=f_n, fill=(226, 175, 84))
            y += 30
            last_title = title
        if im is None:
            d.text((40, y + 10), "[缺] %s" % fn, font=f_s, fill=(220, 120, 120))
            y += hh
            continue
        # 缩放
        sc = Z
        if im.height * sc > 300:
            sc = 300.0 / im.height
        show = im.resize((max(1, int(im.width * sc)), max(1, int(im.height * sc))), Image.NEAREST)
        show = flat(show, BG)
        d.text((40, y + 4), "%s" % note, font=f_n, fill=(236, 232, 224))
        d.text((40, y + 26), "%s  ·  源 %dx%d" % (fn, im.width, im.height), font=f_s, fill=(150, 150, 158))
        sheet.paste(show, (CELL_W, y))
        d.rectangle([CELL_W - 2, y - 2, CELL_W + show.width + 1, y + show.height + 1], outline=(90, 90, 96))
        y += hh

    os.makedirs(OUT, exist_ok=True)
    p = os.path.join(OUT, "ref_full.png")
    sheet.save(p)
    print("wrote", p, sheet.size)


def build_corner():
    """面板底 / 按钮的左上角特写：看倒角、描边、材质处理。"""
    Z = 6
    CROP = 64
    items = [
        ("npc_dialogue_panel_9.png", "对话面板底"),
        ("StdAssets__Popup__canvas.png", "弹窗底"),
        ("General__CharacterCreation__name_input_area.png", "输入区"),
        ("General__Button__main_button_regular.png", "主按钮"),
        ("General__Button__button_cancel.png", "取消按钮"),
        ("StdAssets__close_button.png", "关闭键"),
        ("TitleHeader.png", "标题碑额"),
        ("stone_texture_overlay.png", "石材 overlay"),
    ]
    cols = 4
    cw = CROP * Z + 24
    ch = CROP * Z + 70
    rows = (len(items) + cols - 1) // cols
    W = 24 + cols * cw
    H = 70 + rows * ch
    sheet = Image.new("RGB", (W, H), (34, 34, 38))
    d = ImageDraw.Draw(sheet)
    f_t = font(24)
    f_s = font(15)
    f_n = font(17)
    d.text((24, 20), "左上角特写（64x64 放大 6x）——看倒角与材质", font=f_t, fill=(240, 236, 228))

    for i, (fn, note) in enumerate(items):
        cx = 24 + (i % cols) * cw
        cy = 70 + (i // cols) * ch
        d.text((cx, cy), note, font=f_n, fill=(236, 232, 224))
        d.text((cx, cy + 22), fn[:34], font=f_s, fill=(150, 150, 158))
        p = os.path.join(SRC, fn)
        if not os.path.isfile(p):
            continue
        im = Image.open(p).convert("RGBA")
        box = (0, 0, min(CROP, im.width), min(CROP, im.height))
        crop = im.crop(box)
        if crop.size != (CROP, CROP):
            padim = Image.new("RGBA", (CROP, CROP), (0, 0, 0, 0))
            padim.alpha_composite(crop)
            crop = padim
        show = flat(crop.resize((CROP * Z, CROP * Z), Image.NEAREST), BG)
        sheet.paste(show, (cx, cy + 46))
        d.rectangle([cx - 1, cy + 45, cx + CROP * Z, cy + 46 + CROP * Z], outline=(90, 90, 96))

    os.makedirs(OUT, exist_ok=True)
    p = os.path.join(OUT, "ref_corner.png")
    sheet.save(p)
    print("wrote", p, sheet.size)


if __name__ == "__main__":
    build_full()
    build_corner()
