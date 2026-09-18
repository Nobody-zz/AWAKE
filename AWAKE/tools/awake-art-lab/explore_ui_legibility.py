# -*- coding: utf-8 -*-
"""
AWAKE UI · 材质用量可读性实测
目的：不看材质特写，看"实际用起来"的观感——
      同一块面板，材质按三档用量铺设，压上真实中文（simkai = 游戏内中文字体），
      并输出每档的底色噪声标准差（量化"眼花"）。
"""
import os
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageEnhance, ImageStat

SRC = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out", "explore", "medieval")
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out", "explore", "legibility")
os.makedirs(OUT, exist_ok=True)

FONT = "C:/Windows/Fonts/simkai.ttf"
PW, PH = 600, 400          # 单块面板尺寸（真实 UI 尺度：侧栏级）
HEAD = 52                  # 标题带高

# 基色（材料本色派生，深/浅两系）
BASE_PAPER = (232, 220, 192)
BASE_DARK = (26, 21, 18)
INK_DARK = (42, 33, 24)
INK_LIGHT = (242, 237, 228)
BRASS = (217, 169, 83)


def load(name, size):
    im = Image.open(os.path.join(SRC, name)).convert("RGB")
    return im.resize(size, Image.LANCZOS)


def muted(im, base, amount=0.25, blur=2.5, contrast=0.30):
    """弱化：先低频化（模糊）→ 再降对比 → 再按 amount 混入基色。"""
    m = im.filter(ImageFilter.GaussianBlur(blur))
    m = ImageEnhance.Contrast(m).enhance(contrast)
    b = Image.new("RGB", im.size, base)
    return Image.blend(b, m, amount)


def stat(im):
    s = ImageStat.Stat(im.convert("L"))
    return s.stddev[0], s.mean[0]


def panel(mat_img, base, dark_mode, level):
    """
    level: 1=原样铺 2=弱化到承文字 3=只做锚点（底素净，材质只在件上）
    """
    W, H = PW, PH
    if level == 1:
        body = mat_img.copy()
        head_mat = mat_img.crop((0, 0, W, HEAD))
        btn_mat = mat_img.crop((0, 0, 160, 34)).copy()
    elif level == 2:
        body = muted(mat_img, base, amount=0.22, blur=3.0, contrast=0.26)
        head_mat = muted(mat_img.crop((0, 0, W, HEAD)), base, amount=0.55, blur=2.0, contrast=0.45)
        btn_mat = muted(mat_img.crop((0, 0, 160, 34)), base, amount=0.70, blur=1.2, contrast=0.55)
    else:
        body = Image.new("RGB", (W, H), base)
        head_mat = mat_img.crop((0, 0, W, HEAD)).filter(ImageFilter.GaussianBlur(0.8))
        btn_mat = mat_img.crop((0, 0, 160, 34)).copy()

    img = body.copy()
    img.paste(head_mat, (0, 0))
    d = ImageDraw.Draw(img)

    # 标题带底金线
    d.line([(0, HEAD - 1), (W, HEAD - 1)], fill=BRASS, width=1)

    f_title = ImageFont.truetype(FONT, 25)
    f_body = ImageFont.truetype(FONT, 17)
    f_small = ImageFont.truetype(FONT, 14)
    f_btn = ImageFont.truetype(FONT, 16)

    tcol = INK_LIGHT if dark_mode else INK_DARK
    scol = (200, 176, 136) if dark_mode else (120, 100, 72)
    stroke = (12, 9, 7) if dark_mode else None
    tstroke = dict(stroke_width=1, stroke_fill=stroke) if stroke else {}

    # 标题
    d.text((18, 13), "醒世 · 深谈", font=f_title, fill=BRASS, **tstroke)

    # 正文（世界书样例中立内核，出处：项目样例）
    y = HEAD + 16
    for line in ["德里亚特是卡琉斯堡左近的一座村庄，",
                 "夹在瓦尔切格湾和埃博半岛之间的山脊上。"]:
        d.text((18, y), line, font=f_body, fill=tcol, **tstroke)
        y += 26

    # 六身份条目
    y += 8
    for who in ["村民听到的", "商人知道的", "贵族看到的"]:
        d.text((18, y), who, font=f_small, fill=scol, **tstroke)
        d.text((110, y), "—— 各说各话，同一件事三种说法。", font=f_small, fill=tcol, **tstroke)
        d.line([(18, y + 22), (W - 18, y + 22)],
               fill=(70, 58, 44) if dark_mode else (190, 176, 148), width=1)
        y += 30

    # 输入区（蜡板木牍的用法位置）
    inp_y = H - 74
    d.rounded_rectangle([(18, inp_y), (W - 130, inp_y + 34)], radius=5,
                        fill=(16, 12, 10) if dark_mode else (214, 202, 176),
                        outline=(70, 58, 44) if dark_mode else (176, 160, 132), width=1)
    d.text((30, inp_y + 8), "向此人开口…", font=f_small,
           fill=(120, 106, 88) if dark_mode else (140, 126, 104), **tstroke)

    # 按钮（材质锚点）
    for i, (label, bx) in enumerate([("落笔", W - 120), ("收起", W - 62)]):
        img.paste(btn_mat.resize((52, 34), Image.LANCZOS), (bx, inp_y))
        d.rectangle([(bx, inp_y), (bx + 51, inp_y + 33)],
                    outline=BRASS if i == 0 else (110, 94, 70), width=1)
        tw = d.textlength(label, font=f_btn)
        d.text((bx + (52 - tw) / 2, inp_y + 8), label, font=f_btn, fill=tcol, **tstroke)

    return img


def tile(img, label, sub, lvl_stat):
    W, H = img.size
    canvas = Image.new("RGB", (W, H + 54), (14, 12, 11))
    canvas.paste(img, (0, 44))
    d = ImageDraw.Draw(canvas)
    f1 = ImageFont.truetype(FONT, 19)
    f2 = ImageFont.truetype(FONT, 14)
    d.text((4, 4), label, font=f1, fill=(240, 236, 228))
    d.text((4, 26), sub, font=f2, fill=(150, 140, 126))
    return canvas


def main():
    print("=== 材质用量可读性实测 ===")
    combos = [
        ("羊皮纸底（浅）", "el_vellum.jpg", BASE_PAPER, False),
        ("羊毛毡底（深）", "el_wool_felt.jpg", BASE_DARK, True),
    ]
    levels = [
        ("① 原样铺（上一轮给的样子）", 1),
        ("② 弱化到能承文字", 2),
        ("③ 只做锚点（底素净）", 3),
    ]
    cols = []
    for ci, (cn, fn, base, dark) in enumerate(combos):
        rows = []
        for li, (ln, lv) in enumerate(levels):
            mat = load(fn, (PW, PH))
            p = panel(mat, base, dark, lv)
            # 量化：正文区（标题带以下、输入区以上）的噪声
            body = p.crop((0, HEAD, PW, PH - 90))
            sd, mn = stat(body)
            print("  [%s] %s -> 正文区 stddev=%.1f mean=%.1f" % (cn, ln, sd, mn))
            rows.append(tile(p, "%s　%s" % (cn, ln), "正文区噪声 stddev=%.1f" % sd, sd))
        cols.append(rows)

    W = PW * 3 + 30 * 2
    H = (PH + 54) * 2 + 40
    sheet = Image.new("RGB", (W, H), (10, 9, 8))
    for c in range(3):
        for r in range(2):
            sheet.paste(cols[r][c], (c * (PW + 30), r * (PH + 54 + 20)))
    d = ImageDraw.Draw(sheet)
    f = ImageFont.truetype(FONT, 22)
    d.text((6, H - 30), "同一块面板 · 材质三档用量 · 文字为游戏内 simkai 楷体 · 尺寸 600×400（真实 UI 尺度）",
           font=f, fill=(200, 190, 172))
    out = os.path.join(OUT, "ui_legibility_compare.png")
    sheet.save(out)
    print("saved:", out, sheet.size)
    # 另存单张（档2 档3）便于细看
    for r in range(2):
        cols[r][1].save(os.path.join(OUT, "level2_%s.png" % r))
        cols[r][2].save(os.path.join(OUT, "level3_%s.png" % r))
    print("saved singles")


if __name__ == "__main__":
    main()
