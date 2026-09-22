# -*- coding: utf-8 -*-
"""核 camel / 牲畜 / 马的 XML 原文（value 到底配没配）。"""
import sqlite3, re, io

DB = r'C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db'
con = sqlite3.connect('file:%s?mode=ro' % DB.replace('\\', '/'), uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()
cur.execute("SELECT c0, c2 FROM xml_documents_fts_content WHERE c0 LIKE '%horses_and_others.xml%'")
row = cur.fetchone()
path, body = row[0], row[1]
print("文件:", path, " 长度:", len(body))
print()
print("== 抽全部 <Item ...> 开标签里的 id / value / weight ==")
for m in re.finditer(r'<Item\b(.*?)/?>', body, re.S):
    b = m.group(1)
    def g(k):
        mm = re.search(r'%s="([^"]*)"' % k, b)
        return mm.group(1) if mm else "—"
    print("  %-22s value=%-6s weight=%-6s type=%-8s name=%s" % (
        g("id"), g("value"), g("weight"), g("itemType") or g("Type"), g("name")[:48]))
print()
print("== pack_camel_unmountable / camel 附近原文 ==")
i = body.find('id="camel"')
print(body[max(0,i-300): i+400])
con.close()
