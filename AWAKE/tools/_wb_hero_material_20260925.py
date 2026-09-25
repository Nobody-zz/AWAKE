# -*- coding: utf-8 -*-
"""hero 素材盘点（09-25）：有多少 hero 有官方背景文、有多少能靠家族/王国/聚落间接成文。
只读索引。"""
import sqlite3
import json
import io
import os

DB = r"C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db"
REG = r"D:\AWAKE-Dev\AWAKE\docs\mappings\persona-entity\generations\b1-7590085e3662512d7c1c6646c529694bda9850146234827b2dafbd9f20c6a2b3\entity-registry.v1.json"

con = sqlite3.connect("file:%s?mode=ro" % DB, uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()

reg = json.load(io.open(REG, encoding="utf-8"))
heroes = [e for e in reg["entities"] if e["kind"] == "hero"]
print("registry hero:", len(heroes))

# 游戏侧：有 text 的 heroId
cur.execute("SELECT heroId, text FROM bannerlord_heroes WHERE text IS NOT NULL AND text<>''")
withtext = {}
for r in cur.fetchall():
    withtext[r["heroId"]] = r["text"]
# locale key -> 中文
def cn_of(txt):
    m = None
    import re
    mm = re.match(r"^\{=([^}]+)\}(.*)$", txt, re.S)
    if not mm:
        return None
    key = mm.group(1)
    cur.execute("SELECT text FROM localization_entries WHERE language='CNs' AND stringId=? LIMIT 1", (key,))
    r = cur.fetchone()
    return r["text"] if r else None

print("游戏侧有背景文 hero:", len(withtext))
print()

hit = 0
miss_samples = []
for h in heroes:
    code = h.get("hero_code")
    if code in withtext:
        hit += 1
    else:
        if len(miss_samples) < 8:
            miss_samples.append((code, h.get("display_name_zh"), h.get("family_name_zh"),
                                 (h.get("related_codes") or {}).get("kingdom")))
print("registry hero 中游戏侧有背景文:", hit, "/", len(heroes))
print()
print("=== 无背景文的样例（看能挂什么间接素材）===")
for c, n, fam, kd in miss_samples:
    print("  %-22s %-16s clan=%-22s kingdom=%s" % (c, n, fam, kd))

print()
print("=== 有背景文的 27 条：出中文 ===")
n_cn = 0
for code, txt in list(withtext.items()):
    cn = cn_of(txt)
    if cn:
        n_cn += 1
        if n_cn <= 30:
            print("  [%s] %s" % (code, cn[:200]))
print("  有中文的:", n_cn, "/", len(withtext))

print()
print("=== 各王国 hero 数（registry）===")
import collections
c = collections.Counter()
for h in heroes:
    c[(h.get("related_codes") or {}).get("kingdom", "?")] += 1
for k, v in c.most_common():
    print("  %-16s %d" % (k, v))

print()
print("=== 各大 clan 的成员数（registry，含 hero）===")
clans = [e for e in reg["entities"] if e["kind"] == "clan"]
cc = collections.Counter()
for cl in clans:
    cc[cl["clan_code"]] = len(cl.get("member_entity_ids") or [])
for k, v in sorted(cc.items()):
    print("  %-28s %d" % (k, v))

con.close()
