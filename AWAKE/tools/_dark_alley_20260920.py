# -*- coding: utf-8 -*-
"""暗面批取数第 3 步：把「巷子」相关文案看全 + 抓城镇/村庄的区域名 + 几个关键长句。"""
import io
import json
import sqlite3

DB = r'C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db'
con = sqlite3.connect('file:%s?mode=ro' % DB.replace('\\', '/'), uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()

out = {}

rows = cur.execute("SELECT stringId, text, filePath FROM localization_entries WHERE language='CNs'").fetchall()

print('== 「巷子」全部 %d 条 ==' % sum(1 for r in rows if '巷子' in (r['text'] or '')))
for r in rows:
    if '巷子' in (r['text'] or ''):
        print('  %-10s %s' % (r['stringId'], (r['text'] or '')[:230]))

print()
print('== 城镇三个区域名 / 村庄三个区域名 候选 ==')
for r in rows:
    t = (r['text'] or '').strip()
    if t in ('后街', '空地', '码头', '牧场', '灌木丛', '沼泽'):
        print('  %-10s %-8s %s' % (r['stringId'], t, r['filePath']))

print()
print('== 长句：接管 / 失去 / 保护 / 头目死了 ==')
for r in rows:
    t = r['text'] or ''
    if len(t) > 40 and any(k in t for k in ['巷子', '帮派头目', '犯罪等级']):
        print('  %-10s %s' % (r['stringId'], t[:300]))
        print()

print('== Roguery（流氓/巧计）技能与特质 ==')
for r in rows:
    t = r['text'] or ''
    if any(k in t for k in ['流氓', '巧计', '恶名']) and len(t) < 220:
        print('  %-10s %s' % (r['stringId'], t[:220]))
