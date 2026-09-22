# -*- coding: utf-8 -*-
"""军事装备与兵种批 · 开工前盘点（只读）。

三件：
 ① 分类体系里「战争」域的二级主题有哪些、哪些已被用（对着 482 档数）；
 ② 游戏库（BannerlordSage SQLite，只读）里兵种/物品相关的表与规模；
 ③ 现有 war 域 与 economy.items 档的覆盖，指出缺口。
"""
import io
import json
import os
import sqlite3

import yaml

REPO = r"D:\AWAKE-Dev\AWAKE"
MIRR = os.path.join(REPO, r"docs\worldbook-migration\projection\authoring-out")
TAX = os.path.join(REPO, r"docs\worldbook-studio-plan\knowledge-taxonomy.v1.json")
DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"


def z(v):
    if isinstance(v, dict):
        return v.get("zh-CN") or v.get("en") or ""
    return v or ""


# ---- 现有档的 domain/subdomain 分布 ----
used = {}
for p in sorted(os.listdir(MIRR)):
    if not p.endswith(".yaml") or p.startswith(("_", "source-")):
        continue
    d = yaml.safe_load(io.open(os.path.join(MIRR, p), encoding="utf-8"))
    key = (d.get("domain"), d.get("subdomain"))
    used.setdefault(key, []).append(p)

print("=" * 78)
print("① 分类体系 × 已用情况")
print("=" * 78)
t = json.load(io.open(TAX, encoding="utf-8"))
for dom in t["domains"]:
    did = dom["id"]
    subs = dom.get("subdomains") or []
    hit = sum(len(v) for k, v in used.items() if k[0] == did)
    print("== %-9s %-6s  已用档 %-4d 二级主题 %d 个" % (did, z(dom.get("label")), hit, len(subs)))
    for s in subs:
        k = (did, s["id"])
        n = len(used.get(k) or [])
        mark = "已用" if n else "未用"
        print("     - %-16s %-8s [%s %-3s] %s" % (s["id"], z(s.get("label")), mark, n or "", z(s.get("help"))[:38]))

print()
print("=" * 78)
print("② 游戏库（只读）里的相关表")
print("=" * 78)
con = sqlite3.connect("file:" + DB + "?mode=ro", uri=True)
cur = con.cursor()
tabs = [r[0] for r in cur.execute(
    "select name from sqlite_master where type='table' order by name")]
print("总表数:", len(tabs))
kw = ("troop", "item", "weapon", "armor", "equip", "skill", "perk", "culture", "clan", "hero")
for name in tabs:
    low = name.lower()
    if any(k in low for k in kw):
        try:
            n = cur.execute("select count(*) from [%s]" % name).fetchone()[0]
        except Exception as e:
            n = "ERR " + str(e)[:30]
        print("   %-42s %s" % (name, n))

print()
print("--- 兵种相关表的列 ---")
for name in tabs:
    if "troop" in name.lower() or "unit" in name.lower():
        cols = [r[1] for r in cur.execute("pragma table_info([%s])" % name)]
        print("   %s: %s" % (name, cols))

print()
print("=" * 78)
print("③ 现有 war 域档（含 economy.items 里像装备的）")
print("=" * 78)
by = {}
for (dom, sub), files in sorted(used.items()):
    if dom in ("war",) or (dom == "economy" and sub == "items"):
        by[(dom, sub)] = files
for k, files in by.items():
    print("== %s.%s  共 %d 档" % (k[0], k[1], len(files)))
    for f in files:
        print("     ", f)
con.close()
