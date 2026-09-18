# -*- coding: utf-8 -*-
"""
Button.Primary 五金细节对照 —— 两张页。

甲方原话：
  「我还是说，按钮控件的四个角，包括上下左右四边都可以用铆钉或者尖刺做一点细节，
    保持对称，但不单调」

四档：
  none   无细节（参照）
  studs  四角大钉 ＋ 四边小钉
  spikes 内缘锯齿一圈
  mixed  四角大钉 ＋ 四边尖刺   ← 当前默认

产出：
  out/button-primary/sheet_detail_study.png   候选对照页（×4 整颗 ＋ 实尺 ＋ 一排 4 颗）
  out/button-primary/zoom_detail_study.png    ×9 特写页（角／上边中段／下边中段）——验像素用

只复用 make_button_primary 的骨与材质，不改那边的任何常量。
"""
import os

from PIL import Image, ImageDraw, ImageFont, ImageChops, ImageOps

import make_button_primary as M

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "out", "button-primary")

W, H = M.W, M.H
ZOOM = 4
CW, CH = W * ZOOM, H * ZOOM
GRP_N, GAP = 4, 12
GW = GRP_N * W + (GRP_N - 1) * GAP + 24

Z = 9
BOX_CORNER = (0, 0, 18, 16)          # 左上角：角钉（＋相领的一枚齿）
BOX_TOP = (39, 0, 67, 12)            # 上边中段：亮侧（x=44 / 65 那两枚）
BOX_BOT = (39, 23, 67, 35)           # 下边中段：暗侧

MODES = [
    ("none",   "无细节（参照）", "上一版的「几何零细节」——干净，但读不出「有人做过它」"),
    ("studs",  "四角高一枚 ＋ 四边各两枚", "软凸起（值域起伏，不是实心色块）。角是主、边是从。当前默认"),
    ("spikes", "内缘软锯齿一圈", "备选：齿同样走软掩膜，不再硬三角"),
]

BG = (14, 12, 11)
BOXBG = (28, 24, 22)
EDGE = (60, 52, 44)


def group_row(state, n=GRP_N, gap=GAP):
    w = n * W + (n - 1) * gap
    row = Image.new("RGBA", (w + 24, H + 24), BOXBG + (255,))
    for i in range(n):
        row.alpha_composite(state, (12 + i * (W + gap), 12))
    return row.convert("RGB")


def solo(state):
    box = Image.new("RGBA", (W + 24, H + 24), BOXBG + (255,))
    box.alpha_composite(state, (12, 12))
    return box.convert("RGB")


def crop_zoom(state, box, z):
    cv = state.crop(box).resize(((box[2] - box[0]) * z, (box[3] - box[1]) * z), Image.NEAREST)
    canvas = Image.new("RGBA", (cv.width + 2, cv.height + 2), EDGE + (255,))
    canvas.alpha_composite(cv, (1, 1))
    return canvas.convert("RGB")


def mirror_delta(im):
    """左右镜像平均差（0–255）。木纹本身是随机的，所以不会到 0；加了对称五金后不该升高。"""
    a = im.convert("L")
    d = ImageChops.difference(a, ImageOps.mirror(a))
    data = list(d.getdata())
    return sum(data) / float(len(data))


def render(blocks):
    f_t = ImageFont.truetype(M.FONT, 27)
    f_l = ImageFont.truetype(M.FONT, 20)
    f_n = ImageFont.truetype(M.FONT, 15)
    f_s = ImageFont.truetype(M.FONT, 14)
    return f_t, f_l, f_n, f_s


def build_sheet(blocks):
    f_t, f_l, f_n, f_s = render(blocks)
    pad = 40
    x4 = pad
    xr = pad + CW + 40
    sw = xr + max(GW, W + 24) + pad
    blk = 20 + CH + 34 + 36
    sh = Image.new("RGB", (sw, pad + 80 + len(blocks) * blk + 300), BG)
    d = ImageDraw.Draw(sh)
    d.text((pad, pad), "Awake.Button.Primary · 五金细节对照 · 四角／四边", font=f_t,
           fill=(214, 196, 160))
    d.text((pad, pad + 44),
           "点位靠镜像算（x ↔ w-x、y ↔ h-y）取四向并集；细节全落在外框那一条上，轮廓仍是严格矩形；"
           "五金在缩回 1× 之后才画",
           font=f_s, fill=(140, 124, 104))

    y = pad + 80
    for mode, title, note, st in blocks:
        d.text((pad, y), title, font=f_l, fill=(226, 208, 172))
        d.text((pad, y + 24), note, font=f_n, fill=(146, 130, 108))
        yt = y + 48

        sh.paste(st[""].resize((CW, CH), Image.NEAREST), (x4, yt))
        d.rectangle([x4, yt, x4 + CW, yt + CH], outline=(58, 50, 42))
        d.text((x4, yt + CH + 6), "整颗 ×%d（NEAREST）　DETAIL = \"%s\"" % (ZOOM, mode),
               font=f_s, fill=(120, 106, 90))

        d.text((xr, yt - 18), "实尺单颗", font=f_s, fill=(146, 130, 108))
        sh.paste(solo(st[""]), (xr, yt))
        sh.paste(group_row(st[""]), (xr, yt + H + 24 + 22))
        d.text((xr, yt + H + 24 + 4), "一排 %d 颗（实尺）" % GRP_N, font=f_s, fill=(146, 130, 108))

        y += blk

    yf = y + 6
    d.text((pad, yf), "参量", font=f_l, fill=(226, 208, 172))
    for j, ln in enumerate([
        "凸起：角 r=%.1f、边 r=%.1f（1× 像素，圆不是菱形）；落影圈再往外 %.1f。"
        % (M.STUD_CORNER_R, M.STUD_EDGE_R, M.STUD_RING),
        "点位：长边 x = 37／72（间距 35，匀）；短边正中一枚。",
        "⛔ 语汇红线：不能是「实心亮块＋硬边＋等距重复」——那是体素语言（甲方指出）。",
        "一手核过原版主按钮 271×84：一个亮块铆钉都没有 —— 细节全在轮廓剖面与值域起伏里。",
        "它按高度缩到 35 之后，小结构是主动糊成渐变的，不是硬撑成方块。",
        "所以五金＝往一个色混（不是填实色，底下材质还在）＋掩膜过高斯（软边）＋低对比。",
        "掩膜半径 %.2f（1× 像素）—— 这一条就是「不像 minecraft」的关键。" % M.STUD_SOFT,
        "迎光 %.2f 往 (198,192,182) 混、落影 %.2f 往 (20,16,14) 混；"
        % (M.STUD_LIGHT_A, M.STUD_DARK_A),
        "上下底色相反 ⇒ 亮侧读「影圈」、暗侧读「亮心」，同一枚钉两边都成立。",
        "迎光心比落影圈**上移** %.1f 像素 —— 这一移才读成圆包，不移就是贴片。"
        % M.STUD_HILITE,
    ]):
        d.text((pad, yf + 34 + j * 22), ln, font=f_s, fill=(150, 134, 112))

    path = os.path.join(OUT, "sheet_detail_study.png")
    sh.save(path)
    print("  -> %s  %dx%d" % (path, sh.width, sh.height))


def build_zoom(blocks):
    f_t, f_l, f_n, f_s = render(blocks)
    pad = 40
    x0 = pad + 150
    cz = crop_zoom(blocks[0][3][""], BOX_CORNER, Z)
    tz = crop_zoom(blocks[0][3][""], BOX_TOP, Z)
    bz = crop_zoom(blocks[0][3][""], BOX_BOT, Z)
    rowh = max(cz.height, tz.height + bz.height + 8)
    sw = x0 + cz.width + tz.width + pad
    sh = Image.new("RGB", (sw, pad + 64 + len(blocks) * (rowh + 34) + 40), BG)
    d = ImageDraw.Draw(sh)
    d.text((pad, pad), "五金特写 ×%d —— 角／上边中段（亮侧）／下边中段（暗侧）" % Z, font=f_t,
           fill=(214, 196, 160))
    d.text((pad, pad + 40), "每一格都是 NEAREST 放大，没有平滑",
           font=f_s, fill=(140, 124, 104))

    y = pad + 64
    for mode, title, note, st in blocks:
        cz = crop_zoom(st[""], BOX_CORNER, Z)
        tz = crop_zoom(st[""], BOX_TOP, Z)
        bz = crop_zoom(st[""], BOX_BOT, Z)
        d.text((pad, y + 40), title, font=f_l, fill=(226, 208, 172))
        d.text((pad, y + 66), "DETAIL = \"%s\"" % mode, font=f_s, fill=(120, 106, 90))
        sh.paste(cz, (x0, y + 24))
        sh.paste(tz, (x0 + cz.width, y + 24))
        sh.paste(bz, (x0 + cz.width, y + 24 + tz.height + 8))
        d.text((x0, y + 4), "左上角", font=f_s, fill=(146, 130, 108))
        d.text((x0 + cz.width + 6, y + 4), "上边中段（亮侧）", font=f_s, fill=(146, 130, 108))
        d.text((x0 + cz.width + 6, y + 28 + tz.height), "下边中段（暗侧）",
               font=f_s, fill=(146, 130, 108))
        y += rowh + 34

    path = os.path.join(OUT, "zoom_detail_study.png")
    sh.save(path)
    print("  -> %s  %dx%d" % (path, sh.width, sh.height))


def main():
    os.makedirs(OUT, exist_ok=True)
    gold = M.sample_gold(M.load_plaque(M.IRON_SRC))
    blocks = []
    for mode, title, note in MODES:
        print("[%s]" % mode)
        st = M.build_states(gold, detail=mode)["soft"]
        print("  镜像差 %.2f" % mirror_delta(st[""]))
        blocks.append((mode, title, note, st))
    build_sheet(blocks)
    build_zoom(blocks)


if __name__ == "__main__":
    main()
