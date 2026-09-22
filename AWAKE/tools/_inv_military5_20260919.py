# -*- coding: utf-8 -*-
"""军事批 · 取数第五轮：把两件事问清。
  ① 库里有哪 43 张表、有没有打造（crafting）相关表；
  ② weapons.xml 到底装了哪 81 件（战役近战武器是不是根本不在这里）；
  ③ 兵种按「文化 × 职业」全貌 ＋ 匪帮／雇佣／民兵这几条线。
"""
import io
import json
import re
import sqlite3
from collections import Counter, defaultdict

DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"
con = sqlite3.connect("file:" + DB + "?mode=ro", uri=True)
cur = con.cursor()
TOK = re.compile(r"^\{=([^}]+)\}(.*)$")


def parse(v):
    if not v:
        return None, ""
    m = TOK.match(v.strip())
    return (m.group(1), m.group(2).strip()) if m else (None, v.strip())


CN = {}
for sid, txt in cur.execute("select stringId, text from localization_entries where language='CNs'"):
    CN.setdefault(sid, txt)

print("=== ① 全部表 ===")
tabs = [r[0] for r in cur.execute("select name from sqlite_master where type='table' order by name")]
for i, n in enumerate(tabs):
    c = cur.execute("select count(*) from [%s]" % n).fetchone()[0]
    mark = "  <== 打造/武器相关" if any(k in n.lower() for k in ("craft", "weapon", "piece")) else ""
    print("   %-42s %-8s%s" % (n, c, mark))

print()
print("=== ② weapons.xml / tournament 里的 81 件 ===")
for row in cur.execute("""select entityId, name, itemType, filePath from bannerlord_items
                          where filePath like '%items/weapons.xml%'
                             or filePath like '%tournament_weapons.xml%'
                          order by itemType, entityId"""):
    eid, nm, it, fp = row
    t, en = parse(nm)
    print("   %-40s %-16s %-10s %s" % (eid, CN.get(t, "?"), it, en))

print()
print("=== ③ 兵种全貌：文化 × 职业 ===")
rows = list(cur.execute("""select characterId, name, level, culture, occupation, upgradeTargetsJson
                           from bannerlord_troops
                           where isHero=0 and characterId not like 'mp\\_%' escape '\\'"""))
g = defaultdict(list)
for cid, nm, lv, cu, oc, up in rows:
    g[((cu or "").replace("Culture.", ""), oc or "-")].append((cid, lv))
for k in sorted(g, key=lambda x: (-len(g[x]), x)):
    print("   %-24s %-14s %3d 个" % (k[0], k[1], len(g[k])))

print()
print("=== 非六文化主线的兵种线（匪帮／雇佣／民兵／中立）===")
KEY = ("bandit", "raider", "looter", "company_of", "brotherhood", "militia",
       "mercenary", "conspiracy", "deserter", "vakken", "nord", "darshi", "chieftain")
seenline = defaultdict(list)
for cid, nm, lv, cu, oc, up in rows:
    low = cid.lower()
    if any(k in low for k in KEY):
        t, en = parse(nm)
        seenline[(cu or "").replace("Culture.", "")].append((lv, cid, CN.get(t, "?"), up or ""))
for cu in sorted(seenline):
    print("   【%s】" % cu)
    for lv, cid, cn, up in sorted(seenline[cu], key=lambda x: (x[0] or "", x[1])):
        print("      lv%-3s %-42s %-18s -> %s" % (lv, cid, cn, up.replace("NPCCharacter.", "")[:56]))

print()
print("=== 兵种装备线索：skillTemplate 取值分布 ===")
print("   ", Counter(r[5] for r in cur.execute(
    "select skillTemplate from bannerlord_troops where isHero=0")).most_common(10))
con.close()
