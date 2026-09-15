#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""R5：跨卡印章侦察——同一个开头 8 字/12 字在多少张卡里重复出现（O5 阈值校准）。"""
import json, glob, os, re, collections

CHARDIR = r'D:\AWAKE-Dev\AWAKE\tools\persona-workbench\characters'
FRAME = re.compile(r'([？?]\s*[—\-]{1,2}\s*[“"「]?)|(会\s*(如何|怎样|怎么)|要\s*(如何|怎样|怎么))')

open8 = collections.Counter()
open12 = collections.Counter()
frame_cards = 0
total = 0
for f in sorted(glob.glob(os.path.join(CHARDIR, '*.persona.json'))):
    j = json.load(open(f, encoding='utf-8'))
    exs = [re.sub(r'\s+', '', str(x)) for x in (j.get('selfClaimExamples') or [])]
    total += 1
    if exs and sum(1 for e in exs if FRAME.search(e)) / len(exs) >= 0.5:
        frame_cards += 1
    for e in exs:
        b = e.lstrip('（')
        if len(b) >= 8:
            open8[b[:8]] += 1
        if len(b) >= 12:
            open12[b[:12]] += 1

print('卡数', total)
print('结构问答框占比>=50% 的卡:', frame_cards)
print()
print('=== 跨卡重复的开头 8 字（count>=3） ===')
for k, v in open8.most_common(20):
    if v >= 3:
        print(f'  {v:2d}  {k}')
print()
print('=== 跨卡重复的开头 12 字（count>=3） ===')
for k, v in open12.most_common(20):
    if v >= 3:
        print(f'  {v:2d}  {k}')
