# -*- coding: utf-8 -*-
"""盘点 80 个待补家族的可用素材：官方 descriptionText / 成员 / 王国 / 文化 / 主城。"""
import sqlite3
import json
import io
import re
import os

DB = r"C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db"
REG = r"D:\AWAKE-Dev\AWAKE\docs\mappings\persona-entity\generations\b1-7590085e3662512d7c1c6646c529694bda9850146234827b2dafbd9f20c6a2b3\entity-registry.v1.json"
AU = r"D:\AWAKE-Dev\AWAKE\tools\worldbook-studio\workspace\full-geo1\authoring"

con = sqlite3.connect("file:%s?mode=ro" % DB, uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()

reg = json.load(io.open(REG, encoding="utf-8"))
clans = [e for e in reg["entities"] if e["kind"] == "clan"]

# 已绑 clan 的档
covered = set()
for f in os.listdir(AU):
    if not f.endswith(".yaml"):
        continue
    t = io.open(os.path.join(AU, f), encoding="utf-8").read()
    for m in re.finditer(r"^\s*-\s*(entity\.clan\.[A-Za-z0-9_.\-]+)\s*$", t, re.M):
        covered.add(m.group(1))

need = [c for c in clans if c["entity_id"] not in covered]
print("注册 clan:", len(clans), " 已绑:", len(covered), " 待补:", len(need))
print()

def cn_of(token):
    if not token:
        return None, None
    m = re.match(r"^\{=([^}]+)\}(.*)$", token, re.S)
    if not m:
        return None, token
    cur2 = con.cursor()
    cur2.execute("SELECT text FROM localization_entries WHERE language='CNs' AND stringId=? LIMIT 1", (m.group(1),))
    r = cur2.fetchone()
    return (r["text"] if r else None), m.group(2)


withdesc = 0
rows = []
for c in need:
    code = c["clan_code"]
    cur.execute("SELECT name, descriptionText, culture, superFaction, initialHomeSettlement, tier, isNoble, isMercenary, isMinorFaction FROM bannerlord_clans WHERE clanId=?", (code,))
    g = cur.fetchone()
    zh, en = (None, None)
    desc_zh = None
    kd = (c.get("related_codes") or {}).get("kingdom", "")
    home = (c.get("related_codes") or {}).get("home_settlement", "")
    if g:
        zh, en = cn_of(g["name"])
        if g["descriptionText"]:
            dzh, den = cn_of(g["descriptionText"])
            if dzh:
                desc_zh = dzh
                withdesc += 1
    rows.append({
        "code": code, "eid": c["entity_id"], "name_zh": zh or c.get("display_name_zh"),
        "name_en": en, "kingdom": kd, "home": home,
        "members": len(c.get("member_entity_ids") or []),
        "tier": g["tier"] if g else None,
        "desc": desc_zh,
    })

print("有官方 descriptionText 的:", withdesc, "/", len(need))
print()
print("=== 样例 ===")
for r in rows[:2]:
    print(json.dumps(r, ensure_ascii=False, indent=1))
print()
print("=== 有描述的家族（前 15）===")
n = 0
for r in rows:
    if r["desc"]:
        n += 1
        if n <= 15:
            print("  %-26s %-12s | %s" % (r["code"], r["name_zh"], r["desc"][:110]))

json.dump(rows, io.open(r"D:\AWAKE-Dev\AWAKE\tools\_uw_clan_roster_20260925.json", "w", encoding="utf-8"),
          ensure_ascii=False, indent=1)
print()
print("roster 写出 tools/_uw_clan_roster_20260925.json  (%d 条)" % len(rows))
con.close()
