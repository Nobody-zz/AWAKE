# -*- coding: utf-8 -*-
"""dump 帝国具装骑兵在 spnpccharacters.xml 里的原始块 + 物品表结构。"""
import io
import re
import sqlite3

GAME = r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules"
F = GAME + "/SandBoxCore/ModuleData/spnpccharacters.xml"
DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"

txt = io.open(F, encoding="utf-8-sig").read()

con = sqlite3.connect("file:" + DB + "?mode=ro", uri=True)
cur = con.cursor()
print("--- bannerlord_items 列 ---")
print([r[1] for r in cur.execute("PRAGMA table_info(bannerlord_items)").fetchall()])
con.close()

for tid in ("imperial_cataphract", "imperial_elite_cataphract", "vlandian_banner_knight"):
    m = re.search(r'<NPCCharacter\s+id="%s"[\s\S]*?</NPCCharacter>' % re.escape(tid), txt)
    print()
    print("=" * 72)
    print("###", tid, "找到" if m else "未找到")
    print("=" * 72)
    if m:
        print(m.group(0)[:2600])
