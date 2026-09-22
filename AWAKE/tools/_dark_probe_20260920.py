# -*- coding: utf-8 -*-
"""暗面批（09-20）取数第 1 步：先摸 bannerlord.db 的表结构，再扫官方文案。

一次性摸清：
  ① localization_entries 的列
  ② 哪些表存 text / id
  ③ 「巷子/帮派/犯罪/头目/区域」这类词在官方中文里的实际写法与条目 id
"""
import io
import json
import sqlite3
import sys

DB = r'C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db'
con = sqlite3.connect('file:%s?mode=ro' % DB.replace('\\', '/'), uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()

print('== 全部表 ==')
tabs = [r[0] for r in cur.execute("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name")]
for t in tabs:
    try:
        n = cur.execute('SELECT COUNT(*) FROM "%s"' % t).fetchone()[0]
    except Exception as e:
        n = '?'
    print('  %-34s %s' % (t, n))

print()
for t in ('localization_entries',):
    if t in tabs:
        print('== %s 列 ==' % t)
        for r in cur.execute('PRAGMA table_info("%s")' % t):
            print('   ', r['name'], r['type'])
        print('  样例行:')
        for r in cur.execute('SELECT * FROM "%s" LIMIT 2' % t):
            print('   ', dict(r))
