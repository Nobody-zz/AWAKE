# -*- coding: utf-8 -*-
"""查城镇 name 原始值与描述文样例。"""
import sqlite3, re

DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"
con = sqlite3.connect(f"file:{DB}?mode=ro", uri=True)
con.row_factory = sqlite3.Row

rows = con.execute(
    "SELECT settlementId,name,descriptionText,culture,owner,boundSettlement FROM bannerlord_settlements WHERE settlementType='town' ORDER BY settlementId").fetchall()
for r in rows[:6]:
    print(r["settlementId"], "|", repr(r["name"]), "|", (r["culture"] or "")[:30], "| owner:", r["owner"], "| bound:", r["boundSettlement"])
    print("   desc:", (r["descriptionText"] or "")[:80])
print("...")
# 沙拉斯 town_V7 对照
r = con.execute("SELECT settlementId,name,descriptionText FROM bannerlord_settlements WHERE settlementId='town_V7'").fetchone()
print("V7:", r["settlementId"] if r else None, "|", repr(r["name"]) if r else None)
if r:
    print("   desc:", (r["descriptionText"] or "")[:80])
# 描述文本身的语言：是否有中文？
r2 = rows[0]
print("\ndesc[0] 前 120 字:", r2["descriptionText"][:120])
