# -*- coding: utf-8 -*-
"""从 bannerlord.db 取写「村庄/城堡/城镇」概念词条要用的**一手素材**。

只读（mode=ro）。
"""
import io
import json
import sqlite3
import sys
import hashlib
import collections

sys.stdout.reconfigure(encoding="utf-8")

DB = r"C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db"
con = sqlite3.connect(f"file:{DB}?mode=ro", uri=True)
con.row_factory = sqlite3.Row

print("=" * 78)
print("① 聚落按类型计数（这 494 座就是卡拉迪亚的全部聚落）")
for r in con.execute("SELECT settlementType, COUNT(*) n FROM bannerlord_settlements GROUP BY settlementType ORDER BY n DESC"):
    print("   %-10s %d" % (r["settlementType"], r["n"]))

print("\n" + "=" * 78)
print("② 村庄的「依附」关系：boundSettlement 是什么类型")
q = """
SELECT b.settlementType AS boundType, COUNT(*) n
FROM bannerlord_settlements a
JOIN bannerlord_settlements b ON b.settlementId = a.boundSettlement
WHERE a.settlementType='village'
GROUP BY b.settlementType
"""
for r in con.execute(q):
    print("   %d 个村庄依附于 %s" % (r["n"], r["boundType"]))

print("\n   没有 boundSettlement 的村庄数：%d" % con.execute(
    "SELECT COUNT(*) FROM bannerlord_settlements WHERE settlementType='village' AND (boundSettlement IS NULL OR boundSettlement='')").fetchone()[0])
print("   城堡/城镇有 boundSettlement 的：%d / %d" % (
    con.execute("SELECT COUNT(*) FROM bannerlord_settlements WHERE settlementType='castle' AND boundSettlement IS NOT NULL AND boundSettlement<>''").fetchone()[0],
    con.execute("SELECT COUNT(*) FROM bannerlord_settlements WHERE settlementType='town' AND boundSettlement IS NOT NULL AND boundSettlement<>''").fetchone()[0]))

print("\n" + "=" * 78)
print("③ 村庄官方描述文里，有没有说「供给/出产/交租」这类关系（取前 8 条）")
rows = list(con.execute("""SELECT settlementId, descriptionText FROM bannerlord_settlements
                           WHERE settlementType='village' AND descriptionText IS NOT NULL AND descriptionText<>'' LIMIT 400"""))
print("   有描述文的村庄：%d" % len(rows))
kw = ["供", "粮", "谷", "收成", "交", "税", "租", "领主", "堡", "城"]
for r in rows[:8]:
    t = r["descriptionText"]
    print("   %-26s %s" % (r["settlementId"], t[:160]))

print("\n" + "=" * 78)
print("④ 城镇官方描述文（前 4 条）")
for r in con.execute("""SELECT settlementId, descriptionText FROM bannerlord_settlements
                        WHERE settlementType='town' AND descriptionText IS NOT NULL AND descriptionText<>'' LIMIT 4"""):
    print("   %-14s %s" % (r["settlementId"], r["descriptionText"][:200]))

print("\n" + "=" * 78)
print("⑤ 本地化里有没有「村庄/城堡/城镇」的通用解释条目（CNs 短文）")
for w in ["村庄", "城堡", "城镇"]:
    rs = list(con.execute("""SELECT stringId, text FROM localization_entries
                             WHERE language='CNs' AND text LIKE ? AND LENGTH(text) <= 40 LIMIT 6""", ("%" + w + "%",)))
    print("   「%s」 短条目 %d 条，样例：" % (w, len(rs)))
    for r in rs[:4]:
        print("       %-46s %s" % (r["stringId"], r["text"]))

print("\n" + "=" * 78)
print("⑥ bannerlord.db 的 sha256（对一下已入库源里那个 hash 是什么）")
h = hashlib.sha256()
with open(DB, "rb") as f:
    for chunk in iter(lambda: f.read(1 << 22), b""):
        h.update(chunk)
print("   db sha256 =", h.hexdigest())
print("   已入库 source.calradia.game.settlements.geography2 的 source_content_hash 前 16 位 = 9af64df767747545")

print("\n" + "=" * 78)
print("⑦ 一个村庄 + 它依附的城堡/城镇，取成对样本（写「依附」用）")
for r in con.execute("""
  SELECT v.settlementId vid, v.culture vculture, b.settlementId bid, b.settlementType btype
  FROM bannerlord_settlements v JOIN bannerlord_settlements b ON b.settlementId=v.boundSettlement
  WHERE v.settlementType='village' LIMIT 6"""):
    print("   %-26s -> %-22s (%s)" % (r["vid"], r["bid"], r["btype"]))
