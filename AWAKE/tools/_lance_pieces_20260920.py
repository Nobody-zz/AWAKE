# -*- coding: utf-8 -*-
"""对比帝国与瓦兰迪亚的「长枪/骑枪」在部件层的数据，找 couch（骑枪冲锋）能力的判据。"""
import io
import os
import re

GAME = r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules"
WF = []
for p in ("SandBoxCore/ModuleData/items/weapons.xml",
          "Native/ModuleData/items/weapons.xml",
          "SandBox/ModuleData/items/weapons.xml",
          "SandBoxCore/ModuleData/crafting_pieces.xml",
          "SandBoxCore/ModuleData/items/crafting_pieces.xml"):
    f = os.path.join(GAME, p)
    if os.path.exists(f):
        WF.append(f)
        print("存在:", p, os.path.getsize(f))
    else:
        print("缺失:", p)

items_txt = io.open(WF[0], encoding="utf-8-sig").read()

# 1) 所有名字含 lance 的 CraftedItem
print()
print("=" * 74)
print("CraftedItem 里名字含 Lance 的")
print("=" * 74)
for m in re.finditer(r"<CraftedItem\b([\s\S]*?)</CraftedItem>", items_txt):
    blk = m.group(1)
    nm = re.search(r'name="([^"]*)"', blk)
    if not nm or "lance" not in nm.group(1).lower():
        continue
    cid = re.search(r'id="([^"]*)"', blk)
    cul = re.search(r'culture="([^"]*)"', blk)
    tmpl = re.search(r'crafting_template="([^"]*)"', blk)
    blades = re.findall(r'id="([^"]+)"\s*\n?\s*Type="([^"]+)"', blk)
    print("  %-26s %-22s %-14s %-18s %s" % (
        cid.group(1) if cid else "?", nm.group(1).replace("{=", "").split("}")[-1][:22],
        (cul.group(1) if cul else "").replace("Culture.", ""),
        tmpl.group(1) if tmpl else "",
        [b[0] for b in blades if b[1] == "Blade"]))

# 2) crafting_pieces 里 blade 的 Weapon 行
pieces = [f for f in WF if "crafting_pieces" in f]
if pieces:
    ptxt = io.open(pieces[0], encoding="utf-8-sig").read()
    print()
    print("=" * 74)
    print("crafting_pieces:", pieces[0].split("Modules/")[-1])
    print("=" * 74)
    for bid in ("spear_blade_4", "spear_blade_41"):
        m = re.search(r'<CraftingPiece\b[^>]*id="%s"[\s\S]{0,1500}?</CraftingPiece>' % bid, ptxt)
        print()
        print("--- %s ---" % bid)
        print(m.group(0)[:900] if m else "未找到")
else:
    print()
    print("!! 未找到 crafting_pieces.xml，列 Native/SandBox 模块的 ModuleData 顶层:")
    for mod in os.listdir(GAME):
        d = os.path.join(GAME, mod, "ModuleData")
        if os.path.isdir(d):
            hits = [x for x in os.listdir(d) if "craft" in x.lower()]
            if hits:
                print("   ", mod, hits)
