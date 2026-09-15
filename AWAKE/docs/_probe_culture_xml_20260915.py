# -*- coding: utf-8 -*-
"""查 cultures.xml 里文化自带的数值属性与 cultural_feats 段的真实写法（只读）。"""
import sqlite3, re

DB = r'C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db'
con = sqlite3.connect(f'file:{DB}?mode=ro', uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()

print('=== 哪些文件含 militia_bonus / prosperity_bonus / naval_factor ===')
for kw in ('militia_bonus', 'prosperity_bonus', 'naval_factor', 'cultural_feats', 'effect_bonus'):
    cur.execute("SELECT c0, c1 FROM xml_documents_fts_content WHERE c2 LIKE ? GROUP BY c0", (f'%{kw}%',))
    rows = cur.fetchall()
    print(f'  [{kw}] 命中文件 {len(rows)}:')
    for r in rows[:6]:
        print('     ', r['c1'], '|', r['c0'])

print('\n=== 抓一份 cultures.xml 的原文片段（含 cultural_feats 与数值属性） ===')
cur.execute("""SELECT c0, c1, c2 FROM xml_documents_fts_content
               WHERE c2 LIKE '%cultural_feats%' AND c2 LIKE '%militia_bonus%' LIMIT 3""")
for r in cur.fetchall():
    txt = r['c2']
    print(f"\n--- {r['c1']} | {r['c0']} | {len(txt)} 字符 ---")
    # 打印每个 <Culture ...> 元素的开头 + cultural_feats 段
    for m in re.finditer(r'<Culture\b.*?</Culture>', txt, re.S):
        block = m.group(0)
        head = block[:900]
        print('\n===== Culture block =====')
        print(head)
        break
    feats = re.findall(r'<cultural_feats>.*?</cultural_feats>', txt, re.S)
    for f in feats[:3]:
        print('\n  [cultural_feats]', f.strip()[:400])

print('\n=== 抓 Feat 定义（<Feat id=... ） ===')
cur.execute("""SELECT c0, c1, c2 FROM xml_documents_fts_content
               WHERE c2 LIKE '%<Feat %' OR c2 LIKE '%Feat id=%' LIMIT 3""")
for r in cur.fetchall():
    txt = r['c2']
    fs = re.findall(r'<Feat\b[^>]*>', txt)
    print(f"\n--- {r['c1']} | {r['c0']} ---")
    for f in fs[:8]:
        print('  ', f)

con.close()
