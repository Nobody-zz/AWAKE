# -*- coding: utf-8 -*-
"""
形制研究 · 按钮"方不方"：同一副材质与同一套光，**只换轮廓**，四款并排。

为什么先做这个再出图：
  材质调了九轮，形却一直是最标准的矩形（只有 1.5px 圆角）。"不方"要动的是
  **轮廓线本身**，而不是把角磨圆——甲方此前明确过「圆润的没有棱角就离谱了，
  我希望按钮的四个角比较突出」，所以圆角这条路是关着的。

四款（都守住"四角要立"）：
  A 现行对照   —— 矩形，角上只留 1.5 的抗锯齿斜切
  B 端切角     —— 左右两端各斜切 6.5，中间仍是直边 => 读成"木牌／条盾"，
                  四角从"一个点"变成"一条折线"，反而更立
  C 鼓形       —— 上下缘中段外扩 2.0（两端收），读成"一块绷起来的板"；
                  注意：中段高度变了，纵拉伸时中段会先被拉平，得同步改九宫格
  D 八角       —— 四角各斜切 3.5，其余直边，最克制，像锻出来的铁件

A/B/C/D 四款的**铁箍宽度、缝位、铆钉、木铁肌理、光泽分工、克制金线全同**，
所以看到差异就是形状的差异。

产物：out/button-shape-study/sheet_shape_study.png
（本脚本只出研究图，**不落盘任何交付 sprite**；定了形再改 make_button_primary.py 的轮廓）
"""
import os
import sys

from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import make_button_primary as M          # 复用取材／材质／金值与全部调好的常量

OUT = os.path.join(HERE, "out", "button-shape-study")
W, H, SS = M.W, M.H, M.SS
FRAME, SEAM, GOLD_INSET, RIVET_R = M.FRAME, M.SEAM, M.GOLD_INSET, M.RIVET_R


# ------------------------------------------------------------------ 轮廓

def poly_mask(pts, size, ss):
    """凸多边形 -> L 掩膜（点按 1× 给）。"""
    m = Image.new("L", size, 0)
    ImageDraw.Draw(m).polygon([(x * ss, y * ss) for x, y in pts], fill=255)
    return m


def offset_poly(pts, d):
    """凸多边形每条边沿**外**法线内推 d，顶点＝相邻两条偏移边的交点。"""
    n = len(pts)
    cx = sum(p[0] for p in pts) / n
    cy = sum(p[1] for p in pts) / n
    lines = []
    for i in range(n):
        x0, y0 = pts[i]
        x1, y1 = pts[(i + 1) % n]
        ex, ey = x1 - x0, y1 - y0
        L = max(1e-9, (ex * ex + ey * ey) ** 0.5)
        nx, ny = ey / L, -ex / L
        mx, my = (x0 + x1) * 0.5, (y0 + y1) * 0.5
        if (mx - cx) * nx + (my - cy) * ny < 0:      # 保证指向外
            nx, ny = -nx, -ny
        lines.append((x0 - nx * d, y0 - ny * d, nx, ny))

    out = []
    for i in range(n):
        ax, ay, anx, any_ = lines[i - 1]
        bx, by, bnx, bny = lines[i]
        det = anx * bny - any_ * bnx
        if abs(det) < 1e-9:
            out.append((bx, by))
            continue
        ca = anx * ax + any_ * ay
        cb = bnx * bx + bny * by
        out.append(((ca * bny - any_ * cb) / det, (anx * cb - ca * bnx) / det))
    return out


def band(pts, size, d0, d1, ss):
    """两条内推线之间的一圈（铁箍／缝都用它）。"""
    outer = poly_mask(offset_poly(pts, d0), size, ss) if d0 > 0 else poly_mask(pts, size, ss)
    inner = poly_mask(offset_poly(pts, d1), size, ss)
    return ImageChops.subtract(outer, inner)


def rivets(pts, frame_mask, ss, inset):
    """铆钉：从四个角沿对角线往里走，取落在铁箍带里的那一段的中点。
    这样对任何轮廓都成立，不用一款一款手填坐标。"""
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
    got = []
    for cx, cy, dx, dy in ((x0, y0, 1, 1), (x1, y0, -1, 1), (x0, y1, 1, -1), (x1, y1, -1, -1)):
        run = []
        t = 0.0
        while t < inset:
            px = int(round((cx + dx * t) * ss))
            py = int(round((cy + dy * t) * ss))
            if 0 <= px < frame_mask.width and 0 <= py < frame_mask.height \
                    and frame_mask.getpixel((px, py)) > 200:
                run.append((cx + dx * t, cy + dy * t))
            t += 0.2
        if run:
            got.append(run[len(run) // 2])
    return got


# ------------------------------------------------------------------ 四款轮廓

def v_rect():
    c = 1.5
    return [(c, 0), (W - c, 0), (W, c), (W, H - c), (W - c, H), (c, H), (0, H - c), (0, c)]


def v_end(hx, vy):
    """两端斜切：横向切 hx、纵向切 vy（只切左右两端，中间保持直边）。"""
    return [(hx, 0), (W - hx, 0), (W, vy), (W, H - vy), (W - hx, H), (hx, H),
            (0, H - vy), (0, vy)]


def v_barrel(b, c=1.5):
    """鼓形：上下缘中段外扩 b，两端收回去。"""
    return [(c, b), (W * 0.20, b * 0.35), (W * 0.5, 0), (W * 0.80, b * 0.35),
            (W - c, b), (W, b + c * 0.7), (W, H - b - c * 0.7),
            (W - c, H - b), (W * 0.80, H - b * 0.35), (W * 0.5, H),
            (W * 0.20, H - b * 0.35), (c, H - b), (0, H - b - c * 0.7),
            (0, b + c * 0.7)]


VARIANTS = [
    ("A 基准（现行）", "矩形 · 角上只留 1.5 抗锯齿", v_rect(),
     ["四条等宽直边", "四角＝一个点", "现代 UI 的默认形"]),
    ("B 端斜切 7", "两端横切 7 · 纵切 4.5", v_end(7.0, 4.5),
     ["收得住，仍是控件", "四角变成折线 → 更立", "风险最小的一档"]),
    ("C 端斜切 12", "两端横切 12 · 纵切 7", v_end(12.0, 7.0),
     ["读成木牌／条盾", "最“不方”的一档", "两端只剩 21px 高"]),
    ("D 微鼓", "上下缘中段外扩 3.5 · 两端收", v_barrel(3.5),
     ["读成绷起来的板", "⚠️ 纵拉伸要先改九宫格", "曲面在中段会被拉平"]),
    ("E 八角", "四角各斜切 5 · 其余直边", v_end(5.0, 5.0),
     ["像锻出来的铁件", "最克制", "几乎不影响拉伸"]),
]


# ------------------------------------------------------------------ 出图

def render(pts, wood, iron, gold, w1, w2, w3):
    size = (W * SS, H * SS)
    shape = poly_mask(pts, size, SS)
    frame = band(pts, size, 0, FRAME, SS)
    seam = band(pts, size, GOLD_INSET, GOLD_INSET + SEAM, SS)

    img = Image.composite(iron, wood, frame)

    # 光：木只吃轻形影、铁另吃强高光（与 make_button_primary 同一套）
    ramp = Image.new("L", size, 0)
    rp = ramp.load()
    for y in range(size[1]):
        v = int(255 * (1.0 - y / float(size[1] - 1)) ** 1.35)
        for x in range(size[0]):
            rp[x, y] = v
    sh = ramp.point(lambda v: int(255 - (255 - v) * M.WOOD_SHADE))
    img = ImageChops.multiply(img, Image.merge("RGB", (sh, sh, sh)))
    spec = ImageChops.multiply(frame, ramp.point(lambda v: int(v * M.IRON_GLOSS)))
    lit = Image.blend(ImageEnhance.Brightness(img).enhance(M.IRON_LIFT),
                      Image.new("RGB", size, M.IRON_HI_TINT), M.IRON_HI_MIX)
    img = Image.composite(lit, img, spec)

    bh = max(1, int(FRAME * M.SPEC_BAND * SS))
    bmask = Image.new("L", size, 0)
    ImageDraw.Draw(bmask).rectangle([0, 0, size[0] - 1, bh], fill=255)
    bmask = ImageChops.multiply(frame, bmask.filter(
        ImageFilter.GaussianBlur(max(1.0, 0.8 * SS))))
    img = Image.composite(Image.new("RGB", size, M.SPEC_TINT), img,
                          bmask.point(lambda v: int(v * M.SPEC_MIX)))

    # 克制金线（三档里的默认档）
    gl = M.GOLD_TIERS["soft"]
    gm = ImageChops.multiply(seam, ImageChops.multiply(w1, w2))
    gm = ImageChops.multiply(gm, w3).point(lambda v: int(v * gl["a"]))
    img = Image.composite(Image.new("RGB", size, gold), img, gm)
    img = Image.composite(Image.new("RGB", size, (16, 12, 9)), img,
                          seam.point(lambda v: v * gl["shadow"] // 255))

    # 上下两条细倒角
    th = max(1, int(round(0.9 * SS)))
    hi = Image.new("L", size, 0)
    lo = Image.new("L", size, 0)
    ImageDraw.Draw(hi).rectangle([0, 0, size[0] - 1, th - 1], fill=255)
    ImageDraw.Draw(lo).rectangle([0, size[1] - th, size[0] - 1, size[1] - 1], fill=255)
    img = Image.composite(ImageEnhance.Brightness(img).enhance(1.16), img,
                          ImageChops.multiply(hi, frame))
    img = Image.composite(ImageEnhance.Brightness(img).enhance(0.78), img,
                          ImageChops.multiply(lo, frame))

    # 铆钉
    d = ImageDraw.Draw(img)
    r = RIVET_R * SS
    pts_r = rivets(pts, frame, SS, FRAME * 2.2) + [
        (W * 0.5, FRAME * 0.5), (W * 0.5, H - FRAME * 0.5)]
    for cx, cy in pts_r:
        cx, cy = cx * SS, cy * SS
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(58, 56, 54))
        d.ellipse([cx - r * 0.55, cy - r * 0.75, cx + r * 0.45, cy + r * 0.15],
                  fill=(126, 122, 116))

    rgba = img.convert("RGBA")
    rgba.putalpha(shape)
    return rgba.resize((W, H), Image.LANCZOS)


def silhouette(pts, z=3):
    """剪影要按大倍数出——1× 下切角会被吃掉，等于没画。"""
    m = poly_mask(pts, (W * z, H * z), z)
    im = Image.new("RGB", (W * z, H * z), (16, 14, 13))
    im.paste((154, 140, 116), (0, 0), m)
    return im


def main():
    print("[1] 取材（与交付同源）")
    wood = M.material(M.load_plaque(M.WOOD_SRC), (0.28, 0.28, 0.72, 0.72),
                      (W * SS, H * SS), M.TARGET_WOOD, grain=M.WOOD_GRAIN)
    iron = M.material(M.load_plaque(M.IRON_SRC), (0.36, 0.46, 0.64, 0.74),
                      (W * SS, H * SS), M.TARGET_IRON,
                      grain=M.IRON_GRAIN, grain_dark=M.IRON_GRAIN_DARK,
                      contrast=1.00, sat=M.IRON_SAT, blur_f=0.13)
    gold = M.sample_gold(M.load_plaque(M.IRON_SRC))
    size = (W * SS, H * SS)
    w1 = M.value_noise(size, (22, 6), seed=20260914).point(lambda v: 104 + v * 151 // 255)
    w2 = M.value_noise(size, (64, 18), seed=13).point(lambda v: 166 + v * 89 // 255)
    w3 = M.value_noise(size, (9, 2), seed=77).point(lambda v: 140 + v * 115 // 255)

    print("[2] %d 款轮廓" % len(VARIANTS))
    shots = []
    for name, spec, pts, notes in VARIANTS:
        shots.append((name, spec, render(pts, wood, iron, gold, w1, w2, w3),
                      silhouette(pts), notes))
        print("    %s ok" % name)

    print("[3] 研究页")
    os.makedirs(OUT, exist_ok=True)
    pad, gap, z = 40, 22, 3
    cw, ch = W * z, H * z
    n = len(shots)
    y0 = pad + 74
    y_lbl = y0 + ch + 8
    y_sil = y_lbl + 116
    y_one = y_sil + ch + 34
    height = y_one + H + 116

    sh = Image.new("RGB", (pad * 2 + n * cw + (n - 1) * gap, height), (16, 14, 13))
    d = ImageDraw.Draw(sh)
    F = lambda k: ImageFont.truetype(M.FONT, k)
    d.text((pad, pad), "形制研究 · 同一副材质与光，只换轮廓", font=F(26), fill=(214, 196, 160))
    d.text((pad, pad + 38),
           "铁箍／缝／铆钉／光泽分工／克制金线 五款全同 —— 看到的差异就是形状的差异",
           font=F(14), fill=(140, 124, 104))

    for i, (name, spec, im, sil, notes) in enumerate(shots):
        x = pad + i * (cw + gap)
        sh.paste(im.resize((cw, ch), Image.NEAREST), (x, y0))
        d.rectangle([x, y0, x + cw, y0 + ch], outline=(60, 52, 44))
        d.text((x, y_lbl), name, font=F(19), fill=(216, 198, 162))
        d.text((x, y_lbl + 26), spec, font=F(13), fill=(146, 130, 108))
        d.text((x, y_lbl + 52), "剪影 ↓", font=F(13), fill=(110, 96, 80))
        sh.paste(sil, (x, y_lbl + 70))
        d.rectangle([x, y_lbl + 70, x + cw, y_lbl + 70 + ch], outline=(48, 42, 36))
        d.text((x, y_sil + ch + 4), "实尺 ↓", font=F(13), fill=(110, 96, 80))
        sh.paste(im, (x, y_one))
        for j, ln in enumerate(notes):
            d.text((x, y_one + H + 26 + j * 20), ln, font=F(13), fill=(150, 134, 112))

    p = os.path.join(OUT, "sheet_shape_study.png")
    sh.save(p)
    print("  -> %s  %dx%d" % (p, sh.width, sh.height))
    print("done.")


if __name__ == "__main__":
    main()
