# -*- coding: utf-8 -*-
"""核 v35 编译产物：条数 / 域分布 / 三继承者正文原句 / 4 条探针污染是否在包内"""
import json, os, sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

BASE = r'D:\AWAKE-Dev\AWAKE\tools\worldbook-studio\workspace\full-geo1\compiled'
V35 = os.path.join(BASE, 'geo1-v35-concepts-slim')
print('== 目录内容 ==')
for f in sorted(os.listdir(V35)):
    p = os.path.join(V35, f)
    print(f'  {f:34s} {os.path.getsize(p):>10,} bytes')

rt = os.path.join(V35, 'runtime.json')
print(f'\n== runtime.json ==')
d = json.load(open(rt, encoding='utf-8'))
print('  top keys:', list(d.keys())[:20])
ents = d.get('entries') or d.get('documents') or (d if isinstance(d, list) else [])
print('  条目数 =', len(ents))

def txt(v, n=150):
    if isinstance(v, dict):
        v = v.get('zh-CN') or v.get('en') or next(iter(v.values()), '')
    return str(v)[:n]

# 域分布
from collections import Counter
dom = Counter(); sub = Counter()
for e in ents:
    dom[str(e.get('domain','?'))] += 1
    sub[str(e.get('subdomain','?'))] += 1
print('\n== 域分布 ==')
for k, v in dom.most_common(): print(f'  {k:14s} {v}')
print('\n== subdomain (top 15) ==')
for k, v in sub.most_common(15): print(f'  {k:20s} {v}')

# 三继承者 + 政体相关
print('\n== 三继承者 / 政体相关条目 ==')
KW = ['empire','senate','royal','veteran','feudal','kingdom','polity','政体','concept-kingdom']
seen = 0
for e in ents:
    eid = str(e.get('id',''))
    if any(k in eid.lower() for k in KW):
        t = txt(e.get('title'), 60)
        b = txt(e.get('body') or e.get('content') or e.get('text'), 180)
        print(f'  [{eid}]')
        print(f'      T: {t}')
        if b: print(f'      B: {b}')
        seen += 1
        if seen >= 16: break
print(f'  (命中 {seen} 条，已截断)' if seen >= 16 else f'  (命中 {seen} 条)')

# 探针污染
print('\n== 探针污染检查 ==')
probes = [e for e in ents if 'probe' in str(e.get('id','')).lower()]
print('  含 probe 的条目数 =', len(probes))
for e in probes[:8]:
    print('   ', e.get('id'), '|', txt(e.get('title'), 40))
print('\nDONE')
