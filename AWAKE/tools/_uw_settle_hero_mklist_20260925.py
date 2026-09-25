# -*- coding: utf-8 -*-
"""合并本批名单：321 聚落 + 19 hero = 340 档（v31 批）。"""
import io, json, os, re, glob

ROOT = r"D:\AWAKE-Dev\AWAKE"
FULL = os.path.join(ROOT, "tools", "worldbook-studio", "workspace", "full-geo1")
AUTH = os.path.join(FULL, "authoring")

settle = json.load(io.open(os.path.join(ROOT, "tools", "_uw_settle_written_20260925.json"),
                           encoding="utf-8"))
# 本轮 hero 只动了这 19 档（keywords<=3 的非 probe 档）；其余档（八君主等）别名早就有、未改动。
# 判据与 _uw_hero_aliases_probe 一致：读 v30 包，keywords<=3 的 hero 档。
PKG = os.path.join(FULL, "compiled", "geo1-v30-settle-alias", "runtime.json")
ents = json.load(io.open(PKG, encoding="utf-8")).get("entries") or []
heroes = []
for x in ents:
    i = str(x.get("id", ""))
    if "hero-" not in i:
        continue
    if len(x.get("keywords") or []) > 3:
        continue
    slug = i.split("entry:")[-1].split(".", 1)[-1]
    if "probe" in slug:
        continue
    heroes.append(slug)

allst = sorted(set(settle) | set(heroes))
out = os.path.join(ROOT, "tools", "_uw_settle_hero_written_20260925.json")
io.open(out, "w", encoding="utf-8").write(json.dumps(allst, ensure_ascii=False, indent=1))
print("聚落 %d + hero %d = 合并 %d -> %s" % (len(settle), len(heroes), len(allst), out))
