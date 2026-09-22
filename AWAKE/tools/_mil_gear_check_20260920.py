# -*- coding: utf-8 -*-
"""军事批 · 机制对表 v2：标签其实是 <EquipmentRoster>，属性跨行，重解。
"""
import io
import re
import sqlite3

GAME = r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules"
F = GAME + "/SandBoxCore/ModuleData/spnpccharacters.xml"
ITEMS_DIR = GAME + "/SandBoxCore/ModuleData/items"
DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"
OUT = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/_mil_gear_check_20260920.txt"

TARGETS = [
    "vlandian_banner_knight", "vlandian_knight", "vlandian_squire", "vlandian_voulgier",
    "khuzait_khans_guard", "khuzait_heavy_horse_archer",
    "druzhinnik", "druzhinnik_champion", "varyag", "varyag_veteran",
    "ghilman_tier_3", "ghilman_tier_2",
    "legion_of_the_betrayed_tier_3",
    "company_of_the_boar_tier_3",
]

txt = io.open(F, encoding="utf-8-sig").read()
con = sqlite3.connect("file:" + DB + "?mode=ro", uri=True)
cur = con.cursor()
CN = {}
for sid, t in cur.execute("select stringId, text from localization_entries where language='CNs'"):
    CN.setdefault(sid, t)


def i18n(raw):
    m = re.match(r"^\{=([^}]+)\}(.*)$", raw or "")
    if m:
        return CN.get(m.group(1)) or (m.group(2) or "").strip()
    return (raw or "").strip()


# 物品 id → 中文名：直接扫 items 目录的 XML（比 DB 表全）
ITEMCN = {}
import os
for root, _, files in os.walk(ITEMS_DIR):
    for fn in files:
        if not fn.lower().endswith(".xml"):
            continue
        try:
            it = io.open(os.path.join(root, fn), encoding="utf-8-sig").read()
        except Exception:
            continue
        for m in re.finditer(r'<(?:Item|CraftedItem)\s[^>]*?\bid="([^"]+)"[^>]*?\bname="([^"]*)"', it, re.S):
            ITEMCN[m.group(1)] = i18n(m.group(2))
        for m in re.finditer(r'<(?:Item|CraftedItem)\s[^>]*?\bname="([^"]*)"[^>]*?\bid="([^"]+)"', it, re.S):
            ITEMCN.setdefault(m.group(2), i18n(m.group(1)))
# DB 兜底
for eid, nm in cur.execute("select entityId, name from bannerlord_items"):
    e = str(eid).strip()
    ITEMCN.setdefault(e, i18n(nm))
    ITEMCN.setdefault(e.replace("Item.", ""), i18n(nm))

def cn_of(itemid):
    k = itemid.strip()
    return ITEMCN.get(k) or ITEMCN.get(k.replace("Item.", "")) or ""


buf = []
for tid in TARGETS:
    m = re.search(r'<NPCCharacter\s[^>]*?\bid="%s"([\s\S]*?)</NPCCharacter>' % re.escape(tid), txt)
    if not m:
        buf.append("### %s —— 未找到\n" % tid)
        continue
    body = m.group(1)
    nm = re.search(r'\bname="([^"]*)"', body)
    lv = re.search(r'\blevel="([^"]*)"', body)
    grp = re.search(r'\bdefault_group="([^"]*)"', body)
    sk = re.findall(r'<skill\s+id="([^"]+)"\s+value="([^"]+)"', body)
    buf.append("=" * 74)
    buf.append("### %s  |  %s  |  lv %s  |  %s" % (
        tid, i18n(nm.group(1)) if nm else "?", lv.group(1) if lv else "?", grp.group(1) if grp else "?"))
    buf.append("  技能: " + ", ".join("%s=%s" % (a, b) for a, b in sk))
    rosters = re.findall(r'<EquipmentRoster>([\s\S]*?)</EquipmentRoster>', body)
    for ri, rb in enumerate(rosters):
        pairs = re.findall(r'slot="([^"]+)"[\s\n]*id="([^"]+)"', rb)
        if ri == 0:
            for slot, item in pairs:
                buf.append("      %-8s %-42s %s" % (slot, item, cn_of(item)))
    buf.append("  （EquipmentRoster 共 %d 套）" % len(rosters))
    buf.append("")

io.open(OUT, "w", encoding="utf-8", newline="\n").write("\n".join(buf))
print("\n".join(buf))
print("已写:", OUT)
con.close()
