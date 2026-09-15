# -*- coding: utf-8 -*-
"""编年史第三步：全量 337 条归轴台账生成器（09-12）
分类依据全部落 class_basis，可人工复核；不做任何内容私断。
数据源：
  R = 编年史 rules（B 级）
  REG = persona-entity 官方实体登记表（890 实体，A 级锚点判定用）
  DB = bannerlord.db 官方本地化（A 级交叉核对）
"""
import json, glob, os, re, sqlite3, io

BASE = r'D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/AnimusForge/PlayerExports/卡拉迪亚编年史/knowledge/rules'
REGF = sorted(glob.glob(r'D:/AWAKE-Dev/AWAKE/docs/mappings/persona-entity/generations/*/entity-registry.v1.json'))[-1]
OUT_JSON = r'D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/_chronicle_ledger_20260912.json'

# ---------- 载入官方实体登记表 ----------
reg = json.load(open(REGF, encoding='utf-8'))
ents = reg.get('entities') or reg
if isinstance(ents, dict):
    ents = list(ents.values())
# 官方名 -> entity 行（显示名精确表）
name2ent = {}
for e in ents:
    dn = e.get('display_name_zh') or ''
    if dn:
        name2ent.setdefault(dn, e)
        # 别名若有也挂
        for al in (e.get('aliases') or []):
            if al and al not in name2ent:
                name2ent[al] = e

# ---------- 官方本地化 CN 文本集合（A 级交叉核对用） ----------
db = sqlite3.connect(r'file:C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db?mode=ro', uri=True)
cur = db.cursor()
cn_texts = set()
for (t,) in cur.execute("SELECT text FROM localization_entries WHERE language='CNs'").fetchall():
    if t: cn_texts.add(t.strip())
db.close()

def official_hit(name, keywords):
    """A 级交叉核对：官方实体登记表精确命中 > 官方 CN 文本命中 > 无。返回 (级别, 依据)。"""
    cands = [name] + list(keywords or [])
    # 1) 登记表精确命中（最强：官方实体）
    for c in cands:
        c = (c or '').strip()
        if len(c) >= 2 and c in name2ent:
            e = name2ent[c]
            return ('Ⅰ锚定', 'registry:' + e['entity_id'])
    # 2) 官方 CN 本地化文本中作为子串出现（弱：仅说明词在官方文本里出现过）
    for c in cands:
        c = (c or '').strip()
        if len(c) >= 2:
            hits = sum(1 for t in cn_texts if c in t)
            if hits:
                return ('A文本提及', 'localization:出现于%d条官方CN文本' % hits)
    return ('无官方锚', '-')

# ---------- 分类词典（启发式，全部落依据） ----------
PLACE = re.compile(r'湾|湖|海|河|江|山|峰|岛|半岛|平原|草原|森林|林|谷|滩|崖|城|镇|村|堡|要塞|关隘|渡口|港|荒野|雪原|平原|荒漠|沙漠|绿洲|中原|边|疆|高原|盆地')
EVENT = re.compile(r'战役|战争|之战|叛乱|起义|独立|遇刺|刺杀|围攻|征服|建国|王朝|革命|政变|条约|盟|事变|分裂|统一|流放|远征|入侵|劫|夺|屠')
FAMILY = re.compile(r'家族|氏族|部族|部落|王朝|世家|巴努|族裔| clan |家系')
GOODS  = re.compile(r'马种|马|酒|羊毛|铁|银|丝|盐|毛皮|武器|盔甲|船|骆驼|货物|商|工艺|银器')
FAITH  = re.compile(r'女神|男神|神明|信仰|祭祀|祭坛|先知|祈祷|祭司|神庙|圣|蒙$|教派')
LEGACY = re.compile(r'旧作|传承|骑砍|卡拉迪亚传奇')

def domain_of(name, text):
    """五域启发式：geography/politics/war/economy/culture"""
    if re.search(r'战役|战争|之战|叛乱|围攻|征服|远征|入侵|屠|伏击|军团|驻军|雇佣兵|军事', name+text): return 'war'
    if PLACE.search(name): return 'geography'
    if re.search(r'家族|王朝|苏丹|国王|皇帝|元老院|政治|王位|继承|宣称|统治|封|税|法律', name+text): return 'politics'
    if re.search(r'贸易|商|货物|银|金币|市场|物价|经济|作坊|羊毛|丝|马种|物产', name+text): return 'economy'
    return 'culture'

# ---------- 主循环 ----------
rows = []
seen_ids = {}
for f in sorted(glob.glob(os.path.join(BASE, '*.json'))):
    d = json.load(open(f, encoding='utf-8-sig'))
    rid = d.get('Id') or os.path.basename(f)[:-5]
    seen_ids[rid] = seen_ids.get(rid, 0) + 1
    name = rid.replace('rule_', '')
    kws = d.get('Keywords') or []
    rags = d.get('RagShortTexts') or []
    variants = d.get('Variants') or []
    has_tm = bool(d.get('TextMappings'))
    # When 维度摘要
    when_dims = {}
    for v in variants:
        w = v.get('When') or {}
        for k in ['Cultures','KingdomIds','SettlementIds','IdentityIds','Roles','HeroIds']:
            if w.get(k): when_dims[k] = when_dims.get(k, 0) + len(w[k])
    full_text = ' '.join([v.get('Content','') for v in variants if isinstance(v.get('Content'), str)])[:4000]
    # 类型
    if FAITH.search(name): kind = '信仰（红线单列）'
    elif LEGACY.search(name) or '马种' in name or re.match(r'^rule_.*马$', name): kind = '马种/旧作传承词（单列）'
    elif FAMILY.search(name): kind = '家族/部族'
    elif EVENT.search(name): kind = '事件'
    elif PLACE.search(name): kind = '地点'
    elif GOODS.search(name): kind = '物产/经济'
    elif re.search(r'战役|战争|之战|叛乱|围攻|征服|远征|入侵|独立|遇刺|伏击', full_text[:500]): kind = '事件候选（按正文）'
    else: kind = '人物/概念'
    dom = domain_of(name, full_text)
    lvl, basis = official_hit(name, kws)
    rows.append({
        'rule_id': rid, 'name': name, 'kind_guess': kind, 'domain': dom,
        'form_hint': lvl, 'anchor_basis': basis,
        'keywords': kws, 'n_rag': len(rags), 'n_variants': len(variants),
        'when_dims': when_dims, 'has_textmapping': has_tm,
        'rag_head': (rags[0][:80] if rags else ''),
    })

# 重复 Id
dups = {k: v for k, v in seen_ids.items() if v > 1}
for r in rows:
    if seen_ids[r['rule_id']] > 1:
        r['dup_id'] = True

json.dump(rows, io.open(OUT_JSON, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)

# ---------- 统计 ----------
def cnt(key):
    out = {}
    for r in rows: out[r[key]] = out.get(r[key], 0) + 1
    return dict(sorted(out.items(), key=lambda x: -x[1]))

print('rows:', len(rows))
print('dup_ids:', dups)
print('kind:', cnt('kind_guess'))
print('domain:', cnt('domain'))
print('form:', cnt('form_hint'))
print('with_when:', sum(1 for r in rows if r['when_dims']))
print('with_textmapping:', sum(1 for r in rows if r['has_textmapping']))
