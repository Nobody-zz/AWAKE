# -*- coding: utf-8 -*-
"""建第三个来源快照：游戏百科通识条目（std_concept_strings + spcultures +
spkingdoms + spworkshops）。

口径与 _uw_mk_sources_20260925.py 一致：
  - 快照格式 `<stringId> => <正文>`，UTF-8 LF 无 BOM；
  - source_content_hash = sha256(快照原始字节) 小写；
  - locator = `bannerlord.db#localization.<stringId>`。

★ 只收「整段世界观通识文」，判据：
  1. length >= 60；2. 不含花括号宏；3. 不含任务/对话口吻。
"""
import io
import os
import json
import hashlib
import sqlite3
import re

ROOT = r"D:\AWAKE-Dev\AWAKE"
SDIR = os.path.join(ROOT, "tools", "worldbook-studio", "workspace",
                    "full-geo1", "authoring", "sources")
DB = r"C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db"

WANT = ["std_concept_strings", "std_spcultures", "std_spkingdoms", "std_spworkshops"]
SID = "source.calradia.game.concepts"
SVER = "bannerlord-1.3.15.110062"

con = sqlite3.connect("file:%s?mode=ro" % DB.replace("\\", "/"), uri=True)
cur = con.cursor()

rows = cur.execute(
    "SELECT stringId, text, filePath FROM localization_entries "
    "WHERE language='CNs' AND length(text)>=60").fetchall()

PH = re.compile(r"\{[^}]*\}|\{!|\{\?")
BAD = ["你收到", "我亲爱的", "玩家", "任务", "您的"]

picked = []
for sid, txt, fp in rows:
    f = (fp or "").split("/")[-1]
    if not any(w in f for w in WANT):
        continue
    if PH.search(txt) or any(b in txt for b in BAD):
        continue
    picked.append((sid, txt, f))

picked.sort(key=lambda t: t[0])
print("百科通识条目 %d 条：" % len(picked))
for sid, txt, f in picked:
    print("  %-10s %-30s %d 字  %s" % (sid, f.replace("_xml-zho-CN.xml", ""), len(txt), txt[:34]))

body = "".join("%s => %s\n" % (sid, txt.replace("\n", " ").strip()) for sid, txt, _ in picked)
raw = body.encode("utf-8")
h = hashlib.sha256(raw).hexdigest()

fn = "game-concepts.txt"
with open(os.path.join(SDIR, fn), "wb") as f:
    f.write(raw)
print("\n写出 %s  字节=%d  sha256=%s" % (fn, len(raw), h))

# ── 注册 yaml ──
reg = """source_id: {sid}
source_version: {sver}
source_nature: game_snapshot
universe: awake_current
era: current
locator_root: {fn}
source_content_hash: {h}
content_tier: base
license_status: permitted
use_status: active
valid_until: null
imported_at: '2026-09-25T00:00:00Z'
normalization_version: utf8-lf-no-bom-v1
""".format(sid=SID, sver=SVER, h=h, fn=fn)
with open(os.path.join(SDIR, "source-game-concepts.yaml"), "wb") as f:
    f.write(reg.encode("utf-8"))
print("注册 yaml: source-game-concepts.yaml")

# 追加进 hash 登记表（供生成器消费）
jp = os.path.join(ROOT, "tools", "_uw_sources_20260925.json")
reg = json.load(io.open(jp, encoding="utf-8"))
reg[SID] = {"hash": h, "n": len(picked), "bytes": len(raw)}
io.open(jp, "w", encoding="utf-8").write(
    json.dumps(reg, ensure_ascii=False, indent=1))
print("已追加进 _uw_sources_20260925.json（共 %d 个来源）" % len(reg))
con.close()
