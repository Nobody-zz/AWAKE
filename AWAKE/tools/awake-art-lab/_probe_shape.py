# -*- coding: utf-8 -*-
"""
把**原版的轮廓**量出来 —— 甲方：「重点是，那个形状就比我们设计的好看！」

我此前把甲方「几何对称」读成了「严格矩形」，那是**我自己加的码**：
原版两端收尖、四角是折线，**那也是几何对称的**。这一页只做一件事：
从原版 alpha 里把轮廓线取出来，变成可抄的坐标。

产物：
  out/study/shape/ref_silhouette.png   （原版 alpha ×3 ／ 缩到 h=35 ／ 我们现行轮廓）
  out/study/shape/shape_probe.txt      （逐列上下缘坐标，归一化后可直接抄进生成器）
"""
import os

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
SPRITES = os.path.normpath(os.path.join(HERE, "..", "awake-ui-lab", "out", "atlas", "sprites"))
OUT = os.path.join(HERE, "out", "study", "shape")
REF = os.path.join(SPRITES, "General__Button__main_button_regular.png")
FONT = r"C:\Windows\Fonts\simkai.ttf"

PGC = (128, 128, 128, 255)
DARK = (24, 21, 19, 255)


def edges(mask):
    """逐列取上下缘：返回 [(x, ytop, ybot)]，只含不透明列。"""
    w, h = mask.size
    px = mask.load()
    out = []
    for x in range(w):
        ys = [y for y in range(h) if px[x, y] > 128]
        if ys:
            out.append((x, ys[0], ys[-1]))
    return out


def main():
    os.makedirs(OUT, exist_ok=True)
    im = Image.open(REF).convert("RGBA")
    a = im.split()[3]
    w, h = im.size
    es = edges(a)
    x0, x1 = es[0][0], es[-1][0]
    ytop_min = min(e[1] for e in es)
    ybot_max = max(e[2] for e in es)
    bh = ybot_max - ytop_min + 1

    lines = []
    lines.append("原版 %s  %dx%d" % (os.path.basename(REF), w, h))
    lines.append("不透明 bbox: x %d..%d (宽 %d)  y %d..%d (高 %d)"
                 % (x0, x1, x1 - x0 + 1, ytop_min, ybot_max, bh))
    lines.append("")
    lines.append("逐列上下缘（每 4 列取一，坐标**相对 bbox 左上**）")
    lines.append("  i/W     x   ytop  ybot   高   ytop/W  ybot/W  (相对 bbox 高)")
    for k in range(0, len(es), 4):
        x, yt, yb = es[k]
        u = (x - x0) / float(x1 - x0)
        lines.append("  %5.3f  %4d  %4d  %4d  %4d   %5.3f  %5.3f   %.3f/%.3f"
                     % (u, x, yt - ytop_min, yb - ytop_min, yb - yt + 1,
                        (yt - ytop_min) / float(bh), (yb - ytop_min) / float(bh),
                        (yt - ytop_min) / float(bh), (yb - ytop_min) / float(bh)))

    # 归一化成 h=1 的轮廓（供抄进生成器）
    lines.append("")
    lines.append("=== 归一化轮廓（bbox 高 = 1，宽 = %d/%d = %.3f）===" % (x1 - x0 + 1, bh,
                                                          (x1 - x0 + 1) / float(bh)))
    lines.append("顶部曲线 (u, 顶缘 y)：")
    lines.append("  " + " ".join("(%.3f,%.3f)" % ((x - x0) / float(x1 - x0), (yt - ytop_min) / float(bh))
                                  for x, yt, yb in es[::max(1, len(es) // 16)]))
    lines.append("底部曲线 (u, 底缘 y)：")
    lines.append("  " + " ".join("(%.3f,%.3f)" % ((x - x0) / float(x1 - x0), (yb - ytop_min) / float(bh))
                                  for x, yt, yb in es[::max(1, len(es) // 16)]))

    # ---------- 图 ----------
    Z = 3
    body = im.crop((x0, ytop_min, x1 + 1, ybot_max + 1))
    sil = Image.new("RGBA", body.size, (0, 0, 0, 0))
    sil.paste((236, 224, 200, 255), (0, 0), body.split()[3])
    h35 = max(1, int(round(body.height * 35.0 / body.height)))  # 1:1 高度基准
    b35 = body.resize((int(round(body.width * 35.0 / body.height)), 35), Image.LANCZOS)
    s35 = Image.new("RGBA", b35.size, (0, 0, 0, 0))
    s35.paste((236, 224, 200, 255), (0, 0), b35.split()[3])

    ours = Image.open(os.path.join(HERE, "..", "..", "GUI", "SpriteParts",
                                   "ui_awake_button", "btn_primary_110.png")).convert("RGBA")
    osil = Image.new("RGBA", ours.size, (0, 0, 0, 0))
    osil.paste((236, 224, 200, 255), (0, 0), ours.split()[3])

    pad = 30
    tiles = [("原版实体 ×3", body, Z), ("原版轮廓 ×3", sil, Z),
             ("原版缩到 h=35", b35, 3), ("本项目轮廓 ×3", osil, 3)]
    tw = max(t[1].width * t[2] for t in tiles)
    th = max(t[1].height * t[2] for t in tiles)
    sh = Image.new("RGB", (pad * 2 + tw, pad + 40 + th + 60 + 200), (16, 14, 13))
    d = ImageDraw.Draw(sh)
    d.text((pad, pad), "原版的轮廓线（甲方：「那个形状就比我们设计的好看」）",
           font=ImageFont.truetype(FONT, 24), fill=(214, 196, 160))
    d.text((pad, pad + 30), "只取 alpha ⇒ 只看形，不看材质。下面是同一张图缩到 h=35、与我们的轮廓对照。",
           font=ImageFont.truetype(FONT, 13), fill=(140, 124, 104))
    y = pad + 56
    x = pad
    for lbl, img, z in tiles:
        t = img.resize((img.width * z, img.height * z), Image.NEAREST)
        bg = Image.new("RGBA", t.size, PGC)
        bg.alpha_composite(t.convert("RGBA"))
        sh.paste(bg.convert("RGB"), (x, y))
        d.text((x, y - 18), lbl, font=ImageFont.truetype(FONT, 15), fill=(226, 208, 172))
        x += t.width + 30
    y2 = y + th + 20
    for j, ln in enumerate([
        "读法：① 上下缘是不是**两头就往里收**（不是直边到角）② 四角是**折线**还是圆角 ③ 端头有没有一个**小钮**。",
        "归一化坐标见 out/study/shape/shape_probe.txt —— 直接用那组数字重建 shape，不要凭手感画。",
    ]):
        d.text((pad, y2 + j * 22), ln, font=ImageFont.truetype(FONT, 14), fill=(150, 134, 112))

    p = os.path.join(OUT, "ref_silhouette.png")
    sh.save(p)
    with open(os.path.join(OUT, "shape_probe.txt"), "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    print("->", p, sh.size)
    print("\n".join(lines[:8]))


if __name__ == "__main__":
    main()
