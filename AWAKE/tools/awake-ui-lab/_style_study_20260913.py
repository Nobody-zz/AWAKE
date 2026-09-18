# -*- coding: utf-8 -*-
"""画风功课：原版 UI 组件 / 对话图标 拼版，便于肉眼读风格。"""
import os
import glob
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw

GAME = r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord"
_p = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_p, "awake_ui.py")):
    _p = os.path.dirname(_p)
LAB = _p
OUT = os.path.join(LAB, "out", "style-study")
os.makedirs(OUT, exist_ok=True)
MODULES = ("Native", "SandBox", "SandBoxCore", "StoryMode", "Multiplayer")

index = {}
for m in MODULES:
    for p in glob.glob(os.path.join(GAME, "Modules", m, "GUI", "*SpriteData.xml")):
        try:
            root = ET.parse(p).getroot()
        except Exception:
            continue
        for part in root.iter("SpritePart"):
            nm = part.get("Name") or part.findtext("Name")
            if not nm:
                continue
            try:
                index[nm] = (part.findtext("CategoryName"),
                             int(part.findtext("SheetID") or 1),
                             int(part.findtext("SheetX") or 0),
                             int(part.findtext("SheetY") or 0),
                             int(part.findtext("Width") or 0),
                             int(part.findtext("Height") or 0))
            except ValueError:
                pass


def checker(dr, x, y, w, h, s=12):
    for by in range(0, h, s):
        for bx in range(0, w, s):
            c = (58, 58, 64) if ((bx // s + by // s) % 2 == 0) else (42, 42, 48)
            dr.rectangle([x + bx, y + by, x + min(bx + s - 1, w - 1),
                          y + min(by + s - 1, h - 1)], fill=c)


def montage(items, path, cols=5, cellw=340, maxdim=180, pad=12, title_h=30):
    """items: list of (label, PIL.Image)"""
    rows = [items[i:i + cols] for i in range(0, len(items), cols)]
    heights = []
    for r in rows:
        h = 0
        for _lbl, im in r:
            sc = min(4.0, maxdim / max(im.width, im.height, 1))
            sc = max(sc, 1.0)
            h = max(h, int(im.height * sc))
        heights.append(h + title_h + pad * 2)
    W = cols * cellw + pad
    H = sum(heights) + pad
    canvas = Image.new("RGB", (W, H), (22, 22, 26))
    dr = ImageDraw.Draw(canvas)
    y = pad
    for ri, r in enumerate(rows):
        for ci, (lbl, im0) in enumerate(r):
            im0 = im0.convert("RGBA")
            sc = min(4.0, maxdim / max(im0.width, im0.height, 1))
            sc = max(sc, 1.0)
            im = im0.resize((max(1, int(im0.width * sc)), max(1, int(im0.height * sc))), Image.NEAREST)
            x = ci * cellw + pad
            yy = y + title_h
            checker(dr, x - 4, yy - 4, min(im.width + 8, cellw - 8), im.height + 8)
            canvas.paste(im, (x, yy), im)
            dr.text((x, y + 8), "%s  (%dx%d)" % (lbl[:40], im0.width, im0.height), fill=(235, 235, 235))
        y += heights[ri]
    canvas.save(path)
    print("saved", os.path.basename(path), canvas.size)


# ---- 1. 对话系 28 个 sprite（拆到像素级看图标语言）
sheet = Image.open(os.path.join(LAB, "out", "atlas", "ui_conversation_1.png")).convert("RGBA")
convs = sorted([(n, v) for n, v in index.items() if v[0] == "ui_conversation"],
               key=lambda t: -(t[1][4] * t[1][5]))


def crop(v):
    _c, _s, x, y, w, h = v
    return sheet.crop((x, y, x + w, y + h))


montage([(n.split("\\")[-1], crop(v)) for n, v in convs],
        os.path.join(OUT, "01_conversation_icons.png"), cols=7, cellw=300, maxdim=170)

# ---- 2. 界面骨架类 sprite（已抽出的 33 个，去掉九宫格派生）
spdir = os.path.join(LAB, "out", "atlas", "sprites")
items = []
for f in sorted(os.listdir(spdir)):
    if not f.endswith(".png") or f.startswith("__nine_"):
        continue
    im = Image.open(os.path.join(spdir, f))
    if im.width >= 300 or im.height >= 300:
        continue
    items.append((f[:-4].replace("__", "\\"), im))
montage(items, os.path.join(OUT, "02_chrome_icons.png"), cols=6, cellw=320, maxdim=160)

# ---- 3. 大件单独存（石板/按钮/底板）
big = []
for f in sorted(os.listdir(spdir)):
    if not f.endswith(".png") or f.startswith("__nine_"):
        continue
    im = Image.open(os.path.join(spdir, f))
    if im.width >= 300 or im.height >= 300:
        big.append((f[:-4].replace("__", "\\"), im))
montage(big, os.path.join(OUT, "03_chrome_large.png"), cols=2, cellw=680, maxdim=420)

# ---- 4. 九宫格扩展样例（看边距怎么切）
nine = []
for f in sorted(os.listdir(spdir)):
    if f.startswith("__nine_"):
        continue
for f in sorted(os.listdir(spdir)):
    if f.startswith("__nine_"):
        nine.append((f[7:-4], Image.open(os.path.join(spdir, f))))
montage(nine, os.path.join(OUT, "04_nine_slices.png"), cols=2, cellw=560, maxdim=260)

# ---- 逐 sprite 落盘
d = os.path.join(OUT, "conversation_sprites")
os.makedirs(d, exist_ok=True)
for n, v in convs:
    crop(v).save(os.path.join(d, n.split("\\")[-1] + ".png"))
print("conversation sprites ->", d)
