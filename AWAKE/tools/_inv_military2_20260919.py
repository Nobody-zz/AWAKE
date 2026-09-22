# -*- coding: utf-8 -*-
"""军事批 · 取数明细（只读 DB）：兵种分布、兵种树、物品分类、官方中文名可得性。"""
import io
import sqlite3
from collections import Counter

DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"
con = sqlite3.connect("file:" + DB + "?mode=ro", uri=True)
cur = con.cursor()


def cols(t):
    return [r[1] for r in cur.execute("pragma table_info([%s])" % t)]


print("=== bannerlord_items 列 ===")
print(cols("bannerlord_items"))
print("=== localization_entries 列 ===")
print(cols("localization_entries"))

print()
print("=== 兵种：按 isHero / culture / occupation 分布 ===")
for row in cur.execute("select isHero, count(*) from bannerlord_troops group by isHero"):
    print("   isHero=%s -> %d" % row)
print("   culture 分布（前 25）:")
for row in cur.execute("""select culture, count(*) c from bannerlord_troops
                          group by culture order by c desc limit 25"""):
    print("      %-22s %d" % row)
print("   occupation 分布:")
for row in cur.execute("""select occupation, count(*) c from bannerlord_troops
                          group by occupation order by c desc limit 25"""):
    print("      %-22s %d" % row)
print("   level 范围:", cur.execute("select min(level), max(level) from bannerlord_troops").fetchone())

print()
print("=== 非英雄兵种样本（帝国）===")
for row in cur.execute("""select characterId, name, level, culture, occupation,
                          length(upgradeTargetsJson) from bannerlord_troops
                          where culture='empire' and (isHero=0 or isHero is null)
                          order by level limit 12"""):
    print("   ", row)

print()
print("=== 兵种表里 level 分布 ===")
for row in cur.execute("""select level, count(*) c from bannerlord_troops
                          group by level order by level"""):
    print("   lv%-3s %d" % row)
con.close()
