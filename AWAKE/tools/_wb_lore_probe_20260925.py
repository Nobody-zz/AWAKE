# -*- coding: utf-8 -*-
"""概念类素材探针（09-25）：为 23 个空子域找 A 级素材来源。
只读 BannerlordSage 索引。产出各主题的官方文本命中情况。
"""
import sqlite3
import re
import json
import io

DB = r"C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db"
con = sqlite3.connect("file:%s?mode=ro" % DB, uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()

print("=== 1. localization 里世界观设定文的落点文件 top ===")
cur.execute("""
SELECT filePath, COUNT(*) n FROM localization_entries
WHERE language='CNs' AND filePath LIKE '%lore%' GROUP BY filePath ORDER BY n DESC LIMIT 20
""")
for r in cur.fetchall():
    print("  %5d  %s" % (r["n"], r["filePath"]))

print()
print("=== 2. std_world_lore_strings 命中数 ===")
cur.execute("""
SELECT COUNT(*) n FROM localization_entries
WHERE language='CNs' AND filePath LIKE '%world_lore%'
""")
print("  ", cur.fetchone()["n"])

print()
print("=== 3. 关键概念词在官方文本里的命中（中文 CNs）===")
WORDS = ["税", "币", "第纳尔", "债", "商队", "商路", "工坊", "婚", "葬", "神",
         "节", "语言", "方言", "气候", "季风", "冬", "道路", "海路", "渡口",
         "俘虏", "赎金", "围城", "攻城", "战术", "阵法", "补给", "继承",
         "王位", "官职", "头人", "长老", "酋长", "荣誉", "誓言", "决斗"]
for w in WORDS:
    cur.execute("""
    SELECT COUNT(*) n FROM localization_entries
    WHERE language='CNs' AND text LIKE ?
    """, ("%" + w + "%",))
    print("  %-6s %5d" % (w, cur.fetchone()["n"]))

print()
print("=== 4. 设定文样例（world_lore，前 12 条正文）===")
cur.execute("""
SELECT stringId, text FROM localization_entries
WHERE language='CNs' AND filePath LIKE '%world_lore%' LIMIT 12
""")
for r in cur.fetchall():
    print("  [%s] %s" % (r["stringId"], r["text"][:150]))

print()
print("=== 5. 城镇描述文（descriptionText）样例 ===")
cur.execute("SELECT settlementId, descriptionText FROM bannerlord_settlements WHERE descriptionText IS NOT NULL LIMIT 3")
for r in cur.fetchall():
    print("  %s -> %s" % (r["settlementId"], str(r["descriptionText"])[:200]))

print()
print("=== 6. 文化表 ===")
cur.execute("SELECT cultureId, name FROM bannerlord_cultures ORDER BY cultureId")
for r in cur.fetchall():
    print("  %-16s %s" % (r["cultureId"], r["name"]))

print()
print("=== 7. 王国表 ===")
cur.execute("SELECT kingdomId, name, culture FROM bannerlord_kingdoms ORDER BY kingdomId")
for r in cur.fetchall():
    print("  %-16s %-40s %s" % (r["kingdomId"], r["name"], r["culture"]))

con.close()
