# -*- coding: utf-8 -*-
"""抽 v35 里 politics/kingdoms 相关条目的真正文（expressions[].detail）"""
import json, os, sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

V35 = r'D:\AWAKE-Dev\AWAKE\tools\worldbook-studio\workspace\full-geo1\compiled\geo1-v35-concepts-slim'
docs = json.load(open(os.path.join(V35, 'documents.json'), encoding='utf-8'))

def zh(v, n=999):
    if isinstance(v, dict): v = v.get('zh-CN') or v.get('en') or ''
    return str(v)[:n]

# documents.json 里有哪些字段
print('== doc 字段 ==', list(docs[0].keys()))

TARGET = ['concept-kingdom','concept-law-of-kingdom','politics-succession','throne-paravenos',
          'military-empire-north','military-empire-south','military-empire-west','military-empire-system']
for d in docs:
    did = str(d.get('id',''))
    if not any(t in did for t in TARGET): continue
    print('\n' + '='*70)
    print('ID :', did, '| domain=', d.get('domain'), '| subdomain=', d.get('subdomain'))
    print('T  :', zh(d.get('title')))
    print('S  :', zh(d.get('summary'), 300))
    for a in (d.get('assertions') or []):
        for ex in (a.get('expressions') or []):
            print(f"   <{ex.get('layer')} id={ex.get('id')}>")
            print('      ', zh(ex.get('detail'), 700))
            for g in (ex.get('grants') or [])[:3]:
                print('        grant:', json.dumps(g, ensure_ascii=False)[:150])
print('\nDONE')
