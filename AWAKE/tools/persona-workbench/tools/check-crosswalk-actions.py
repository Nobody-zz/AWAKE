# -*- coding: utf-8 -*-
"""镜像 PersonaAuthoringV2Adapter.Build 的动作查表逻辑，预检所有会阻断编译的取值。
口径严格对照 PersonaAuthoringV2.cs:
  - tag:            tags.Contains(sourceId[5:]) -> bool -> ToActionKey -> 'true'/'false'
  - facet_strength: facetStrengths[sourceId[15:]] -> int
  - axis:           ReadPropertyPath(document, sourceId)  例 'traitProfile.caution'
  - legacy_field:   ReadPropertyPath -> string
  TryGetAction: encodings[row.valueEncoding].actions[ToActionKey(raw)]
  HasSourceValue: tag -> bool true; legacy_field -> 非空串; 其他 -> value is not None
"""
import json, glob, os, collections

ROOT = r'D:\AWAKE-Dev\AWAKE'
CW = json.load(open(os.path.join(ROOT, 'docs/persona-contract/persona-workbench-to-awake.crosswalk.v1.json'), encoding='utf-8'))
REG = json.load(open(os.path.join(ROOT, 'ModuleData/Worldbook/persona_definitions/tag_registry.json'), encoding='utf-8'))
TAGS = set(t['id'] for t in REG['tags'])

enc_map = {}
for e in CW['valueEncodings']:
    enc_map[e['id']] = {k: v for k, v in (e.get('actions') or {}).items()}

rows = CW['rows']

def upper_first(s):
    return s[:1].upper() + s[1:] if s else s

def read_path(doc, path):
    cur = doc
    for seg in path.split('.'):
        if cur is None or not isinstance(cur, dict):
            return None
        cur = cur.get(upper_first(seg))
    return cur

def raw_value(doc, row):
    sk = row['sourceKind']
    sid = row['sourceId']
    if sk == 'tag':
        return sid[5:] in (doc.get('tags') or [])
    if sk == 'facet_strength':
        fs = doc.get('facetStrengths') or {}
        return fs.get(sid[15:])
    return read_path(doc, sid)

def has_value(sk, v):
    if sk == 'tag':
        return v is True
    if sk == 'legacy_field':
        return isinstance(v, str) and len(v) > 0
    return v is not None

def action_key(v):
    if isinstance(v, bool):
        return 'true' if v else 'false'
    return str(v)

blockers = collections.defaultdict(list)
checked = 0
files = sorted(glob.glob(os.path.join(ROOT, 'tools/persona-workbench/characters', '*.persona.json')))
for f in files:
    name = os.path.basename(f).split('_')[0]
    doc = json.load(open(f, encoding='utf-8'))
    for row in rows:
        sk = row['sourceKind']
        if sk not in ('tag', 'facet_strength', 'axis', 'legacy_field'):
            continue
        v = raw_value(doc, row)
        if not has_value(sk, v):
            continue
        checked += 1
        acts = enc_map.get(row['valueEncoding'])
        if acts is None:
            blockers['ENCODING_MISSING'].append((name, row['sourceId'], row['valueEncoding']))
            continue
        if action_key(v) not in acts:
            blockers['NO_ACTION'].append((name, row['sourceId'], sk, repr(v), row['valueEncoding'], sorted(acts.keys())))

print('卡片数:', len(files), '  动作查表次数:', checked)
print()
for k, v in blockers.items():
    print('###', k, len(v))
    agg = collections.Counter((x[1], x[4] if len(x) > 4 else '') for x in v)
    for (sid, enc), n in sorted(agg.items()):
        print('   %-40s %-22s x%d' % (sid, enc, n))
    print('   受影响的卡:', sorted(set(x[0] for x in v)))
    print()

# 另：域声明与动作键的一致性
print('### 编码 domain vs actions 键不一致（仅提示）')
for e in CW['valueEncodings']:
    dom = e.get('domain')
    if isinstance(dom, list):
        dom_s = set(str(x) for x in dom)
        act_s = set((e.get('actions') or {}).keys())
        if dom_s != act_s:
            print('   %-28s domain=%s actions=%s' % (e['id'], sorted(dom_s), sorted(act_s)))
