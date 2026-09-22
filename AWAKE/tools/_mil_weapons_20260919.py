# -*- coding: utf-8 -*-
"""军事批 · 从**一手游戏 XML** 取装备清单（不再走 bannerlord_items 表 —— 它漏了 CraftedItem）。

产出 `docs/worldbook-migration/_mil_inventory2_20260919.json`：
  { "crafted": [...], "plain": [...], "counts": {...} }
中文名：从 `{=token}英文名` 取 token 去 localization_entries(CNs) 查。
"""
import io
import json
import re
import sqlite3
from collections import Counter

GAME = r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules"
F = GAME + "/SandBoxCore/ModuleData/items/weapons.xml"
OUT = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/_mil_inventory2_20260919.json"
DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"

txt = io.open(F, encoding="utf-8-sig").read()
print("文件字节:", len(txt))

# ---- CraftedItem（近战武器：刀剑枪斧锤）----
crafted = []
for m in re.finditer(r"<CraftedItem\b(.*?)</CraftedItem>", txt, re.S):
    blk = m.group(1)
    def attr(k):
        mm = re.search(k + r'="([^"]*)"', blk)
        return mm.group(1) if mm else ""
    pieces = re.findall(r"<Piece\s+id=\"([^\"]+)\"\s*\n?\s*Type=\"([^\"]+)\"", blk)
    if not pieces:
        pieces = [(a, b) for a, b in re.findall(r'id="([^"]+)"\s*\n?\s*Type="([^"]+)"', blk)]
    crafted.append({"id": attr("id"), "name": attr("name"),
                    "template": attr("crafting_template"), "culture": attr("culture"),
                    "modifier_group": attr("modifier_group"),
                    "pieces": [{"id": a, "type": b} for a, b in pieces]})

print()
print("=== CraftedItem（组装好的近战武器）===")
print("   件数:", len(crafted))
print("   crafting_template 分布:", Counter(c["template"] for c in crafted).most_common())
print("   culture 分布:", Counter(c["culture"] for c in crafted).most_common())
print("   配件数分布:", Counter(len(c["pieces"]) for c in crafted).most_common())

# ---- 普通 Item（弓弩箭矢投掷盾杂）----
plain = []
for m in re.finditer(r"<Item\b(.*?)</Item>", txt, re.S):
    blk = m.group(1)
    def attr(k):
        mm = re.search(k + r'="([^"]*)"', blk)
        return mm.group(1) if mm else ""
    plain.append({"id": attr("id"), "name": attr("name"), "type": attr("Type"),
                  "culture": attr("culture"), "value": attr("value"), "weight": attr("weight")})
print()
print("=== 普通 Item ===")
print("   件数:", len(plain))
print("   Type 分布:", Counter(p["type"] for p in plain).most_common())

# ---- 中文名 ----
con = sqlite3.connect("file:" + DB + "?mode=ro", uri=True)
cur = con.cursor()
CN = {}
for sid, t in cur.execute("select stringId, text from localization_entries where language='CNs'"):
    CN.setdefault(sid, t)
con.close()
TOK = re.compile(r"^\{=([^}]+)\}(.*)$")


def resolve(nm):
    m = TOK.match(nm or "")
    if m:
        return CN.get(m.group(1), ""), m.group(2)
    return "", nm or ""


cn_c = sum(1 for c in crafted if resolve(c["name"])[0])
cn_p = sum(1 for p in plain if resolve(p["name"])[0])
print()
print("中文名可得：CraftedItem %d/%d，普通 Item %d/%d" % (cn_c, len(crafted), cn_p, len(plain)))

print()
print("=== 近战武器中文名样例（前 30，按模板分组）===")
for c in crafted:
    cn, en = resolve(c["name"])
    if cn:
        print("   %-34s %-22s %-18s %s" % (c["id"], cn, c["template"], c["culture"].replace("Culture.", "")))

json.dump({"crafted": crafted, "plain": plain,
           "counts": {"crafted": len(crafted), "plain": len(plain)}},
          io.open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print()
print("已落盘:", OUT)
