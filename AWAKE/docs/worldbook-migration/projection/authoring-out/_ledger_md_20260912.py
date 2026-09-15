# -*- coding: utf-8 -*-
"""台账 md 生成器（09-12）"""
import json, io, collections

rows = json.load(io.open(r'D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/_chronicle_ledger_20260912.json', encoding='utf-8'))
OUT = r'D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/WORLDBOOK-CHRONICLE-LEDGER-20260912.md'

def cnt(key):
    return collections.Counter(r[key] for r in rows)

L = []
L.append('# 编年史第三步 · 全量 337 条归轴台账（2026-09-12）\n')
L.append('> **性质**：编年史知识库全量逐条归轴的**机器初分台账**（口径＝盘点文档 §五-4 第三步：先地点/事件，后家族/人物，马种与旧作传承词单列）。**机器分类只作草判**，人工逐条裁定沿本台账推进。\n')
L.append('> **数据源**：编年史 337 rule（B 级）× 官方实体登记表 890 实体（锚点判定）× 官方本地化 CN 全量文本（提及判定）。\n')
L.append('> **机器件**：`projection/authoring-out/_chronicle_ledger_20260912.json`（全量结构化数据）＋ `_ledger337_20260912.py`（分类器，分类规则内联可查）。\n')
L.append('\n## 一、总览\n')
L.append('| 维度 | 分布 |\n|---|---|\n')
L.append('| 条数 | **337**（变体 1,926；顶层 TextMappings 112 条；带 When 条件 288 条） |\n')
L.append('| 类型草判 | ' + ' · '.join('%s %d' % kv for kv in cnt('kind_guess').most_common()) + ' |\n')
L.append('| 五域草判 | ' + ' · '.join('%s %d' % kv for kv in cnt('domain').most_common()) + ' |\n')
L.append('| 官方交叉 | **Ⅰ锚定（登记表精确命中实体）107** · A 文本提及（官方 CN 文本出现该词）151 · 无官方锚 79 |\n')
L.append('| 已知源缺陷 | 双文件：`rule_中原`×2（同内容、别名不同：中原 vs 中土/珀拉斯特）、`rule_巴旦尼亚水之女神`×2（同 Id 同内容）→ 合并 keywords 后取一 |\n')

anchor = [r for r in rows if r['form_hint'] == 'Ⅰ锚定']
L.append('\n## 二、Ⅰ锚定 %d 条（有官方实体锚，立档最高优先级）\n' % len(anchor))
L.append('\n| 条目 | 官方实体 | 类型草判 | 五域 | 变体 | When 维度 |\n|---|---|---|---|---|---|\n')
for r in sorted(anchor, key=lambda x: (x['domain'], x['name'])):
    wd = ','.join(k for k in r['when_dims']) or '—'
    L.append('| %s | %s | %s | %s | %d | %s |\n' % (r['name'], r['anchor_basis'].replace('registry:', ''), r['kind_guess'], r['domain'], r['n_variants'], wd))

L.append('\n## 三、红线与单列（不进常规立档流）\n')
L.append('\n| 条目 | 处置 | 依据 |\n|---|---|---|\n')
for r in rows:
    if r['kind_guess'] == '信仰（红线单列）':
        L.append('| %s | **红线**：信仰整节排除，仅其它内容自然涉及处带一句（B 级） | Max 09-12 裁定；官方交叉=%s |\n' % (r['name'], r['form_hint']))
for r in rows:
    if r['kind_guess'] == '马种/旧作传承词（单列）':
        L.append('| %s | 单列处理（盘点 §五-4） | 官方交叉=%s |\n' % (r['name'], r['form_hint']))

SECT = [('地点', '四、地点（%d）——第三步主攻第一批'),
        ('事件', '五、事件（名中明确+正文候选，%d）——第三步主攻第二批'),
        ('家族/部族', '六、家族/部族（%d）'),
        ('物产/经济', '七、物产/经济（%d）'),
        ('人物/概念', '八、人物/概念（%d）——面最大，人工逐条裁')]
for key, title_tpl in SECT:
    sel = [r for r in rows if r['kind_guess'] == key]
    L.append('\n## ' + title_tpl % len(sel) + '\n')
    L.append('\n| 条目 | 变体 | When 维度 | 五域 | 官方交叉 | aliases | 摘要头 |\n|---|---|---|---|---|---|---|\n')
    for r in sorted(sel, key=lambda x: (x['domain'], x['name'])):
        wd = ','.join(k[:4] for k in r['when_dims']) or '—'
        L.append('| %s%s | %d | %s | %s | %s | %d | %s |\n' % (
            r['name'], ' ⚠dup' if r.get('dup_id') else '', r['n_variants'], wd,
            r['domain'], r['form_hint'], len(r['keywords']),
            (r['rag_head'] or '')[:40].replace('|', '，')))

io.open(OUT, 'w', encoding='utf-8', newline='\n').write(''.join(L))
print('ledger md written, anchors:', len(anchor), 'lines:', len(L))
