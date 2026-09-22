# -*- coding: utf-8 -*-
"""查帝国具装骑兵在**游戏数据**里的真实装备与属性：能不能举盾、有没有骑枪。
一手源＝SandBoxCore/ModuleData/spnpccharacters.xml 的 EquipmentSet。
"""
import io
import os
import re
import sqlite3

GAME = r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules"
F = GAME + "/SandBoxCore/ModuleData/spnpccharacters.xml"
DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"

txt = io.open(F, encoding="utf-8-sig").read()
print("spnpccharacters.xml 字节:", len(txt))

TARGETS = ["imperial_cataphract", "imperial_elite_cataphract", "imperial_heavy_horseman",
           "imperial_equite", "vlandian_banner_knight", "vlandian_knight"]

con = sqlite3.connect("file:" + DB + "?mode=ro", uri=True)
cur = con.cursor()
CN = {}
for sid, t in cur.execute("select stringId, text from localization_entries where language='CNs'"):
    CN.setdefault(sid, t)


def i18n(raw):
    """{=token}名字 → 中文名"""
    m = re.match(r"^\{=([^}]+)\}(.*)$", raw or "")
    if m:
        return CN.get(m.group(1), m.group(2))
    return raw or ""


for tid in TARGETS:
    m = re.search(r'<NPCCharacter\s+id="%s"[\s\S]*?</NPCCharacter>' % re.escape(tid), txt)
    if not m:
        print("\n### %s  —— 未找到" % tid)
        continue
    blk = m.group(0)
    print("\n" + "=" * 70)
    print("### %s" % tid)
    print("=" * 70)
    nm = re.search(r'\bname="([^"]*)"', blk)
    print("  名:", i18n(nm.group(1)) if nm else "?")
    lv = re.search(r'\blevel="([^"]*)"', blk)
    print("  等级:", lv.group(1) if lv else "?")

    sets = re.findall(r'<EquipmentSet\s+([^>]*?)>([\s\S]*?)</EquipmentSet>', blk)
    for si, (attrs, body) in enumerate(sets):
        tag = re.search(r'id="([^"]*)"', attrs)
        print("  --- EquipmentSet[%d] %s ---" % (si, tag.group(1) if tag else ""))
        for slot, item in re.findall(r'<equipment\s+slot="([^"]+)"\s+id="([^"]+)"', body):
            print("      %-16s %-34s %s" % (slot, item, i18n("{=" + item.replace("Item.", "") + "}" ) if False else ""))

    # 直接从 Item 表补中文名
    print("  [装备中文名]")
    for slot, item in re.findall(r'<equipment\s+slot="([^"]+)"\s+id="([^"]+)"', blk):
        row = cur.execute("select name from bannerlord_items where itemId=?", (item,)).fetchone()
        raw = row[0] if row else None
        cn = i18n(raw) if raw else "(不在 bannerlord_items 表)"
        print("      %-16s %-34s %s" % (slot, item, cn))

con.close()
