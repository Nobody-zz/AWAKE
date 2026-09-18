# -*- coding: utf-8 -*-
"""**形状方案对照页** —— 甲方：「重点是，那个形状就比我们设计的好看！」

把原版端头放大 ×8 看（`_probe_end.py`），"那个形"其实是三件事叠起来的：
  ① 外轮廓＝**八边形**（上下缘平直 + 两端约 45° 斜切 + 一条很短的竖立面）
  ② **端头那块铁件比长边轨厚得多**（原版长边轨 12% 高，端头铸件纵深 55% 高）
     ⇒ 原版自己就**不是**"四面等宽外框"。我写的"四面等宽＝几何对称"是我自己加的码。
  ③ 铸件里一道**拱**（木面的端头是半圆头，嵌进拱里）＋ 拱外侧一个**圆孔**

这一页把这三件事拆成可选的档，同屏比：
  旧矩形 / 只收尖 / 收尖＋端头铸件 / ＋轴上一孔 / ＋上下两孔

产物：out/study/shape/shape_sheet.png
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

# (标签, tip_len, tip_h, end_bracket, holes)
VARIANTS = [
    ("旧矩形 TIP 0",        0.0,  0.0,  False, "none"),
    ("收尖 12",             12.0, 0.16, False, "none"),
    ("收尖 12 ＋端头铸件",  12.0, 0.16, True,  "none"),
    ("＋轴上一孔",          12.0, 0.16, True,  "axis"),
    ("＋上下两孔",          12.0, 0.16, True,  "pair"),
]


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
    body = im.crop(a.getbbox())
    w = max(1, int(round(body.width * mb.H / float(body.height))))
    return body.resize((w, mb.H), Image.LANCZOS)


def silhouette(im):
    m = Image.new("RGBA", im.size, (0, 0, 0, 0))
    m.paste((236, 224, 200, 255), (0, 0), im.split()[3].point(lambda v: 255 if v > 110 else 0))
    return m


def main():
    os.makedirs(OUT, exist_ok=True)
    gold = mb.sample_gold(mb.load_plaque(mb.IRON_SRC))

    states = []
    for lbl, tl, th, br, ho in VARIANTS:
        print("[形] %s" % lbl)
        st = mb.build_states(gold, detail="none", tip_len=tl, tip_h=th,
                             end_bracket=br, holes=ho)["soft"][""]
        states.append((lbl, st))
        mb.write_sprite({"": st}, out_dir=OUT, tag="_shape_%s" % lbl.replace(" ", "").replace("＋", "+"))

    ref = ref_body()
    rows = [("原版实体 → h35", ref)] + states

    z, zz = 4, 8
    cw, ch = mb.W * z, mb.H * z
    zw = 30 * zz
    zh = mb.H * zz
    pad, gap = 36, 22
    colw = max(cw, zw)
    f_t = ImageFont.truetype(FONT, 26)
    f_l = ImageFont.truetype(FONT, 15)
    f_s = ImageFont.truetype(FONT, 13)
    n = len(rows)
    total_w = pad * 2 + colw * n + gap * (n - 1)
    total_h = pad + 92 + ch + 40 + mb.H + 34 + zh + 40 + zh + 40 + 300
    sh = Image.new("RGB", (total_w, total_h), (16, 14, 13))
    d = ImageDraw.Draw(sh)
    d.text((pad, pad), "形状方案对照 · 甲方：「重点是，那个形状就比我们设计的好看！」",
           font=f_t, fill=(214, 196, 160))
    for j, ln in enumerate([
        "把原版端头放大 ×8（_ref_end.png）看，「那个形」＝ ① 八边形外轮廓　② **端头铁件比长边轨厚得多**（原版长边轨占 12% 高，"
        "端头铸件纵深占 55% 高）",
        "　　⇒ **原版自己就不是「四面等宽外框」**　③ 铸件里一道拱（木面端头是半圆头）＋ 拱外侧一个圆孔。",
        "本页＝把这三点拆成档同屏比。轮廓左右上下都严格镜像（end_taper_points 出 8 点，四向对称是算出来的）。",
    ]):
        d.text((pad, pad + 34 + j * 19), ln, font=f_s, fill=(140, 124, 104))

    y = pad + 92
    for i, (lbl, im) in enumerate(rows):
        x = pad + i * (colw + gap)
        sh.paste(on_bg(zoom(im, z), PANEL), (x, y))
        d.rectangle([x, y, x + cw, y + ch], outline=(60, 52, 44))
        d.text((x, y + ch + 6), lbl, font=f_l, fill=(226, 208, 172))
        sh.paste(on_bg(im, PGC), (x, y + ch + 26))

    y2 = y + ch + 42 + mb.H + 28
    d.text((pad, y2 - 22), "轮廓（只看形）×8", font=f_l, fill=(226, 208, 172))
    for i, (lbl, im) in enumerate(rows):
        sh.paste(on_bg(zoom(silhouette(im), zz), PGC), (pad + i * (colw + gap), y2))

    y3 = y2 + zh + 38
    d.text((pad, y3 - 22), "左端 ×8（看端头那件东西）", font=f_l, fill=(226, 208, 172))
    for i, (lbl, im) in enumerate(rows):
        bw = min(42, im.width)
        sh.paste(on_bg(zoom(im, zz, (0, 0, bw, mb.H)), PANEL), (pad + i * (colw + gap), y3))

    y4 = y3 + zh + 34
    for j, ln in enumerate([
        "读法：① 端头铸件**深了会吃掉木面**（110 宽里两侧共 39px ⇒ 木面只剩 71px），但不是问题——原版也是这么干的；",
        "      ② 圆孔**必须软边**，硬边读成「钻头打的」；③ 全长必须左右镜像，孔也要（本项目口径：几何对称）。",
        "⚠️ 工程：端头铸件纵深 36.5px ⇒ 横向若 StretchToParent，`ExtendLeft/Right` 必须 ≥ 37（110 里只剩 36 可拉）。纵向只需 ≥ 9。",
        "五金（铆钉/尖刺）**全关** —— 甲方：「你这个铆钉看着就是一坨鼻涕滴在上面」。原版也确实一个亮块铆钉都没有。",
    ]):
        d.text((pad, y4 + j * 22), ln, font=f_s, fill=(150, 134, 112))

    p = os.path.join(OUT, "shape_sheet.png")
    sh.save(p)
    print("->", p, sh.size)


if __name__ == "__main__":
    main()
