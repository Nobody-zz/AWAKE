# -*- coding: utf-8 -*-
"""查 Bannerlord 政策 / 文化 的表结构与实际取值（只读）。"""
import sqlite3, json

DB = r'C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db'
con = sqlite3.connect(f'file:{DB}?mode=ro', uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()

def cols(t):
    cur.execute(f'PRAGMA table_info({t})')
    return [r['name'] for r in cur.fetchall()]

print('=== 表清单（含 culture / polic / feat 的表） ===')
cur.execute("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name")
for r in cur.fetchall():
    n = r['name']
    if any(k in n.lower() for k in ('cultur', 'polic', 'feat')):
        print(' ', n)

for t in ('bannerlord_policies', 'bannerlord_cultures'):
    print(f'\n=== {t} 列 ===')
    print(' ', cols(t))

print('\n=== bannerlord_policies 全部行 ===')
cur.execute('SELECT * FROM bannerlord_policies')
rows = cur.fetchall()
print('  行数:', len(rows))
if rows:
    print('  字段:', list(rows[0].keys()))
    for r in rows[:6]:
        print('  ', dict(r))

print('\n=== bannerlord_cultures：cultureId / 数值列 / defaultPolicyIds ===')
c = cols('bannerlord_cultures')
print('  列:', c)
sel = [x for x in c if x in ('cultureId','name','militia_bonus','prosperity_bonus','naval_factor',
                             'defaultPolicyIdsJson','default_policy_ids_json','basicTroop','isMainCulture')]
cur.execute(f"SELECT {','.join(sel)} FROM bannerlord_cultures")
for r in cur.fetchall():
    d = dict(r)
    dp = d.get('defaultPolicyIdsJson') or d.get('default_policy_ids_json')
    if dp:
        try:
            d['defaultPolicyIdsJson'] = json.loads(dp)
        except Exception:
            pass
    print('  ', d)

con.close()
