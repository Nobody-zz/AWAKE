# -*- coding: utf-8 -*-
"""钉死 10.1 那批（写在 DefaultItems.cs）的贸易品在库里的状态与官方中文名。"""
import sqlite3, re, io, json

DB = r'C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db'
con = sqlite3.connect('file:%s?mode=ro' % DB.replace('\\', '/'), uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()

def cn_of(token):
    if not token: return ""
    m = re.search(r'\{=([^}]+)\}', token)
    if not m: return token
    key = m.group(1)
    c2 = con.cursor()
    c2.execute("SELECT text FROM localization_entries WHERE language='CNs' AND stringId=? LIMIT 1", (key,))
    r = c2.fetchone()
    if not r: return ""
    t = r[0]
    t = re.sub(r'\{@Plural\}.*?\{\\@\}', '', t, flags=re.S)   # 掐复数尾巴（{\@} 带反斜杠）
    t = re.sub(r'\{@Plural\}.*?\{@\}', '', t, flags=re.S)
    t = re.sub(r'\{[^}]*\}', '', t)
    return t.strip()

IDS = ["grain","meat","hides","hardwood","charcoal","iron",
       "ironIngot1","ironIngot2","ironIngot3","ironIngot4","ironIngot5","ironIngot6",
       "planks","felt","tools","_itemTrash","mule","saddle_horse","old_horse","camel","pack_camel"]

print("== 逐 id 查 bannerlord_items ==")
print("%-14s %-8s %-10s %-8s %s" % ("entityId", "itemType", "value", "weight", "中文名(token剥)"))
found = {}
for i in IDS:
    cur.execute("SELECT * FROM bannerlord_items WHERE entityId=?", (i,))
    r = cur.fetchone()
    if not r:
        print("%-14s %s" % (i, "—— 库里无此 entityId"))
        continue
    d = dict(r)
    zh = cn_of(d.get("name"))
    found[i] = {"id": i, "type": d.get("itemType"), "value": d.get("value"), "weight": d.get("weight"), "zh": zh}
    print("%-14s %-8s %-10s %-8s %s" % (i, d.get("itemType"), d.get("value"), d.get("weight"), zh))
print()

print("== 按 value 反查（防 id 拼错/漏）==")
for v in [10, 25, 30, 50, 110, 120, 130, 140, 180, 220, 230, 250, 260, 20, 60, 100, 160]:
    cur.execute("SELECT entityId, itemType, name FROM bannerlord_items WHERE value=? AND itemType IN ('Goods','None','Horse') LIMIT 6", (v,))
    rows = cur.fetchall()
    for r in rows:
        print("  value=%-5s %-18s %-8s %s" % (v, r[0], r[1], cn_of(r[2])))
print()

# Horses 的 FilePath 定位
print("== Horse 全清单（含 filePath）==")
cur.execute("SELECT entityId, name, value, filePath FROM bannerlord_items WHERE itemType='Horse' AND (entityId LIKE '%mule%' OR entityId LIKE '%camel%' OR entityId LIKE '%saddle%' OR entityId LIKE '%old%' OR entityId LIKE '%pack%')")
for r in cur.fetchall():
    print("  %-24s %-12s %-6s %s" % (r[0], cn_of(r[1]), r[2], r[3]))
print()

json.dump(found, io.open(r"D:\AWAKE-Dev\AWAKE\tools\_eco_defaultitems_db_20260920.json", "w", encoding="utf-8"),
          ensure_ascii=False, indent=1)
print("已落盘")
con.close()
