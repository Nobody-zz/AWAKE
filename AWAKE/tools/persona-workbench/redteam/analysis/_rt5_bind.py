#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""R5 侦察：v4 的条件化要求会把「绑定句」推向哪里、什么形状？
如果全库都写到 selfClaimRules 且形状统一 → E5 只看 examples，新脸谱漏在盲区。"""
import json, glob, os, re, collections

CHARDIR = r'D:\AWAKE-Dev\AWAKE\tools\persona-workbench\characters'
HARD = ['绝不','绝不让','绝不容','绝不退','不容','宁可','宁死','断然','寸步不让','一石不让','一步不让','留不得']
COND = ['若','但凡','除非','一旦','只要','假如','万一','纵使','即便','容我','可以改','就算','宁可']
FRAME = re.compile(r'([？?]\s*[—\-]{1,2}\s*[“"「]?)|(会\s*(如何|怎样|怎么)|要\s*(如何|怎样|怎么))')

def seg_has(t, words):
    return [w for w in words if w in t]

loc = collections.Counter()     # 绑定句落在哪个字段
openers = collections.Counter() # 绑定句以什么开头
for f in sorted(glob.glob(os.path.join(CHARDIR, '*.persona.json'))):
    j = json.load(open(f, encoding='utf-8'))
    exs = [str(x) for x in (j.get('selfClaimExamples') or [])]
    rules = [str(x) for x in (j.get('selfClaimRules') or [])]
    rbs = [str(x) for x in (j.get('realSelfBehaviors') or [])]
    contra = str(j.get('contradictionDescription') or '')
    found = None
    for fld, arr in (('examples', exs), ('rules', rules), ('rbs', rbs), ('contra', [contra])):
        for s in arr:
            if seg_has(s, HARD) and seg_has(s, COND):
                found = (fld, s); break
        if found: break
    if not found:
        loc['无绑定句'] += 1
        continue
    fld, s = found
    loc[fld] += 1
    openers[s[:2]] += 1

print('=== 满足「底线∩条件」的绑定句落在哪个字段 ===')
for k, v in loc.most_common():
    print(f'  {k:8s} {v}')
print()
print('=== 绑定句开头 2 字分布（看是否句式趋同） ===')
for k, v in openers.most_common(15):
    print(f'  「{k}」 {v}')

print()
print('=== 反向检查：含底线词的句子，有多少同时也含条件词（即真正绑定） ===')
tot_hard = 0
tot_bound = 0
for f in glob.glob(os.path.join(CHARDIR, '*.persona.json')):
    j = json.load(open(f, encoding='utf-8'))
    for s in ([str(x) for x in (j.get('selfClaimExamples') or [])]
              + [str(x) for x in (j.get('selfClaimRules') or [])]
              + [str(x) for x in (j.get('realSelfBehaviors') or [])]):
        if seg_has(s, HARD):
            tot_hard += 1
            if seg_has(s, COND):
                tot_bound += 1
print(f'  含底线词的句子 {tot_hard} 句，其中含条件词的 {tot_bound} 句（{tot_bound/max(1,tot_hard):.0%}）')
