# -*- coding: utf-8 -*-
"""① 核编年史量词表提到的「海象牙/鲸油」游戏里有没有；② 列全部 Horse/Animal/Goods 找它们的 id。"""
import sqlite3, re, io, json

DB = r'C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db'
con = sqlite3.connect('file:%s?mode=ro' % DB.replace('\\', '/'), uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()

def cn_of(token):
    if not token: return ""
    m = re.search(r'\{=([^}]+)\}', token)
    if not m: return token
    c2 = con.cursor()
    c2.execute("SELECT text FROM localization_entries WHERE language='CNs' AND stringId=? LIMIT 1", (m.group(1),))
    r = c2.fetchone()
    if not r: return ""
    t = re.sub(r'\{@Plural\}.*?\{\\@\}', '', r[0], flags=re.S)
    t = re.sub(r'\{[^}]*\}', '', t)
    return t.strip()

print("== 找 ivory / whale 相关 item ==")
cur.execute("SELECT entityId, itemType, name, value FROM bannerlord_items WHERE entityId LIKE '%ivory%' OR entityId LIKE '%whale%' OR name LIKE '%ivory%' OR name LIKE '%whale%'")
rows = cur.fetchall()
if not rows:
    print("  0 命中")
for r in rows:
    print("  %-20s %-10s %-14s %s" % (r["entityId"], r["itemType"], cn_of(r["name"]), r["value"]))
print()

print("== 本地化里搜「海象牙」「鲸油」==")
for kw in ["海象牙", "鲸油", "鲸"]:
    cur.execute("SELECT stringId, text, filePath FROM localization_entries WHERE language='CNs' AND text LIKE ? LIMIT 5", ("%" + kw + "%",))
    rs = cur.fetchall()
    print("  「%s」命中 %d" % (kw, len(rs)))
    for r in rs:
        print("     [%s] %s <%s>" % (r[0], r[1][:70], (r[2] or "").split("/")[-1]))
print()

print("== 全部 Animal ==")
cur.execute("SELECT entityId, name, value FROM bannerlord_items WHERE itemType='Animal' ORDER BY value")
for r in cur.fetchall():
    print("  %-14s %-8s %s" % (r["entityId"], r["value"], cn_of(r["name"])))
print()

print("== 全部有 value 的 Horse ==")
cur.execute("SELECT entityId, name, value FROM bannerlord_items WHERE itemType='Horse' AND value IS NOT NULL ORDER BY value")
for r in cur.fetchall():
    print("  %-18s %-8s %s" % (r["entityId"], r["value"], cn_of(r["name"])))
con.close()
