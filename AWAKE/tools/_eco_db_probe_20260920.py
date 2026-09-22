# -*- coding: utf-8 -*-
"""从 BannerlordSage 索引取：① 全部贸易品（Goods）中文名+标价；② 牲畜/马匹；③ 村庄产出机制。"""
import sqlite3, re, io, json, sys

DB = r'C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db'
con = sqlite3.connect('file:%s?mode=ro' % DB.replace('\\', '/'), uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()

def cn(token):
    if not token: return ""
    m = re.search(r'\{=([^}]+)\}', token)
    if not m: return token
    key = m.group(1)
    cur2 = con.cursor()
    cur2.execute("SELECT text FROM localization_entries WHERE language='CNs' AND stringId=? LIMIT 1", (key,))
    r = cur2.fetchone()
    t = r[0] if r else ""
    t = re.sub(r'\{@Plural\}.*?\{@\}', '', t, flags=re.S)
    t = re.sub(r'\{[^}]*\}', '', t)
    return t.strip()

print("== bannerlord_items 表结构 ==")
cur.execute("PRAGMA table_info(bannerlord_items)")
cols = [r[1] for r in cur.fetchall()]
print(cols)
print()

print("== itemType 分布 ==")
cur.execute("SELECT itemType, COUNT(*) FROM bannerlord_items GROUP BY itemType ORDER BY 2 DESC")
for r in cur.fetchall():
    print("  %-16s %d" % (r[0], r[1]))
print()

print("== 全部 Goods（贸易品）==  id | 中文 | value | 重量")
cur.execute("SELECT * FROM bannerlord_items WHERE itemType='Goods' ORDER BY value")
rows = cur.fetchall()
goods = []
for r in rows:
    d = dict(r)
    eid = d.get("entityId")
    zh = cn(d.get("name"))
    val = d.get("value")
    wt = d.get("weight")
    goods.append({"id": eid, "zh": zh, "value": val, "weight": wt})
    print("  %-16s | %-8s | %-6s | %s" % (eid, zh, val, wt))
print("  小计:", len(goods))
print()

print("== Horse / Animal ==")
cur.execute("SELECT * FROM bannerlord_items WHERE itemType IN ('Horse','Animal') ORDER BY itemType, value")
for r in cur.fetchall():
    d = dict(r)
    print("  [%s] %-18s | %-10s | %s" % (d.get("itemType"), d.get("entityId"), cn(d.get("name")), d.get("value")))
print()

json.dump(goods, io.open(r"D:\AWAKE-Dev\AWAKE\tools\_eco_goods_db_20260920.json", "w", encoding="utf-8"),
          ensure_ascii=False, indent=1)
print("已落盘 tools/_eco_goods_db_20260920.json")
con.close()
