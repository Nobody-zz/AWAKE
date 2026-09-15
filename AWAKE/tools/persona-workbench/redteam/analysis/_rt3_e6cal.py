#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""R3 校准：E6 处境覆盖阈值不能误伤「已知好卡」（拉盖娅/那得娅等 7 张干净卡）。"""
import json, glob, os, collections

CHARDIR = r'D:\AWAKE-Dev\AWAKE\tools\persona-workbench\characters'
CLEAN = ['卢伊汉', '埃隆', '拉盖娅', '普林多尔', '梅利迪尔', '那得娅', '阿拉德维尔']

SITUATIONS = {
    '利益交换': ['钱', '财', '价', '货', '买卖', '付', '金', '银', '买', '卖', '酬', '礼'],
    '索取求助': ['求', '请', '讨', '借', '索取', '求助', '托', '赏'],
    '质询审问': ['质问', '凭什么', '交代', '问', '盘', '审', '责问'],
    '战争武力': ['战', '兵', '杀', '攻', '守', '血', '阵', '军', '箭', '刃'],
    '结盟投靠': ['盟', '投', '依附', '效忠', '归附', '结盟', '臣服'],
    '亲族婚嫁': ['婚', '娶', '嫁', '妻', '夫', '子', '女', '兄弟', '父', '母', '族'],
    '生死殉难': ['死', '殉', '葬', '丧', '命', '赴死'],
    '背叛告发': ['叛', '告', '背弃', '骗', '出卖', '反水'],
    '日常生计': ['田', '收', '市', '食', '粮', '伤', '病', '冬', '饥'],
}

def sit(t):
    best, bn = '其他', 0
    for k, ws in SITUATIONS.items():
        n = sum(1 for w in ws if w in t)
        if n > bn:
            best, bn = k, n
    return best

dist = collections.Counter()
rows = []
for f in sorted(glob.glob(os.path.join(CHARDIR, '*.persona.json'))):
    j = json.load(open(f, encoding='utf-8'))
    exs = [str(x) for x in (j.get('selfClaimExamples') or [])]
    zh = os.path.basename(f).split('_')[0]
    classes = [sit(e) for e in exs]
    uniq = len({c for c in classes if c != '其他'})
    if uniq == 0:
        uniq_eff = 0
    else:
        uniq_eff = uniq
    dist[uniq_eff] += 1
    rows.append((zh, uniq_eff, '/'.join(classes)))

print('=== E6 校准：每卡样本覆盖的处境类数分布 ===')
for k in sorted(dist):
    print(f'  处境类 {k} 种 : {dist[k]} 张')
print()
print('=== 7 张「已知好卡」的覆盖 ===')
for zh, u, cl in rows:
    if zh in CLEAN:
        print(f'  {zh:8s} 类={u}  {cl}')
print()
print('=== 类数==1 的卡（会被 E6 判 FAIL） ===')
one = [r for r in rows if r[1] == 1]
print(f'  共 {len(one)} 张：', '、'.join(z for z, _, _ in one[:30]))
print()
print('=== 类数==0 的卡（全落「其他」，须人工看） ===')
zero = [r for r in rows if r[1] == 0]
print(f'  共 {len(zero)} 张：', '、'.join(z for z, _, _ in zero[:30]))
