# -*- coding: utf-8 -*-
"""
组排研究 · 按钮"摆成一排好不好看"

背景（甲方原话）：
  「你觉得现在的按钮排列起来看还好看吗…是不是排列看起来乱，总之越看越不顺眼」

诊断（我的判断）：
  单颗的材质与光是判过的、是对的。问题出在**装饰量是按"单件"定的**：
  四面闭合的铁框 ＋ 四角角件 ＋ 四枚铆钉 ＋ 一圈内嵌金线 ＋ 顶上一条镜面白。
  单颗＝完成度高；**摆成一排 = 每一份装饰乘以 N**：
    4 颗 → 4 个独立小盒子（不是"一组按钮"）
    4 颗 × 4 铆钉 → 16 个亮点，眼睛没地方歇
    每颗顶上一条镜面白 → 连成一串抖动的亮点
    值域 12.5× ：单颗叫有冲击力，一排叫吵
  ⇒ **单件的强度 ≠ 成组的强度。装饰量要按"一排最多几颗"倒推。**

本页三块：
  一、现状（按生产常量原样复现）
  二、减法（去铆钉／金线只走上下两条／镜面收一半／铁箍 5.5→4.5）
  三、供 UI 线决策：Tab 改"分段"形制（需要把 Prefab 的 Spacing 改成 0）

产物：out/button-group-study/sheet_group_study.png（**研究图，不是交付 sprite**）
"""
import os
import sys

from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import make_button_primary as M

OUT = os.path.join(HERE, "out", "button-group-study")
SS = M.SS


# ---------------------------------------------------------------- 单颗

def render(w, h, wood, iron, gold, rivets=True, gold_sides=True,
           spec=M.SPEC_MIX, bevel=(1.34, 0.62), frame=M.FRAME):
    ww, hh = w * SS, h * SS
    R = M.RADIUS * SS
    F, S = frame * SS, M.SEAM * SS
    INSET = (frame - 0.5) * SS

    shape = Image.new("L", (ww, hh), 0)
    ImageDraw.Draw(shape).rounded_rectangle((0, 0, ww - 1, hh - 1), radius=R, fill=255)

    def ring(inset, thick, rad):
        m = Image.new("L", (ww, hh), 0)
        ImageDraw.Draw(m).rounded_rectangle(
            (inset, inset, ww - 1 - inset, hh - 1 - inset),
            radius=max(1, int(rad)), outline=255, width=max(1, int(round(thick))))
        return m

    fr = ring(0, F, R)
    cs = M.CORNER * SS
    cb = Image.new("L", (ww, hh), 0)
    cbd = ImageDraw.Draw(cb)
    for x0, y0 in ((0, 0), (ww - int(cs), 0), (0, hh - int(cs)), (ww - int(cs), hh - int(cs))):
        cbd.rounded_rectangle((x0, y0, x0 + int(cs) - 1, y0 + int(cs) - 1), radius=R, fill=255)
    fr = ImageChops.lighter(fr, cb)
    cb2 = Image.new("L", (ww, hh), 0)
    c2 = ImageDraw.Draw(cb2)
    cs2 = int((M.CORNER + 2.4) * SS)
    for x0, y0 in ((0, 0), (ww - cs2, 0), (0, hh - cs2), (ww - cs2, hh - cs2)):
        c2.rounded_rectangle((x0, y0, x0 + cs2 - 1, y0 + cs2 - 1), radius=R, fill=255)

    seam = ring(int(INSET), S, max(1, int(round((M.RADIUS - 0.5) * SS))))
    # 缝在四角被角件截断：只走四条直边
    seam = ImageChops.subtract(seam, cb2)

    img = Image.composite(iron, wood, fr)

    ramp = Image.new("L", (ww, hh), 0)
    rp = ramp.load()
    for y in range(hh):
        v = int(255 * (1.0 - y / float(hh - 1)) ** 1.35)
        for x in range(ww):
            rp[x, y] = v
    sh = ramp.point(lambda v: int(255 - (255 - v) * M.WOOD_SHADE))
    img = ImageChops.multiply(img, Image.merge("RGB", (sh, sh, sh)))
    gspec = ImageChops.multiply(fr, ramp.point(lambda v: int(v * M.IRON_GLOSS)))
    lit = Image.blend(ImageEnhance.Brightness(img).enhance(M.IRON_LIFT),
                      Image.new("RGB", (ww, hh), M.IRON_HI_TINT), M.IRON_HI_MIX)
    img = Image.composite(lit, img, gspec)
    bh = max(1, int(frame * M.SPEC_BAND * SS))
    bm = Image.new("L", (ww, hh), 0)
    ImageDraw.Draw(bm).rectangle([0, 0, ww - 1, bh], fill=255)
    bm = ImageChops.multiply(fr, bm.filter(ImageFilter.GaussianBlur(max(1.0, 0.8 * SS))))
    img = Image.composite(Image.new("RGB", (ww, hh), M.SPEC_TINT), img,
                          bm.point(lambda v: int(v * spec)))

    # 金线：全周（现状）／只走上下两条直边（减法）
    gm = seam
    if not gold_sides:
        keep = Image.new("L", (ww, hh), 0)
        e = int(16 * SS)
        ImageDraw.Draw(keep).rectangle([e, 0, ww - 1 - e, hh - 1], fill=255)
        gm = ImageChops.multiply(gm, keep)
    gl = M.GOLD_TIERS["soft"]
    wn = M.value_noise((ww, hh), (14, 3), seed=77)
    gm = ImageChops.multiply(gm, wn.point(lambda v: int(255 - (255 - v) * gl["wear"])))
    gm = gm.point(lambda v: int(v * gl["a"]))
    col = tuple(min(255, int(c * gl["gain"])) for c in gold)
    img = Image.composite(Image.new("RGB", (ww, hh), col), img, gm)
    img = Image.composite(Image.new("RGB", (ww, hh), (16, 12, 9)), img,
                          seam.point(lambda v: v * gl["shadow"] // 255))

    th = max(1, int(round(0.9 * SS)))
    hi = Image.new("L", (ww, hh), 0)
    lo = Image.new("L", (ww, hh), 0)
    ImageDraw.Draw(hi).rectangle([0, 0, ww - 1, th - 1], fill=255)
    ImageDraw.Draw(lo).rectangle([0, hh - th, ww - 1, hh - 1], fill=255)
    img = Image.composite(ImageEnhance.Brightness(img).enhance(bevel[0]), img,
                          ImageChops.multiply(hi, fr))
    img = Image.composite(ImageEnhance.Brightness(img).enhance(bevel[1]), img,
                          ImageChops.multiply(lo, fr))

    if rivets:
        d = ImageDraw.Draw(img)
        r = M.RIVET_R * SS
        for cx, cy in ((cs * 0.5, cs * 0.5), (ww - cs * 0.5, cs * 0.5),
                       (cs * 0.5, hh - cs * 0.5), (ww - cs * 0.5, hh - cs * 0.5)):
            d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(54, 52, 49))

    rgba = img.convert("RGBA")
    rgba.putalpha(shape)
    out = rgba.resize((w, h), Image.LANCZOS)
    rgb = out.convert("RGB").filter(ImageFilter.UnsharpMask(0.7, 58, 3)).convert("RGBA")
    rgb.putalpha(out.split()[3])
    return rgb


# ---------------------------------------------------------------- 分段条带（第三块）

def render_segments(w, h, segs, wood, iron, gold, frame=4.5):
    """一条木＋铁的条带，被切成 n 格：只在外周包铁，格与格之间是**竖分隔条**。"""
    ww, hh = w * SS, h * SS
    F = frame * SS
    shape = Image.new("L", (ww, hh), 0)
    ImageDraw.Draw(shape).rounded_rectangle((0, 0, ww - 1, hh - 1),
                                            radius=M.RADIUS * SS, fill=255)

    divider = Image.new("L", (ww, hh), 0)
    dd = ImageDraw.Draw(divider)
    for x in segs[1:-1]:
        dd.rectangle([int(x * SS) - int(1.1 * SS), 0,
                      int(x * SS) + int(1.1 * SS), hh - 1], fill=255)

    fr = Image.new("L", (ww, hh), 0)
    ImageDraw.Draw(fr).rounded_rectangle((0, 0, ww - 1, hh - 1),
                                         radius=M.RADIUS * SS,
                                         outline=255, width=int(F))
    fr = ImageChops.lighter(fr, divider)

    img = Image.composite(iron, wood, fr)
    ramp = Image.new("L", (ww, hh), 0)
    rp = ramp.load()
    for y in range(hh):
        v = int(255 * (1.0 - y / float(hh - 1)) ** 1.35)
        for x in range(ww):
            rp[x, y] = v
    sh = ramp.point(lambda v: int(255 - (255 - v) * M.WOOD_SHADE))
    img = ImageChops.multiply(img, Image.merge("RGB", (sh, sh, sh)))
    gs = ImageChops.multiply(fr, ramp.point(lambda v: int(v * M.IRON_GLOSS)))
    lit = Image.blend(ImageEnhance.Brightness(img).enhance(M.IRON_LIFT),
                      Image.new("RGB", (ww, hh), M.IRON_HI_TINT), M.IRON_HI_MIX)
    img = Image.composite(lit, img, gs)
    bh = max(1, int(M.FRAME * M.SPEC_BAND * SS))
    bm = Image.new("L", (ww, hh), 0)
    ImageDraw.Draw(bm).rectangle([0, 0, ww - 1, bh], fill=255)
    bm = ImageChops.multiply(fr, bm.filter(ImageFilter.GaussianBlur(max(1.0, 0.8 * SS))))
    img = Image.composite(Image.new("RGB", (ww, hh), M.SPEC_TINT), img,
                          bm.point(lambda v: int(v * 0.34)))

    # 金线：上下两条通长，遇分隔条断开
    seam = Image.new("L", (ww, hh), 0)
    sd = ImageDraw.Draw(seam)
    y0 = int((M.FRAME - 0.5) * SS)
    sd.rectangle([0, y0, ww - 1, y0 + int(M.SEAM * SS)], fill=255)
    sd.rectangle([0, hh - y0 - int(M.SEAM * SS), ww - 1, hh - y0], fill=255)
    seam = ImageChops.subtract(seam, divider)      # 金线遇分隔条断开
    wn = M.value_noise((ww, hh), (14, 3), seed=77)
    gl = M.GOLD_TIERS["soft"]
    gm = ImageChops.multiply(seam, wn.point(lambda v: int(255 - (255 - v) * gl["wear"])))
    gm = gm.point(lambda v: int(v * gl["a"]))
    col = tuple(min(255, int(c * gl["gain"])) for c in gold)
    img = Image.composite(Image.new("RGB", (ww, hh), col), img, gm)

    out = img.convert("RGBA").resize((w, h), Image.LANCZOS)
    rgb = out.convert("RGB").filter(ImageFilter.UnsharpMask(0.7, 58, 3)).convert("RGBA")
    rgb.putalpha(out.split()[3])
    return rgb


# ---------------------------------------------------------------- 页

def main():
    print("[1] 取材")
    plate_w = M.load_plaque(M.WOOD_SRC)
    plate_i = M.load_plaque(M.IRON_SRC)
    gold = M.sample_gold(plate_i)

    def mats(w, h, tw, ti, wood_struct=M.STRUCT_GAIN, iron_struct=M.STRUCT_GAIN):
        """材质必须**按每种交付尺寸各自取块**：取块尺度跟交付尺寸绑定，
        不能拿一张大图裁——裁剪后的纹理尺度会跟交付尺寸对不上。
        wood_struct 是"减法"的关键一刀：**肉要安静、骨要清楚**——
        木面上那几块大花在单颗上是质感，摆成一排就是一片脏。"""
        size = (w * SS, h * SS)
        cp = (int(w * M.CROP_ZOOM), int(h * M.CROP_ZOOM))
        return (M.material(plate_w, (0.28, 0.28, 0.72, 0.72), size, tw,
                           grain=M.WOOD_GRAIN, crop_px=cp, structure=wood_struct),
                M.material(plate_i, (0.36, 0.46, 0.64, 0.74), size, ti,
                           grain=M.IRON_GRAIN, grain_dark=M.IRON_GRAIN_DARK,
                           contrast=1.00, sat=M.IRON_SAT, blur_f=0.13,
                           crop_px=cp, structure=iron_struct))

    print("[2] 两颗风格 × 两种尺寸（Primary 110×35 / Tab 105×35）")
    now, cut = {}, {}
    for w, h in ((110, 35), (105, 35)):
        now[(w, h)] = render(w, h, *mats(w, h, M.TARGET_WOOD, M.TARGET_IRON), gold)
        cut[(w, h)] = render(w, h, *mats(w, h, 30.0, 56.0, wood_struct=0.42), gold,
                             rivets=False, gold_sides=False, spec=0.34,
                             bevel=(1.22, 0.70), frame=4.5)
    seg = render_segments(325, 35, [0, 105, 215, 325],
                          *mats(325, 35, 30.0, 56.0, wood_struct=0.42), gold)

    print("[3] 组排页")
    os.makedirs(OUT, exist_ok=True)
    Wp, Hp = 560, 470
    sh = Image.new("RGB", (Wp * 2 + 60, 890), (16, 15, 14))
    d = ImageDraw.Draw(sh)
    F = lambda k: ImageFont.truetype(M.FONT, k)
    bg = (22, 20, 19)

    def panel(x, y, w, h):
        d.rectangle([x, y, x + w, y + h], fill=bg, outline=(38, 34, 30))

    def row(x, y, sprites, gap):
        cx = x
        for s in sprites:
            sh.paste(s, (cx, y), s)
            cx += s.width + gap

    def label(x, y, t, size=15, col=(196, 180, 150)):
        d.text((x, y), t, font=F(size), fill=col)

    d.text((40, 26), "组排研究 · 单颗好看 ≠ 一排好看", font=F(27), fill=(214, 196, 160))
    d.text((40, 62), "同一副材质与光；左边按生产常量原样，右边只做减法（去铆钉／金线只走上下／镜面收半／铁箍 5.5→4.5）",
           font=F(14), fill=(140, 124, 104))

    for col, (title, sp, note) in enumerate([
        ("一 · 现状", now, "4 个独立小盒子／16 个铆钉亮点／顶上四条白"),
        ("二 · 减法", cut, "去掉铆钉、金线只留上下、镜面收半 ⇒ 一排安静下来"),
    ]):
        x0 = 40 + col * (Wp + 20)
        label(x0, 104, title, 20, (216, 198, 162))
        label(x0, 132, note, 13, (146, 130, 108))

        panel(x0 + 20, 160, 500, 70)
        row(x0 + 30, 178, [sp[(110, 35)]] * 4, 10)
        label(x0, 244, "Primary ×4（DeveloperCheck 的做法：Spacing 10）", 13, (150, 134, 112))

        panel(x0 + 20, 290, 500, 60)
        row(x0 + 30, 303, [sp[(105, 35)]] * 3, 5)
        label(x0, 364, "Tab ×3（AwakeMessenger 的做法：Spacing 5）", 13, (150, 134, 112))

        # 相邻两颗的接缝放大（2× 像素真实；宽度必须落在本列内，别压到隔壁）
        strip = Image.new("RGB", (230, 35), bg)
        strip.paste(sp[(110, 35)], (0, 0), sp[(110, 35)])
        strip.paste(sp[(110, 35)], (120, 0), sp[(110, 35)])
        sh.paste(strip.resize((460, 70), Image.NEAREST), (x0 + 30, 400))
        label(x0, 480, "相邻两颗接缝（2× 像素真实）", 13, (150, 134, 112))

    # 第三块：分段形制
    y = 570
    label(40, y, "三 · 供 UI 线决策：Tab 走「分段」形制", 20, (216, 198, 162))
    label(40, y + 28, "把三个 Tab 读成「一条木铁条带被切成三格」——只在最外两端包角，格与格之间是竖分隔条。",
          13, (146, 130, 108))
    label(40, y + 48, "注意：需要 Prefab 改一处 —— AwakeMessenger / NpcDialogue 里 Tab 的 Spacing 5 改 0，并改引这一个 brush。",
          13, (176, 138, 108))
    panel(60, y + 74, 560, 60)
    sh.paste(seg, (80, y + 87), seg)
    sh.paste(seg.resize((975, 105), Image.NEAREST), (80, y + 150))

    p = os.path.join(OUT, "sheet_group_study.png")
    sh.save(p)
    print("  -> %s  %dx%d" % (p, sh.width, sh.height))
    print("done.")


if __name__ == "__main__":
    main()
