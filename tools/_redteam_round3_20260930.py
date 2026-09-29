# -*- coding: utf-8 -*-
"""最终 redteam：复核 09-30 本轮所有新增硬读数（对照 docs 里的每个数字）"""
import json, os, sqlite3, sys, io
from collections import Counter
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

P = F = 0
def chk(label, got, want):
    global P, F
    ok = got == want
    if ok: P += 1
    else:  F += 1
    print(f"  {'PASS' if ok else 'FAIL'}  {label}")
    if not ok: print(f"        got={got}\n        want={want}")

V35 = r'D:\AWAKE-Dev\AWAKE\tools\worldbook-studio\workspace\full-geo1\compiled\geo1-v35-concepts-slim'
DB  = r'C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db'

# ===== A. v35 结构 =====
print('=== A. v35 产物 ===')
rt = json.load(open(os.path.join(V35,'runtime.json'), encoding='utf-8'))
docs = json.load(open(os.path.join(V35,'documents.json'), encoding='utf-8'))
val = json.load(open(os.path.join(V35,'validation.json'), encoding='utf-8'))
chk('runtime 条数 = 792', len(rt['entries']), 792)
chk('documents 条数 = 792', len(docs), 792)
dom = Counter(e.get('domain') for e in rt['entries'])
chk('geography 417', dom['geography'], 417)
chk('economy 151', dom['economy'], 151)
chk('politics 131', dom['politics'], 131)
chk('war 72', dom['war'], 72)
chk('culture 21', dom['culture'], 21)
sub = Counter((e.get('extensions') or {}).get('subdomain') for e in rt['entries'])
chk('subdomain 种数 = 43', len(sub), 43)
chk('settlements 397', sub['settlements'], 397)
chk('kingdoms 9', sub['kingdoms'], 9)
chk('law 13', sub['law'], 13)
chk('throne 2', sub['throne'], 2)
chk('succession 2', sub['succession'], 2)
chk('offices 3', sub['offices'], 3)
chk('diplomacy 3', sub['diplomacy'], 3)
chk('polity 不存在', 'polity' in sub, False)
poli_cnt = sum(v for k,v in sub.items() if k in ('throne','succession','offices','diplomacy','kingdoms','law'))
chk('政体相关 6 格合计 = 32', poli_cnt, 32)

# ===== B. validation =====
print('\n=== B. validation ===')
chk('Valid = True', val.get('Valid'), True)
d = val.get('Diagnostics') or []
chk('诊断总数 = 21', len(d), 21)
chk('全部 warning', set(x.get('Severity') for x in d), {'warning'})
chk('唯一 Code = WB-INDEX-AMBIGUOUS', set(x.get('Code') for x in d), {'WB-INDEX-AMBIGUOUS'})
chk('error 数 = 0', sum(1 for x in d if x.get('Severity')=='error'), 0)

# ===== C. 探针污染 =====
print('\n=== C. 探针污染 ===')
pr = [e['id'] for e in rt['entries'] if 'probe' in e['id'].lower()]
chk('探针条数 = 4', len(pr), 4)
chk('含 clan-probe', any('clan-probe' in x for x in pr), True)
chk('含 hero-probe', any('hero-probe' in x for x in pr), True)

# ===== D. 正文真身 =====
print('\n=== D. 正文真身 ===')
e0 = rt['entries'][0]
chk('runtime 首条无 assertions', 'assertions' in e0, False)
chk('runtime 首条有 summary', 'summary' in e0, True)
d0 = docs[0]
chk('documents 首条有 assertions', 'assertions' in d0, True)
chk('documents 首条有 subdomain(顶层)', 'subdomain' in d0, True)
ex0 = d0['assertions'][0]['expressions'][0]
chk('expression 有 text 字段', 'text' in ex0, True)
chk('text 是 dict', isinstance(ex0['text'], dict), True)
chk('text 含 zh-CN', 'zh-CN' in ex0['text'], True)

# ===== E. 三继承者政策（复核） =====
print('\n=== E. 三继承者开局政策 ===')
con = sqlite3.connect(DB); cur = con.cursor()
cur.execute("SELECT kingdomId, policyIdsJson FROM bannerlord_kingdoms WHERE culture LIKE '%empire%'")
kk = {r[0]: set(json.loads(r[1]) if r[1] else []) for r in cur.fetchall()}
chk('北 = feudal_inheritance', kk.get('empire'), {'policy_feudal_inheritance'})
chk('南 = royal_privilege', kk.get('empire_s'), {'policy_royal_privilege'})
chk('西 = land_grants_for_veterans', kk.get('empire_w'), {'policy_land_grants_for_veterans'})
con.close()

# ===== F. 六文化原型依据件里的政策 =====
print('\n=== F. 六文化默认政策 ===')
con = sqlite3.connect(DB); cur = con.cursor()
cur.execute("SELECT cultureId, defaultPolicyIdsJson FROM bannerlord_cultures")
cc = {r[0]: set(json.loads(r[1]) if r[1] else []) for r in cur.fetchall()}
chk('vlandia = feudal+casthcart', cc.get('vlandia'), {'policy_feudal_inheritance','policy_castle_charters'})
chk('khuzait = grazing+sacred', cc.get('khuzait'), {'policy_grazing_rights','policy_sacred_majesty'})
con.close()

print(f'\n===== FINAL REDTEAM: PASS={P}  FAIL={F} =====')
