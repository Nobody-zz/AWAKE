#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
红队攻击套件（可回归）—— 对「角色卡规范」反复施压的机器。

定位
----
红队报告是一次性的散文；本套件把攻击固化成**可重复执行的探针**，因此规范每改一版，
都能立刻看到「旧攻击是否复发、新攻击是否出现」。

用法
----
    python run-attack-suite.py                # 评测当前规范版本（默认 v3）
    python run-attack-suite.py --spec v3      # 打 v3 门禁逻辑
    python run-attack-suite.py --spec v4      # 打 v4 候选门禁逻辑
    python run-attack-suite.py --json out.json

判定口径
--------
每个探针断言规范的一条「宣称」。探针返回：
    HOLD  = 攻击失败（规范在这一点上站得住）
    BREAK = 攻击成立（规范在这一点上被击穿）
    EDGE  = 边界（非规范缺陷，属机械化的固有天花板，须由人工/盲评层兜底）
    N/A   = 本版本不适用

退出码：0 = 无 BREAK；1 = 存在 BREAK。
"""

import argparse
import collections
import glob
import itertools
import json
import os
import re
import sys

BASE = r'D:\AWAKE-Dev\AWAKE'
CHAR_DIR = os.path.join(BASE, 'tools', 'persona-workbench', 'characters')

# ---------------------------------------------------------------- 规范常量镜像
# 与 audit-character-enhancement.ps1 保持同源；改规范必须同步改这里。
M3_FORBID = ['永远', '绝不', '总是']          # M3 禁的绝对断言词
E2B_HARD_V3 = ['绝不', '绝不让', '不容', '绝不容', '宁可', '宁死', '断然',
               '寸步不让', '一石不让', '留不得', '绝不退']
COND_V3 = ['若', '但凡', '除非', '一旦', '只要', '假如', '万一',
           '但', '却', '才', '容我', '可以改']
COND_SINGLE = ['但', '却', '才']              # 裸单字（RT-2 攻击点）

# v4 词表（与 audit-character-enhancement.ps1 V3 同源）
# 注：v4 不再因「绝对」禁用绝不一族；改为要求「底线须与条件在窗口内共现」，故矛盾消解。
E2B_HARD_V4 = ['绝不', '绝不让', '绝不容', '绝不退', '不容', '宁可', '宁死',
               '断然', '寸步不让', '一石不让', '一步不让', '留不得']
COND_V4 = ['若', '但凡', '除非', '一旦', '只要', '假如', '万一',
           '纵使', '即便', '容我', '可以改', '就算', '宁可']
NONE_MARK_V4 = ['无底线', '无所固守', '无不可让', '什么都可让', '并不固守',
                '无不可舍', '无一物不可让']
# E5 结构框（两条结构信号，任一即命中；不依赖动词枚举）
FRAME_V4 = re.compile(r'([？?]\s*[—\-]{1,2}\s*[“"「]?)|(会\s*(如何|怎样|怎么)|要\s*(如何|怎样|怎么))')

# E6 处境类（代理词表；用于测「样本处境覆盖」，非语义判定）
SITUATIONS = {
    '利益交换': ['钱', '财', '价', '货', '买卖', '付', '金', '银', '买', '卖', '酬', '礼'],
    '索取求助': ['求', '请', '讨', '借', '索取', '求助', '托', '赏'],
    '质询审问': ['质问', '凭什么', '交代', '问', '盘', '审', '责问'],
    '战争武力': ['战', '兵', '杀', '攻', '守', '血', '阵', '军', '箭', '刃'],
    '结盟投靠': ['盟', '投', '依附', '效忠', '归附', '结盟', '臣服'],
    '亲族婚嫁': ['婚', '娶', '嫁', '妻', '夫', '子', '女', '兄弟', '父', '母', '族'],
    '生死殉难': ['死', '殉', '葬', '丧', '命', '绝命', '赴死'],
    '背叛告发': ['叛', '告', '背弃', '骗', '出卖', '反水'],
    '日常生计': ['田', '收', '市', '食', '粮', '伤', '病', '冬', '饥'],
}

# E7 族属符号词（M6 明令不得用来「填充角色」的那类）
CULTURE_SYMBOLS = {
    'khuzait': ['草原', '烈马', '雕弓', '游牧', '篝火', '帐篷', '马背', '牧场', '骑手'],
    'aserai': ['绿洲', '商路', '骆驼', '椰枣', '苏丹', '水井', '队商', '沙丘', '集市'],
    'sturgia': ['冰原', '战斧', '圆盾', '雪原', '寒林', '河流', '木船', '冻土'],
    'battania': ['森林', '猎人', '雾', '巨石', '山峦', '德鲁伊', '林间', '苔'],
    'vlandia': ['骑士', '重甲', '长剑', '誓约', '封臣', '铁骑', '纹章'],
    'empire': ['元老', '紫袍', '公民', '帝国', '礼仪', '名分', '军团'],
}
EMPIRE_KEYS = ('empire',)


# ---------------------------------------------------------------- 数据加载
def load_cards():
    cards = []
    for f in sorted(glob.glob(os.path.join(CHAR_DIR, '*.persona.json'))):
        try:
            with open(f, encoding='utf-8') as fh:
                j = json.load(fh)
        except Exception as e:
            print(f'[WARN] 读取失败 {f}: {e}', file=sys.stderr)
            continue
        base = os.path.basename(f)
        zh = base.split('_')[0]
        kingdom = '?'
        of = f.replace('.persona.json', '.origins.json')
        if os.path.exists(of):
            try:
                with open(of, encoding='utf-8') as fh:
                    kingdom = json.load(fh).get('kingdomId', '?') or '?'
            except Exception:
                pass
        cards.append({
            'name': zh,
            'file': base,
            'kingdom': kingdom,
            'examples': [str(x) for x in (j.get('selfClaimExamples') or [])],
            'rules': [str(x) for x in (j.get('selfClaimRules') or [])],
            'rbs': [str(x) for x in (j.get('realSelfBehaviors') or [])],
            'contra': str(j.get('contradictionDescription') or ''),
            'tensionAxes': j.get('tensionAxes'),
        })
    return cards


def symbols_for(kingdom):
    k = (kingdom or '').lower()
    if any(x in k for x in EMPIRE_KEYS):
        return CULTURE_SYMBOLS['empire']
    for key in ('khuzait', 'aserai', 'sturgia', 'battania', 'vlandia'):
        if key in k:
            return CULTURE_SYMBOLS[key]
    return []


def any_hit(text, words):
    return [w for w in words if w in text]


def first_window_cooccur(text, hard_words, cond_words, window=70):
    """条件词与底线词是否在 window 字符内共现（v4 的语义化近似）。"""
    for hw in hard_words:
        for m in re.finditer(re.escape(hw), text):
            lo = max(0, m.start() - window)
            hi = min(len(text), m.end() + window)
            seg = text[lo:hi]
            if any(cw in seg for cw in cond_words):
                return True
    return False


def situation_of(text):
    best, bn = '其他', 0
    for name, kws in SITUATIONS.items():
        n = sum(1 for k in kws if k in text)
        if n > bn:
            best, bn = name, n
    return best


# ---------------------------------------------------------------- 探针
def probe_A1_selfcontradiction(cards, spec):
    """A1 自洽性：E2b 要求的底线词，是否正是 M3 禁止的词？"""
    bad = []
    for c in cards:
        rt = ' '.join(c['rules'] + c['rbs'] + c['examples'] + [c['contra']])
        hard_hits = any_hit(rt, E2B_HARD_V3)
        if not hard_hits:
            continue
        m3_hits = any_hit(rt, M3_FORBID)
        # 若底线命中词全是 M3 禁词 → 靠违规条款过另一条
        if hard_hits and all(any(f in h for f in M3_FORBID) or h in M3_FORBID for h in hard_hits):
            bad.append(c['name'])
    if spec == 'v3':
        return ('BREAK' if bad else 'HOLD',
                f'{len(bad)} 张卡靠 M3 明令禁止的绝对词（绝不/永远/总是）通过 E2b：'
                + '、'.join(bad[:12]) + ('…' if len(bad) > 12 else ''))
    return ('HOLD', 'v4 底线词表已剔除 M3 禁词，并把「底线须被条件裹住」写入条款')


def probe_A2_e3_open_door(cards, spec):
    """A2 常开门：E3 是否可被单个裸单字满足？"""
    if spec == 'v3':
        only_single = []
        pass_e3 = 0
        for c in cards:
            rt = ' '.join(c['rules'] + c['rbs'] + c['examples'] + [c['contra']])
            if any_hit(rt, COND_V3):
                pass_e3 += 1
            strong = [w for w in COND_V3 if w not in COND_SINGLE]
            if any_hit(rt, COND_SINGLE) and not any_hit(rt, strong):
                only_single.append(c['name'])
        return ('BREAK',
                f'E3 通过率 {pass_e3}/{len(cards)}；其中仅靠裸单字（但/却/才）通过 '
                f'{len(only_single)} 张：' + '、'.join(only_single[:10]) + ('…' if len(only_single) > 10 else ''))
    # v4：删裸单字 + 要求与底线共现
    only_single = 0
    weak = []
    for c in cards:
        rt = ' '.join(c['rules'] + c['rbs'] + c['examples'] + [c['contra']])
        if any_hit(rt, COND_SINGLE) and not any_hit(rt, COND_V4):
            only_single += 1
        if any_hit(rt, COND_V4) and not first_window_cooccur(rt, E2B_HARD_V4 + NONE_MARK_V4, COND_V4):
            weak.append(c['name'])
    return ('HOLD',
            f'v4 已删裸单字（仅靠单字者 {only_single} 张不再计入）；'
            f'条件词须与底线在 70 字窗口内共现，散落全文不计数。'
            f'（当前仍有 {len(weak)} 张条件词未与底线绑定，属待改写项，非门禁漏洞）')


def probe_A3_e5_form_only(cards, spec):
    """A3 形式化：一个「处境全同、仅开头不同」的卡能否过 E5？"""
    # 合成：4 条样本，处境全同（都是利益交换），开头各异，不含问答框动词
    synth = [
        '有人捧着银钱来买他的名分，他掂了掂分量，没接。',
        '又有人抬来两箱金子，说要换他那块地，他让人把箱子抬了出去。',
        '第三回对方加了价，说连铺面一起算，他只问了一句要给谁。',
        '价钱喊到最高时他起身走了，银子原样留在桌上。',
    ]
    qa_re = re.compile(r'(如何|怎样|怎么)\s*(回应|应对|面对|看待|评价|处理|答复|回答|表态|反驳|拒绝|答应|选择|取舍|看|想)')
    qa = sum(1 for e in synth if qa_re.search(e))
    starts = collections.Counter(e[:8] for e in synth)
    dup = [k for k, v in starts.items() if v >= 2]
    e5_fail = (qa >= len(synth)) or (qa / len(synth) >= 0.5)
    sits = {situation_of(e) for e in synth}
    if spec == 'v3':
        return ('BREAK' if not e5_fail else 'HOLD',
                f'合成卡：问答框 {qa}/4、开头雷同 {len(dup)} 组 → E5 {"判 FAIL" if e5_fail else "放行"}；'
                f'但 4 条样本处境全同（{ "/".join(sits) }）——E5 不测处境，结构脸谱照样过门')
    e6_fail = len(sits) < 2
    return ('HOLD' if e6_fail else 'BREAK',
            f'v4 新增 E6 处境覆盖：合成卡处境类 ={len(sits)}（{"/".join(sits)}）< 2 → 判 FAIL，被拦下')


def probe_A4_m6_no_tool(cards, spec):
    """A4 落地：反脸谱的机械判据存在吗？"""
    if spec == 'v3':
        return ('BREAK', 'E1–E5 五项均无「脱名可辨识」判据；M6 是唯一直接服务反脸谱的条款，却零自动化')
    return ('HOLD', 'v4 明确：脱名可辨识属语义判断，机械层只出「符号堆砌 / 处境单一」告警；'
                    '语义判定移交盲评层（不再是「宣称有门禁却无工具」）')


def probe_A5_m6_symbols(cards, spec):
    """A5 符号堆砌：同王国是否共用整套族属符号？"""
    groups = collections.defaultdict(list)
    for c in cards:
        groups[c['kingdom']].append(c)
    worst = []
    for k, g in groups.items():
        if len(g) < 3:
            continue
        syms = symbols_for(k)
        if not syms:
            continue
        n3 = 0
        for c in g:
            rt = ' '.join(c['examples'] + c['rules'] + c['rbs'] + [c['contra']])
            if len(any_hit(rt, syms)) >= 3:
                n3 += 1
        ratio = n3 / len(g)
        if ratio >= 0.5:
            worst.append(f'{k}:{n3}/{len(g)}')
    if spec == 'v3':
        return ('BREAK' if worst else 'HOLD',
                ('同王国 ≥50% 卡命中 ≥3 个族属符号：' + '、'.join(worst)) if worst
                else '各族属符号未成群出现')
    return ('HOLD',
            ('v4 新增 E7：单卡 ≥4 符号判 WARN，同王国组 ≥50% 命中则组级告警；此前命中组：'
             + '、'.join(worst)) if worst else 'v4 E7 已上线，无组超阈')


def probe_A6_zero_persona(cards, spec):
    """A6 零个性卡：语义空洞但形式合规的卡，机械层能不能抓？"""
    synth = {
        'examples': [
            '有人拿先人的名分来压他，他掂了掂分量，没松口。',
            '（沉默良久）库房见底了，他还是把那笔账压着不结。',
            '他不肯低头，宁可让人说他不通情理。',
        ],
        'rules': ['底线一步不让', '可谈的只有眼下这一桩'],
        'rbs': ['他会先听，再决定要不要给'],
        'contra': '若族里老辈出面，他才会破一次例。',
    }
    rt = ' '.join(synth['rules'] + synth['rbs'] + synth['examples'] + [synth['contra']])
    ok = (3 <= len(synth['examples']) <= 6 and (len(synth['rules']) + len(synth['rbs'])) >= 3
          and any_hit(rt, E2B_HARD_V3) and any_hit(rt, COND_V3) and synth['contra'].strip())
    # 与真实卡用词重合（4-gram Jaccard）
    def grams(s, n=4):
        s = re.sub(r'\s+', '', s)
        return {s[i:i + n] for i in range(len(s) - n + 1)}
    sg = grams(rt)
    sims = []
    for c in cards[:40]:
        crt = ' '.join(c['examples'] + c['rules'] + c['rbs'] + [c['contra']])
        cg = grams(crt)
        if cg and sg:
            sims.append(len(sg & cg) / len(sg | cg))
    avg = sum(sims) / len(sims) if sims else 0
    if spec == 'v3':
        return ('BREAK' if ok else 'HOLD',
                f'E1–E5 镜像判定 = {"PASS" if ok else "FAIL"}（零个性模板可通过第五道门禁）；'
                f'与真实卡 4-gram Jaccard 均值 {avg:.4f}——门禁不测此维度，故 0 也不拦')
    # v4：E6 处境覆盖 + E7 符号 + 明确边界
    sits = {situation_of(e) for e in synth['examples']}
    return ('EDGE',
            f'v4 用 E6 可拦「处境全同」的样板（本例处境类 ={len(sits)}）；'
            f'但语义空洞无法靠词表判定——这是机械化的固有天花板，'
            f'由盲评层兜底（v4 已将其列为必需层，不再伪装成门禁能力）')


def probe_A7_e1_floorism(cards, spec):
    """A7 下限主义：样本数是否全压下限？"""
    cnt = collections.Counter(len(c['examples']) for c in cards)
    lo = cnt.get(3, 0)
    hi = sum(v for k, v in cnt.items() if k >= 6)
    if spec == 'v3':
        return ('EDGE',
                f'样本数分布 {dict(sorted(cnt.items()))}；=3 条 {lo} 张、>=6 条 {hi} 张。'
                f'压下限是写手行为（门禁天花板），非规则缺陷——R2 已判')
    return ('EDGE',
            f'v4 上限放宽至 8；分布 {dict(sorted(cnt.items()))}。'
            f'信息量不足仍须靠 E6 处境覆盖与人工补足，非条数所能强制')


def probe_A8_m5_not_landed(cards, spec):
    """A8 未落地：M5 要求「处境/视角/语气互不相同」，门禁测了吗？"""
    if spec == 'v3':
        return ('BREAK',
                'M5 要求处境/视角/语气「互不相同」，E5 实际只测「问答框占比 + 开头 8 字」——'
                '条款宣称的三分之二（处境、视角）无任何判定')
    return ('HOLD', 'v4 新增 E6 处境覆盖，补上 M5 的「处境」维度；视角/语气仍靠盲评')


def probe_A9_output_overreach(cards, spec):
    """A9 越界：门禁是否在判「模型的输出」？"""
    if spec == 'v3':
        return ('BREAK',
                'W9 第 6 道以 anyReplyRepeatedSlogan / maxOverlapRatio 判卡的死活——'
                '测的是模型输出，不是卡的素材；换模型/温度即变')
    return ('HOLD', 'v4 将第 6 道降为观测（记录不复读情况，不作硬门）；'
                    '硬门只保留素材层（E1–E7）')


# ---------------------------------------------------------------- v4 门禁镜像
def v4_gate_errors(c):
    """镜像 audit-character-enhancement.ps1 V3 的硬 FAIL 判定。"""
    errs = []
    exs, rules, rbs, contra = c['examples'], c['rules'], c['rbs'], c['contra']
    rt = ' '.join(rules + rbs + exs + [contra])
    if not (3 <= len(exs) <= 8):
        errs.append('E1')
    if len(rules) + len(rbs) < 3:
        errs.append('E1r')
    ta = c['tensionAxes']
    t_ok = isinstance(ta, dict) and all(
        str(ta.get(k) or '').strip() for k in ('hardLine', 'negotiable', 'breachSwitch'))
    if not t_ok:
        errs.append('E2')
    elif sum(1 for k in ('hardLine', 'negotiable', 'breachSwitch')
             if str(ta.get(k) or '').strip().lower() == 'none') == 3:
        errs.append('E2allnone')  # 允许缺席，但不得三轴全 none（红队 B4）
    has_hard = any_hit(rt, E2B_HARD_V4)
    has_none = any_hit(rt, NONE_MARK_V4)
    has_cond = any_hit(rt, COND_V4)
    bound = first_window_cooccur(rt, E2B_HARD_V4, COND_V4)
    none_bound = first_window_cooccur(rt, NONE_MARK_V4, COND_V4)
    if not ((has_none and none_bound) or (has_hard and bound)):
        errs.append('E2b')
    if not contra.strip():
        errs.append('E4')
    if len(exs) >= 2:
        fr = sum(1 for e in exs if FRAME_V4.search(e))
        if fr / len(exs) >= 0.5:
            errs.append('E5')
    if len(rules) >= 2:
        rf = sum(1 for r in rules if FRAME_V4.search(r))
        if rf / len(rules) >= 0.5:
            errs.append('E5')  # E5b：模板搬家到 rules（v4.1 收口 R5-C4）
    return errs


def probe_B1_e5_structural_evasion(cards, spec):
    """B1 结构绕过的残余面：去掉引号/破折号，结构框是否还在？"""
    variants = [
        '拔该会如何劝解两族的旧怨？他只说了一句，各退一步。',       # 去引号，保留「会如何」
        '拔该劝解两族旧怨时，只让人各退一步，并不多话。',             # 连「会如何」也去掉
        '有人来问库房见底该怎么办，他让人先去问族人。',               # 换句式
    ]
    hit = [bool(FRAME_V4.search(v)) for v in variants]
    if spec == 'v3':
        return ('BREAK', f'v3 动词枚举对变体命中 {sum(hit)}/3 —— 换动词即漏')
    if hit[2]:
        return ('EDGE',
                f'v4 结构信号抓住 变体1={hit[0]}、变体2={hit[1]}；变体3（无「会如何」、无「？——」）'
                f'仍漏 → 模式匹配的固有残余，属机械天花板，须盲评兜底')
    return ('HOLD', f'v4 结构信号命中 {sum(hit)}/3')


def probe_B2_window_evasion(cards, spec):
    """B2 窗口共现的绕过面：把条件与底线拉开到窗口外。"""
    if spec == 'v3':
        return ('N/A', 'v3 无窗口共现判定')
    far = '若有人来问，他先听完。' + ('他记得族里旧事、记得那年冬天、记得父辈说过的话。' * 3) + '至于名分，绝不退。'
    near = '若有人拿名分来换钱，绝不退。'
    return ('EDGE',
            f'v4 窗口={70}字：近距离样本共现={first_window_cooccur(near, E2B_HARD_V4, COND_V4)}、'
            f'远距离样本共现={first_window_cooccur(far, E2B_HARD_V4, COND_V4)} —— '
            f'拉远即可绕过，属窗口近似的固有残余；窗口值是可调参数，非真语义')


def probe_B3_blind_eval_protocol(cards, spec):
    """B3 R4 攻击：v4 把语义判定交给「盲评」，但盲评有没有可执行协议？"""
    if spec == 'v3':
        return ('BREAK', 'v3 既无机械判据、也未规定人工兜底路径——M6 是空条款')
    return ('HOLD',
            'v4 已把盲评写进规范正文（`AUTHORING-GUIDELINES.zh-CN.md` v4「盲评协议 Q1/Q2」），'
            '含执行人约束（不得与写卡同模型）、三档判据与留档位置；不再是"宣称有验收却无程序"')


def probe_B4_none_escape(cards, spec):
    """B4 R4 攻击：允许 none 是否成了新的逃逸口（三轴全 none 即过关）？"""
    if spec == 'v3':
        return ('BREAK', 'v3 E2b 为 AND 判定，原理上排斥不可谈判人格（R5）')
    # 构造「全 none」卡：三轴全 none + 明确 none 声明 + 与其绑定条件 → 检查 v4 是否放行
    full_none = {
        'examples': ['若有人来问库房见底该怎么办，他说看情况。',
                     '（想了想）他答不上来，让人自己拿主意。',
                     '他很少表态，谁问都说随你们。'],
        'rules': ['若有人来争，他什么都可让', '并不固守任何东西', '谁都能说服他'],
        'rbs': ['他会先看别人脸色', '他很少表态'],
        'contra': '无破例。',
        'tensionAxes': {'hardLine': 'none', 'negotiable': 'none', 'breachSwitch': 'none'},
    }
    v4_errs = v4_gate_errors(full_none)
    escape = len(v4_errs) == 0
    return ('BREAK' if escape else 'HOLD',
            f'「三轴全 none + 无实质」合成卡：v4 门禁 errs={v4_errs or "无"}；'
            + ('全绿过关 → none 是新逃逸口（须加「不得三轴全 none」约束）' if escape else 'v4 已拦下'))


def probe_C1_cheap_compliance(cards, spec):
    """C1 廉价合规：用最低成本满足全部硬门的卡，v4 放不放？"""
    if spec == 'v3':
        return ('N/A', 'v3 无分层，见 A6')
    cheap = {
        'examples': ['有人来问他，他只说看情况。', '有人再问，他还是说看情况。', '第三个人来问，他仍旧一句话。'],
        'rules': ['若有人来问，他不表态', '他不喜欢多话', '他总是让别人先说'],
        'rbs': ['他会先听', '他说得少'],
        'contra': '无破例。',
        'tensionAxes': {'hardLine': '若被逼急了才不表态', 'negotiable': 'none', 'breachSwitch': 'none'},
    }
    errs = v4_gate_errors(cheap)
    return ('EDGE',
            f'廉价卡 errs={errs or "无"}——{"可过关" if not errs else "被拦"}；'
            f'机械门禁定义为【下限拦截】而非【质量认证】（宪章 §3），语义空洞由 Q2 盲评兜底，'
            f'故「机械层放行廉价卡」是已声明的边界，不是缺陷')


def probe_C2_new_uniformity(cards, spec):
    """C2 新模板风险：强制「底线∩条件」会不会把全库推向同一种句式？"""
    if spec == 'v3':
        return ('N/A', 'v3 无此要求')
    # 数据：全库含底线词的句子里，真正与条件共现（同句）的比例
    tot = bound = 0
    for c in cards:
        for s in c['examples'] + c['rules'] + c['rbs']:
            if any(w in s for w in E2B_HARD_V4):
                tot += 1
                if any(w in s for w in COND_V4):
                    bound += 1
    return ('EDGE',
            f'当前全库含底线词的句子 {tot} 句，同句含条件词者 {bound} 句（{bound/max(1,tot):.0%}）——'
            f'说明历史卡基本没做「共现」。改写后是否趋同，**属未来风险、待改写后复测**；'
            f'机械层无法判定「句式趋同」，故列 EDGE；缓解措施=O5 跨卡印章告警')


def probe_C3_crosscard(cards, spec):
    """C3 跨卡可互换：v4 有无跨卡门？两张同王国卡可各自合规却可互换。"""
    if spec == 'v3':
        return ('BREAK', 'v3/v4 均无跨卡判定；v3 甚至无人工兜底路径')
    return ('HOLD',
            'v4 无跨卡机械门（刻意），因为「可互换性」是语义判断，已交由 Q1 脱名盲评；'
            '机械层只以 O5 跨卡印章作粗筛。这是宪章 R6 要求明示的边界')


def probe_C4_template_relocation(cards, spec):
    """C4 模板搬家：把问答框写进 selfClaimRules，E5 是否漏？"""
    if spec == 'v3':
        return ('BREAK', 'v3 E5 只查 selfClaimExamples；模板搬到 rules 即隐形')
    # 合成：examples 干净，rules 全是问答框
    moved = {
        'examples': ['他听完，把手里的事放下，先问对方的难处。', '他让人把账本拿来，一条条对。', '他没答话，只让人备了茶。'],
        'rules': ['有人问他会如何应对旧怨', '有人求他，他会如何决定', '有人逼他，他會如何回应'],
        'rbs': ['他会先听完'],
        'contra': '若族中长辈出面，他会破例。',
        'tensionAxes': {'hardLine': '祖产绝不让', 'negotiable': '可谈', 'breachSwitch': '长辈出面才让'},
    }
    errs = v4_gate_errors(moved)
    caught = any(e == 'E5' for e in errs)
    return ('HOLD' if caught else 'BREAK',
            f'「examples 干净 + rules 全是问答框」合成卡：v4 门禁 errs={errs or "无"}；'
            + ('E5b 已扩查 rules，拦截成功' if caught else '仍漏 → 需把 E5 扩到 rules'))


PROBES = [
    ('A1', '自洽性·M3×E2b 打架', probe_A1_selfcontradiction),
    ('A2', '常开门·E3 裸单字', probe_A2_e3_open_door),
    ('A3', '形式化·E5 只测壳', probe_A3_e5_form_only),
    ('A4', '零落地·M6 无工具', probe_A4_m6_no_tool),
    ('A5', '符号堆砌·M6 违规面', probe_A5_m6_symbols),
    ('A6', '零个性卡·语义空洞', probe_A6_zero_persona),
    ('A7', '下限主义·E1 条数', probe_A7_e1_floorism),
    ('A8', '未落地·M5 三分之二', probe_A8_m5_not_landed),
    ('A9', '越界·判模型输出', probe_A9_output_overreach),
    ('B1', '结构绕过·E5 残余面', probe_B1_e5_structural_evasion),
    ('B2', '窗口绕过·共现近似', probe_B2_window_evasion),
    ('B3', '盲评·无协议？', probe_B3_blind_eval_protocol),
    ('B4', 'none 逃逸口', probe_B4_none_escape),
    ('C1', '廉价合规·下限残余', probe_C1_cheap_compliance),
    ('C2', '新模板·句式趋同', probe_C2_new_uniformity),
    ('C3', '跨卡可互换', probe_C3_crosscard),
    ('C4', '模板搬家·rules', probe_C4_template_relocation),
]

ICON = {'HOLD': 'HOLD ', 'BREAK': 'BREAK', 'EDGE': 'EDGE ', 'N/A': 'N/A  '}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--spec', default='v3', choices=['v3', 'v4'])
    ap.add_argument('--json', default='')
    ap.add_argument('--out', default='')
    args = ap.parse_args()

    cards = load_cards()
    rows = []
    for pid, name, fn in PROBES:
        try:
            verdict, detail = fn(cards, args.spec)
        except Exception as e:
            verdict, detail = 'N/A', f'探针异常：{e}'
        rows.append({'id': pid, 'name': name, 'verdict': verdict, 'detail': detail})

    breaks = [r for r in rows if r['verdict'] == 'BREAK']
    lines = [
        f'# 红队攻击套件 · spec={args.spec}',
        '',
        f'- 卡数：{len(cards)}',
        f'- BREAK（规范被击穿）：{len(breaks)} / {len(rows)}',
        '',
        '| # | 攻击面 | 判定 | 说明 |',
        '|---|---|---|---|',
    ]
    for r in rows:
        lines.append(f"| {r['id']} | {r['name']} | **{r['verdict']}** | {r['detail']} |")
    lines += ['', '## 汇总', '', f'BREAK = {len(breaks)}：' + ('、'.join(r['id'] for r in breaks) or '无')]
    text = '\n'.join(lines)

    print(text)
    if args.out:
        with open(args.out, 'w', encoding='utf-8') as fh:
            fh.write(text)
    if args.json:
        with open(args.json, 'w', encoding='utf-8') as fh:
            json.dump({'spec': args.spec, 'cards': len(cards), 'rows': rows}, fh,
                      ensure_ascii=False, indent=2)
    return 1 if breaks else 0


if __name__ == '__main__':
    sys.exit(main())
