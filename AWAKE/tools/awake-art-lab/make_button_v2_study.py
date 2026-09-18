# -*- coding: utf-8 -*-
"""
v2 对照页：**照原版语汇重做的骨** vs 原版 vs 当前落盘版。

动机（2026-09-14 02:2x）：甲方拍「按原版语汇重做」。重做的三条＝
  ① 框放宽到占高 1/4 上下   ② 线改成 2~3 行的剖面   ③ 面提到中间调
先在研究目录出图对照，**不动已落盘的 SpriteParts**（那个要等看过再换）。

产物：out/study/v2/sheet_v2_compare.png ＋ out/study/v2/profile_v2.txt
"""
import os
import shutil

from PIL import Image, ImageDraw, ImageFont

import make_button_primary as mb

HERE = mb.HERE
STUDY = os.path.join(HERE, "out", "study")
V2 = os.path.join(STUDY, "v2")
PREV = os.path.join(V2, "_prev")
FONT = mb.FONT

PGC = (128, 128, 128)          # 中性灰底（看形）
PANEL = (26, 23, 21)           # 对话面板的深底（看立不立得住）

REF = os.path.join(os.path.normpath(os.path.join(
    HERE, "..", "awake-ui-lab", "out", "atlas", "sprites")),
    "General__Button__main_button_regular.png")


def fit_h(im, h):
    w = max(1, int(round(im.width * h / float(im.height))))
    return im.resize((w, h), Image.LANCZOS)


def on_bg(im, bg):
    c = Image.new("RGBA", im.size, bg)
    c.alpha_composite(im.convert("RGBA"), (0, 0))
    return c.convert("RGB")


def zoom(im, z, box=None):
    c = im.crop(box) if box else im
    return c.resize((c.width * z, c.height * z), Image.NEAREST)


def profile(im, tag):
    """中央 60% 宽的纵向剖面的平均亮度（1× 逐行）—— 拿去和原版对表。"""
    w, h = im.size
    x0, x1 = int(w * 0.20), int(w * 0.80)
    rows = []
    for y in range(h):
        acc, n = 0.0, 0
        for x in range(x0, x1):
            p = im.getpixel((x, y))
            if p[3] > 0:
                acc += 0.2126 * p[0] + 0.7152 * p[1] + 0.0722 * p[2]
                n += 1
        rows.append(acc / n if n else -1.0)
    return tag, rows


def fmt(tag, rows):
    return "%-22s " % tag + " ".join(("%3d" % round(v)) if v >= 0 else "  ." for v in rows)


def main():
    os.makedirs(V2, exist_ok=True)
    # 先备份当前落盘版（对照要用它，别被后面的重跑覆盖）
    os.makedirs(PREV, exist_ok=True)
    for suf in ("", "_hover", "_pressed", "_disabled"):
        src = os.path.join(mb.OUT_BTN, "btn_primary_110%s.png" % suf)
        if os.path.exists(src):
            shutil.copy2(src, os.path.join(PREV, "btn_primary_110%s.png" % suf))

    gold = mb.sample_gold(mb.load_plaque(mb.IRON_SRC))
    print("[v2] A 黑边陡棱 · 无五金")
    a = mb.build_states(gold, detail="none", frame_l=mb.FRAME_L_EDGE)["soft"]
    print("[v2] B 亮铁缓坡 · 无五金")
    b = mb.build_states(gold, detail="none", frame_l=mb.FRAME_L_BEVEL)["soft"]
    print("[v2] A 黑边陡棱 · studs")
    bs = mb.build_states(gold, detail="studs", frame_l=mb.FRAME_L_EDGE)["soft"]
    mb.write_sprite(a, out_dir=V2, tag="_v2A")
    mb.write_sprite(b, out_dir=V2, tag="_v2B")
    mb.write_sprite(bs, out_dir=V2, tag="_v2Bstuds")

    ref = fit_h(Image.open(REF).convert("RGBA"), mb.H)
    cur = Image.open(os.path.join(mb.OUT_BTN, "btn_primary_110.png")).convert("RGBA")

    items = [("原版 main_button_regular → h35", ref),
             ("① 当前落盘（框 4px／面平色）", cur),
             ("② v2·B 亮铁缓坡", b[""]),
             ("③ v2·A 黑边陡棱", a[""]),
             ("④ v2·A ＋ 五金 studs", bs[""])]

    # ---- 剖面数字 ----
    lines = []
    for tag, im in items:
        t, rows = profile(im, tag)
        lines.append(fmt(t, rows))
    lines.append("")
    lines.append("（四态 · A 档）")
    for suf, nm in (("_hover", "hover"), ("_pressed", "pressed"), ("_disabled", "disabled")):
        t, rows = profile(a[suf], "A " + nm)
        lines.append(fmt(t, rows))
    with open(os.path.join(V2, "profile_v2.txt"), "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")

    # ---- 对照页 ----
    pad, gap, z, zz = 40, 26, 4, 10
    cw, ch = mb.W * z, mb.H * z
    zw, zh = 30 * zz, 20 * zz          # 左上角切片
    f_t = ImageFont.truetype(FONT, 26)
    f_l = ImageFont.truetype(FONT, 17)
    f_s = ImageFont.truetype(FONT, 13)

    colw = max(cw, zw)
    n = len(items)
    total_w = pad * 2 + colw * n + gap * (n - 1)
    total_h = pad + 60 + ch + 50 + mb.H + 40 + zh + 34 + mb.H * zz + 230
    sh = Image.new("RGB", (total_w, total_h), (16, 14, 13))
    d = ImageDraw.Draw(sh)
    d.text((pad, pad), "Awake.Button.Primary · v2 返工对照（照原版语汇：框 8px／剖面／面拱）",
           font=f_t, fill=(214, 196, 160))
    d.text((pad, pad + 34),
           "原版＝main_button_regular 按高缩到 35（113×35，与本项目同尺寸）。"
           "v2 只改「预算」：FRAME 4→8、删最上沿镜面带、木面 32→96 并改拱形。",
           font=f_s, fill=(140, 124, 104))

    y = pad + 60
    for i, (lbl, im) in enumerate(items):
        x = pad + i * (colw + gap)
        sh.paste(on_bg(zoom(im, z), PANEL), (x, y))
        d.rectangle([x, y, x + cw, y + ch], outline=(60, 52, 44))
        d.text((x, y + ch + 8), lbl, font=f_l, fill=(226, 208, 172))
        d.text((x, y + ch + 30), "实尺 ↓", font=f_s, fill=(120, 106, 90))
        sh.paste(on_bg(im, PGC), (x, y + ch + 50))

    # 左上角 ×10
    y2 = y + ch + 50 + mb.H + 32
    d.text((pad, y2 - 24), "左上角 ×10", font=f_l, fill=(226, 208, 172))
    for i, (lbl, im) in enumerate(items):
        x = pad + i * (colw + gap)
        sh.paste(on_bg(zoom(im, zz, (0, 0, 30, 20)), PGC), (x, y2))

    # 上边中段 ×10
    y3 = y2 + zh + 30
    d.text((pad, y3 - 24), "上边中段 ×10", font=f_l, fill=(226, 208, 172))
    for i, (lbl, im) in enumerate(items):
        x = pad + i * (colw + gap)
        bw = min(30, mb.W - 41)
        sh.paste(on_bg(zoom(im, zz, (41, 0, 41 + bw, 16)), PGC), (x, y3))

    # 判读
    y4 = y3 + mb.H * zz + 28
    for j, ln in enumerate([
        "读法 ① 看「最外侧那一行是亮还是暗」：原版最外 4 行是 0（暗轮廓）；① 当前的最外一行是 184（白线）——凸起感是反的。",
        "读法 ② 看「面」有没有起伏：原版面 50→75→67（拱，行号 9~25）；① 当前是 23~27 平到底（动态范围 4 级）。",
        "读法 ③ 看箍：原版是「暗→渐亮→亮棱→回落」的剖面（亮棱 88，只比面峰 75 高一点）；① 当前是一块平亮色 ＋ 一条 1px 倒角线。",
        "② 与 ③ 是同一条语汇的两个**坡形**：② 亮铁缓坡＝箍是一条连续斜面，金属感最清楚；③ 黑边陡棱＝外缘 5px 近黑、亮棱 1~2px 内骤起（原版原样）。",
        "④ 是 ③ 加五金 studs，只作对照——原版一个亮块铆钉都没有，它的细节全在剖面与值域起伏上。",
    ]):
        d.text((pad, y4 + j * 24), ln, font=f_s, fill=(150, 134, 112))

    p = os.path.join(V2, "sheet_v2_compare.png")
    sh.save(p)
    print("->", p, sh.size)
    print(open(os.path.join(V2, "profile_v2.txt"), encoding="utf-8").read())


if __name__ == "__main__":
    main()
