import sqlite3, json, re, sys, os, datetime

sys.stdout.reconfigure(encoding='utf-8')

DB = r'C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db'
OUT_JSON = r'D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/WORLDBOOK-CONTENT-CATALOG-20260912.json'
OUT_TXT = r'D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/_catalog-dump.txt'

c = sqlite3.connect(f'file:{DB}?mode=ro', uri=True)

CN = dict(c.execute("SELECT stringId,text FROM localization_entries WHERE language='CNs' AND text IS NOT NULL"))
TEXTS = list(CN.values())

def cn(sid):
    return CN.get(sid) if sid else None

def cn_key(braced):
    m = re.search(r'\{=([^}]+)\}', braced or '')
    return CN.get(m.group(1)) if m else None

def mentions(name):
    if not name or len(name) < 2:
        return 0
    return sum(1 for t in TEXTS if name in t)

# ---------- 王国 ----------
kingdoms = []
for kid, name, cul, home in c.execute('SELECT kingdomId,name,culture,initialHomeSettlement FROM bannerlord_kingdoms'):
    zh = cn_key(name)
    kingdoms.append({'id': kid, 'zh': zh, 'entityId': f'entity.kingdom.{kid}',
                     'culture': (cul or '').replace('Culture.', ''),
                     'ownText': True, 'mentions': mentions(zh), 'home': home})

# ---------- 文化 ----------
cultures = []
for cid, name, main, dt in c.execute('SELECT cultureId,name,isMainCulture,descriptionText FROM bannerlord_cultures'):
    zh = cn_key(name)
    cultures.append({'id': cid, 'zh': zh, 'entityId': f'entity.culture.{cid}',
                     'isMain': bool(main), 'ownText': bool(dt), 'mentions': mentions(zh)})

# ---------- 家族 ----------
clans = []
for row in c.execute('SELECT clanId,name,culture,owner,isNoble,isMinorFaction,isBandit,isMercenary,tier,descriptionText FROM bannerlord_clans'):
    cid, name, cul, own, noble, minor, bandit, merc, tier, dt = row
    kind = 'noble' if noble else 'minor' if minor else 'bandit' if bandit else 'mercenary' if merc else 'other'
    zh = cn_key(name)
    clans.append({'id': cid, 'zh': zh, 'entityId': f'entity.clan.{cid}',
                  'kind': kind, 'tier': tier,
                  'culture': (cul or '').replace('Culture.', ''),
                  'ownText': bool(dt), 'mentions': mentions(zh)})

# ---------- 聚落 ----------
settl = []
for sid, name, stype, cul, own, dt in c.execute(
        'SELECT settlementId,name,settlementType,culture,owner,descriptionText FROM bannerlord_settlements'):
    zh = cn(f'Settlements.Settlement.name.{sid}') or cn_key(name)
    settl.append({'id': sid, 'zh': zh, 'entityId': f'entity.settlement.{sid.lower()}',
                  'type': stype, 'culture': (cul or '').replace('Culture.', ''),
                  'owner': (own or '').replace('Faction.', '') or None,
                  'ownText': bool(dt), 'mentions': mentions(zh)})

# ---------- 英雄 ----------
heroes = []
for hid, fac, txt in c.execute('SELECT heroId,faction,text FROM bannerlord_heroes'):
    kind = 'dead' if hid.startswith('dead_lord') else 'lord' if hid.startswith('lord') else 'other'
    key = re.search(r'\{=([^}]+)\}', txt or '')
    zh_desc = CN.get(key.group(1)) if key else None
    heroes.append({'id': hid, 'clan': (fac or '').replace('Faction.', '') or None,
                   'kind': kind, 'ownText': bool(txt),
                   'descHead': (zh_desc or '')[:40]})

def grp(items, key):
    out = {}
    for x in items:
        out.setdefault(x[key], []).append(x)
    return out

def summarize(items):
    return {'total': len(items),
            'ownText': sum(1 for x in items if x['ownText']),
            'hasMention': sum(1 for x in items if x['mentions'] > 0)}

sc = grp(settl, 'type')
cl = grp(clans, 'kind')

catalog = {
    'generatedAt': datetime.datetime.now().isoformat(timespec='seconds'),
    'source': 'BannerlordSage bannerlord.db（v1.3.15.110062 = 模组目标 API）',
    'materialRule': '【v1 口径，已修正】本目录只索引「Ⅰ 锚定对象」的素材可得性，不是准入门槛。'
                    '做法见 WORLDBOOK-KNOWLEDGE-SYSTEM-CATALOG-20260912.md：'
                    '驱动是「世界需要什么知识」，素材分 A 官方文本/游戏数据 · B 编年史 · C 模组提取 · D 开发者原创（填补空白，合法）。'
                    '「无描述文」不等于「无知识」——城堡/藏身处等仍有 owner/culture/位置/被提及可用。',
    'objectTypes': [
        {'type': 'kingdom', 'label': '王国', 'summary': summarize(kingdoms),
         'docPlan': '1 档 / 个（政治：王国概况 + 君主）', 'basis': '我提的，待裁', 'items': kingdoms},
        {'type': 'culture', 'label': '文化', 'summary': summarize(cultures),
         'docPlan': '仅主要文化（6）各 1 档', 'basis': '实测：只有 6 个有描述文', 'items': cultures},
        {'type': 'clan', 'label': '家族', 'summary': summarize(clans),
         'breakdown': {k: summarize(v) for k, v in cl.items()},
         'docPlan': 'noble 73 个中「被提及 ≥1」的先做；自身无描述文，料来自成员英雄',
         'basis': '实测：noble 自身 0 描述文', 'items': clans},
        {'type': 'settlement.town', 'label': '城镇', 'summary': summarize(sc.get('town', [])),
         'docPlan': '地理 1 档 + 政治 1~2 档', 'basis': '帕拉汶德样例（6 条 / 3 档）', 'items': sc.get('town', [])},
        {'type': 'settlement.village', 'label': '村庄', 'summary': summarize(sc.get('village', [])),
         'docPlan': '地理 1 档 / 个', 'basis': '我提的，待裁', 'items': sc.get('village', [])},
        {'type': 'settlement.castle', 'label': '城堡', 'summary': summarize(sc.get('castle', [])),
         'docPlan': '做（无简介文，但有 owner/culture/位置，可写归属与扼守意义）', 'basis': 'v1 误判为"不做"，已修正', 'items': sc.get('castle', [])},
        {'type': 'settlement.hideout', 'label': '藏身处', 'summary': summarize(sc.get('hideout', [])),
         'docPlan': '做（无简介文，被提及 154 次，可写匪患与藏身地理）', 'basis': 'v1 误判为"不做"，已修正', 'items': sc.get('hideout', [])},
        {'type': 'hero', 'label': '英雄', 'summary': {'total': len(heroes),
                                                     'ownText': sum(1 for x in heroes if x['ownText']),
                                                     'hasMention': None},
         'docPlan': '仅 27 个有描述文的可做人物档', 'basis': '实测', 'items': heroes},
    ],
    'existing': {
        'note': '现有 12 档写的是语义实体（lore），与游戏锚定实体目录不重叠',
        'loreAnchors': ['拉科尼斯湖', '沙拉斯湾', '卡恰尔半岛', '黎明山脉', '德里亚特'],
        'sample': 'doc.politics.caladog-accession（entity.hero.lord_5_1 + entity.clan.clan_battania_1）',
    },
}

with open(OUT_JSON, 'w', encoding='utf-8') as f:
    json.dump(catalog, f, ensure_ascii=False, indent=1)

lines = []
for o in catalog['objectTypes']:
    s = o['summary']
    lines.append('%-18s 总 %4d  自身有料 %4d  被提及>0 %s' % (
        o['label'], s['total'], s['ownText'], s['hasMention']))
lines.append('')
lines.append('=== 27 个有描述文的英雄 ===')
for h in heroes:
    if h['ownText']:
        lines.append('  %-16s %-26s %s' % (h['id'], h['clan'], h['descHead']))
lines.append('')
lines.append('=== 6 个有描述文的主要文化 ===')
lines.append('  ' + '、'.join(x['zh'] or x['id'] for x in cultures if x['ownText']))
lines.append('')
lines.append('=== noble 家族（73）：被提及次数降序前 15 ===')
nb = sorted([x for x in clans if x['kind'] == 'noble'], key=lambda x: -x['mentions'])
for x in nb[:15]:
    lines.append('  %-26s %-14s 被提及 %d' % (x['id'], x['zh'], x['mentions']))
lines.append('')
lines.append('=== 城镇 53：被提及次数降序前 12 ===')
for x in sorted(sc.get('town', []), key=lambda x: -x['mentions'])[:12]:
    lines.append('  %-14s %-14s 被提及 %d' % (x['id'], x['zh'], x['mentions']))

with open(OUT_TXT, 'w', encoding='utf-8') as f:
    f.write('\n'.join(lines))

print('JSON:', OUT_JSON, os.path.getsize(OUT_JSON), 'bytes')
print('DUMP:', OUT_TXT)
print()
print('\n'.join(lines))
