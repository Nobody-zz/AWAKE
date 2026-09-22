# -*- coding: utf-8 -*-
"""① 抽全图村庄 village_type；② 找 VillageType 定义文件与产物映射。"""
import sqlite3, re, io, json, collections

DB = r'C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db'
con = sqlite3.connect('file:%s?mode=ro' % DB.replace('\\', '/'), uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()

cur.execute("SELECT c0, c2 FROM xml_documents_fts_content WHERE c0 LIKE '%settlements.xml%' AND c0 LIKE '%SandBox/%'")
row = cur.fetchone()
body = row[1]

# 以 <Settlement 为块切，块内取 id / name / village_type
blocks = re.findall(r'<Settlement\b(.*?)</Settlement>', body, re.S)
print("Settlement 块数:", len(blocks))
vt = collections.Counter()
vlist = []
for b in blocks:
    sid = re.search(r'\bid="([^"]+)"', b)
    vt_m = re.search(r'village_type="VillageType\.([^"]+)"', b)
    if not vt_m: continue
    vt[vt_m.group(1)] += 1
    vlist.append(sid.group(1) if sid else "?")
print()
print("== village_type 分布（全图，%d 个村庄）==" % len(vlist))
for k, v in vt.most_common():
    print("  %-20s %d" % (k, v))
print()
print("== 找 VillageType 定义文件 ==")
cur.execute("SELECT DISTINCT c0 FROM xml_documents_fts_content WHERE c2 LIKE '%VillageType%' OR c0 LIKE '%village%'")
for r in cur.fetchall():
    print("  ", r[0])
con.close()
