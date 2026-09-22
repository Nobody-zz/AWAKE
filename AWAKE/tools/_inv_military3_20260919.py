# -*- coding: utf-8 -*-
"""军事批 · 取数第三轮（只读）：物品分类面、现有 poc 快照覆盖、兵种树与官方中文名。"""
import io
import sqlite3
from collections import Counter

import yaml

REPO = r"D:\AWAKE-Dev\AWAKE"
SRC = REPO + r"\tools\worldbook-studio\workspace\full-geo1\authoring\sources"
DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"
con = sqlite3.connect("file:" + DB + "?mode=ro", uri=True)
cur = con.cursor()

print("=== 现有 poc 快照覆盖什么 ===")
for n in ("game-items-poc1.txt", "game-items-poc2.txt"):
    print("--", n)
    for ln in io.open(REPO + "\\tools\\worldbook-studio\\workspace\\full-geo1\\authoring\\sources\\" + n,
                      encoding="utf-8").read().splitlines()[:6]:
        print("   ", ln[:120])

print()
print("=== bannerlord_items: itemType 分布 ===")
for k, c in cur.execute("select itemType, count(*) from bannerlord_items group by itemType order by 2 desc"):
    print("   %-28s %d" % (k, c))

print()
print("=== bannerlord_items: pieceType 分布（装备类）===")
for k, c in cur.execute("""select pieceType, count(*) from bannerlord_items
                           where pieceType is not null and pieceType<>''
                           group by pieceType order by 2 desc limit 40"""):
    print("   %-28s %d" % (k, c))

print()
print("=== 武器类（itemType 含 weapon 的行）按 itemType 细分 ===")
for k, c in cur.execute("""select itemType, count(*) from bannerlord_items
                           where lower(itemType) like '%weapon%' or pieceType is null
                           group by itemType order by 2 desc limit 30"""):
    print("   %-28s %d" % (k, c))

print()
print("=== 官方中文名可得性（物品 / 兵种）===")
tot = cur.execute("select count(*) from bannerlord_items").fetchone()[0]
have = cur.execute("""select count(*) from bannerlord_items i where exists
    (select 1 from localization_entries l where l.language='CNs' and l.stringId=i.name)""").fetchone()[0]
print("   物品: %d / %d 有 CNs（%.1f%%）" % (have, tot, 100.0 * have / tot))
tot2 = cur.execute("select count(*) from bannerlord_troops where isHero=0").fetchone()[0]
have2 = cur.execute("""select count(*) from bannerlord_troops t where t.isHero=0 and exists
    (select 1 from localization_entries l where l.language='CNs' and l.stringId=t.name)""").fetchone()[0]
print("   兵种(非英雄): %d / %d 有 CNs（%.1f%%）" % (have2, tot2, 100.0 * have2 / tot2))

print()
print("=== 兵种树样本：帝国 Soldier 系（按 characterId）===")
q = """select characterId, name, level, occupation, upgradeTargetsJson from bannerlord_troops
       where culture='Culture.empire' and occupation='Soldier' limit 14"""
for row in cur.execute(q):
    print("   %-44s lv%-4s up=%s" % (row[0], row[2], (row[4] or "")[:100]))
con.close()
