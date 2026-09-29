# -*- coding: utf-8 -*-
"""查 runtime.json 单条目结构与 subdomain 真实字段；并抽三继承者正文（title 是 dict）"""
import json, os, sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

V35 = r'D:\AWAKE-Dev\AWAKE\tools\worldbook-studio\workspace\full-geo1\compiled\geo1-v35-concepts-slim'
d = json.load(open(os.path.join(V35, 'runtime.json'), encoding='utf-8'))
ents = d['entries']

print('== 第 1 条完整结构 ==')
e0 = ents[0]
for k, v in e0.items():
    s = json.dumps(v, ensure_ascii=False)
    print(f'  {k:18s} : {s[:180]}')

print('\n== 随机取一条 politics.concept-* 的全部键 ==')
for e in ents:
    eid = str(e.get('id',''))
    if 'concept' in eid and 'politics' in eid:
        print(' id =', eid)
        for k in e.keys(): print('   -', k)
        break

print('\n== documents.json 里有没有 subdomain ==')
docs = json.load(open(os.path.join(V35, 'documents.json'), encoding='utf-8'))
print(' type =', type(docs).__name__)
if isinstance(docs, dict):
    print(' keys =', list(docs.keys())[:12])
    dl = docs.get('documents') or docs.get('entries') or []
else:
    dl = docs
print(' len =', len(dl))
if dl:
    d0 = dl[0]
    print(' doc[0] keys =', list(d0.keys()))
    print(' doc[0] =', json.dumps(d0, ensure_ascii=False)[:600])

print('\n== index.json 结构 ==')
idx = json.load(open(os.path.join(V35, 'index.json'), encoding='utf-8'))
print(' type =', type(idx).__name__)
if isinstance(idx, dict):
    for k in list(idx.keys())[:15]:
        v = idx[k]
        print(f'  {k:22s} {type(v).__name__} len={len(v) if hasattr(v,"__len__") else "-"}')
        if isinstance(v, dict):
            ks = list(v.keys())[:6]
            print('      sample keys:', ks)

print('\n== validation.json 摘要 ==')
val = json.load(open(os.path.join(V35, 'validation.json'), encoding='utf-8'))
print(' keys =', list(val.keys()) if isinstance(val, dict) else type(val))
print(json.dumps(val, ensure_ascii=False)[:1200])
print('\nDONE')
