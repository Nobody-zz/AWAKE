# -*- coding: utf-8 -*-
"""取村庄产出机制：从 settlements.xml 正文里抽 <Village ... production="xxx">。"""
import sqlite3, re, io, json, collections

DB = r'C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db'
con = sqlite3.connect('file:%s?mode=ro' % DB.replace('\\', '/'), uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()

# 1) 全库找含 <Village 的 xml
print("== 含 '<Village' 的 xml 文件 ==")
cur.execute("SELECT DISTINCT c0, c1 FROM xml_documents_fts_content WHERE c2 LIKE '%<Village%' LIMIT 20")
for r in cur.fetchall():
    print("  %-70s | %s" % (r[0], r[1]))
print()

# 2) 抽 production 属性
print("== 抽 production= 分布 ==")
cur.execute("SELECT c0, c2 FROM xml_documents_fts_content WHERE c2 LIKE '%production=%'")
prod = collections.Counter()
samples = {}
for r in cur.fetchall():
    path, body = r[0], r[1]
    for m in re.finditer(r'<Village\b([^>]*)>', body, re.S):
        body2 = m.group(1)
        p = re.search(r'production="([^"]+)"', body2)
        sid = re.search(r'\bid="([^"]+)"', body2)
        if p:
            key = p.group(1)
            prod[key] += 1
            samples.setdefault(key, []).append((sid.group(1) if sid else "?", path.split("/")[-1]))
print("  ---- 全库 production 取值 ----")
for k, v in prod.most_common():
    print("  %-16s %d" % (k, v))
print("  合计:", sum(prod.values()))
print()
print("== 每种产物的样例村庄（前 6）==")
for k in sorted(samples):
    print("  [%s] %s" % (k, ", ".join("%s(%s)" % (a, b) for a, b in samples[k][:6])))
con.close()
