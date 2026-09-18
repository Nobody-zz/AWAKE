# -*- coding: utf-8 -*-
"""
"原版的细节也处理得更好" —— 把两边放到**同一屏面积、同一相对位置**上比。

关键：原版 271×84，本项目 110×35，两者**不是同一个尺寸的东西**。
直接并排会变成"大图比小图"。所以这里的做法是——
**同一个相对区域（占按钮高度的同一个比例）× 不同的放大倍数 ⇒ 输出同一个屏幕尺寸**。
这样差出来的就只剩「画法」，没有「尺寸」的干扰。

区域（都按高度取同一比例）：
  端部／左上角：原版 (0,0,60,84)＝高度的 71%   ×5  → 300×420
                本版 (0,0,25,35)＝高度的 71%   ×12 → 300×420
  上边中段    ：原版 (100,0,160,42)            ×5  → 300×210
                本版 (41,0,66,18)              ×12 → 300×216
  面板内      ：原版 (110,30,160,60)           ×6  → 300×180
                本版 (45,12,65,25)             ×15 → 300×195

产物：out/study/ref_detail_same_area.png
"""
import os

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.normpath(os.path.join(HERE, "..", "awake-ui-lab", "out", "atlas", "sprites"))
MINE = os.path.normpath(os.path.join(HERE, "..", "..", "GUI", "SpriteParts",
                                     "ui_awake_button", "btn_primary_110.png"))
OUT = os.path.join(HERE, "out", "study")

PGC = (128, 128, 128, 255)      # 中性灰底：两边都不占便宜
FONT = r"C:\Windows\Fonts\simkai.ttf"
BOX_W = 300                     # 两列都输出到这个宽度 ⇒ 每屏像素代表的"按钮高度比例"一致

PAIRS = [
    ("端部／左上角（高的 71%）", (0, 0, 60, 84), 5, (0, 0, 25, 35), 12),
    ("上边中段", (100, 0, 160, 42), 5, (41, 0, 66, 18), 12),
    ("面板内", (110, 30, 160, 60), 6, (45, 12, 65, 25), 15),
]


def on_bg(im):
    c = Image.new("RGBA", im.size, PGC)
    c.alpha_composite(im.convert("RGBA"), (0, 0))
    return c.convert("RGB")


def zoom(im, box, z):
    return im.crop(box).resize(((box[2] - box[0]) * z, (box[3] - box[1]) * z), Image.NEAREST)


def whole(im, z):
    return im.resize((im.width * z, im.height * z), Image.NEAREST)


def main():
    os.makedirs(OUT, exist_ok=True)
    f_t = ImageFont.truetype(FONT, 26)
    f_l = ImageFont.truetype(FONT, 18)
    f_s = ImageFont.truetype(FONT, 13)

    ref = Image.open(os.path.join(SRC, "General__Button__main_button_regular.png")).convert("RGBA")
    mine = Image.open(MINE).convert("RGBA")

    ctx_r = whole(ref, 2)       # 542×168
    ctx_m = whole(mine, 5)      # 550×175
    ctx_h = max(ctx_r.height, ctx_m.height)

    rows = []
    for title, rb, rz, mb, mz in PAIRS:
        a, b = zoom(ref, rb, rz), zoom(mine, mb, mz)
        rows.append((title, a, b, rb, rz, mb, mz))

    pad = 40
    gapx = 56
    sh = Image.new("RGB", (pad * 2 + BOX_W * 2 + gapx,
                           pad + 84 + ctx_h + 30
                           + sum(max(a.height, b.height) + 46 for _, a, b, *_ in rows)
                           + 170), (18, 17, 16))
    d = ImageDraw.Draw(sh)
    d.text((pad, pad), "原版 vs 本项目 · 同一屏面积、同一相对位置比「细节处理」", font=f_t,
           fill=(214, 196, 160))
    d.text((pad, pad + 40),
           "原版 271×84／本版 110×35 —— 不是同一尺寸的东西，所以先按**同一相对区域**裁，再放大到同一屏宽",
           font=f_s, fill=(140, 124, 104))

    xr, xm = pad, pad + BOX_W + gapx
    y = pad + 84
    sh.paste(on_bg(ctx_r), (xr, y))
    sh.paste(on_bg(ctx_m), (xm, y))
    d.text((xr, y - 20), "原版 ×2（%dx%d）" % (ref.width, ref.height), font=f_l, fill=(226, 208, 172))
    d.text((xm, y - 20), "本版 ×5（%dx%d）" % (mine.width, mine.height), font=f_l, fill=(226, 208, 172))
    y += ctx_h + 30

    for title, a, b, rb, rz, mb, mz in rows:
        d.text((pad, y), title, font=f_l, fill=(226, 208, 172))
        d.text((xr, y + 24), "原版 %s ×%d" % (str(rb), rz), font=f_s, fill=(146, 130, 108))
        d.text((xm, y + 24), "本版 %s ×%d" % (str(mb), mz), font=f_s, fill=(146, 130, 108))
        sh.paste(on_bg(a), (xr, y + 44))
        sh.paste(on_bg(b), (xm, y + 44))
        y += max(a.height, b.height) + 46

    d.text((pad, y + 4), "判读要点", font=f_l, fill=(226, 208, 172))
    for j, ln in enumerate([
        "① 原版每一道「线」其实是 **2~3 行的台阶**（暗行＋亮行相邻）＝ 一个有剖面的沟／台；",
        "　 本项目是 **1 行单线**（金线 1px、倒角 1px），1px 没有做剖面的余地。",
        "② 原版的台阶**沿着轮廓走**，到端部随着轮廓收窄而收窄 ⇒ 是「形体」，不是「贴纸」。",
        "③ 原版的小件（端部那枚钮）是**剖面的一部分**，不是盖在上面的；所以它读成「一个东西」。",
        "④ 原版是**画大的、缩下来的**：缩到 35 高时糊掉的是最细的一层，留下的台阶还在。",
        "⑤ 本项目是**直接画小的**：每一笔都必须是 1px，于是要么看不见，要么读成方块。",
    ]):
        d.text((pad, y + 36 + j * 22), ln, font=f_s, fill=(150, 134, 112))

    p = os.path.join(OUT, "ref_detail_same_area.png")
    sh.save(p)
    print("->", p, sh.size)


if __name__ == "__main__":
    main()
