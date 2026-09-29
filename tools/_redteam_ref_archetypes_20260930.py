# -*- coding: utf-8 -*-
"""redteam v2: 复核 REF-CULTURE-ARCHETYPES-20260930.md 的硬读数
正确 schema: bannerlord_cultures.cultureId / bannerlord_kingdoms.kingdomId / bannerlord_policies.policyId
"""
import sqlite3, json, os, sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

DB = r'C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db'
con = sqlite3.connect(DB); cur = con.cursor()
PASS = FAIL = 0

def chk(label, got, want):
    global PASS, FAIL
    ok = (got == want)
    if ok: PASS += 1
    else:  FAIL += 1
    print(f"  {'PASS' if ok else 'FAIL'}  {label}")
    if not ok:
        print(f"        got  = {got}")
        print(f"        want = {want}")

def jset(s):
    try: return set(json.loads(s)) if s else set()
    except: return set()

# ---------- 1. 六文化默认政策 ----------
print('=== 1. 六文化 defaultPolicyIdsJson ===')
want_cult = {
    'empire':   {'policy_senate'},
    'aserai':   {'policy_council_of_the_commons','policy_senate'},
    'sturgia':  {'policy_lawspeakers','policy_precarial_land_tenure'},
    'vlandia':  {'policy_feudal_inheritance','policy_castle_charters'},
    'battania': {'policy_cantons','policy_lawspeakers'},
    'khuzait':  {'policy_grazing_rights','policy_sacred_majesty'},
}
cur.execute("SELECT cultureId, defaultPolicyIdsJson FROM bannerlord_cultures")
got_cult = {r[0]: jset(r[1]) for r in cur.fetchall()}
for k, v in want_cult.items():
    chk(f'culture {k}', got_cult.get(k), v)

# ---------- 2. 三帝国开局政策 ----------
print('\n=== 2. 三帝国王国层 policyIdsJson ===')
want_king = {
    'empire':   {'policy_feudal_inheritance'},
    'empire_s': {'policy_royal_privilege'},
    'empire_w': {'policy_land_grants_for_veterans'},
}
cur.execute("SELECT kingdomId, culture, policyIdsJson FROM bannerlord_kingdoms")
got_king = {r[0]: (r[1], jset(r[2])) for r in cur.fetchall()}
for k, v in want_king.items():
    chk(f'kingdom {k}', got_king.get(k, (None,None))[1], v)
print('  -- 全部 8 王国实况 --')
for k, (c, p) in sorted(got_king.items()):
    print(f'    {k:16s} culture={str(c):10s} {sorted(p)}')

# ---------- 3. 17 条政策支持度 ----------
print('\n=== 3. 政策三方支持度 ===')
want_sup = {
    'policy_senate':                    (-0.7,   0.85,  0.7),
    'policy_tribunes_of_the_people':    (-0.6,  -0.2,   0.55),
    'policy_royal_privilege':           ( 0.8,  -0.15, -0.75),
    'policy_sacred_majesty':            ( 0.85,  0.1,  -0.9),
    'policy_lords_privy_council':       (-0.5,   0.7,  -0.15),
    'policy_marshals':                  (-0.45,  0.5,   0.0),
    'policy_feudal_inheritance':        (-0.75,  0.75,  0.65),
    'policy_precarial_land_tenure':     ( 0.75,  0.0,  -0.6),
    'policy_cantons':                   (-0.2,  -0.1,   0.4),
    'policy_lawspeakers':               ( 0.0,   0.25,  0.45),
    'policy_grazing_rights':            (-0.75, -0.3,   0.7),
    'policy_council_of_the_commons':    (-0.5,   0.1,   0.7),
    'policy_land_grants_for_veterans':  (-0.35, -0.15,  0.5),
    'policy_serfdom':                   (-0.4,   0.5,  -0.25),
    'policy_forgiveness_of_debts':      (-0.4,  -0.4,   0.6),
    'policy_citizenship':               (-0.65, -0.35,  0.7),
}
cur.execute("SELECT policyId, rulerSupport, lordsSupport, commonsSupport FROM bannerlord_policies")
got_sup = {r[0]: (r[1], r[2], r[3]) for r in cur.fetchall()}
print(f'  政策总数 = {len(got_sup)}')
for k, v in want_sup.items():
    g = got_sup.get(k)
    gg = tuple(round(float(x),2) for x in g) if g else None
    chk(f'{k}', gg, v)

# ---------- 4. 统治者称谓 ----------
print('\n=== 4. 八王国统治者称谓 ===')
cur.execute("SELECT kingdomId, name, rulerTitle, title FROM bannerlord_kingdoms")
for r in sorted(cur.fetchall()):
    print(f'    {r[0]:16s} name={r[1]}  rulerTitle={r[2]}  title={r[3]}')

con.close()
print(f'\n===== REDTEAM: PASS={PASS}  FAIL={FAIL} =====')
