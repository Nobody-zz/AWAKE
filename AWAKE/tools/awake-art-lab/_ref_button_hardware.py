# -*- coding: utf-8 -*-
"""
参照研究：**原版主按钮**的五金/边框是怎么画的（只读，不改任何源文件）。

起因：甲方说「你这钉子的像素...看着不像骑砍的，倒向 minecraft 的」。
⇒ 先看一手源，别拿猜的。原版贴图缓存在 `awake-ui-lab/out/atlas/sprites/`（游戏 UI Lab 裁切缓存）。

关键一步：原版主按钮是 **271×84**，本项目按钮是 **110×35**。
把原版按高度缩到 35（⇒ 113×35，与本项目 110×35 几乎同宽），
**再看它在"我们的尺寸"上还剩什么** —— 这才是可比的语汇。

产物：out/study/ref_button_hardware.png
"""
import os

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.normpath(os.path.join(HERE, "..", "awake-ui-lab", "out", "atlas", "sprites"))
MINE = os.path.join(HERE, "..", "..", "GUI", "SpriteParts", "ui_awake_button",
                    "btn_primary_110.png")
OUT = os.path.join(HERE, "out", "study")

REF = "General__Button__main_button_regular.png"
REF2 = "General__Button__main_button_regular_hover.png"

FONT = r"C:\Windows\Fonts\simkai.ttf"
BG = (26, 22, 20, 255)          # AWAKE 面板暖黑：看"放到真实底色上什么样"

H_TARGET = 35                   # 对齐高度
Z = 4                           # 整颗放大
CZ = 10                         # 特写放大
BOX_CORNER = (0, 0, 24, 24)
BOX_TOP = (28, 0, 52, 13)


def on_bg(im, bg=BG):
    c = Image.new("RGBA", im.size, bg)
    c.alpha_composite(im.convert("RGBA"), (0, 0))
    return c.convert("RGB")


def fit_h(im, h):
    w = max(1, int(round(im.width * h / float(im.height))))
    return im.resize((w, h), Image.LANCZOS)


def zoom(im, z, box=None):
    if box:
        im = im.crop(box)
    return im.resize((im.width * z, im.height * z), Image.NEAREST)


def main():
    os.makedirs(OUT, exist_ok=True)
    f_t = ImageFont.truetype(FONT, 26)
    f_l = ImageFont.truetype(FONT, 18)
    f_s = ImageFont.truetype(FONT, 14)

    ref_raw = Image.open(os.path.join(SRC, REF)).convert("RGBA")
    ref = fit_h(ref_raw, H_TARGET)
    mine = Image.open(MINE).convert("RGBA")

    cols = [("原版 " + REF.replace("__", "／"), ref), ("本项目 btn_primary_110", mine)]

    pad = 40
    cw = max(ref.width, mine.width) * Z
    cch = max(BOX_CORNER[3], 1) * CZ
    cth = BOX_TOP[3] * CZ
    colw = cw
    sh = Image.new("RGB", (pad * 2 + colw * 2 + 40, pad + 76 + 20 + Z * H_TARGET + 34
                           + cch + 30 + cth + 46 + 150), (14, 12, 11))
    d = ImageDraw.Draw(sh)
    d.text((pad, pad), "原版主按钮 vs 本项目 · 五金／边框语汇对照", font=f_t, fill=(214, 196, 160))
    d.text((pad, pad + 40),
           "原版原始尺寸 %dx%d，本项目 110x35 —— 所以左边先按**高度缩到 35**（⇒ %dx%d）再看，才可比"
           % (ref_raw.width, ref_raw.height, ref.width, ref.height),
           font=f_s, fill=(140, 124, 104))

    y = pad + 76
    xs = [pad, pad + colw + 40]
    for i, (title, im) in enumerate(cols):
        x = xs[i]
        d.text((x, y), title, font=f_l, fill=(226, 208, 172))
        # 整颗（缩放到同高）＋ 原大
        big = zoom(im, Z)
        sh.paste(on_bg(big), (x, y + 22))
        d.rectangle([x, y + 22, x + big.width, y + 22 + big.height], outline=(58, 50, 42))
        d.text((x, y + 22 + big.height + 6), "×%d（%dx%d）" % (Z, im.width, im.height),
               font=f_s, fill=(120, 106, 90))

    yy = y + 22 + Z * H_TARGET + 34
    for i, (title, im) in enumerate(cols):
        x = xs[i]
        d.text((x, yy), "左上角 ×%d" % CZ, font=f_l, fill=(226, 208, 172))
        cz = zoom(im, CZ, BOX_CORNER)
        sh.paste(on_bg(cz), (x, yy + 22))
        d.rectangle([x, yy + 22, x + cz.width, yy + 22 + cz.height], outline=(58, 50, 42))

    yy2 = yy + 22 + cch + 30
    for i, (title, im) in enumerate(cols):
        x = xs[i]
        d.text((x, yy2), "上边中段 ×%d" % CZ, font=f_l, fill=(226, 208, 172))
        ct = zoom(im, CZ, BOX_TOP)
        sh.paste(on_bg(ct), (x, yy2 + 22))
        d.rectangle([x, yy2 + 22, x + ct.width, yy2 + 22 + ct.height], outline=(58, 50, 42))

    yf = yy2 + 22 + cth + 30
    d.text((pad, yf), "判读要点", font=f_l, fill=(226, 208, 172))
    for j, ln in enumerate([
        "① 原版没有「亮块铆钉」：边框上的细节是值域上的小起伏（暗一点／亮一点），不是一块实心色。",
        "② 原版的暗部压得住：亮只在最上沿很窄的一条，往下迅速落到很暗 —— 细节全在明暗过渡里，不在亮点里。",
        "③ 原版缩到 35 高之后，小结构主动消失（糊成一段渐变），不是硬撑成一个方块 —— 这是「画大再缩」的手感。",
        "④ ⇒ 判定：本项目原先把五金做成「实心亮块 ＋ 硬边 ＋ 等距重复」＝体素语言，已改为**软凸起**。",
        "　 （改法见 `sheet_detail_study.png`：往一个色混＋掩膜过高斯＋迎光心上移半像素。）",
        "⑤ **待议**：原版的「细节」其实是**连续线脚**，不是离散点。若要做到最像，",
        "　 应把角上的凸起换成一圈连续的线脚（或把外框做成两段剖面），而不是继续摆珠子。",
    ]):
        d.text((pad, yf + 32 + j * 22), ln, font=f_s, fill=(150, 134, 112))

    p = os.path.join(OUT, "ref_button_hardware.png")
    sh.save(p)
    print("->", p, sh.size)


if __name__ == "__main__":
    main()
