# -*- coding: utf-8 -*-
"""20 条单档子域的精准分类：看每条的「引文」跟「标题」到底对不对得上"""
import json, os, sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

V35 = r'D:\AWAKE-Dev\AWAKE\tools\worldbook-studio\workspace\full-geo1\compiled\geo1-v35-concepts-slim'
docs = json.load(open(os.path.join(V35, 'documents.json'), encoding='utf-8'))

def zh(v, n=999):
    if isinstance(v, dict): v = v.get('zh-CN') or v.get('en') or ''
    return str(v)[:n]

from collections import defaultdict
by = defaultdict(list)
for d in docs: by[d.get('subdomain')].append(d)

print(f"{'subdomain':20s} {'条目 id':42s} {'引文数':6s} {'有无 sources':12s} 标题")
print('-'*140)
for k in sorted(by):
    if len(by[k]) != 1: continue
    d = by[k][0]
    exprs = []
    for a in (d.get('assertions') or []):
        for ex in (a.get('expressions') or []): exprs.append(ex)
    nsrc = sum(1 for ex in exprs if ex.get('sources'))
    print(f"{k:20s} {str(d.get('id'))[5:47]:42s} {len(exprs):<6d} {str(nsrc)+'/'+str(len(exprs)):<9s} {zh(d.get('title'),30)}")

print('\n\n===== 逐条：标题 vs 实际引了什么 =====')
for k in sorted(by):
    if len(by[k]) != 1: continue
    d = by[k][0]
    exprs = []
    for a in (d.get('assertions') or []):
        for ex in (a.get('expressions') or []): exprs.append(ex)
    # 取 rumor 档的第一句（就是"引错了"的那句）
    rumor = next((zh(e.get('text'),60) for e in exprs if e.get('layer')=='rumor'), '')
    detail = next((zh(e.get('text'), 60) for e in exprs if e.get('layer')=='detail'), '')
    print(f"\n[{k}] {zh(d.get('title'),30)}")
    print(f"   主题句: {zh(d.get('summary'),60)}")
    print(f"   rumor : {rumor}")
    if detail: print(f"   detail: {detail}")
    # 引文来源域名
    for e in exprs:
        for s in (e.get('sources') or []):
            print(f"      <- {s.get('locator')}")
            break
