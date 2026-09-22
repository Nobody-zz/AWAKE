# -*- coding: utf-8 -*-
"""军事批 · 取数第六轮：直接问 XML 索引表 —— 近战武器到底在哪个文件、有多少。"""
import sqlite3
from collections import Counter

DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"
con = sqlite3.connect("file:" + DB + "?mode=ro", uri=True)
cur = con.cursor()

print("=== xml_files 列 ===")
print([r[1] for r in cur.execute("pragma table_info(xml_files)")])
print("=== xml_entities 列 ===")
print([r[1] for r in cur.execute("pragma table_info(xml_entities)")])

print()
print("=== 与武器/装备/兵种有关的 XML 文件 ===")
for f, n in cur.execute("""select filePath, count(*) from xml_entities
                           group by filePath order by 2 desc"""):
    low = f.lower()
    if any(k in low for k in ("weapon", "craft", "troop", "npccharacter", "armor", "shield", "item")):
        print("   %-64s %d" % (f, n))

print()
print("=== weapons.xml 里的实体按类型 ===")
rows = list(cur.execute("""select entityKind, count(*) from xml_entities
                           where filePath like '%items/weapons.xml%' group by entityKind"""))
for r in rows:
    print("   ", r)

print()
print("=== crafting_templates / crafted items 在哪 ===")
for f, n in cur.execute("""select filePath, count(*) from xml_entities
                           where filePath like '%crafting%' group by filePath order by 2 desc"""):
    print("   %-64s %d" % (f, n))

print()
print("=== 兵种 XML ===")
for f, n in cur.execute("""select filePath, count(*) from xml_entities
                           where filePath like '%troops%' or filePath like '%NPCCharacters%'
                           group by filePath order by 2 desc limit 15"""):
    print("   %-64s %d" % (f, n))
con.close()
