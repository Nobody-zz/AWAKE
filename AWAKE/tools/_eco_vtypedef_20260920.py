# -*- coding: utf-8 -*-
"""找 VillageType 定义（id + 产物 item id + 中文名）。"""
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

print("== 搜含 VillageType 定义的 xml（找 <VillageType）==")
cur.execute("SELECT c0 FROM xml_documents_fts_content WHERE c2 LIKE '%<VillageType%'")
for r in cur.fetchall():
    print("  ", r[0])
print()

print("== dump 定义片段 ==")
cur.execute("SELECT c0, c2 FROM xml_documents_fts_content WHERE c2 LIKE '%<VillageType%' LIMIT 3")
for r in cur.fetchall():
    path, body = r[0], r[1]
    idx = body.find("<VillageType")
    print("---- %s (从第 %d 字) ----" % (path, idx))
    print(body[max(0,idx-200): idx+1400])
    print()
con.close()
