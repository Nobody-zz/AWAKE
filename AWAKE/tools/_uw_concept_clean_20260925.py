# -*- coding: utf-8 -*-
"""捞出游戏里真正的「世界观通识条目」——即百科式整段叙述文，而非任务模板。

判据（三条同时满足才算）：
  1. `length(text) >= 60`（整段而非短语）；
  2. 不含任务/对话占位符：`{..._LINK}` / `{?` / `{!}` 等花括号宏；
  3. 不含第二人称对话口吻（"你收到"、"我亲爱的"、"玩家"）。

来源：`localization_entries`（language='CNs'）。
输出：按 filePath 分组的候选，供概念档人工挑选。
"""
import io
import os
import json
import sqlite3
import re

ROOT = r"D:\AWAKE-Dev\AWAKE"
DB = r"C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db"
OUT = os.path.join(ROOT, "tools", "_uw_concept_clean_20260925.json")

con = sqlite3.connect("file:%s?mode=ro" % DB.replace("\\", "/"), uri=True)
cur = con.cursor()

PLACEHOLDER = re.compile(r"\{[^}]*\}|\{!|\{\?")
BANNED = ["你收到", "我亲爱的", "玩家", "任务", "您的"]

rows = cur.execute(
    "SELECT stringId, text, filePath FROM localization_entries "
    "WHERE language='CNs' AND length(text)>=60 ORDER BY length(text) DESC"
).fetchall()

clean = []
for sid, txt, fp in rows:
    if PLACEHOLDER.search(txt):
        continue
    if any(b in txt for b in BANNED):
        continue
    clean.append({"stringId": sid, "text": txt, "filePath": fp, "len": len(txt)})

print("CNs >=60 字总条数 %d，洗净后 %d" % (len(rows), len(clean)))

# 按 filePath 归类
by_fp = {}
for r in clean:
    fp = (r["filePath"] or "(空)").split("/")[-1]
    by_fp.setdefault(fp, []).append(r)

print("\n按文件分组（前 25）：")
for fp, rs in sorted(by_fp.items(), key=lambda t: -len(t[1]))[:25]:
    print("  %-40s %d 条" % (fp, len(rs)))

io.open(OUT, "w", encoding="utf-8").write(
    json.dumps({"by_file": by_fp, "all": clean}, ensure_ascii=False, indent=1))
print("\n落盘 %s" % OUT)
con.close()
