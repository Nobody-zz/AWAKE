# -*- coding: utf-8 -*-
"""
AWAKE UI · 材质"实际用起来"三版对照
结论导向：渲染一块真实尺寸面板（600×400，深色基调），三版对比：
  ① 满版铺材质      -> 眼花（上一轮交付的形式）
  ② 淡化材质        -> 中间态：既丢材质身份、又没换来安静（最差解）
  ③ 材质只在"有边界的件"上 -> 底极安静 + 标题带/蜡板输入区/封蜡按钮
量化口径修正：只统计**无文字空白区**的 stddev（上一版把文字像素也算进去了）。
"""
import os
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageEnhance, ImageStat

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "out", "explore", "medieval")
OUT = os.path.join(HERE, "out", "explore", "legibility")
os.makedirs(OUT, exist_ok=True)

FONT = "C:/Windows/Fonts/simkai.ttf"
PW, PH, HEAD = 600, 400, 52
BASE_DARK = (24, 20, 17)
INK = (242, 237, 228)
BRASS = (217, 169, 83)
LINEN_LINE = (108, 92, 68)

# 无文字空白区（用于量化）：面板右下角一块
BLANK = (430, 150, 580, 300)


def load(name, size):
    return Image.open(os.path.join(SRC, name)).convert("RGB").resize(size, Image.LANCZOS)


def lowfreq(im, base, amount=0.5, blur=22):
    """只保留大尺度明暗不均，抹掉所有高频纹理 —— 这是'底'该有的材质用量。"""
    m = im.filter(ImageFilter.GaussianBlur(blur))
    m = ImageEnhance.Contrast(m).enhance(0.85)
    return Image.blend(Image.new("RGB", im.size, base), m, amount)


def faint(im, base, amount=0.22, blur=3.0):
    """淡化：高频还在，只是幅度小 —— 就是那个失败的中间态。"""
    m = ImageEnhance.Contrast(im.filter(ImageFilter.GaussianBlur(blur))).enhance(0.26)
    return Image.blend(Image.new("RGB", im.size, base), m, amount)


def circle(src, box, dia):
    im = src.crop(box).resize((dia, dia), Image.LANCZOS)
    m = Image.new("L", (dia, dia), 0)
    ImageDraw.Draw(m).ellipse((0, 0, dia - 1, dia - 1), fill=255)
    out = Image.new("RGBA", (dia, dia), (0, 0, 0, 0))
    out.paste(im, (0, 0), m)
    return out


def draw_text_layer(img, mode):
    d = ImageDraw.Draw(img)
    ft = ImageFont.truetype(FONT, 25)
    fb = ImageFont.truetype(FONT, 17)
    fs = ImageFont.truetype(FONT, 14)
    fb2 = ImageFont.truetype(FONT, 16)
    st = dict(stroke_width=1, stroke_fill=(10, 8, 6))

    d.text((18, 13), "醒世 · 深谈", font=ft, fill=BRASS, **st)
    y = HEAD + 16
    for line in ["德里亚特是卡琉斯堡左近的一座村庄，",
                 "夹在瓦尔切格湾和埃博半岛之间的山脊上。"]:
        d.text((18, y), line, font=fb, fill=INK, **st)
        y += 26
    y += 8
    for who in ["村民听到的", "商人知道的", "贵族看到的"]:
        d.text((18, y), who, font=fs, fill=(200, 176, 136), **st)
        d.text((110, y), "—— 各说各话，同一件事三种说法。", font=fs, fill=INK, **st)
        d.line([(18, y + 22), (PW - 18, y + 22)], fill=LINEN_LINE, width=1)
        y += 30
    iy = PH - 74
    return img, (iy, fb2)


def v1(mat):
    """① 满版铺"""
    img = mat.copy()
    d = ImageDraw.Draw(img)
    d.line([(0, HEAD - 1), (PW, HEAD - 1)], fill=BRASS, width=1)
    img, (iy, fb2) = draw_text_layer(img, 1)
    d = ImageDraw.Draw(img)
    for bx, lb in [(PW - 120, "落笔"), (PW - 62, "收起")]:
        d.rounded_rectangle([(bx, iy), (bx + 51, iy + 33)], radius=4,
                            fill=(40, 34, 28), outline=BRASS, width=1)
        tw = d.textlength(lb, font=fb2)
        d.text((bx + (52 - tw) / 2, iy + 8), lb, font=fb2, fill=INK,
               stroke_width=1, stroke_fill=(10, 8, 6))
    d.rounded_rectangle([(18, iy), (PW - 130, iy + 34)], radius=5,
                        fill=(16, 12, 10), outline=(70, 58, 44), width=1)
    d.text((30, iy + 8), "向此人开口…", font=ImageFont.truetype(FONT, 14),
           fill=(128, 114, 94), stroke_width=1, stroke_fill=(10, 8, 6))
    return img


def v2(mat):
    """② 淡化铺（中间态）"""
    img = faint(mat, BASE_DARK, amount=0.22, blur=3.0)
    d = ImageDraw.Draw(img)
    d.line([(0, HEAD - 1), (PW, HEAD - 1)], fill=BRASS, width=1)
    img, (iy, fb2) = draw_text_layer(img, 2)
    d = ImageDraw.Draw(img)
    for bx, lb in [(PW - 120, "落笔"), (PW - 62, "收起")]:
        d.rounded_rectangle([(bx, iy), (bx + 51, iy + 33)], radius=4,
                            fill=(40, 34, 28), outline=BRASS, width=1)
        tw = d.textlength(lb, font=fb2)
        d.text((bx + (52 - tw) / 2, iy + 8), lb, font=fb2, fill=INK,
               stroke_width=1, stroke_fill=(10, 8, 6))
    d.rounded_rectangle([(18, iy), (PW - 130, iy + 34)], radius=5,
                        fill=(16, 12, 10), outline=(70, 58, 44), width=1)
    d.text((30, iy + 8), "向此人开口…", font=ImageFont.truetype(FONT, 14),
           fill=(128, 114, 94), stroke_width=1, stroke_fill=(10, 8, 6))
    return img


def v3(felt, vellum, tablet, seal):
    """③ 材质只做「有边界的件」"""
    # 底：只保留低频起伏（有材料感的呼吸，但没有高频噪声）
    img = lowfreq(felt, BASE_DARK, amount=0.45, blur=24)

    # 标题带：与底同族材质（毡）压暗 + 金线界定上下边（避免与底色断层）
    band = lowfreq(felt.crop((0, 0, PW, HEAD)), BASE_DARK, amount=0.95, blur=12)
    band = ImageEnhance.Brightness(band).enhance(1.25)
    img.paste(band, (0, 0))
    d = ImageDraw.Draw(img)
    d.line([(0, HEAD - 2), (PW, HEAD - 2)], fill=BRASS, width=1)
    d.line([(0, 1), (PW, 1)], fill=(124, 102, 66), width=1)

    img, (iy, fb2) = draw_text_layer(img, 3)
    d = ImageDraw.Draw(img)

    # 输入区：凹槽（不是拉伸贴图）—— 左端一枚蜡板方标（正方形，不变形）
    d.rounded_rectangle([(18, iy), (PW - 130, iy + 34)], radius=5,
                        fill=(14, 11, 9), outline=(92, 78, 58), width=1)
    tab = tablet.crop((455, 425, 565, 535)).resize((26, 26), Image.LANCZOS)
    img.paste(tab, (24, iy + 4))
    d = ImageDraw.Draw(img)
    d.text((58, iy + 8), "向此人开口…", font=ImageFont.truetype(FONT, 14),
           fill=(128, 114, 94), stroke_width=1, stroke_fill=(10, 8, 6))

    # 主按钮：封蜡（最强锚点，圆形 + 金圈界定边界）
    seal_img = circle(seal, (102, 125, 482, 505), 46)
    img.paste(seal_img, (PW - 128, iy - 7), seal_img)
    d.ellipse([(PW - 128, iy - 7), (PW - 128 + 45, iy - 7 + 45)],
              outline=(176, 138, 80), width=1)
    tw = d.textlength("落笔", font=fb2)
    d.text((PW - 128 + (46 - tw) / 2, iy + 6), "落笔", font=fb2, fill=(252, 246, 236),
           stroke_width=1, stroke_fill=(70, 12, 8))
    # 次按钮：素净（材质用量为零，靠线框）
    d.rounded_rectangle([(PW - 66, iy), (PW - 18, iy + 34)], radius=4,
                        fill=(30, 25, 21), outline=(110, 94, 68), width=1)
    tw = d.textlength("收起", font=fb2)
    d.text((PW - 66 + (48 - tw) / 2, iy + 8), "收起", font=fb2, fill=INK,
           stroke_width=1, stroke_fill=(10, 8, 6))
    return img


def tile(img, label, verdict, sd):
    W, H = img.size
    c = Image.new("RGB", (W, H + 78), (12, 10, 9))
    c.paste(img, (0, 56))
    d = ImageDraw.Draw(c)
    f1 = ImageFont.truetype(FONT, 20)
    f2 = ImageFont.truetype(FONT, 14)
    d.text((4, 4), label, font=f1, fill=(240, 236, 228))
    d.text((4, 30), "空白区噪声 stddev=%.1f ｜ %s" % (sd, verdict), font=f2,
           fill=(196, 150, 92) if sd > 8 else (140, 186, 140))
    return c


def main():
    print("=== 材质实际用法三版对照（深色基调）===")
    felt = load("el_wool_felt.jpg", (PW, PH))
    vellum = load("el_vellum.jpg", (PW, PH))
    tablet = load("el_wax_tablet.jpg", (PW, PH))
    seal = load("el_wax_seal.jpg", (PW, PH))

    items = [
        (v1(felt), "① 满版铺材质（上一轮交付的样子）", "眼花：纹理与文字同级竞争"),
        (v2(felt), "② 淡化材质（中间态）", "最差解：材质身份没了，也没更安静"),
        (v3(felt, vellum, tablet, seal), "③ 材质只做有边界的件（本线推荐）", "底安静、件有料"),
    ]
    tiles = []
    for img, lb, vd in items:
        sd = ImageStat.Stat(img.crop(BLANK).convert("L")).stddev[0]
        print("  %s -> 空白区 stddev=%.1f" % (lb, sd))
        tiles.append(tile(img, lb, vd, sd))

    W = PW * 3 + 30 * 2
    H = PH + 78 + 30
    sheet = Image.new("RGB", (W, H), (8, 7, 6))
    for i, t in enumerate(tiles):
        sheet.paste(t, (i * (PW + 30), 0))
    d = ImageDraw.Draw(sheet)
    f = ImageFont.truetype(FONT, 19)
    d.text((6, H - 26), "同一块面板 600×400（真实 UI 尺度）· 文字＝游戏内 simkai 楷体 · 噪声仅统计无文字空白区",
           font=f, fill=(190, 180, 164))
    out = os.path.join(OUT, "ui_usage_3ways.png")
    sheet.save(out)
    print("saved:", out, sheet.size)
    tiles[2].save(os.path.join(OUT, "usage_recommended.png"))


if __name__ == "__main__":
    main()
