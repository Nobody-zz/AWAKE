# -*- coding: utf-8 -*-
"""暗面批取数第 2 步：扫官方简体中文文案里跟「暗面」有关的句子。

目标：把「巷子 / 帮派 / 头目 / 犯罪 / 区域名 / 洗」这些词在官方文本里的
**实际说法与 stringId** 抓出来 —— 这些就是断言的 A 级引文来源。
"""
import io
import json
import sqlite3

DB = r'C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db'
con = sqlite3.connect('file:%s?mode=ro' % DB.replace('\\', '/'), uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()

KEYS = ['巷子', '帮派', '头目', '犯罪', '后街', '空地', '码头', '地下', '走私', '打点', '贿赂',
        '保护费', '洗', '头子', '匪', '黑', '赎', '罪行', '名声']

print('== 简体中文里命中暗面关键词的条目 ==')
rows = cur.execute("SELECT stringId, text, filePath FROM localization_entries WHERE language='CNs'").fetchall()
print('  简中条目总数:', len(rows))

hit = {}
for r in rows:
    t = r['text'] or ''
    for k in KEYS:
        if k in t:
            hit.setdefault(k, []).append((r['stringId'], t, r['filePath']))
            break

for k in KEYS:
    v = hit.get(k)
    print()
    print('---- %s ：%d 条 ----' % (k, len(v) if v else 0))
    if not v:
        continue
    # 短句优先（更像一句话），长句可能是整段
    v2 = sorted(v, key=lambda x: len(x[1]))[:14]
    for sid, t, fp in v2:
        print('   %-9s %s' % (sid, t[:150]))

json.dump({k: [{'id': a, 'text': b, 'file': c} for a, b, c in v] for k, v in hit.items()},
          io.open(r'D:\AWAKE-Dev\AWAKE\tools\_dark_cn_hits_20260920.json', 'w', encoding='utf-8'),
          ensure_ascii=False, indent=1)
print()
print('已落盘 tools/_dark_cn_hits_20260920.json')
