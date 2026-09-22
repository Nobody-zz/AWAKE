# -*- coding: utf-8 -*-
"""军事装备与兵种批 · 取数落盘（只读 DB）。

产出 `docs/worldbook-migration/_mil_inventory_20260919.json`：
  { "weapons": [...], "troops": {...}, "trees": {...} }

口径（前一轮踩过的两个坑都修了）：
  · 中文名必须从 `{=token}英文名` 里取 token 再查 CNs（直接拿 name 比 stringId 恒 0）。
  · 兵种剔掉 `mp_`（联机）与英雄；只看战役兵种。
"""
import io
import json
import re
import sqlite3
from collections import Counter, defaultdict

REPO = r"D:\AWAKE-Dev\AWAKE"
OUT = REPO + r"\docs\worldbook-migration\_mil_inventory_20260919.json"
DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"
con = sqlite3.connect("file:" + DB + "?mode=ro", uri=True)
cur = con.cursor()

TOK = re.compile(r"^\{=([^}]+)\}(.*)$")


def parse(v):
    """返回 (token, 英文名)。"""
    if not v:
        return None, ""
    m = TOK.match(v.strip())
    if m:
        return m.group(1), m.group(2).strip()
    return None, v.strip()


CN = {}
for sid, txt in cur.execute("select stringId, text from localization_entries where language='CNs'"):
    CN.setdefault(sid, txt)

# ---------------- 武器 ----------------
print("=== 战役武器（items/weapons.xml + tournament_weapons.xml）===")
weapons = []
for row in cur.execute("""select entityId, name, itemType, tier, weaponLength, swingDamage,
                                 swingDamageType, thrustDamage, thrustDamageType, speedRating,
                                 balanceOrHitPoints, filePath
                          from bannerlord_items
                          where filePath like '%items/weapons.xml%'
                             or filePath like '%tournament_weapons.xml%'"""):
    (eid, nm, it, tier, wl, sd, sdt, td, tdt, sp, bh, fp) = row
    t, en = parse(nm)
    weapons.append({"entityId": eid, "cn": CN.get(t, ""), "en": en, "token": t,
                    "itemType": it, "tier": tier, "weaponLength": wl,
                    "swingDamage": sd, "swingDamageType": sdt,
                    "thrustDamage": td, "thrustDamageType": tdt,
                    "speedRating": sp, "balanceOrHitPoints": bh, "filePath": fp})
named = [w for w in weapons if w["cn"]]
print("   武器件数:", len(weapons), " 有中文名:", len(named))
print("   类别分布:", Counter(w["itemType"] for w in weapons).most_common())
print("   tier 分布:", Counter(w["tier"] for w in weapons).most_common())
print("   中文名样例:")
for w in weapons[:10]:
    print("      %-30s %-22s %-16s tier=%s" % (w["entityId"], w["cn"], w["itemType"], w["tier"]))

# ---------------- 兵种 ----------------
print()
print("=== 战役兵种（非英雄、非 mp_）===")
rows = list(cur.execute("""select characterId, name, level, culture, occupation,
                                  skillTemplate, upgradeTargetsJson, filePath
                           from bannerlord_troops
                           where isHero=0 and characterId not like 'mp\\_%' escape '\\'"""))
troops = {}
for cid, nm, lv, cu, oc, st, up, fp in rows:
    t, en = parse(nm)
    troops[cid] = {"characterId": cid, "cn": CN.get(t, ""), "en": en, "token": t,
                   "level": lv, "culture": (cu or "").replace("Culture.", ""),
                   "occupation": oc, "skillTemplate": st,
                   "upgradeTargets": [x.replace("NPCCharacter.", "") for x in json.loads(up or "[]")],
                   "filePath": fp}
havecn = sum(1 for v in troops.values() if v["cn"])
print("   兵种行数:", len(troops), " 有中文名:", havecn)
print("   文化分布:", Counter(v["culture"] for v in troops.values()).most_common(12))

# 兵种树：从六文化的招募兵出发
CULTS = ["empire", "vlandia", "khuzait", "aserai", "sturgia", "battania"]
trees = {}
for cu in CULTS:
    roots = [cid for cid, v in troops.items()
             if v["culture"] == cu and v["level"] == "6"]
    seen = {}
    stack = list(roots)
    while stack:
        cid = stack.pop()
        if cid in seen or cid not in troops:
            continue
        seen[cid] = troops[cid]
        for nxt in troops[cid]["upgradeTargets"]:
            if nxt not in seen:
                stack.append(nxt)
    trees[cu] = sorted(seen.values(), key=lambda v: (v["level"] or "", v["characterId"]))
print()
print("   各文化兵种树（从 level 6 的招募兵展开）:")
for cu in CULTS:
    ls = trees[cu]
    print("      %-9s %2d 个兵种  层级: %s" % (cu, len(ls), sorted({x["level"] for x in ls})))
    for x in ls:
        print("         lv%-3s %-28s %-16s -> %s"
              % (x["level"], x["characterId"], x["cn"], "/".join(x["upgradeTargets"])[:60]))

json.dump({"generated": "2026-09-19", "weapons": weapons, "troops": troops, "trees": trees},
          io.open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print()
print("已落盘:", OUT)
con.close()
