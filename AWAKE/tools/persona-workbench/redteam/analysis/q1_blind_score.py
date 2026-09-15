# -*- coding: utf-8 -*-
"""给 Q1 子智能体盲评答卷打分：对照真值算 top-1 命中率。"""
import json, re, collections, sys

ANS = sys.argv[1] if len(sys.argv) > 1 else r'D:\AWAKE-Dev\.workbuddy\tmp\q1\q1_answer_subagent.md'
TR = sys.argv[2] if len(sys.argv) > 2 else r'D:\AWAKE-Dev\.workbuddy\tmp\q1_truth\q1_truth.json'

truth = json.load(open(TR, encoding='utf-8'))
pid2name = {c['pid']: c['name'] for c in truth['cards']}
sid2name = {s: v['name'] for s, v in truth['samples'].items()}
sid2card = {s: v['card'] for s, v in truth['samples'].items()}

txt = open(ANS, encoding='utf-8').read()
rows = re.findall(r'^\|\s*(S\d{2})\s*\|\s*(P\d{2}|无法判断)\s*\|', txt, flags=re.M)

n = len(rows)
hit = 0
unk = 0
conf = collections.Counter()
per_person = collections.defaultdict(lambda: [0, 0])   # name -> [correct, total]
by_sid = {}
for sid, pred in rows:
    truth_name = sid2name.get(sid)
    pred_name = pid2name.get(pred) if pred.startswith('P') else None
    ok = (pred_name == truth_name)
    by_sid[sid] = (pred, truth_name, ok)
    if pred == '无法判断':
        unk += 1
    elif ok:
        hit += 1
    per_person[truth_name][1] += 1
    if ok:
        per_person[truth_name][0] += 1

print('判读条数:', n, ' 其中「无法判断」:', unk)
print('随机基线 = 1/12 = 8.3%')
print('top-1 命中: %d/%d = %.1f%%' % (hit, n, 100.0 * hit / max(n, 1)))
print()
print('=== 逐人命中 ===')
for name, (c, t) in sorted(per_person.items(), key=lambda x: (x[1][0] / max(x[1][1], 1), x[0])):
    print('  %-8s %d/%d' % (name, c, t))
print()
print('=== 逐条 ===')
for sid in sorted(by_sid):
    pred, tn, ok = by_sid[sid]
    print('  %s 判=%-4s 真=%-8s %s' % (sid, pred, tn, '✓' if ok else '✗'))

# 混淆：真名 -> 被误判成的名字
conf_map = collections.Counter()
for sid, (pred, tn, ok) in by_sid.items():
    if not ok and pred.startswith('P'):
        conf_map[(tn, pid2name.get(pred))] += 1
print()
print('=== 主要混淆（真->误判）===')
for (a, b), k in conf_map.most_common(10):
    print('  %-8s -> %-8s x%d' % (a, b, k))
