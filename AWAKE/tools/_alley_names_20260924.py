# 查 Alley / GangLeader / CrimeRating 的官方中文译名与机制文案
import sqlite3, re, sys

sys.stdout.reconfigure(encoding="utf-8")
DB = r'C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db'
con = sqlite3.connect(f'file:{DB}?mode=ro', uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()

print("=" * 70)
print("一、英文原文 → stringId（库里无 EN，用 BR/DE 等未译语言定位）")
print("=" * 70)
for word in ["Alley", "Gang Leader", "Crime Rating", "Crime", "Gang"]:
    rows = cur.execute(
        "SELECT DISTINCT stringId FROM localization_entries WHERE text=? LIMIT 5", (word,)
    ).fetchall()
    print(f"  [{word}] -> {[r['stringId'] for r in rows]}")

print()
print("=" * 70)
print("二、CNs 里含「巷」的所有条目")
print("=" * 70)
rows = cur.execute(
    "SELECT stringId, text, filePath FROM localization_entries "
    "WHERE language='CNs' AND text LIKE '%巷%' ORDER BY filePath, stringId"
).fetchall()
print(f"  共 {len(rows)} 条")
files = {}
for r in rows:
    files.setdefault(r["filePath"], 0)
    files[r["filePath"]] += 1
for f, n in sorted(files.items(), key=lambda x: -x[1]):
    print(f"    {n:4d}  {f}")
