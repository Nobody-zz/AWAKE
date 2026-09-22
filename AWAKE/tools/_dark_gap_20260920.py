# -*- coding: utf-8 -*-
"""暗面批取数第 5 步：补缺口 —— 洗罪/罚款、棚户、滨水、帮派头目定义、赃物。"""
import sqlite3

DB = r'C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db'
con = sqlite3.connect('file:%s?mode=ro' % DB.replace('\\', '/'), uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()
rows = cur.execute("SELECT stringId, text FROM localization_entries WHERE language='CNs'").fetchall()

KEYS = ['棚户', '滨水', '贫民', '洗脱', '赦免', '罚款', '罚金', '赎罪', '赃', '偷来', '赌债', '人质',
        '保护', '地盘', '恶名', '流氓', '恐吓', '敲诈', '打手', '收税', '通行费']
for k in KEYS:
    vs = [(r['stringId'], r['text']) for r in rows if k in (r['text'] or '')]
    print('---- %s ：%d 条 ----' % (k, len(vs)))
    for sid, t in sorted(vs, key=lambda x: len(x[1]))[:6]:
        print('   %-10s %s' % (sid, t[:190]))
    print()
