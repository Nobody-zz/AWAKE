# -*- coding: utf-8 -*-
"""dump settlements.xml 里 <Village 的上下文 + 找产出字段。"""
import sqlite3, re, io

DB = r'C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db'
con = sqlite3.connect('file:%s?mode=ro' % DB.replace('\\', '/'), uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()
cur.execute("SELECT c0, c2 FROM xml_documents_fts_content WHERE c0 LIKE '%settlements.xml%' AND c2 LIKE '%<Village%'")
row = cur.fetchone()
path, body = row[0], row[1]
print("文件:", path, " 长度:", len(body))
idx = body.find("<Village")
print("---- 首个 <Village 前后 900 字 ----")
print(body[max(0, idx-400): idx+900])
print()
# 看有没有 production / Production / outputs 之类
for kw in ["production", "Production", "output", "Output", "item", "Item"]:
    print("%-14s 命中 %d" % (kw, body.count(kw)))
print()
print("---- 所有 <Village 开标签的前 60 个 ----")
tags = re.findall(r'<Village\b[^>]*>', body, re.S)
for t in tags[:8]:
    print(re.sub(r'\s+', ' ', t))
print("  ...共", len(tags))
con.close()
