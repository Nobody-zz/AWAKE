#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""R3 引用数字核对：M3 禁词覆盖面 & E2b 归因。"""
import json, glob, os

CHARDIR = r'D:\AWAKE-Dev\AWAKE\tools\persona-workbench\characters'
M3 = ['永远', '绝不', '总是']
HARD = ['绝不','绝不让','不容','绝不容','宁可','宁死','断然','寸步不让','一石不让','留不得','绝不退']
COND = ['若','但凡','除非','一旦','只要','假如','万一','但','却','才','容我','可以改']

n_m3 = 0
n_only_m3 = 0
n_e2b_fail = 0
tot = 0
for f in sorted(glob.glob(os.path.join(CHARDIR, '*.persona.json'))):
    j = json.load(open(f, encoding='utf-8'))
    tot += 1
    rt = ' '.join([str(x) for x in (j.get('selfClaimRules') or [])]
                  + [str(x) for x in (j.get('realSelfBehaviors') or [])]
                  + [str(x) for x in (j.get('selfClaimExamples') or [])]
                  + [str(j.get('contradictionDescription') or '')])
    if any(w in rt for w in M3):
        n_m3 += 1
    hard_hits = [w for w in HARD if w in rt]
    if hard_hits and all(any(m in h for m in M3) for h in hard_hits):
        n_only_m3 += 1
    if not (hard_hits and any(w in rt for w in COND)):
        n_e2b_fail += 1

print(f'卡数 {tot}')
print(f'含 M3 禁词（永远/绝不/总是）的卡: {n_m3}')
print(f'  其中「全部底线词都属 M3 禁词」的卡: {n_only_m3}')
print(f'旧 E2b（底线 ∩ 条件，全文命中）判 FAIL 的卡: {n_e2b_fail}')
