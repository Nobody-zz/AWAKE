# -*- coding: utf-8 -*-
"""把 WORLDBOOK-CONTENT-CATALOG-*.json 渲染成人读的 Markdown 目录。
用法: python _write_catalog_md.py [catalog.json] [输出.md]
只读输入；不连数据库（数据库侧见 _build_catalog.py）。"""
import json, sys, os, io

HERE = os.path.dirname(os.path.abspath(__file__))
DOCS = os.path.normpath(os.path.join(HERE, '..', '..', 'docs', 'worldbook-migration'))
SRC = sys.argv[1] if len(sys.argv) > 1 else os.path.join(DOCS, 'WORLDBOOK-CONTENT-CATALOG-20260912.json')
OUT = sys.argv[2] if len(sys.argv) > 2 else os.path.join(DOCS, 'WORLDBOOK-CONTENT-CATALOG-20260912.md')

def load(p):
    b = open(p, 'rb').read()
    for enc in ('utf-8', 'gbk'):
        try:
            return json.loads(b.decode(enc))
        except UnicodeDecodeError:
            continue
    raise RuntimeError('cannot decode ' + p)

d = load(SRC)
types = {o['type']: o for o in d['objectTypes']}
def T(t): return types[t]

L = []
w = L.append
w('# AWAKE 世界书 · 全量内容目录（草案 v1 · 2026-09-12）')
w('')
w('> ⚠ **本文口径已被修正，见 `WORLDBOOK-KNOWLEDGE-SYSTEM-CATALOG-20260912.md`。**')
w('> 错在把「有官方描述文」当成了能否编纂的**门槛**——**没文本 ≠ 没知识**。项目既有规则（v3 手册 B9）本就允许 **D 开发者原创裁定**填补空白。')
w('> 本文仍可用的部分：**「Ⅰ 锚定对象」的素材清单与 ID/中文名**；其中所有标 ❌「不做」的（城堡 67、藏身处 99、次要文化 17、小家族/匪帮）**全部重新进入范围**。')
w('')
w('> **用途**：定「世界书要做哪些条目」的盘子。')
w('> **数据源**：BannerlordSage `bannerlord.db` v1.3.15.110062（= 模组目标 API），只读。')
w('> **总口径（已修正）**：驱动是「世界需要什么知识」，素材分 A 官方文本/游戏数据 · B 编年史 · C 模组提取 · **D 开发者原创（填补空白）**。**「无描述文」不等于「无知识」。**')
w('> **与现有 12 档的关系**：' + d['existing']['note'] + '（现有 lore 锚点：' + '、'.join(d['existing']['loreAnchors']) + '）。')
w('')

w('## 一、总量一览')
w('')
w('| 类型 | 总数 | 自身有描述文 | 被提及 | 可做？ | 拟定档 |')
w('|---|---|---|---|---|---|')
rows = [
    ('王国', 'kingdom', '✅ 全做', '1 档/个（政治：概况+君主）'),
    ('文化', 'culture', '✅ 仅主文化', '1 档/个'),
    ('家族', 'clan', '⚠ 仅贵族（料来自成员）', '1 档/个（谱系+政治）'),
    ('聚落·城镇', 'settlement.town', '✅ 全做', '1~2 档/个（地理+政治）'),
    ('聚落·村庄', 'settlement.village', '⚠ 量大同质', '1 档/个（地理）'),
    ('聚落·城堡', 'settlement.castle', '✅ 做（走 D）', '地理+政治：归属与扼守意义'),
    ('藏身处', 'settlement.hideout', '✅ 做（走 D）', '地理+战争：匪患'),
    ('英雄', 'hero', '✅ 仅 27 个', '1 档/个'),
]
for label, key, ok, plan in rows:
    s = T(key)['summary']
    mt = s.get('hasMention')
    w('| %s | %d | %d | %s | %s | %s |' % (label, s['total'], s['ownText'], ('—' if mt is None else mt), ok, plan))
w('')
w('> 「自身有描述文」= 游戏里直接写了这个对象的介绍；「被提及」= 别的文本里提到过它的次数 > 0。')
w('> 注：家族行「14」指**小家族**（有自述），**73 个贵族家族无自述**，其料须从成员英雄的描述文取。')
w('')

w('## 二、逐类细目')
w('')
k = T('kingdom'); w('### 王国（%d/%d 全做）' % (k['summary']['ownText'], k['summary']['total'])); w('')
for i in k['items']:
    w('- **%s**（`%s`）' % (i['zh'], i['entityId']))
w('')
c = T('culture')
main = []
seen = set()
for i in c['items']:
    if i.get('isMain') and i['zh'] not in seen:
        seen.add(i['zh']); main.append(i)
w('### 文化（主文化 %d 个可做）' % len(main)); w('')
w('- ' + '、'.join(i['zh'] for i in main))
w('- 其余 %d 个（中立/诺德/瓦肯/达西/劫匪/海寇…）**无自述，但可走 D 补体系知识**（语言/信仰/习俗）。' % (c['summary']['total'] - len(main)))
w('')
cl = T('clan'); bd = cl.get('breakdown', {})
w('### 家族（%d 总数；贵族 %s 个为主力）' % (cl['summary']['total'], bd.get('noble', {}).get('total', '?')))
w('')
w('- **贵族家族无自述** → 料须来自**成员英雄**的描述文。')
w('- 小家族 %s / 匪帮 %s 无自述，**可走 D 或并入所属文化/王国档**。' % (bd.get('minor', {}).get('total', '?'), bd.get('bandit', {}).get('total', '?')))
noble = [i for i in cl['items'] if i.get('kind') == 'noble']
noble.sort(key=lambda x: -(x.get('mentions') or 0))
w('- 被提及最多的前 10：' + '、'.join('%s(%d)' % (i['zh'], i.get('mentions') or 0) for i in noble[:10]))
w('')
for label, key, note in [('聚落·城镇', 'settlement.town', '），**全做**'), ('聚落·村庄', 'settlement.village', '），**全做**；量大且同质')]:
    s = T(key)
    w('### %s（%d）' % (label, s['summary']['total']))
    w('')
    items = sorted(s['items'], key=lambda x: -(x.get('mentions') or 0))
    w('- 被提及最多前 10：' + '、'.join('%s(%d)' % (i['zh'], i.get('mentions') or 0) for i in items[:10]))
    w('- 内容计划：%s' % s.get('docPlan', ''))
    w('')
for key, label in [('settlement.castle', '聚落·城堡'), ('settlement.hideout', '藏身处')]:
    s = T(key)
    w('### %s（%d）—— 无自述，**走 D（开发者原创）**' % (label, s['summary']['total']))
    w('')
    w('- 依据：%s' % s.get('basis', ''))
    w('- 无简介文，但**有 `owner`/`culture`/位置/被提及** —— 可写「归谁、为什么建在这、扼住哪条路」。')
    w('')
h = T('hero')
own = [i for i in h['items'] if i.get('ownText')]
w('### 英雄（%d 总数 → **仅 %d 个有描述文可做人物档**）' % (h['summary']['total'], len(own)))
w('')
w('| 英雄 id | 所属家族 | 描述开头 |')
w('|---|---|---|')
for i in own:
    head = (i.get('descHead') or '').replace('|', ' ')[:34]
    w('| `%s` | `%s` | %s |' % (i['id'], i.get('clan', ''), head))
w('')

w('## 三、建议梯队')
w('')
w('- **P0 人物 + 家族**：27 位有描述文英雄 + 其所属贵族家族。素材现成、玩家最常问、编年史最弱；且 `entity.hero.*`/`entity.clan.*` **天然绕开当前编译阻塞（C16）**。')
w('- **P1 城镇**：53 个城镇有官方描述文，地理 + 政治各 1~2 档。已有帕拉汶德样例可复用结构。')
w('- **P2 村庄**：273 个，量最大、结构同质，适合后置或批量。')
w('- **P1 王国 / 文化**：8 王国 + 6 主文化，各 1 档，作为顶层骨架。')
w('- **走 D 补**：城堡 67、藏身处 99、次要文化 17、小家族 15 / 匪帮 7（无自述，但有 owner/文化/位置/被提及可依）。')
w('- **Ⅱ 体系知识**（律法/商路/军制/宗教/史事）与 **Ⅲ 常识层** 不在本目录内，见 `WORLDBOOK-KNOWLEDGE-SYSTEM-CATALOG-20260912.md`。')
w('')

w('## 四、开放问题（待裁）')
w('')
w('1. 人物档该几条断言？旧档无依据（「一地点簇约 6 条」只对地点）。')
w('2. 全量目标规模？`PLAN-WORLDBOOK-CONTENT-STRUCTURE-ROADMAP` 明说「还没定」。')
w('3. 贵族家族无自述时，家族档怎么写才不像「撮合」？（料只能来自成员英雄的提及）')
w('4. 村庄 273 个是否全做，还是先做被提及最多的若干？')
w('5. 本目录只覆盖**游戏锚定实体**；现有 12 档是**语义实体（lore）**，两套要不要合流、怎么合？')
w('')

txt = '\n'.join(L).rstrip() + '\n'
open(OUT, 'w', encoding='utf-8', newline='').write(txt)
print('WROTE', OUT, len(txt), 'chars')
