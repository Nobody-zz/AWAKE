# -*- coding: utf-8 -*-
"""生成角色卡质量基线 JSON（机器可读），供整改批次作回归基准。

判定口径与 `tools/persona-workbench/tools/audit-character-enhancement.ps1` (V3，规范 v4) 一致：
- E1 条数窗口 3..8，rules+rbs >= 3
- E2 tensionAxes 三轴齐备且非空（可填 none；但不得三轴全 none）
- E2b 底线词 与 条件词 在 70 字窗口内共现（或 none 声明与条件共现）——不再是"全文分别命中"
- E4 contradictionDescription 非空
- E5 结构框（`？——"` 或 `会/要+如何/怎样/怎么`）占比 >= 50% 记 FAIL；rules 同样查
- O2 告警；O3/O4 主判在 ps1；O5 跨卡同句（本文件与 ps1 同源实现，见 o5_*）
  O5 = core 全文 10-gram 滑窗 + examples 开头 8 字，同一串跨 >=3 张卡 -> 告警
       （折叠：按命中卡集分组，锚卡=组内 Ordinal 最小者，在其 core 中合并相邻命中 gram 取最长片段）

用法：python gen-persona-quality-baseline.py
"""
import json
import os
import re
import glob
import collections

CARDS = r'D:\AWAKE-Dev\AWAKE\tools\persona-workbench\characters'
OUT = r'D:\AWAKE-Dev\AWAKE\docs\persona-quality-baseline-20260913.json'

# 与 audit-character-enhancement.ps1 (V3) 完全一致
HARD = ['绝不', '绝不让', '绝不容', '绝不退', '不容', '宁可', '宁死', '断然',
        '寸步不让', '一石不让', '一步不让', '留不得']
COND = ['若', '但凡', '除非', '一旦', '只要', '假如', '万一', '纵使', '即便', '容我', '可以改', '就算', '宁可']
NONE_MARKS = ['无底线', '无所固守', '无不可让', '什么都可让', '并不固守', '无不可舍', '无一物不可让']
FRAME_PAT = re.compile(r'([？?]\s*[—\-]{1,2}\s*[“"「]?)|(会\s*(如何|怎样|怎么)|要\s*(如何|怎样|怎么))')
WINDOW = 70

# O5 v2（与 ps1 逐字同源）
O5_N_CORE = 10
O5_N_EX = 8
O5_MIN = 3
O5_MAX_CARDS = 6
O5_EDGE_RE = re.compile(r'^[，。、；：！？…—·,.!?;:（）()「」『』《》〈〉<>"\'’‘“”]+'
                        r'|[，。、；：！？…—·,.!?;:（）()「」『』《》〈〉<>"\'’‘“”]+$')

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


def window_cooccur(text, keys, cond, win=WINDOW):
    for k in keys:
        start = 0
        while True:
            p = text.find(k, start)
            if p < 0:
                break
            seg = text[max(0, p - win): p + len(k) + win]
            if any(c in seg for c in cond):
                return True
            start = p + len(k)
    return False


def situation(t):
    best, bn = '其他', 0
    for k, ws in SITUATIONS.items():
        n = sum(1 for w in ws if w in t)
        if n > bn:
            best, bn = k, n
    return best


# ── O5 v2（与 ps1 同源）──────────────────────────────────────────────────────
def _norm(s):
    return '' if s is None else ''.join(str(s).split())


def _trim(s):
    return '' if s is None else O5_EDGE_RE.sub('', s)


def _fold_anchor(text, grams, n):
    idx = [i for i in range(len(text) - n + 1) if text[i:i + n] in grams]
    if not idx:
        return ''
    bs, be = idx[0], idx[0] + n
    cs, ce = idx[0], idx[0] + n
    for p in idx[1:]:
        if p <= ce:
            ce = max(ce, p + n)
        else:
            if ce - cs > be - bs:
                bs, be = cs, ce
            cs, ce = p, p + n
    if ce - cs > be - bs:
        bs, be = cs, ce
    return text[bs:be]


def build_o5_index(cards):
    """cards: [(name, core_norm, [ex_prefix8])]  -> (core_inv, ex_inv)"""
    core_inv = collections.defaultdict(set)
    ex_inv = collections.defaultdict(set)
    for name, core, exs in cards:
        for g in {core[i:i + O5_N_CORE] for i in range(len(core) - O5_N_CORE + 1)}:
            core_inv[g].add(name)
        for k in exs:
            ex_inv[k].add(name)
    return core_inv, ex_inv


def o5_findings_for(cards, core_inv, ex_inv, core_text):
    """-> {cardName: [msg,...]}"""
    grouped = collections.defaultdict(list)

    def fold_sets(inv, channel):
        byset = collections.defaultdict(set)
        for g, cs in inv.items():
            if len(cs) >= O5_MIN:
                byset[frozenset(cs)].add(g)
        for cs, gs in byset.items():
            cl = sorted(cs)
            if channel == 'core':
                anchor = cl[0]
                span = _trim(_fold_anchor(core_text.get(anchor, ''), gs, O5_N_CORE)) or sorted(gs)[0]
            else:
                span = sorted(gs)[0]
            shown = '、'.join(cl[:O5_MAX_CARDS]) + ('…' if len(cl) > O5_MAX_CARDS else '')
            msg = ('O5:core 与其它卡同句「%s」（跨 %d 张：%s）' % (span, len(cl), shown)
                   if channel == 'core' else
                   'O5:examples 开头雷同「%s」（跨 %d 张：%s）' % (span, len(cl), shown))
            yield {'n': len(cl), 'ch': channel, 'span': span, 'msg': msg, 'cards': cl}

    items = list(fold_sets(core_inv, 'core')) + list(fold_sets(ex_inv, 'examples'))
    items.sort(key=lambda f: (-f['n'], f['ch'], f['span']))
    by_card = collections.defaultdict(list)
    for f in items:
        for c in f['cards']:
            by_card[c].append(f['msg'])
    return by_card


def main():
    files = sorted(glob.glob(os.path.join(CARDS, '*.persona.json')))
    raw = []
    o5cards = []
    core_text = {}
    for f in files:
        j = json.load(open(f, encoding='utf-8'))
        name = os.path.basename(f).split('_')[0]
        exs = [str(x) for x in (j.get('selfClaimExamples') or [])]
        core = _norm(j.get('core') or '')
        core_text[name] = core
        exkeys = []
        for e in exs:
            b = _norm(e).lstrip('（')
            if len(b) >= O5_N_EX:
                exkeys.append(b[:O5_N_EX])
        o5cards.append((name, core, exkeys))
        raw.append((name, j, exs))

    core_inv, ex_inv = build_o5_index(o5cards)
    o5_by_card = o5_findings_for(o5cards, core_inv, ex_inv, core_text)

    rows = []
    for name, j, exs in raw:
        rules = [str(x) for x in (j.get('selfClaimRules') or [])]
        rbs = [str(x) for x in (j.get('realSelfBehaviors') or [])]
        contra = (j.get('contradictionDescription') or '').strip()
        rt = ' '.join(rules + rbs + exs + [contra])
        t = j.get('tensionAxes')
        t_ok = bool(t) and all(str(t.get(k) or '').strip() for k in ('hardLine', 'negotiable', 'breachSwitch'))
        all_none = t_ok and sum(1 for k in ('hardLine', 'negotiable', 'breachSwitch')
                                if str(t.get(k) or '').strip().lower() == 'none') == 3
        has_hard = any(w in rt for w in HARD)
        has_none = any(w in rt for w in NONE_MARKS)
        has_cond = any(w in rt for w in COND)
        bound = window_cooccur(rt, HARD, COND)
        none_bound = window_cooccur(rt, NONE_MARKS, COND)
        e2b_ok = (has_none and none_bound) or (has_hard and bound)
        ex_frame = sum(1 for e in exs if FRAME_PAT.search(e))
        rule_frame = sum(1 for r in rules if FRAME_PAT.search(r))
        sits = {situation(e) for e in exs if situation(e) != '其他'}

        fails = []
        if len(exs) < 3 or len(exs) > 8 or len(rules) + len(rbs) < 3:
            fails.append('E1')
        if not t_ok or all_none:
            fails.append('E2')
        if not e2b_ok:
            fails.append('E2b')
        if not contra:
            fails.append('E4')
        if len(exs) >= 2 and ex_frame / len(exs) >= 0.5:
            fails.append('E5')
        if len(rules) >= 2 and rule_frame / len(rules) >= 0.5:
            fails.append('E5')

        warns = []
        if len(exs) >= 2 and len(sits) < 2:
            warns.append('O2')
        for m in o5_by_card.get(name, []):
            warns.append('O5')   # 明细见 o5Findings
        rows.append({
            'name': name,
            'cardFile': os.path.basename(
                [f for f in files if os.path.basename(f).split('_')[0] == name][0]),
            'id': j.get('id'),
            'status': j.get('status'),
            'examplesCount': len(exs),
            'frameHits': ex_frame,
            'hasTensionAxes': t_ok,
            'allNoneAxes': all_none,
            'hasHardline': has_hard,
            'hardlineBoundToCond': bound,
            'hasConditional': has_cond,
            'hasContradiction': bool(contra),
            'situationClasses': sorted(sits),
            'o5Findings': o5_by_card.get(name, []),
            'warns': warns,
            'fails': fails,
            'pass': len(fails) == 0,
        })

    summary = {
        'schemaVersion': 'awake.persona.quality-baseline.v2',
        'generatedAt': '2026-09-13',
        'gate': 'audit-character-enhancement.ps1 (V3 / 规范 v4)',
        'total': len(rows),
        'passed': sum(1 for r in rows if r['pass']),
        'failed': sum(1 for r in rows if not r['pass']),
        'byRule': {k: sum(1 for r in rows if k in r['fails']) for k in ['E1', 'E2', 'E2b', 'E4', 'E5']},
        'byWarn': {k: sum(1 for r in rows if k in r['warns']) for k in ['O2', 'O5']},
        'o5TotalFindings': sum(len(r['o5Findings']) for r in rows),
        'passedCards': [r['name'] for r in rows if r['pass']],
        'cards': rows,
    }
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, 'w', encoding='utf-8') as fp:
        json.dump(summary, fp, ensure_ascii=False, indent=2)
        fp.write('\n')
    print('wrote', OUT)
    print('total', summary['total'], 'passed', summary['passed'], 'failed', summary['failed'])
    print('byRule', summary['byRule'])
    print('byWarn', summary['byWarn'], 'o5TotalFindings', summary['o5TotalFindings'])
    print('passed', summary['passedCards'])


if __name__ == '__main__':
    main()
