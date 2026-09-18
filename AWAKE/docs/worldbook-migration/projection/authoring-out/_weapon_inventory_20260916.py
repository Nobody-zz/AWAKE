# -*- coding: utf-8 -*-
"""武器/护甲批 · 盘点（只读 DB）。

从 bannerlord.db 取 entityKind='Item' 的**成品**（排除 CraftingPiece 锻造零件），
按**物理字段**判类（itemType 对武器基本为空，只有 8 件标了 OneHandedWeapon/Polearm），
并从 localization_entries(CNs) 把 {=token} 解成简体中文名。

产出 _weapon_inventory_20260916.json
"""
import collections
import io
import json
import os
import re
import sqlite3

DB = r'C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db'
OUT = r'D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out/_weapon_inventory_20260916.json'

con = sqlite3.connect('file:%s?mode=ro' % DB, uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()

# ---- 1) CNs 名表 ----
cn = {}
cur.execute("SELECT stringId, text FROM localization_entries WHERE language='CNs'")
for r in cur.fetchall():
    cn[r['stringId']] = r['text']
print('CNs entries:', len(cn))

# ---- 2) 成品物品 ----
cur.execute("""
    SELECT entityId, name, itemType, weight, value, tier, pieceType,
           weaponLength, swingDamage, swingDamageType,
           thrustDamage, thrustDamageType, speedRating,
           headArmor, bodyArmor, legArmor, armArmor,
           horseChargeDamage, horseSpeed, horseManeuver, balanceOrHitPoints
    FROM bannerlord_items WHERE entityKind='Item'
""")
rows = cur.fetchall()
print('Item rows:', len(rows))


def parse_name(name):
    if not name:
        return None
    m = re.match(r'^\{=([^}]+)\}(.*)$', name)
    if not m:
        return None
    sid, en = m.group(1), m.group(2).strip()
    return en, cn.get(sid)


def classify(r):
    t = r['itemType'] or ''
    if r['horseSpeed'] or t == 'Horse':
        return 'horse'
    if t == 'Shield':
        return 'shield'
    if r['headArmor']:
        return 'head'
    if r['bodyArmor']:
        return 'body'
    if r['legArmor']:
        return 'leg'
    if r['armArmor']:
        return 'arm'
    if t == 'Cape':
        return 'cape'
    if r['swingDamage'] or r['thrustDamage']:
        return 'weapon'
    if t in ('Bow', 'Crossbow', 'Arrows', 'Bolts', 'Thrown', 'SlingStones'):
        return 'ranged'
    if t == 'Banner':
        return 'banner'
    if t == 'Goods':
        return 'goods'
    return 'other'


inv = []
for r in rows:
    parsed = parse_name(r['name'])
    if not parsed:
        continue
    en, zh = parsed
    inv.append({
        'entityId': r['entityId'],
        'en': en,
        'zh': zh,
        'category': classify(r),
        'itemType': r['itemType'],
        'tier': r['tier'],
        'value': r['value'],
        'weight': r['weight'],
        'swing': r['swingDamage'],
        'thrust': r['thrustDamage'],
        'head': r['headArmor'],
        'body': r['bodyArmor'],
        'leg': r['legArmor'],
        'arm': r['armArmor'],
    })

cnt = collections.Counter(x['category'] for x in inv)
print('--- category counts ---')
for k, v in cnt.most_common():
    print('  %-10s %d' % (k, v))
print('with CN name: %d / %d' % (sum(1 for x in inv if x['zh']), len(inv)))
print('tier dist:', dict(collections.Counter(str(x['tier']) for x in inv).most_common(14)))

# 具名度：entityId 里不含 _b/_c 这类变体后缀的「主件」
named = [x for x in inv if x['category'] in ('weapon', 'head', 'body', 'leg', 'arm', 'shield', 'ranged', 'horse')]
print('gear (weapon+armor+shield+ranged+horse):', len(named))

os.makedirs(os.path.dirname(OUT), exist_ok=True)
with io.open(OUT, 'w', encoding='utf-8') as fh:
    json.dump(inv, fh, ensure_ascii=False, indent=1)
print('written:', OUT, len(inv))
