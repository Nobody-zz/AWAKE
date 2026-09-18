# -*- coding: utf-8 -*-
"""实测：模组自绘 sprite 的「分类名 <-> 贴图文件」对应关系。"""
import os
import glob
import xml.etree.ElementTree as ET

G = r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules"

print("=" * 74)
print("【1】所有模块的 SpriteData 文件")
for p in sorted(glob.glob(os.path.join(G, "*", "GUI", "*.xml"))):
    if "SpriteData" in os.path.basename(p):
        print("   ", os.path.relpath(p, G))

print()
print("=" * 74)
print("【2】所有 GUI 下的 PNG / DDS / jpg（自绘贴图实际落点）")
for root, dirs, files in os.walk(G):
    if "GUI" not in root.replace("\\", "/").split("/"):
        continue
    for f in files:
        if f.lower().endswith((".png", ".dds", ".tpac")):
            print("   ", os.path.relpath(os.path.join(root, f), G))

print()
print("=" * 74)
print("【3】SpriteSheets 目录 -> 文件（模组贴图集容器）")
for d in sorted(glob.glob(os.path.join(G, "*", "GUI", "SpriteSheets", "*"))):
    if os.path.isdir(d):
        print("   ", os.path.relpath(d, G), "->", os.listdir(d))

print()
print("=" * 74)
print("【4】分类名 -> sprite 名前缀（看命名空间惯例）")
for p in sorted(glob.glob(os.path.join(G, "*", "GUI", "*SpriteData.xml"))):
    mod = p.split(os.sep)[-3]
    try:
        root = ET.parse(p).getroot()
    except Exception as e:
        print("   !! parse fail", p, e)
        continue
    cats = [c.findtext("Name") for c in root.iter("SpriteCategory")]
    parts = list(root.iter("SpritePart"))
    print("   [%s] %s" % (mod, p.split(os.sep)[-1]))
    print("        categories:", cats[:8])
    print("        sprite 数: %d" % len(parts))
    for sp in parts[:4]:
        print("          %-40s cat=%-16s %sx%s @(%s,%s)" % (
            sp.findtext("Name"), sp.findtext("CategoryName"),
            sp.findtext("Width"), sp.findtext("Height"),
            sp.findtext("SheetX"), sp.findtext("SheetY")))
