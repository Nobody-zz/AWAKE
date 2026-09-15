# -*- coding: utf-8 -*-
"""Q1 脱名辨认（M6 反脸谱）素材制备。
忠于 M6 定义：「遮住姓名，仅凭口吻能否认出是谁」。
设计：给出 12 位库赛特人物的「身份卡」（名 + 一行概括），
把各卡 selfClaimExamples（自由叙事样本）匿名化后打乱编号，
要求评审者把每条样本归给本人。
对照基准：随机命中 = 1/12 ≈ 8.3%。
"""
import json, glob, os, re, random

CH = r'D:\AWAKE-Dev\AWAKE\tools\persona-workbench\characters'
OUT = r'D:\AWAKE-Dev\.workbuddy\tmp\q1'
TRUTH = r'D:\AWAKE-Dev\.workbuddy\tmp\q1_truth'
os.makedirs(OUT, exist_ok=True)
os.makedirs(TRUTH, exist_ok=True)

MAX_PER = 3          # 每人最多取几条样本
random.seed(20260913)

# 1) 收集库赛特 12 人
cards = []
for f in sorted(glob.glob(os.path.join(CH, '*.origins.json'))):
    o = json.load(open(f, encoding='utf-8'))
    if o.get('kingdomId') != 'khuzait':
        continue
    base = f[:-len('.origins.json')]
    doc = json.load(open(base + '.persona.json', encoding='utf-8'))
    cards.append({'base': os.path.basename(base), 'heroId': o.get('heroId'),
                  'name': doc.get('displayName') or '', 'doc': doc})

# 2) 姓名遮蔽词表：全部 76 张卡的人名 + 12 卡身份文本里 妻子女父母兄 后的名字
allnames = set()
for f in glob.glob(os.path.join(CH, '*.persona.json')):
    d = json.load(open(f, encoding='utf-8'))
    n = d.get('displayName')
    if n:
        allnames.add(n)
for c in cards:
    txt = (c['doc'].get('identityFacts') or '') + (c['doc'].get('summary') or '')
    for m in re.finditer(r'[妻子女父母兄第]([\u4e00-\u9fa5]{2,3})', txt):
        allnames.add(m.group(1))
names = sorted(allnames, key=len, reverse=True)

def mask(s):
    for n in names:
        if n and n in s:
            s = s.replace(n, '某')
    return s

# 3) 取样本
pool = []
for ci, c in enumerate(cards):
    exs = (c['doc'].get('selfClaimExamples') or [])[:MAX_PER]
    for ei, e in enumerate(exs):
        pool.append({'card': c['base'], 'name': c['name'], 'src': mask(str(e))})

random.shuffle(pool)
for i, s in enumerate(pool, 1):
    s['sid'] = 'S%02d' % i

# 4) 写盲评题面（不含真值）
lines = []
lines.append('# 盲评任务：仅凭口吻，认出这是谁')
lines.append('')
lines.append('下面有 **12 位库赛特人物**的身份卡，以及一批**匿名的第一人称独白样本**（S01…）。')
lines.append('样本已遮去人名（出现人名处以「某」代替），并已打乱顺序、与人物编号无关。')
lines.append('')
lines.append('## 你的任务')
lines.append('对**每一条**样本，判断它出自哪一位人物，输出该人物的编号（P01…P12）。')
lines.append('只有当你在两位之间摇摆、确实无法判断时，才写 `无法判断`。')
lines.append('**不要为了凑答案而猜**：判断依据应当是说话的口吻、价值排序、在意的事。')
lines.append('若你发现多条样本其实像同一个人（但你说不准是哪一个），请在末尾指出**哪些样本像同一个人**。')
lines.append('')
lines.append('## 人物身份卡')
lines.append('')
for pi, c in enumerate(cards, 1):
    bio = (c['doc'].get('identityFacts') or '').strip()
    bio = re.sub(r'本卡依.*$', '', bio).strip()      # 去掉"本卡依…补写"的编纂注
    if len(bio) > 200:
        bio = bio[:200] + '…'
    lines.append('**P%02d · %s**' % (pi, c['name']))
    lines.append('- 身份：' + mask(bio))
    lines.append('')
lines.append('## 待判样本（匿名、乱序）')
lines.append('')
for s in pool:
    lines.append('**%s**' % s['sid'])
    lines.append('> ' + s['src'].replace('\n', ' ').strip())
    lines.append('')
lines.append('## 输出格式')
lines.append('先给一张对照表（每行一条）：')
lines.append('')
lines.append('| 样本 | 判定人物 | 把握(高/中/低) | 一句话理由 |')
lines.append('|---|---|---|---|')
lines.append('| S01 | P?? | 中 | …… |')
lines.append('')
lines.append('再给一段「像同一个人的样本组」的说明（若无可写「无」）。')

open(os.path.join(OUT, 'q1_prompt.md'), 'w', encoding='utf-8').write('\n'.join(lines))

# 5) 写真值与人物表（评审者不可见）
truth = {'cards': [{'pid': 'P%02d' % (i + 1), 'base': c['base'], 'name': c['name'], 'heroId': c['heroId']} for i, c in enumerate(cards)],
         'samples': {s['sid']: {'card': s['card'], 'name': s['name'], 'text': s['src']} for s in pool}}
json.dump(truth, open(os.path.join(TRUTH, 'q1_truth.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)

# 6) 回显
print('人物数:', len(cards), ' 样本数:', len(pool))
for c in cards:
    n = sum(1 for s in pool if s['card'] == c['base'])
    print('  %-42s %s  %d条' % (c['base'], c['name'], n))
b = sum(len(s['src']) for s in pool) / len(pool)
print('样本平均字数: %.0f' % b)
print('题面:', os.path.join(OUT, 'q1_prompt.md'))
print('真值:', os.path.join(TRUTH, 'q1_truth.json'))
