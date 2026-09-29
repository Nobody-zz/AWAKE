# -*- coding: utf-8 -*-
"""定位 v35 正文真身：doc.summary 的完整取值 + expression 的全部键 + 真正的正文在哪"""
import json, os, sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

V35 = r'D:\AWAKE-Dev\AWAKE\tools\worldbook-studio\workspace\full-geo1\compiled\geo1-v35-concepts-slim'
docs = json.load(open(os.path.join(V35, 'documents.json'), encoding='utf-8'))

def zh(v, n=999):
    if isinstance(v, dict): v = v.get('zh-CN') or v.get('en') or ''
    return str(v)[:n]

# 一个 expression 的完整键
d0 = docs[0]
ex0 = d0['assertions'][0]['expressions'][0]
print('== expression 全部键 ==')
for k, v in ex0.items():
    print(f'  {k:16s} = {json.dumps(v, ensure_ascii=False)[:200]}')

print('\n== 找正文：全库扫一遍，看哪个键里装长中文 ==')
from collections import Counter
keycnt = Counter(); longkey = Counter()
for d in docs[:200]:
    for a in (d.get('assertions') or []):
        for ex in (a.get('expressions') or []):
            for k, v in ex.items():
                keycnt[k] += 1
                if isinstance(v, str) and len(v) > 30: longkey[k] += 1
                if isinstance(v, dict):
                    for kk in v:
                        if isinstance(v[kk], str) and len(v[kk]) > 30:
                            longkey[f'{k}.{kk}'] += 1
print('  key 出现次数:', dict(keycnt))
print('  长文本 key  :', dict(longkey))

# 三继承者：把 doc.summary 完整打出来 + 所有 expression 的长文本
print('\n' + '='*70)
TARGET = ['military-empire-north','military-empire-south','military-empire-west','military-empire-system',
          'concept-kingdom','politics-succession','throne-paravenos']
for d in docs:
    did = str(d.get('id',''))
    if not any(t in did for t in TARGET): continue
    print(f'\n### {did}   [{d.get("domain")}/{d.get("subdomain")}]')
    print('TITLE  :', zh(d.get('title')))
    print('SUMMARY:', zh(d.get('summary')))
    for a in (d.get('assertions') or []):
        print(f'  -- assertion id={a.get("id")} tier={a.get("content_tier")} era={a.get("era")} --')
        for ex in (a.get('expressions') or []):
            print(f'    [{ex.get("layer")}] id={ex.get("id")}')
            for k, v in ex.items():
                if k in ('grants','denies','sources'): continue
                s = json.dumps(v, ensure_ascii=False)
                if len(s) > 8: print(f'        {k} = {s[:400]}')

print('\nDONE')
