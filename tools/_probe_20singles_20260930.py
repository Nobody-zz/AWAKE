# -*- coding: utf-8 -*-
"""把「单档子域引文错配」的 20 条逐条摊开：标题 / 主题句 / 实际引文 / 用没用到料"""
import json, os, sys, io, re
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

V35 = r'D:\AWAKE-Dev\AWAKE\tools\worldbook-studio\workspace\full-geo1\compiled\geo1-v35-concepts-slim'
docs = json.load(open(os.path.join(V35, 'documents.json'), encoding='utf-8'))

def zh(v, n=999):
    if isinstance(v, dict): v = v.get('zh-CN') or v.get('en') or ''
    return str(v)[:n]

# 找出所有「单档子域」：即一个 subdomain 只有 1 条的情况
from collections import Counter, defaultdict
by = defaultdict(list)
for d in docs: by[d.get('subdomain')].append(d)
singles = {k: v for k, v in by.items() if len(v) == 1}
print(f'== 单档 subdomain 共 {len(singles)} 种 ==')
print('  ', ' | '.join(sorted(singles)))

# 全量打印这些单档条目
for k in sorted(singles):
    d = singles[k][0]
    print('\n' + '='*78)
    print(f"### subdomain={k}   domain={d.get('domain')}")
    print(f"  id    : {d.get('id')}")
    print(f"  title : {zh(d.get('title'))}")
    print(f"  summary: {zh(d.get('summary'))}")
    exprs = []
    for a in (d.get('assertions') or []):
        for ex in (a.get('expressions') or []):
            exprs.append(ex)
    print(f"  引文数: {len(exprs)}")
    for ex in exprs:
        print(f"    <{ex.get('layer')}> {zh(ex.get('text'), 300)}")
        for s in (ex.get('sources') or []):
            print(f"        src locator={s.get('locator')}")
            q = zh(s.get('quote'), 300)
            if q: print(f"            quote: {q}")

print('\n\n===== 单档总数 =====', sum(len(v) for v in singles.values()))
