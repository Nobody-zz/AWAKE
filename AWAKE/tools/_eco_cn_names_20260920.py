# -*- coding: utf-8 -*-
"""批量取：① 22 种村庄类型官方中文名；② 23 种贸易品官方中文名；③ 搜「行情/市价/价」界面文案。"""
import sqlite3, re, io, json

DB = r'C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db'
con = sqlite3.connect('file:%s?mode=ro' % DB.replace('\\', '/'), uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()

def cn_key(key):
    c2 = con.cursor()
    c2.execute("SELECT text FROM localization_entries WHERE language='CNs' AND stringId=? LIMIT 1", (key,))
    r = c2.fetchone()
    if not r: return ""
    t = re.sub(r'\{@Plural\}.*?\{\\@\}', '', r[0], flags=re.S)
    t = re.sub(r'\{[^}]*\}', '', t)
    return t.strip()

VT = {
 "wheat_farm": "BPPG2XF7", "europe_horse_ranch": "eEh752CZ", "steppe_horse_ranch": "eEh752CZ",
 "desert_horse_ranch": "eEh752CZ", "battanian_horse_ranch": "eEh752CZ", "sturgian_horse_ranch": "eEh752CZ",
 "vlandian_horse_ranch": "eEh752CZ", "lumberjack": "YYl1W2jU", "clay_mine": "myuzMhOn",
 "salt_mine": "3aOIY6wl", "iron_mine": "rHcVKSbA", "fisherman": "XpREJNHD",
 "cattle_farm": "bW3csuSZ", "sheep_farm": "QbKbGu2h", "swine_farm": "vqSHB7mJ",
 "vineyard": "ZtxWTS9V", "flax_plant": "Z8ntYx0Y", "date_farm": "2NR2E663",
 "olive_trees": "ewrkbwI9", "silk_plant": "wTyq7LaM", "silver_mine": "aJLQz9iZ",
 "trapper": "RREyouKr",
}
print("== 22 种村庄类型官方中文名 ==")
for k, key in VT.items():
    print("  %-22s %s" % (k, cn_key(key)))
print()

print("== 23 种贸易品官方中文名 ==")
cur.execute("SELECT entityId, name, value FROM bannerlord_items WHERE itemType='Goods' ORDER BY value")
for r in cur.fetchall():
    tok = r["name"] or ""
    m = re.search(r'\{=([^}]+)\}', tok)
    zh = cn_key(m.group(1)) if m else tok
    print("  %-14s %-10s %s" % (r["entityId"], r["value"], zh))
print()

print("== 官方文本里含「行情 / 市价 / 价」的界面文案（前 40）==")
cur.execute("""SELECT stringId, text, filePath FROM localization_entries
               WHERE language='CNs' AND (text LIKE '%行情%' OR text LIKE '%市价%' OR text LIKE '%价格%')
               LIMIT 40""")
for r in cur.fetchall():
    print("  [%s] %s  <%s>" % (r[0], r[1][:80], (r[2] or "").split("/")[-1]))
con.close()
