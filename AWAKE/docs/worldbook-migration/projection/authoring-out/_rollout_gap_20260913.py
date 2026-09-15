# -*- coding: utf-8 -*-
"""铺开批缺口分析：DB 全量聚落 vs 现有地理档。"""
import sqlite3, re, glob, os, yaml, io

DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"
OUT = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out/"
con = sqlite3.connect(f"file:{DB}?mode=ro", uri=True)
con.row_factory = sqlite3.Row


def cn(name):
    tok = re.search(r"\{=(\w+)\}", name or "")
    if not tok:
        return None
    row = con.execute(
        "SELECT text FROM localization_entries WHERE stringId=? AND language='CNs' LIMIT 1",
        (tok.group(1),)).fetchone()
    return row["text"] if row else None


geo = {}
for f in glob.glob(OUT + "*.yaml"):
    b = os.path.basename(f)
    if b.startswith("_"):
        continue
    d = yaml.safe_load(io.open(f, encoding="utf-8"))
    if d.get("domain") == "geography":
        geo[b[:-5]] = d["title"]["zh-CN"]
print("现有地理档", len(geo), ":", sorted(geo.values()))

towns = con.execute(
    "SELECT settlementId,name,descriptionText FROM bannerlord_settlements WHERE settlementType='town' ORDER BY settlementId").fetchall()
with_desc = [t for t in towns if t["descriptionText"]]
print(f"\n城镇 {len(towns)}，有描述文 {len(with_desc)}")

# 已覆盖的官方中文名集合（含别名模糊对照：现有档 title 就是官方译名）
covered = set(geo.values())
missing = []
for t in with_desc:
    n = cn(t["name"]) or "?"
    if n not in covered:
        missing.append((t["settlementId"], n))
print(f"\n有描述文且未覆盖的城镇 {len(missing)}:")
for sid, n in missing:
    print("  ", sid, n)

# 村庄/城堡描述文覆盖
for typ in ("village", "castle"):
    rows = con.execute(
        f"SELECT settlementId,name,descriptionText FROM bannerlord_settlements WHERE settlementType='{typ}'").fetchall()
    wd = [r for r in rows if r["descriptionText"]]
    print(f"{typ}: {len(rows)}，有描述文 {len(wd)}")
