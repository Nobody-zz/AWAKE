# -*- coding: utf-8 -*-
"""扫官方本地化里「成X的Y」格式的条目 —— 交易品名的完整清单。"""
import sqlite3, re, io, json

DB = r'C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db'
con = sqlite3.connect('file:%s?mode=ro' % DB.replace('\\', '/'), uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()

cur.execute("""SELECT stringId, text, filePath FROM localization_entries
               WHERE language='CNs' AND text LIKE '%{@Plural}成%的%'
               ORDER BY text""")
rows = cur.fetchall()
print("命中条数:", len(rows))
for r in rows:
    t = re.sub(r'\{@Plural\}.*?\{\\@\}', '', r["text"], flags=re.S)
    plur = re.search(r'\{@Plural\}(.*?)\{\\@\}', r["text"], flags=re.S)
    print("  [%s] %-10s | 复数：%-16s | %s" % (
        r["stringId"], t.strip(), (plur.group(1) if plur else ""), (r["filePath"] or "").split("/")[-1]))
con.close()
