# -*- coding: utf-8 -*-
"""
轮廓返工对照：**两端收尖**的不同长度 —— 甲方：「那个形状就比我们设计的好看」。

原版实体实测 232×42（`_probe_shape.py`）：上下缘平直，只在两端各 6.9% 宽内收，
尖端高 6/42 = 0.143，收的过程接近直线。本项目按钮更矮（110×35 vs 折算 193×35），
所以收尖的**长度**得按眼挑 —— 这一页出三档 ＋ 旧矩形基准，同屏比。

产物：out/study/shape/taper_sheet.png ＋ taper_profile.txt
"""
import os

from PIL import Image, ImageDraw, ImageFont

import make_button_primary as mb

HERE = mb.HERE
OUT = os.path.join(HERE, "out", "study", "shape")
REF = os.path.normpath(os.path.join(HERE, "..", "awake-ui-lab", "out", "atlas",
                                    "sprites", "General__Button__main_button_regular.png"))
FONT = mb.FONT
PGC = (128, 128, 128)
PANEL = (26, 23, 21)


def on_bg(im, bg):
    c = Image.new("RGBA", im.size, bg + (255,))
    c.alpha_composite(im.convert("RGBA"), (0, 0))
    return c.convert("RGB")


def zoom(im, z, box=None):
    c = im.crop(box) if box else im
    return c.resize((c.width * z, c.height * z), Image.NEAREST)


def ref_body():
    """原版**实体**（去掉软光晕）缩到与本项目同高 35 ⇒ 193×35。"""
    im = Image.open(REF).convert("RGBA")
    a = im.split()[3].point(lambda v: 255 if v > 128 else 0)
    bb = a.getbbox()
    body = im.crop(bb)
    w = max(1, int(round(body.width * mb.H / float(body.height))))
    return body.resize((w, mb.H), Image.LANCZOS)


def silhouette(im):
    m = Image.new("RGBA", im.size, (0, 0, 0, 0))
    m.paste((236, 224, 200, 255), (0, 0), im.split()[3].point(lambda v: 255 if v > 110 else 0))
    return m


def main():
    os.makedirs(OUT, exist_ok=True)
    gold = mb.sample_gold(mb.load_plaque(mb.IRON_SRC))

    variants = [("基准·旧矩形（TIP_LEN 0）", 0.0, 0.0)]
    for tl in (8.0, 12.0, 16.0, 22.0):
        variants.append(("收尖 %g" % tl, tl, 0.16))
    print("[形] 出 %d 档" % len(variants))
    states = []
    for lbl, tl, th in variants:
        st = mb.build_states(gold, detail="none", tip_len=tl, tip_h=th)["soft"][""]
        states.append((lbl, st))
    for lbl, st in states:
        mb.write_sprite({"": st}, out_dir=OUT, tag="_taper%s" % lbl.split()[-1].strip("（）"))

    ref = ref_body()
    rows = [("原版实体 → h35", ref)] + states

    z, zz = 4, 8
    cw, ch = mb.W * z, mb.H * z
    zw, zh = 26 * zz, 35 * zz
    pad, gap = 36, 24
    n = len(rows)
    colw = max(cw, zw)
    f_t = ImageFont.truetype(FONT, 26)
    f_l = ImageFont.truetype(FONT, 16)
    f_s = ImageFont.truetype(FONT, 13)
    total_w = pad * 2 + colw * n + gap * (n - 1)
    total_h = pad + 58 + ch + 46 + mb.H + 34 + zh + 40 + mb.H * 2 + 250
    sh = Image.new("RGB", (total_w, total_h), (16, 14, 13))
    d = ImageDraw.Draw(sh)
    d.text((pad, pad), "轮廓返工 · 两端收尖（甲方：「那个形状就比我们设计的好看」）", font=f_t,
           fill=(214, 196, 160))
    d.text((pad, pad + 32),
           "原版实体＝232×42（去掉软光晕），缩到同高 35 ⇒ 193×35。"
           "它上下缘平直，只在两端各 6.9% 宽内**直线**收尖，尖端高 0.143。",
           font=f_s, fill=(140, 124, 104))

    y = pad + 58
    for i, (lbl, im) in enumerate(rows):
        x = pad + i * (colw + gap)
        sh.paste(on_bg(zoom(im, z), PANEL), (x, y))
        d.rectangle([x, y, x + cw, y + ch], outline=(60, 52, 44))
        d.text((x, y + ch + 6), lbl, font=f_l, fill=(226, 208, 172))
        sh.paste(on_bg(im, PGC), (x, y + ch + 28))

    y2 = y + ch + 46 + mb.H + 30
    d.text((pad, y2 - 22), "轮廓（只看形）×8", font=f_l, fill=(226, 208, 172))
    for i, (lbl, im) in enumerate(rows):
        sh.paste(on_bg(zoom(silhouette(im), zz), PGC), (pad + i * (colw + gap), y2))

    y3 = y2 + zh + 30
    d.text((pad, y3 - 22), "左端 ×8（看收尖怎么收）", font=f_l, fill=(226, 208, 172))
    for i, (lbl, im) in enumerate(rows):
        bw = min(26, im.width)
        sh.paste(on_bg(zoom(im, zz, (0, 0, bw, mb.H)), PANEL), (pad + i * (colw + gap), y3))

    y4 = y3 + zh + 26
    for j, ln in enumerate([
        "读法：① 收尖**不要把面吃光**（面还得留得下一片木）；② 尖端别太细（1× 下会掉成 1 个像素，就是噪点）；",
        "      ③ 左右上下必须**镜像**（对称是算出来的：end_taper_points 生成 8 个点，左右上下各一层镜像）。",
        "五金已**全关**（`DETAIL=none`）——甲方说像鼻涕，原版也确实一个亮块铆钉都没有。",
    ]):
        d.text((pad, y4 + j * 22), ln, font=f_s, fill=(150, 134, 112))

    p = os.path.join(OUT, "taper_sheet.png")
    sh.save(p)
    print("->", p, sh.size)


if __name__ == "__main__":
    main()
