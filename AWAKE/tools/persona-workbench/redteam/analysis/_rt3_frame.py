#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""R3：E5 动词族有限枚举的绕过面——用「结构特征」而非「动词表」重新测量问答框。"""
import json, glob, os, re, collections

CHARDIR = r'D:\AWAKE-Dev\AWAKE\tools\persona-workbench\characters'

E5_VERBS = '回应|应对|面对|看待|评价|处理|答复|回答|表态|反驳|拒绝|答应|选择|取舍|看|想'
E5_RE = re.compile(r'(如何|怎样|怎么)\s*(%s)' % E5_VERBS)
# 结构特征：问句后接破折号（+引号）——「X会如何/为什么/凭什么…？——“台词”」
FRAME_RE = re.compile(r'[？?]\s*[—\-]{1,2}\s*[“"「]?')
Q_RE = re.compile(r'[？?]')

caught, escaped, clean = [], [], []
for f in sorted(glob.glob(os.path.join(CHARDIR, '*.persona.json'))):
    j = json.load(open(f, encoding='utf-8'))
    exs = [str(x) for x in (j.get('selfClaimExamples') or [])]
    if not exs:
        continue
    zh = os.path.basename(f).split('_')[0]
    n = len(exs)
    n_e5 = sum(1 for e in exs if E5_RE.search(e))
    n_frame = sum(1 for e in exs if FRAME_RE.search(e))
    n_q = sum(1 for e in exs if Q_RE.search(e))
    e5_fail = n_e5 / n >= 0.5
    frame_face = n_frame / n >= 0.5
    if frame_face and e5_fail:
        caught.append((zh, n_frame, n, n_e5))
    elif frame_face and not e5_fail:
        escaped.append((zh, n_frame, n, n_e5))
    elif not frame_face and e5_fail:
        caught.append((zh, n_frame, n, n_e5))
    else:
        clean.append(zh)

print('=== 结构脸谱（>=50% 样本为「问号→破折号→引号」问答框） ===')
print('被 E5 动词族抓住 :', len(caught))
print('  ', '、'.join(f'{z}({nf}/{n})' for z, nf, n, _ in caught[:20]))
print()
print('结构脸谱但 E5 漏掉（换动词绕过）:', len(escaped))
print('  ', '、'.join(f'{z}(框{nf}/{n}, 动词命中{ne})' for z, nf, n, ne in escaped))
print()
print('无结构脸谱 :', len(clean), '→', '、'.join(clean))
print()

# 全量汇总
tot_ex = 0
tot_frame = 0
for f in glob.glob(os.path.join(CHARDIR, '*.persona.json')):
    j = json.load(open(f, encoding='utf-8'))
    for e in (j.get('selfClaimExamples') or []):
        tot_ex += 1
        if FRAME_RE.search(str(e)):
            tot_frame += 1
print(f'全量样本 {tot_ex} 条，其中结构问答框 {tot_frame} 条（{tot_frame/tot_ex:.0%}）')
print(f'全量卡 {len(caught)+len(escaped)+len(clean)} 张：E5抓 {len(caught)} / 漏 {len(escaped)} / 干净 {len(clean)}')
