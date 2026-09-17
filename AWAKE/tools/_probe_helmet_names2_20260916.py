# -*- coding: utf-8 -*-
"""续查：① 官方有没有**单独**叫「锅盔」的条目；② 游戏里的食物（Food）官方中文叫什么。
目的：判断「锅盔」当词条名是否合规，以及它会不会跟食物类撞名。
"""
import re
import sqlite3

DB = r'C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db'
con = sqlite3.connect('file:%s?mode=ro' % DB, uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()

print("=== A. 官方 CNs 里是否存在**恰好等于**「锅盔」的条目 ===")
rows = cur.execute("SELECT DISTINCT stringId, filePath FROM localization_entries "
                   "WHERE language='CNs' AND text='锅盔'").fetchall()
print("   精确命中 %d 条" % len(rows))
for r in rows:
    print("   ", r["stringId"], "|", r["filePath"])

print()
print("=== B. 官方 CNs 里「锅盔」的所有形态（去重词形）===")
forms = {}
for r in cur.execute("SELECT text, filePath FROM localization_entries "
                     "WHERE language='CNs' AND text LIKE '%锅盔%'"):
    forms.setdefault(r["text"], r["filePath"])
print("   含「锅盔」的官方词形共 %d 种" % len(forms))
same = [t for t in forms if t == "锅盔"]
print("   其中恰好等于「锅盔」的：%s" % (same or "无"))
print("   单用「锅盔」开头/结尾的（看官方怎么带修饰）：")
for t in sorted(forms):
    if len(t) <= 12:
        print("      ", t)

print()
print("=== C. bannerlord_items 的 itemType 分布 ===")
for r in cur.execute("SELECT itemType, COUNT(*) n FROM bannerlord_items GROUP BY itemType ORDER BY n DESC"):
    print("   %-14s %d" % (r["itemType"], r["n"]))

print()
print("=== D. Food 类物品的官方中文名 ===")
def zh_of_token(tok):
    """'name' 字段形如 {=xxxx}EnglishName —— 剥 hash 查 CNs。"""
    if not tok:
        return None
    m = re.match(r"\{=([^}]+)\}", str(tok))
    if not m:
        return str(tok)
    h = m.group(1)
    r = cur.execute("SELECT text FROM localization_entries WHERE language='CNs' AND stringId=?",
                    (h,)).fetchone()
    return r["text"] if r else None

foods = cur.execute("SELECT entityId, name, itemType FROM bannerlord_items "
                    "WHERE LOWER(itemType) LIKE '%food%' LIMIT 40").fetchall()
print("   Food 行数（前 40）：%d" % len(foods))
for r in foods:
    print("   %-32s %-24s %s" % (r["entityId"], str(r["name"])[:24], zh_of_token(r["name"])))

print()
print("=== E. 官方 CNs 里带「饼 / 面 / 馍 / 麦」的词（看有没有跟「锅盔」抢食物义的名）===")
for r in cur.execute("SELECT DISTINCT text FROM localization_entries WHERE language='CNs' "
                     "AND (text LIKE '%饼%' OR text LIKE '%馍%') LIMIT 30"):
    print("   ", r["text"])

con.close()
