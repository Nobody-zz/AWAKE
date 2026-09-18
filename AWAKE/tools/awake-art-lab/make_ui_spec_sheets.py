# -*- coding: utf-8 -*-
"""UI 控件规范稿 · 素材生成（视觉总表 + 缺口控件施工图）。

只做一件事：把「控件长什么样 + 尺寸/九宫格是多少」画成**能直接看的图**，
交给画布当素材；画布那边只放标题、色卡和契约文字。

产物 out/ui-spec/：
  s_tokens.png    token 色卡
  s_shell.png     面板壳（3 大 + 2 条 + 角部放大 + 装饰件）
  s_buttons.png   按钮族状态矩阵（真实 sprite，逐状态）
  s_icons.png     功能图标（真实 sprite）
  s_gaps.png      缺口控件**施工图**（10 件：暗石面 + 1px 金线 + 九宫格虚线 + 尺寸标注）

⚠️ 施工图画的是**提案**，不是已交付的资产 —— 缺口 9 件尚无出图（见 UI-ART-ASSET-INTERFACE §4）。
⚠️ 一律用**真实 token 色**（UI-FRAMEWORK §2.1），不另造颜色。
"""
import os

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
AWAKE = os.path.normpath(os.path.join(HERE, "..", ".."))
PARTS = os.path.join(AWAKE, "GUI", "SpriteParts")
OUT = os.path.join(HERE, "out", "ui-spec")

# ---- 版面色（规范稿自身，不是游戏 UI）----
BG = (20, 16, 14)
CARD = (27, 22, 19)
LINE = (58, 48, 42)
GOLD = (226, 175, 84)
LBL = (200, 176, 136)
MUT = (138, 124, 102)
INK = (242, 237, 228)
WARN = (214, 124, 96)

# ---- 游戏 UI token（UI-FRAMEWORK §2.1，实测值，不重造）----
T = {
    "bg.panel": (0x18, 0x10, 0x10),
    "bg.panel.deep": (0x0F, 0x0B, 0x07),
    "bg.inset": (0x0B, 0x08, 0x06),
    "line.hairline": (0x8C, 0x6B, 0x38),
    "gold.title": (0xE2, 0xAF, 0x54),
    "gold.icon": (0xD8, 0xA8, 0x50),
    "gold.dim": (0x9C, 0x78, 0x39),
    "fill.brass": (0x48, 0x40, 0x30),
    "fill.hover": (0x70, 0x58, 0x38),
    "fill.confirm": (0x48, 0x50, 0x20),
    "fill.danger": (0x40, 0x30, 0x28),
    "danger.seal": (0x5A, 0x1E, 0x14),
    "text.primary": (0xF2, 0xED, 0xE4),
    "text.secondary": (0xC8, 0xB0, 0x88),
    "text.muted": (0x8A, 0x7C, 0x66),
    "icon.white": (0xF8, 0xF8, 0xF8),
}
HAIR_A = 0.60          # line.hairline 是 @60%

for cand in (r"C:\Windows\Fonts\msyh.ttc", r"C:\Windows\Fonts\simhei.ttf",
             r"C:\Windows\Fonts\simkai.ttf"):
    if os.path.exists(cand):
        FT = cand
        break
FTB = r"C:\Windows\Fonts\msyhbd.ttc" if os.path.exists(r"C:\Windows\Fonts\msyhbd.ttc") else FT


def f(sz, bold=False):
    return ImageFont.truetype(FTB if bold else FT, sz)


def new(w, h, bg=BG):
    im = Image.new("RGB", (w, h), bg)
    return im, ImageDraw.Draw(im)


def scaled(im, maxw, maxh):
    k = min(maxw / float(im.width), maxh / float(im.height), 1.0)
    if k < 1.0:
        return im.resize((max(1, int(im.width * k)), max(1, int(im.height * k))), Image.LANCZOS)
    return im


def zoom(im, k, box=None):
    c = im.crop(box) if box else im
    return c.resize((int(c.width * k), int(c.height * k)), Image.NEAREST)


def on(im, bg=BG):
    c = Image.new("RGB", im.size, bg)
    c.paste(im.convert("RGBA"), (0, 0), im.convert("RGBA"))
    return c


def sprite(name):
    p = None
    for d in os.listdir(PARTS):
        q = os.path.join(PARTS, d, name + ".png")
        if os.path.exists(q):
            p = q
            break
    if p is None:
        return None
    return Image.open(p).convert("RGBA")


def find(prefix):
    """找一个 sprite 的**全部状态**（按 Default→Hovered→Pressed→Disabled→Selected 排序）。"""
    suf = [("", "Default"), ("_hover", "Hovered"), ("_pressed", "Pressed"),
           ("_disabled", "Disabled"), ("_selected", "Selected")]
    out = []
    for s, lab in suf:
        im = sprite(prefix + s)
        if im is not None:
            out.append((lab, im))
    return out


# ============================================================ 1 · token 色卡

def sheet_tokens():
    items = list(T.items())
    cw, ch = 148, 62
    cols = 6
    rows = (len(items) + cols - 1) // cols
    W = 40 + cols * cw
    H = 30 + rows * ch
    im, d = new(W, H, CARD)
    for i, (k, c) in enumerate(items):
        cx = 40 + (i % cols) * cw
        cy = 30 + (i // cols) * ch
        d.rectangle((cx, cy, cx + 22, cy + 22), fill=c,
                    outline=(90, 78, 68))
        d.text((cx + 30, cy - 1), k, font=f(13), fill=INK)
        d.text((cx + 30, cy + 15), "#%02X%02X%02X" % c, font=f(12), fill=MUT)
    p = os.path.join(OUT, "s_tokens.png")
    im.save(p)
    return p, im.size


# ============================================================ 2 · 面板壳

def sheet_shell():
    w = 1500
    big = ["panel_main_1280", "panel_main_960", "panel_dialog_1100"]
    bars = ["panel_input_800", "panel_status_720"]
    orn = ["header_band", "divider_gold", "rule_repeat_24x240",
           "ground_tile_256", "weave_tile_256"]
    y = 26
    im, d = new(w, 1200, CARD)

    d.text((40, y), "A · 面板壳 —— 真实 sprite，缩略看比例", font=f(20, True), fill=GOLD)
    d.text((700, y + 4), "⚠️ 垫了中灰底板：面板本色 #181010 放在深色稿底上会糊成一片，看不出形",
           font=f(12), fill=MUT)
    y += 34
    CELL = 340
    x = 40
    caps = []
    for n in big:
        s = sprite(n)
        if s is None:
            continue
        t = scaled(s, 400, 260)
        pl = paper(t, 14)
        im.paste(pl, (x, y))
        caps.append((x, y + pl.height + 8, "%s  %d×%d" % (n, s.width, s.height)))
        x += 460
    for cx, cy, txt in caps:
        d.text((cx, cy), txt, font=f(13), fill=LBL)
        d.text((cx, cy + 18), "九宫格 32/32/32/32", font=f(12), fill=MUT)
    y += CELL + 6
    d.line((40, y, w - 40, y), fill=LINE)
    y += 22

    d.text((40, y), "B · 底部条 —— 原尺寸（这两条不做缩放，它们是条不是板）", font=f(17, True), fill=GOLD)
    y += 30
    for n in bars:
        s = sprite(n)
        if s is None:
            continue
        pl = paper(s, 12)
        im.paste(pl, (40, y))
        d.text((40 + pl.width + 24, y + 14), "%s  %d×%d" % (n, s.width, s.height), font=f(13), fill=LBL)
        d.text((40 + pl.width + 24, y + 32), "九宫格 16/16/16/16", font=f(12), fill=MUT)
        d.text((40 + pl.width + 24, y + 50), "纵向拉伸不变形（面板贴底那一条）", font=f(12), fill=MUT)
        y += pl.height + 18

    y += 8
    d.line((40, y, w - 40, y), fill=LINE)
    y += 22
    d.text((40, y), "C · 角部放大 —— 看材质与 1px 金线到底是怎么走的", font=f(17, True), fill=GOLD)
    y += 30
    src = sprite("panel_main_1280")
    if src is not None:
        for k, lab in ((1, "1:1"), (3, "×3")):
            c = src.crop((0, 0, 56, 44))
            z = zoom(c, k)
            im.paste(on(z, BG), (40, y))
            d.text((40, y + z.height + 6), "左上角 %s" % lab, font=f(13), fill=LBL)
            x = 40 + z.width + 40
        # 九宫格切线示意（右上角起）
        c = src.crop((src.width - 90, 0, src.width, 60))
        z = zoom(c, 2)
        zz = on(z, BG)
        dz = ImageDraw.Draw(zz)
        for gx in (30, z.width - 40):          # 32 边界（相对裁块）
            dz.line((gx, 0, gx, z.height), fill=GOLD)
        dz.line((0, 32, z.width, 32), fill=GOLD)
        im.paste(zz, (560, y))
        d.text((560, y + zz.height + 6), "右上角 ×2 ＋ 九宫格边界（金线＝32px 处）", font=f(13), fill=LBL)
    y += 200 + 12

    d.line((40, y, w - 40, y), fill=LINE)
    y += 22
    d.text((40, y), "D · 装饰件 —— 真实 sprite", font=f(17, True), fill=GOLD)
    y += 30
    x = 40
    for n in orn:
        s = sprite(n)
        if s is None:
            continue
        t = scaled(s, 260, 90)
        im.paste(on(t, BG), (x, y))
        d.text((x, y + t.height + 6), "%s  %d×%d" % (n, s.width, s.height), font=f(12), fill=LBL)
        x += 290
    y += 90 + 34

    im = im.crop((0, 0, w, y))
    p = os.path.join(OUT, "s_shell.png")
    im.save(p)
    return p, im.size


# ============================================================ 3 · 按钮族

def sheet_buttons():
    fams = [("btn_primary_110", "主按钮", "110×35", "16/16/16/16", "发送 · 开发工具 ×4"),
            ("btn_secondary_100", "次按钮", "100×35", "16/16/16/16", "写信"),
            ("btn_tab_105", "页签", "105×35", "14/14/10/10", "信使 3 · 对话 2"),
            ("btn_close_40", "关闭键", "40×40", "12/12/12/12", "全部面板")]
    Z = 3
    w = 1500
    im, d = new(w, 1400, CARD)
    d.text((40, 26), "A · 按钮族状态矩阵 —— 真实 sprite，×%d（逐状态出图，不是因子提亮）" % Z,
           font=f(20, True), fill=GOLD)
    y = 72
    for prefix, cn, size, nine, place in fams:
        st = find(prefix)
        if not st:
            continue
        d.text((40, y), "%s  ·  %s  ·  %s  ·  九宫格 %s" % (cn, prefix, size, nine),
               font=f(16, True), fill=INK)
        d.text((900, y + 2), "落点：%s" % place, font=f(13), fill=MUT)
        y += 28
        x = 40
        for lab, s in st:
            z = zoom(s, Z)
            holder = Image.new("RGB", (z.width + 16, z.height + 16), (34, 28, 24))
            holder.paste(on(z, BG), (8, 8))
            im.paste(holder, (x, y))
            d.text((x + 8, y + holder.height + 4), lab, font=f(12), fill=LBL)
            x += holder.width + 22
        y += 35 * Z + 16 + 40
        d.line((40, y - 16, w - 40, y - 16), fill=LINE)
    im = im.crop((0, 0, w, y - 6))
    p = os.path.join(OUT, "s_buttons.png")
    im.save(p)
    return p, im.size


# ============================================================ 4 · 图标

def sheet_icons():
    names = sorted(os.listdir(os.path.join(PARTS, "ui_awake_icon")))
    base = sorted({n.rsplit("_", 1)[0] for n in names if n.endswith("_48.png")})
    cw, ch = 200, 132
    cols = 7
    rows = (len(base) + cols - 1) // cols
    w = 40 + cols * cw
    im, d = new(w, 90 + rows * ch, CARD)
    d.text((40, 26), "B · 功能图标 —— 纯白剪影，引擎染色；不烤文字", font=f(20, True), fill=GOLD)
    d.text((40, 54), "交互档 48×48 · 大字档 72×72 · 24 网格 / 笔画 2–3px / 方端点（左：48 档放大 2×　右：72 档原尺寸）",
           font=f(13), fill=MUT)
    for i, b in enumerate(base):
        cx = 40 + (i % cols) * cw
        cy = 90 + (i // cols) * ch
        s48 = sprite(b + "_48")
        s72 = sprite(b + "_72")
        if s48 is not None:
            z = zoom(s48, 2)
            im.paste(on(z, (26, 21, 18)), (cx, cy))
        if s72 is not None:
            # ⚠️ 踩过：72 档原先放在 cx+104，而格宽只有 150 ⇒ 越界压到下一格上，两格读成一组。
            im.paste(on(s72, (26, 21, 18)), (cx + 116, cy + 24))
        d.text((cx, cy + 102), b.replace("icon_", ""), font=f(12), fill=LBL)
    im = im.crop((0, 0, w, 90 + rows * ch + 10))
    p = os.path.join(OUT, "s_icons.png")
    im.save(p)
    return p, im.size


# ============================================================ 5 · 缺口控件施工图
#
# 画法＝**施工图**：暗石面（真 token 色）＋ 1px 金线 ＋ 四角切角 ＋ 九宫格虚线 ＋ 尺寸标注。
# 文字区一律画成**虚线空框 + 标注**（框架硬规矩：sprite 里不烤文字）。

def corner_cut(im, n=2):
    """四角切角：把每个角的 n×n 直角打成透明（原版是"方石"，不是圆角）。"""
    d = ImageDraw.Draw(im)
    w, h = im.size
    for x0, y0, sx, sy in ((0, 0, 1, 1), (w - n, 0, -1, 1), (0, h - n, 1, -1), (w - n, h - n, -1, -1)):
        for i in range(n):
            for j in range(n):
                if i + j < n - 1:
                    d.point((x0 + i * sx if sx > 0 else x0 + (n - 1 - i),
                             y0 + j * sy if sy > 0 else y0 + (n - 1 - j)), fill=(0, 0, 0, 0))


def stone(w, h, face="bg.panel", hair=True, cut=2, alpha_line=0.92):
    """施工图里把金线画到 α0.92 —— 线本身是 α0.60，但**施工图的任务是把几何说清楚**，
    线太淡就看不出形。真实透明度在页面脚注里注明，不靠这张图传达。"""
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rectangle((0, 0, w - 1, h - 1), fill=T[face] + (255,))
    if hair:
        lc = T["line.hairline"] + (int(255 * alpha_line),)
        d.rectangle((0, 0, w - 1, h - 1), outline=lc)
    if cut:
        corner_cut(im, cut)
    return im


def paper(img, p=16, bg=(78, 72, 66)):
    """把施工图放到一块**中灰底板**上。
    ⚠️ 踩过：暗石面（bg.panel #181010）直接放在深色稿底（#1B1613）上**根本看不出来** ——
       两块暗色糊成一片，图上的形全丢了。施工图必须给一个中性底，控件才读成一个物件。"""
    c = Image.new("RGB", (img.width + p * 2, img.height + p * 2), bg)
    c.paste(img.convert("RGBA"), (p, p), img.convert("RGBA"))
    return c


def dashed_box(im, box, color=(150, 134, 108), dash=4, gap=3, width=1):
    d = ImageDraw.Draw(im)
    x0, y0, x1, y1 = box
    for x in range(int(x0), int(x1), dash + gap):
        d.line((x, y0, min(x + dash, x1), y0), fill=color, width=width)
        d.line((x, y1, min(x + dash, x1), y1), fill=color, width=width)
    for y in range(int(y0), int(y1), dash + gap):
        d.line((x0, y, x0, min(y + dash, y1)), fill=color, width=width)
        d.line((x1, y, x1, min(y + dash, y1)), fill=color, width=width)


def guide(im, l, t, r, b, color=(158, 118, 56), dash=10, gap=9):
    """九宫格切线：**注记层**，必须比物件本身（那道金线）弱一档。
    ⚠️ 踩过：注释线画得比形状还亮 ⇒ 图上一半是虚线，读成草图，不像施工图。"""
    d = ImageDraw.Draw(im)
    w, h = im.size
    for x in (l, w - r):
        for y in range(0, h, dash + gap):
            d.line((x, y, x, min(y + dash, h)), fill=color)
    for y in (t, h - b):
        for x in range(0, w, dash + gap):
            d.line((x, y, min(x + dash, w), y), fill=color)


def tag(d, x, y, s, col=LBL, sz=13, bold=False):
    d.text((x, y), s, font=f(sz, bold), fill=col)


def sheet_gaps():
    """10 件缺口控件的**施工图**。

    每件一行：左＝画在中灰底板上的控件，右＝要领。
    ⚠️ 图上颜色一律真 token 值；金线画到 α0.92（真实是 α0.60），**为了把几何说清楚** ——
       线按真实透明度画出来，图上就只剩一道若有若无的痕，读不出形。真实值见脚注。
    """
    GAP_W = 1820
    PAP = 1064          # 左栏（中灰底板）宽
    NX = 1140           # 右栏文字起点
    items = []

    # ---- 1 联系人条目 ----
    c = stone(280, 85, "bg.panel", True, 2)
    d = ImageDraw.Draw(c)
    dashed_box(c, (22, 19, 196, 33))
    dashed_box(c, (22, 40, 150, 52))
    dashed_box(c, (22, 59, 168, 69))
    d.line((262, 3, 262, 81), fill=T["gold.dim"] + (235,), width=1)
    guide(c, 14, 14, 14, 14)
    items.append((paper(zoom(c, 2)), [
        "280×85 · 九宫格 14 / 14 / 14 / 14",
        "暗石面 + 1px 细金描边 + 四角 2px 切角（方石感，不做胶囊）",
        "三行文字由控件渲染：名 17 / 身份 13 / 状态 13",
        "虚线框只标位置 —— sprite 图内绝不烤文字",
        "文字区靠左 10；右侧 262 处留一道选中金线",
        "落点：信使左栏（宽 300），替换现用的 ButtonBrush1 橄榄绿实心条",
        "状态：Default / Hovered / Pressed / Selected",
    ], "1 · 联系人条目   contact_item"))

    # ---- 2 / 3 气泡 ----
    for npc, nm in ((True, "bubble_npc"), (False, "bubble_player")):
        c = stone(600, 72, "bg.panel.deep", True, 2)
        d = ImageDraw.Draw(c)
        ex = 2 if npc else 597
        d.rectangle((ex, 4, ex + 1, 67), fill=T["gold.dim"] + (245,))
        dashed_box(c, (18, 13, 470, 33))
        dashed_box(c, (18, 43, 372, 57))
        guide(c, 20, 20, 20, 14)
        who = "NPC（左对齐，金线在左）" if npc else "玩家（右对齐，金线在右，面暖半档）"
        items.append((paper(c), [
            "600×72 · 九宫格 20 / 20 / 20 / 14",
            who,
            "横向不拉伸；**纵向靠 CoverChildren 自撑 ⇒ 九宫格纵向须能无限延伸**",
            "因此装饰只放左右边与四角 —— 上下沿不许出现横向元素",
            "落点：两个对话面板中栏；现在是 BlankWhite α0.10 / 0.16，暗底上几乎看不见",
            "状态：只有 Default（气泡不是按钮，不做 hover / pressed）",
        ], ("2 · NPC 气泡   " if npc else "3 · 玩家气泡   ") + nm))

    # ---- 4 / 5 侧栏条带 ----
    for wd, nm, place in ((300, "panel_side_300", "信使左栏（宽 300）"),
                          (260, "panel_card_260", "信使右栏卡（宽 260）")):
        full = stone(wd, 700, "bg.panel.deep", False, 2)
        c = full.crop((0, 0, wd, 240))
        d = ImageDraw.Draw(c)
        d.rectangle((wd - 1, 0, wd - 1, 239), fill=T["line.hairline"] + (235,))
        dashed_box(c, (18, 20, wd - 34, 96))
        dashed_box(c, (18, 118, wd - 34, 160))
        guide(c, 24, 24, 24, 24)
        items.append((paper(zoom(c, 1.8)), [
            "%d×700 · 九宫格 24 / 24 / 24 / 24" % wd,
            "图只画上半 240 —— 纵向无限延伸，画全高没有意义",
            "比主面板底再暗一档（bg.panel.deep）；**不描边**",
            "只在与主面板之间留 1px 金缝（图右侧那条）",
            "读法是「暗条坐进亮板里」，不是「再贴一块板」",
            "落点：%s；现在是 BlankWhite α0.08 白洗色，读成一层雾" % place,
            "状态：只有 Default",
        ], ("4 · 信使左栏条带   " if wd == 300 else "5 · 右栏卡条带   ") + nm))

    # ---- 6 列表行 ----
    c = stone(1000, 60, "bg.panel", True, 2)
    d = ImageDraw.Draw(c)
    d.line((1, 58, 998, 58), fill=(0, 0, 0), width=2)
    dashed_box(c, (16, 19, 420, 41))
    dashed_box(c, (700, 21, 880, 39))
    dashed_box(c, (930, 15, 984, 45))
    guide(c, 12, 12, 12, 12)
    items.append((paper(c), [
        "1000×60 · 九宫格 12 / 12 / 12 / 12",
        "暗石面比容器亮半档；**底边一条 1px 暗线**自报家门",
        "行与行靠这条暗线分开 —— 不要再叠一个分隔件（会读成双线）",
        "三段：左主文（撑满）· 右次要信息 · 右端动作位",
        "落点：世界事件收件箱 / 周报浏览 / 开发工具；现为 BlankWhite α0.08",
        "状态：Default / Hovered / Selected",
        "选中是「整圈金线变亮」，不是「把面提亮」",
    ], "6 · 列表行底   list_row"))

    # ---- 7 内凹槽 ----
    c = stone(400, 200, "bg.inset", False, 2)
    d = ImageDraw.Draw(c)
    d.rectangle((0, 0, 399, 1), fill=T["gold.dim"] + (245,))
    d.rectangle((0, 2, 399, 4), fill=(0, 0, 0, 110))
    for i in range(3):
        y0 = 22 + i * 56
        d.rectangle((14, y0, 386, y0 + 44), fill=T["bg.panel"] + (255,))
        d.rectangle((14, y0, 386, y0 + 44), outline=T["line.hairline"] + (150,))
    guide(c, 16, 16, 16, 16)
    items.append((paper(zoom(c, 1.5)), [
        "400×200 · 九宫格 16 / 16 / 16 / 16",
        "**内凹读法**：上沿 1px 金线 + 其下 2px 内阴影",
        "通体比背景暗一档（bg.inset）—— 凹的东西才比周围暗",
        "与列表行的关系是「凹槽里放行」：槽凹、行凸，两者不能同色",
        "落点：三弹窗的列表容器底",
        "状态：只有 Default",
    ], "7 · 内凹槽   inset_slot"))

    # ---- 8 细金线 ----
    c = Image.new("RGBA", (800, 3), (0, 0, 0, 0))
    d = ImageDraw.Draw(c)
    d.point((0, 0), fill=T["line.hairline"] + (60,))
    for x in range(800):
        d.point((x, 1), fill=T["line.hairline"] + (200,))
        d.point((x, 2), fill=T["line.hairline"] + (60,))
    z = Image.new("RGB", (832, 96), (78, 72, 66))
    z.paste(on(c, (78, 72, 66)), (16, 60))
    zz = zoom(c.crop((0, 0, 60, 3)), 12)
    z.paste(on(zz, (78, 72, 66)), (16, 20))
    items.append((z, [
        "800×3 · 不切九宫格，整条拉伸",
        "居中 1px 金线 + 上下各 1px 羽化透明（上：12× 放大预览）",
        "与 divider_gold 的分工：",
        "　divider_gold ＝ **块分隔**（头带下面那条）",
        "　hairline 　　 ＝ **行分隔**（条目之间、列表行之间）",
        "落点：条目之间 / 列表行之间",
        "状态：只有 Default",
    ], "8 · 细金线   hairline"))

    # ---- 9 小标记键 ----
    tiles = []
    for face, lab in (("fill.brass", "Default"), ("fill.hover", "Hovered"),
                      ("bg.inset", "Pressed")):
        t = stone(65, 25, face, True, 2)
        ic = sprite("icon_pin_48")
        if ic is not None:
            ic = ic.resize((16, 16), Image.LANCZOS)
            t.paste(ic, (24, 5), ic)
        tiles.append((zoom(t, 4), lab))
    ZW = sum(t[0].width + 22 for t in tiles)
    z = Image.new("RGB", (ZW, tiles[0][0].height + 26), (78, 72, 66))
    x = 0
    dz = ImageDraw.Draw(z)
    for t, lab in tiles:
        z.paste(on(t, (78, 72, 66)), (x, 0))
        dz.text((x + 4, t.height + 4), lab, font=f(13), fill=(232, 220, 200))
        x += t.width + 22
    items.append((z, [
        "65×25 · 九宫格 12 / 12 / 8 / 8",
        "纵向 8 比横向 12 小 —— 它只有一行高，纵向不需留边",
        "缩小的页签同族：brass 面 + 1px 金线",
        "**只做底板**，图内不烤文字、不烤图形；icon_pin 由 UI 侧叠（上图即叠好的样子）",
        "⚠️ 高只有 25 ⇒ 鼠标点击区偏小。视觉 25、命中区扩到 35",
        "落点：信使「置顶」键",
        "状态：Default / Hovered / Pressed",
    ], "9 · 小标记键底板   btn_chip_65"))

    # ---- 10 头像框 ----
    c = Image.new("RGBA", (200, 200), (0, 0, 0, 0))
    d = ImageDraw.Draw(c)
    d.rectangle((0, 0, 199, 199), outline=T["line.hairline"] + (255,))
    d.rectangle((5, 5, 194, 194), outline=T["gold.dim"] + (150,))
    # ⚠️ 四角刻饰**不能画在边框线上**（踩过：画上去被同位置的边框线盖住，图上什么都看不见）。
    #    正确做法＝**往内退一格**画两个短边，形成 L 形角标，颜色用比边框更亮的一档。
    L = 26
    for cx, cy, sx, sy in ((3, 3, 1, 1), (196, 3, -1, 1), (3, 196, 1, -1), (196, 196, -1, -1)):
        for k in range(L):
            a = int(255 * (1 - k / float(L)) ** 1.2)
            for t2 in (0, 1, 2):
                d.point((cx + k * sx, cy + t2 * sy), fill=T["gold.title"] + (a,))
                d.point((cx + t2 * sx, cy + k * sy), fill=T["gold.title"] + (a,))
    guide(c, 24, 24, 24, 24)
    items.append((paper(zoom(c, 2), 16), [
        "200×200 · 九宫格 24 / 24 / 24 / 24（面板里显示 95×90）",
        "**金只做线，内空**",
        "原版 frame_9 是金色实心框（#FFD700 α0.55）—— 那是「贴了一张金纸」",
        "自绘＝细金线 + 内退一条暗金线 + 四角各一道 26px 渐隐刻饰（图上四角）",
        "落点：信使右栏卡正中",
        "状态：只有 Default",
    ], "10 · 头像框   portrait_frame"))

    im, d = new(GAP_W, 4200, CARD)
    d.text((40, 26), "C · 缺口控件施工图 —— 10 件，图上颜色全部是真 token 值",
           font=f(21, True), fill=GOLD)
    d.text((40, 56), "虚线框 ＝ 文字/内容占位（sprite 里不烤文字）　金色细虚线 ＝ 九宫格边界　"
                     "金线在图上画到 α0.92，实际出图用 α0.60",
           font=f(13), fill=MUT)
    y = 100
    for img, notes, title in items:
        row = max(img.height, len(notes) * 21) + 46
        d.line((40, y, GAP_W - 40, y), fill=LINE)
        d.text((40, y + 11), title, font=f(17, True), fill=GOLD)
        im.paste(img, (40, y + 40))
        for i, ln in enumerate(notes):
            # ⚠️ 这些字要**画进图里**：markdown 的 `**` 不会被渲染，只会原样画出来（踩过）。
            ln = ln.replace("**", "")
            col = WARN if ln.startswith("⚠️") else (INK if i == 0 else LBL)
            d.text((NX, y + 44 + i * 21), ln, font=f(13, i == 0), fill=col)
        y += row
    im = im.crop((0, 0, GAP_W, y))
    p = os.path.join(OUT, "s_gaps.png")
    im.save(p)
    return p, im.size


def main():
    os.makedirs(OUT, exist_ok=True)
    # 五张图**补齐到同一宽度**（＝画布内栏宽），左对齐 ⇒ 放到画布上时左边界自动对齐。
    # 不补齐的话每张宽窄不一，画布上会排成锯齿。
    SHEET_W = 1820
    for fn in (sheet_tokens, sheet_shell, sheet_buttons, sheet_icons, sheet_gaps):
        p, sz = fn()
        im = Image.open(p).convert("RGB")
        if im.width != SHEET_W:
            c = Image.new("RGB", (SHEET_W, im.height), CARD)
            c.paste(im, (0, 0))
            c.save(p)
            im = c
        print("-> %-22s %s" % (os.path.basename(p), im.size))


if __name__ == "__main__":
    main()
