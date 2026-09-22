# -*- coding: utf-8 -*-
"""军事批 · 取数第四轮（只读，修正前一轮的两处口径错）：
  ① 物品/兵种的中文名要**从 `{=token}` 里解析 token 再查 CNs**（上一轮直接拿 name 比 stringId ⇒ 恒 0）；
  ② 兵种要**剔掉 `mp_`（联机）**，只看战役兵种树。
"""
import io
import re
import sqlite3
from collections import Counter

DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"
con = sqlite3.connect("file:" + DB + "?mode=ro", uri=True)
cur = con.cursor()


def tok(v):
    if not v:
        return None
    m = re.match(r"^\{=(.+)\}$", v.strip())
    return m.group(1) if m else v.strip()


CN = {}
for sid, txt in cur.execute("select stringId, text from localization_entries where language='CNs'"):
    CN.setdefault(sid, txt)
print("CNs 条目:", len(CN))

print()
print("=== 物品：按 filePath 看武器在哪 ===")
for k, c in cur.execute("""select filePath, count(*) from bannerlord_items
                           group by filePath order by 2 desc limit 25"""):
    print("   %-46s %d" % (k, c))

print()
print("=== 物品：中文名可得性（按 token 解析后）===")
tot = 0
have = 0
miss = []
for (nm,) in cur.execute("select name from bannerlord_items"):
    tot += 1
    t = tok(nm)
    if t and t in CN:
        have += 1
    elif len(miss) < 6:
        miss.append((nm, t))
print("   物品 %d/%d 有 CNs（%.1f%%）  样例未命中: %s" % (have, tot, 100.0 * have / tot, miss))

print()
print("=== 战役兵种（剔 mp_ / 英雄）：按 culture × level ===")
rows = list(cur.execute("""select characterId, name, level, culture, occupation, upgradeTargetsJson
                           from bannerlord_troops
                           where isHero=0 and characterId not like 'mp\\_%' escape '\\' """))
print("   战役非英雄兵种行数:", len(rows))
byc = Counter()
for cid, nm, lv, cu, oc, up in rows:
    byc[cu] += 1
for k, c in byc.most_common(20):
    print("   %-26s %d" % (k, c))

print()
print("=== 兵种树样本：vlandia 的 Soldier 系（按 characterId）===")
for cid, nm, lv, cu, oc, up in rows:
    if cu == "Culture.vlandia" and oc == "Soldier":
        t = tok(nm)
        print("   %-38s lv%-4s %-26s %s" % (cid, lv, CN.get(t, "?"), (up or "")[:80]))

print()
print("=== occupation 分布（战役非英雄）===")
print("   ", Counter(r[4] for r in rows).most_common(12))

print()
print("=== 有升级目标的兵种行数（能连成树的） ===")
withup = [r for r in rows if (r[5] or "") not in ("", "[]")]
print("   非空 upgradeTargetsJson:", len(withup), "/", len(rows))
for r in withup[:8]:
    print("   %-38s lv%-4s up=%s" % (r[0], r[2], (r[5] or "")[:110]))
con.close()
