# -*- coding: utf-8 -*-
"""为 09-25 概念类/人物类批次落 source 快照 + 注册 yaml。
两条来源：
  A. game-lore-rulers  —— 50 条 std_world_lore_strings（各国统治者的自述/评价），官方 CNs。
  B. game-hero-bio     —— bannerlord_heroes.text 的 27 条人物背景文，官方 CNs。
快照格式与既有 game-lore-war*.txt 一致：`<stringId> => <中文正文>`，一行一条，UTF-8 LF 无 BOM。
hash 口径：sha256(raw bytes) 小写。
"""
import sqlite3
import re
import io
import os
import json
import hashlib

DB = r"C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db"
SDIR = r"D:\AWAKE-Dev\AWAKE\tools\worldbook-studio\workspace\full-geo1\authoring\sources"

con = sqlite3.connect("file:%s?mode=ro" % DB, uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()

VER = "bannerlord-1.3.15.110062"


def write_snapshot(fname, rows):
    """rows: list[(key, text)]。写 UTF-8 LF 无 BOM。"""
    body = "".join("%s => %s\n" % (k, v.replace("\n", " ").strip()) for k, v in rows)
    p = os.path.join(SDIR, fname)
    with io.open(p, "w", encoding="utf-8", newline="\n") as f:
        f.write(body)
    raw = io.open(p, "rb").read()
    return hashlib.sha256(raw).hexdigest(), len(rows), len(raw)


def write_reg(fname, sid, ver, nature, locator_root, content_hash, era="current"):
    reg = (
        "source_id: %s\n"
        "source_version: %s\n"
        "source_nature: %s\n"
        "universe: awake_current\n"
        "era: %s\n"
        "locator_root: %s\n"
        "source_content_hash: %s\n"
        "content_tier: base\n"
        "license_status: permitted\n"
        "use_status: active\n"
        "valid_until: null\n"
        "imported_at: '2026-09-25T00:00:00Z'\n"
        "normalization_version: utf8-lf-no-bom-v1\n"
    ) % (sid, ver, nature, era, locator_root, content_hash)
    with io.open(os.path.join(SDIR, fname), "w", encoding="utf-8", newline="\n") as f:
        f.write(reg)


out = {}

# ---- A. 统治者自述（50 条 world_lore）----
cur.execute("""
SELECT l.stringId, l.text FROM localization_entries l
WHERE l.language='CNs' AND l.filePath LIKE '%world_lore%'
ORDER BY l.stringId
""")
rows = [(r["stringId"], r["text"]) for r in cur.fetchall()]
h, n, nb = write_snapshot("game-lore-rulers.txt", rows)
write_reg("source-game-lore-rulers.yaml", "source.calradia.game.lore.rulers", VER,
          "game_snapshot", "game-lore-rulers.txt", h)
out["source.calradia.game.lore.rulers"] = {"hash": h, "n": n, "bytes": nb}
print("A. game-lore-rulers  %d 条 %d 字节  hash=%s" % (n, nb, h))

# ---- B. 人物背景文（27 条 hero bio）----
cur.execute("SELECT heroId, text FROM bannerlord_heroes WHERE text IS NOT NULL AND text<>'' ORDER BY heroId")
bios = []
for r in cur.fetchall():
    txt = r["text"]
    mm = re.match(r"^\{=([^}]+)\}(.*)$", txt, re.S)
    if not mm:
        continue
    key = mm.group(1)
    cur2 = con.cursor()
    cur2.execute("SELECT text FROM localization_entries WHERE language='CNs' AND stringId=? LIMIT 1", (key,))
    rr = cur2.fetchone()
    if rr:
        bios.append((r["heroId"], rr["text"]))
h2, n2, nb2 = write_snapshot("game-hero-bio.txt", bios)
write_reg("source-game-hero-bio.yaml", "source.calradia.game.hero-bio", VER,
          "game_snapshot", "game-hero-bio.txt", h2)
out["source.calradia.game.hero-bio"] = {"hash": h2, "n": n2, "bytes": nb2}
print("B. game-hero-bio      %d 条 %d 字节  hash=%s" % (n2, nb2, h2))

con.close()

with io.open(r"D:\AWAKE-Dev\AWAKE\tools\_uw_sources_20260925.json", "w", encoding="utf-8") as f:
    json.dump(out, f, ensure_ascii=False, indent=1)
print("\n写出 tools/_uw_sources_20260925.json")
