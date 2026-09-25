# -*- coding: utf-8 -*-
"""建「中文家族名 → entity.clan.<code>」与「中文英雄名 → entity.hero.<code>」索引，
用于把 27 条 bio 里点名的家族挂到正确的实体上。
"""
import sqlite3
import json
import io
import re

DB = r"C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db"
REG = r"D:\AWAKE-Dev\AWAKE\docs\mappings\persona-entity\generations\b1-7590085e3662512d7c1c6646c529694bda9850146234827b2dafbd9f20c6a2b3\entity-registry.v1.json"
OUT = r"D:\AWAKE-Dev\AWAKE\tools\_uw_name_index_20260925.json"

con = sqlite3.connect("file:%s?mode=ro" % DB, uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()
reg = json.load(io.open(REG, encoding="utf-8"))


def cn_of(token):
    if not token:
        return None
    m = re.match(r"^\{=([^}]+)\}(.*)$", token, re.S)
    if not m:
        return None
    cur2 = con.cursor()
    cur2.execute("SELECT text FROM localization_entries WHERE language='CNs' AND stringId=? LIMIT 1", (m.group(1),))
    r = cur2.fetchone()
    return r["text"] if r else None


# 家族：registry 的 display_name_zh 就是官方中文名（已在别处核过一致性）
clans = {}
for c in reg["entities"]:
    if c["kind"] != "clan":
        continue
    nm = c.get("display_name_zh")
    if nm:
        clans.setdefault(nm, []).append(c["entity_id"])
        # 变体：去掉「戴·」「芬·」「巴努·」前缀后也建索引
        for pre in ("戴·", "芬·", "巴努·"):
            if nm.startswith(pre):
                clans.setdefault(nm[len(pre):], []).append(c["entity_id"])

# 英雄：display_name_zh 去引号
heroes = {}
for h in reg["entities"]:
    if h["kind"] != "hero":
        continue
    nm = h.get("display_name_zh")
    if nm:
        heroes.setdefault(nm.replace("“", "").replace("”", ""), []).append(h["entity_id"])

print("家族中文名索引:", len(clans))
print("英雄中文名索引:", len(heroes))

# 把 27 条 bio 里出现的家族名标出来
bio = io.open(r"D:\AWAKE-Dev\AWAKE\tools\worldbook-studio\workspace\full-geo1\authoring\sources\game-hero-bio.txt",
              encoding="utf-8").read()
print()
print("=== 27 条 bio 里点名的家族 ===")
hit = {}
for k, v in [l.split(" => ", 1) for l in bio.strip().split("\n") if " => " in l]:
    for nm, eids in clans.items():
        if nm and len(nm) >= 2 and nm in v:
            hit.setdefault(nm, set()).update(eids)
for nm, eids in sorted(hit.items(), key=lambda x: -len(x[1])):
    print("  %-14s -> %s" % (nm, sorted(eids)))

json.dump({"clans": clans, "heroes": heroes, "bio_named_clans": {k: sorted(v) for k, v in hit.items()}},
          io.open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print()
print("索引写出", OUT)
con.close()
