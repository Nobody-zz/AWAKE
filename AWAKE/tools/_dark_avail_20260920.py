# -*- coding: utf-8 -*-
"""暗面批取数第 6 步：核 A 级可用性 —— 农奴/强盗/山贼/劫匪/要人/小阵营。"""
import sqlite3

DB = r'C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db'
con = sqlite3.connect('file:%s?mode=ro' % DB.replace('\\', '/'), uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()
rows = cur.execute("SELECT stringId, text FROM localization_entries WHERE language='CNs'").fetchall()

print('== 简中短文里含这些词的（长度<40，更像专名/短句）==')
for k in ['农奴', '依附民', '强盗', '山贼', '劫匪', '土匪', '要人', '小阵营']:
    vs = [(r['stringId'], r['text']) for r in rows if k in (r['text'] or '') and len(r['text']) < 40]
    print('---- %s ：%d 条 ----' % (k, len(vs)))
    for sid, t in vs[:12]:
        print('   %-10s %s' % (sid, t))
    print()

print('== 兵种表里的匪类（bannerlord_troops）==')
try:
    cols = [r['name'] for r in cur.execute('PRAGMA table_info(bannerlord_troops)')]
    print('  列:', cols)
except Exception as e:
    print('  ', e)
