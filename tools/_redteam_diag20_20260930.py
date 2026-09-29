# -*- coding: utf-8 -*-
"""复核 DIAG-20-SINGLES-20260930.md 的硬读数"""
import json, os, sys, io
from collections import defaultdict
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

P = F = 0
def chk(label, got, want):
    global P, F
    ok = got == want
    print(f"  {'PASS' if ok else 'FAIL'}  {label}")
    if not ok: print(f"        got={got}\n        want={want}")
    P, F = (P+1, F) if ok else (P, F+1)

V = r'D:\AWAKE-Dev\AWAKE\tools\worldbook-studio\workspace\full-geo1\compiled\geo1-v35-concepts-slim'
docs = json.load(open(os.path.join(V, 'documents.json'), encoding='utf-8'))

def zh(v, n=999):
    if isinstance(v, dict): v = v.get('zh-CN') or v.get('en') or ''
    return str(v)[:n]

by = defaultdict(list)
for d in docs: by[d.get('subdomain')].append(d)
singles = {k: v[0] for k, v in by.items() if len(v) == 1}
chk('单档 subdomain 数 = 20', len(singles), 20)

# 每条的 rumor / detail 结构
def exprs(d):
    out = []
    for a in (d.get('assertions') or []):
        for e in (a.get('expressions') or []): out.append(e)
    return out

chk('全部 20 条都有 2 条引文（除库由格 7 条）',
    sorted(len(exprs(d)) for d in singles.values()),
    sorted([2]*19 + [7]))

n_nosrc = sum(1 for d in singles.values() if not any(e.get('sources') for e in exprs(d)))
chk('零 sources 的条目 = 2（roads/sea_routes）', n_nosrc, 2)
chk('roads 零 sources', any(e.get('sources') for e in exprs(singles['roads'])), False)
chk('sea_routes 零 sources', any(e.get('sources') for e in exprs(singles['sea_routes'])), False)

# C 堆三条
chk('resources = 吕卡隆·银矿', zh(singles['resources'].get('title')), '吕卡隆·银矿')
chk('arts = 富勒格', '富勒格' in zh(singles['arts'].get('title')), True)
chk('war_history = 库由格', zh(singles['war_history'].get('title')), '库由格')
chk('库由格 引文数 7', len(exprs(singles['war_history'])), 7)

# workshops 贴题判据：detail 档正文含"羊毛""毛毡"（rumor 档被截短，只 27 字）
w = zh(exprs(singles['workshops'])[1].get('text'))
chk('workshops detail 含"羊毛"', '羊毛' in w, True)
chk('workshops detail 含"毛毡"', '毛毡' in w, True)

# "去 UI 腔"两条的证据
f = zh(exprs(singles['food'])[0].get('text'))
t = zh(exprs(singles['tactics'])[0].get('text'))
chk('food 引文含"你的部队"（UI 腔证据）', '你的部队' in f, True)
chk('tactics 引文含"所有士兵都有默认的编队种类"', '所有士兵都有默认的编队种类' in t, True)

# 引文错配两条
chk('clothing 引文含"阿塞莱"（跑偏证据）', '阿塞莱' in zh(exprs(singles['clothing'])[0].get('text')), True)
chk('debt 引文含"声望"（跑偏证据）', '声望' in zh(exprs(singles['debt'])[0].get('text')), True)

# detail = summary + 官方文 的拼接判据
d = singles['clothing']
sm = zh(d.get('summary'))
de = zh(exprs(d)[1].get('text'))
chk('clothing detail 以 summary 开头（拼接证据）', de.startswith(sm), True)

print(f'\n===== DIAG REDTEAM: PASS={P} FAIL={F} =====')
