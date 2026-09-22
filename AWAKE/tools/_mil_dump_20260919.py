# -*- coding: utf-8 -*-
"""dump 某一军事题材的全部原始素材：编年史专条变体全文 + 游戏本体含该词的 CN 全文。
用法：python _mil_dump_20260919.py 具装骑兵
"""
import io
import json
import os
import sqlite3
import sys

RULES = r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/AnimusForge/PlayerExports/卡拉迪亚编年史/knowledge/rules"
DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"

kw = sys.argv[1] if len(sys.argv) > 1 else "具装骑兵"
out = io.StringIO()

# ---- 编年史专条 ----
out.write("=" * 72 + "\n编年史专条：" + kw + "\n" + "=" * 72 + "\n")
cand = [f for f in os.listdir(RULES) if f.startswith("rule_" + kw + "__") or f == "rule_" + kw + ".json"]
if not cand:
    # 模糊找
    cand = [f for f in os.listdir(RULES) if kw in f]
for f in cand:
    d = json.load(io.open(os.path.join(RULES, f), encoding="utf-8-sig"))
    out.write("\n### %s\n" % f)
    out.write("  Id: %s\n" % d.get("Id"))
    out.write("  Keywords: %s\n" % d.get("Keywords"))
    out.write("  RagShortTexts: %s\n" % json.dumps(d.get("RagShortTexts"), ensure_ascii=False)[:400])
    vs = d.get("Variants") or []
    out.write("  变体数: %d\n" % len(vs))
    for i, v in enumerate(vs):
        out.write("\n  --- V%d  Priority=%s ---\n" % (i, v.get("Priority")))
        out.write("  When: %s\n" % json.dumps(v.get("When"), ensure_ascii=False))
        out.write("  Content: %s\n" % (v.get("Content") or ""))

# ---- 游戏本体 ----
out.write("\n" + "=" * 72 + "\n游戏本体 CN 文本含「" + kw + "」\n" + "=" * 72 + "\n")
con = sqlite3.connect("file:" + DB + "?mode=ro", uri=True)
cur = con.cursor()
rows = cur.execute(
    "select stringId, text from localization_entries where language='CNs' and text like ?",
    ("%" + kw + "%",)).fetchall()
out.write("命中 %d 条\n" % len(rows))
for sid, t in rows:
    out.write("\n[%s] %s\n" % (sid, t))
con.close()

txt = out.getvalue()
print(txt)
io.open(r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/_mil_dump_%s.txt" % kw, "w",
        encoding="utf-8", newline="\n").write(txt)
print("\n>>> 已落盘 _mil_dump_%s.txt（%d 字）" % (kw, len(txt)))
