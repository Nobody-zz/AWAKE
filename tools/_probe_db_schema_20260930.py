# -*- coding: utf-8 -*-
"""探 bannerlord.db 真实的表结构，然后再写正确的 redteam"""
import sqlite3, sys, io, glob, os
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

DB = r'C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db'
con = sqlite3.connect(DB); cur = con.cursor()

cur.execute("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name")
tabs = [r[0] for r in cur.fetchall()]
print('TABLES (%d):' % len(tabs))
for t in tabs: print('  ', t)

print('\n=== 相关表的列 ===')
for t in tabs:
    if any(k in t.lower() for k in ['culture','kingdom','polic','local']):
        try:
            cur.execute(f'PRAGMA table_info("{t}")')
            cols = [r[1] for r in cur.fetchall()]
            cur.execute(f'SELECT COUNT(*) FROM "{t}"')
            n = cur.fetchone()[0]
            print(f'\n  [{t}]  n={n}')
            print('    cols:', ', '.join(cols))
        except Exception as e:
            print(f'  [{t}] ERR {e}')
con.close()
print('\nDONE')
