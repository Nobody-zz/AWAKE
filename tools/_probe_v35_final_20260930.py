# -*- coding: utf-8 -*-
"""v35 完整收口：subdomain 正确统计 + validation 全诊断 + 三继承者正文 + 探针定位"""
import json, os, sys, io
from collections import Counter
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

V35 = r'D:\AWAKE-Dev\AWAKE\tools\worldbook-studio\workspace\full-geo1\compiled\geo1-v35-concepts-slim'
rt = json.load(open(os.path.join(V35, 'runtime.json'), encoding='utf-8'))
ents = rt['entries']
val = json.load(open(os.path.join(V35, 'validation.json'), encoding='utf-8'))
docs = json.load(open(os.path.join(V35, 'documents.json'), encoding='utf-8'))

def zh(v, n=999):
    if isinstance(v, dict): v = v.get('zh-CN') or v.get('en') or ''
    return str(v)[:n]

# ---------- 1. subdomain 正确统计 ----------
sub = Counter(); dom = Counter()
for e in ents:
    ext = e.get('extensions') or {}
    sub[str(ext.get('subdomain','?'))] += 1
    dom[str(e.get('domain','?'))] += 1
print('== 域分布 ==')
for k, v in dom.most_common(): print(f'  {k:12s} {v}')
print(f'\n== subdomain 共 {len(sub)} 种 ==')
for k, v in sub.most_common(): print(f'  {k:24s} {v}')

# ---------- 2. validation 全诊断 ----------
print('\n== validation.json ==')
print('  Valid =', val.get('Valid'))
diags = val.get('Diagnostics') or []
print('  Diagnostics 总数 =', len(diags))
cc = Counter(d.get('Code') for d in diags)
ss = Counter(d.get('Severity') for d in diags)
print('  按 Code:', dict(cc))
print('  按 Severity:', dict(ss))
# 非 AMBIGUOUS 的都打出来
print('  --- 非 WB-INDEX-AMBIGUOUS 的诊断 ---')
n = 0
for d in diags:
    if d.get('Code') != 'WB-INDEX-AMBIGUOUS':
        print(f"   [{d.get('Severity')}] {d.get('Code')} :: {zh(d.get('Message'),120)}")
        n += 1
        if n > 25: print('   ...'); break
print(f'  (非歧义诊断共 {len(diags) - cc.get("WB-INDEX-AMBIGUOUS",0)} 条)')

# ---------- 3. 三继承者正文 ----------
print('\n== 三继承者相关条目正文 ==')
KW = ['north','south','west','empire','senate','royal','veteran','feudal']
for e in ents:
    eid = str(e.get('id',''))
    if 'concept' in eid and any(k in eid.lower() for k in KW):
        print(f'\n  [{eid}]  dom={e.get("domain")} sub={(e.get("extensions") or {}).get("subdomain")}')
        print('    T:', zh(e.get('title')))
        print('    S:', zh(e.get('summary'), 260))
        for ex in (e.get('expressions') or [])[:3]:
            print(f'      <{ex.get("layer")}> {zh(ex.get("detail") or ex.get("text") or ex.get("body"), 180)}')

# ---------- 4. 探针污染定位 ----------
print('\n== 探针污染 4 条完整信息 ==')
for e in ents:
    if 'probe' in str(e.get('id','')).lower():
        print(f"  {e.get('id')}  dom={e.get('domain')} sub={(e.get('extensions') or {}).get('subdomain')}")
        print(f"     T={zh(e.get('title'),40)}  S={zh(e.get('summary'),100)}")
        print(f"     ext={json.dumps(e.get('extensions'), ensure_ascii=False)}")

# ---------- 5. 政体关键词在 v35 里的实际命中 ----------
print('\n== 政体关键词命中（title 内） ==')
POL = ['政体','元老院','共和','帝','汗','王','部落','封建','苏丹','大公','至高']
for e in ents:
    t = zh(e.get('title'), 40)
    if any(k in t for k in POL):
        print(f"  {t:24s} | {e.get('id')}")
print('\nDONE')
