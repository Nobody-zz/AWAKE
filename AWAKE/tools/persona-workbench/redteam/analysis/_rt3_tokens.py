#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""R3 辅助：数据驱动地看同王国卡之间到底共享什么 token（验证 A5 的 HOLD 是否假阴性）。"""
import json, glob, os, collections, re

BASE = r'D:\AWAKE-Dev\AWAKE'
CHARDIR = os.path.join(BASE, 'tools', 'persona-workbench', 'characters')

groups = collections.defaultdict(list)
for f in sorted(glob.glob(os.path.join(CHARDIR, '*.persona.json'))):
    j = json.load(open(f, encoding='utf-8'))
    zh = os.path.basename(f).split('_')[0]
    k = '?'
    of = f.replace('.persona.json', '.origins.json')
    if os.path.exists(of):
        k = json.load(open(of, encoding='utf-8')).get('kingdomId', '?') or '?'
    txt = ' '.join([str(x) for x in (j.get('selfClaimExamples') or [])]
                   + [str(x) for x in (j.get('selfClaimRules') or [])]
                   + [str(x) for x in (j.get('realSelfBehaviors') or [])]
                   + [str(j.get('contradictionDescription') or '')])
    groups[k].append((zh, re.sub(r'\s+', '', txt)))

print('=== 每王国：出现在 >=60% 卡中的 3-gram（共享签名） ===')
for k in sorted(groups, key=lambda x: -len(groups[x])):
    cards = groups[k]
    if len(cards) < 3:
        continue
    cnt = collections.Counter()
    for _, t in cards:
        grams = {t[i:i+3] for i in range(len(t) - 2)}
        for g in grams:
            cnt[g] += 1
    thresh = max(2, int(len(cards) * 0.6))
    shared = [(g, n) for g, n in cnt.most_common() if n >= thresh]
    print(f'\n--- {k}  n={len(cards)}  阈值>={thresh} ---')
    if not shared:
        print('  (无共享 3-gram)')
    for g, n in shared[:12]:
        print(f'  {g}  {n}/{len(cards)}')

print('\n=== 全域：出现在 >=25% 卡中的 3-gram ===')
allc = [t for v in groups.values() for _, t in v]
cnt = collections.Counter()
for t in allc:
    for g in {t[i:i+3] for i in range(len(t) - 2)}:
        cnt[g] += 1
th = int(len(allc) * 0.25)
for g, n in cnt.most_common(40):
    if n >= th:
        print(f'  {g}  {n}/{len(allc)}')
