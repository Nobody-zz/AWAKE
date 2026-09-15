# -*- coding: utf-8 -*-
"""Q1 严格变体（B）：连"族属符号"一起遮，只留口吻/价值/在意之事。
与变体 A 的差别：额外遮去部族名与亲属名，让辨认只能靠说话的样子。"""
import json, glob, os, re, random

CH = r'D:\AWAKE-Dev\AWAKE\tools\persona-workbench\characters'
OUT = r'D:\AWAKE-Dev\.workbuddy\tmp\q1b'
TRUTH = r'D:\AWAKE-Dev\.workbuddy\tmp\q1b_truth'
os.makedirs(OUT, exist_ok=True)
os.makedirs(TRUTH, exist_ok=True)

MAX_PER = 3
random.seed(20260913)

cards = []
for f in sorted(glob.glob(os.path.join(CH, '*.origins.json'))):
    o = json.load(open(f, encoding='utf-8'))
    if o.get('kingdomId') != 'khuzait':
        continue
    base = f[:-len('.origins.json')]
    doc = json.load(open(base + '.persona.json', encoding='utf-8'))
    cards.append({'base': os.path.basename(base), 'heroId': o.get('heroId'),
                  'name': doc.get('displayName') or '', 'doc': doc})

# 1) 人名（全体 76 卡）
names = set()
for f in glob.glob(os.path.join(CH, '*.persona.json')):
    d = json.load(open(f, encoding='utf-8'))
    if d.get('displayName'):
        names.add(d['displayName'])

# 2) 部族名 / 亲属名：从 12 卡身份文本与 origins 里抽
CLANS = ['兀儿浑乃特', '兀儿浑', '巴鲁台特', '斡不力特', '阔勒帖特', '帖克力特',
         '库吉特', '阿契特', '颜赛力特', '合儿必特']
for c in cards:
    txt = (c['doc'].get('identityFacts') or '') + (c['doc'].get('summary') or '')
    for m in re.finditer(r'库赛特([\u4e00-\u9fa5]{2,5}?)(?:氏族|部|家)', txt):
        CLANS.append(m.group(1))
    for m in re.finditer(r'[妻子女父母兄第]([\u4e00-\u9fa5]{2,3})', txt):
        names.add(m.group(1))
    for m in re.finditer(r'、([\u4e00-\u9fa5]{2,3})[。，]', txt):
        names.add(m.group(1))
CLANS = sorted(set(c for c in CLANS if c), key=len, reverse=True)
# 清词表：剥掉"汗国"前缀，剔除误捕的普通词
BAD = {'统治', '汗国', '部族', '氏族', '家族'}
CLANS = [c[2:] if c.startswith('汗国') and len(c) > 4 else c for c in CLANS]
CLANS = sorted({c for c in CLANS if c and c not in BAD}, key=len, reverse=True)
names = sorted((n for n in names if n and n != '某'), key=len, reverse=True)

# 注意：库赛特 = 汗国名，12 人共有，遮它无意义，保留
CLANS = [c for c in CLANS if c != '库赛特']

def mask(s):
    for n in names:
        s = s.replace(n, '某')
    for c in CLANS:
        s = s.replace(c, '本族')
    return s

pool = []
for c in cards:
    for e in (c['doc'].get('selfClaimExamples') or [])[:MAX_PER]:
        pool.append({'card': c['base'], 'name': c['name'], 'src': mask(str(e))})
random.shuffle(pool)
for i, s in enumerate(pool, 1):
    s['sid'] = 'S%02d' % i

L = []
L.append('# 盲评任务（严格版）：仅凭口吻，认出这是谁')
L.append('')
L.append('下面有 **12 位库赛特人物**的身份卡，以及一批**匿名打乱的第一人称独白样本**（S01…）。')
L.append('样本已遮去**人名与部族名**（人名处「某」，部族名处「本族」），并已打乱顺序、与人物编号无关。')
L.append('注意：部族名一律写作「本族」，所以**不能靠部族归属**判断——只能靠说话的样子。')
L.append('')
L.append('## 你的任务')
L.append('对**每一条**样本，判断它出自哪一位人物，输出编号（P01…P12）。')
L.append('确实无法判断时才写 `无法判断`。**不要硬猜**——判断依据是口吻、价值排序、脾气、在意的事。')
L.append('末尾请指出**哪些样本像同一个人在说**（但你也说不准是哪一个）。')
L.append('')
L.append('## 人物身份卡')
L.append('')
for pi, c in enumerate(cards, 1):
    bio = (c['doc'].get('identityFacts') or '').strip()
    bio = re.sub(r'本卡依.*$', '', bio).strip()
    if len(bio) > 200:
        bio = bio[:200] + '…'
    L.append('**P%02d · %s**' % (pi, c['name']))
    L.append('- 身份：' + mask(bio))
    L.append('')
L.append('## 待判样本（匿名、乱序）')
L.append('')
for s in pool:
    L.append('**%s**' % s['sid'])
    L.append('> ' + s['src'].replace('\n', ' ').strip())
    L.append('')
L.append('## 输出格式')
L.append('先给对照表：')
L.append('')
L.append('| 样本 | 判定人物 | 把握(高/中/低) | 一句话理由 |')
L.append('|---|---|---|---|')
L.append('| S01 | P?? | 中 | …… |')
L.append('')
L.append('再给「像同一个人的样本组」。')

open(os.path.join(OUT, 'q1b_prompt.md'), 'w', encoding='utf-8').write('\n'.join(L))

truth = {'cards': [{'pid': 'P%02d' % (i + 1), 'base': c['base'], 'name': c['name'], 'heroId': c['heroId']} for i, c in enumerate(cards)],
         'samples': {s['sid']: {'card': s['card'], 'name': s['name'], 'text': s['src']} for s in pool}}
json.dump(truth, open(os.path.join(TRUTH, 'q1b_truth.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)

print('遮词条数  人名:', len(names), ' 部族名:', len(CLANS))
print('遮去的部族名:', CLANS)
print('样本数:', len(pool))
print('题面:', os.path.join(OUT, 'q1b_prompt.md'))
