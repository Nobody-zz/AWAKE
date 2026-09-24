# 取三个词的 CNs 译名 + 巷子相关全部文案正文
import sqlite3, sys

sys.stdout.reconfigure(encoding="utf-8")
DB = r'C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db'
con = sqlite3.connect(f'file:{DB}?mode=ro', uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()

print("=" * 78)
print("一、三个词的官方中文（先拿 stringId，再查 CNs）")
print("=" * 78)
pairs = [("Alley", "XkrL8Wok"), ("Gang Leader", "DIuI1moW"), ("Crime", "xj0MgUO0")]
for en, sid in pairs:
    rows = cur.execute(
        "SELECT text, filePath FROM localization_entries WHERE language='CNs' AND stringId=?",
        (sid,)
    ).fetchall()
    print(f"\n  [{en}]  stringId={sid}")
    for r in rows:
        print(f"    CNs = {r['text']!r}")
        print(f"    出处 = {r['filePath']}")

print()
print("=" * 78)
print("二、含「巷」的全部 CNs 条目正文（按出处分组）")
print("=" * 78)
rows = cur.execute(
    "SELECT stringId, text, filePath FROM localization_entries "
    "WHERE language='CNs' AND text LIKE '%巷%' ORDER BY filePath, stringId"
).fetchall()
cur_file = None
for r in rows:
    if r["filePath"] != cur_file:
        cur_file = r["filePath"]
        print(f"\n--- {cur_file} ---")
    t = (r["text"] or "").replace("\n", " ")
    print(f"  [{r['stringId']}] {t}")
