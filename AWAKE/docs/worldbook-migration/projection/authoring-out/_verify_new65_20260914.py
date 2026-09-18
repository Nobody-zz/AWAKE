# -*- coding: utf-8 -*-
"""核验本批新档（battania+sturgia 65）结构：id/别名/locator/quote 与 DB 官方首句一致。"""
import os, glob, sqlite3, re, yaml, io

ROOT = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out"
DB = r"C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db"
TARGET_CULT = {"Culture.battania", "Culture.sturgia"}

con = sqlite3.connect(f"file:{DB}?mode=ro", uri=True)
con.row_factory = sqlite3.Row
def loc(sid, lang="CNs"):
    r = con.execute("SELECT text FROM localization_entries WHERE stringId=? AND language=? LIMIT 1", (sid, lang)).fetchone()
    return r["text"] if r else None

rows = con.execute("SELECT settlementId,name,descriptionText FROM bannerlord_settlements "
                   "WHERE settlementType='village' AND culture IN ('Culture.battania','Culture.sturgia')").fetchall()
want = {}
for r in rows:
    sid = r["settlementId"]
    desc = r["descriptionText"] or ""
    dtok = desc.split("{=}")[0]
    dtok = desc.split("{=")[1].split("}")[0] if "{=" in desc else ""
    dc = loc(dtok)
    first = re.sub(r"\s+", "", dc).split("。", 1)[0] + "。" if dc else None
    want[sid] = first
print("DB 巴旦尼亚+斯特吉亚村数:", len(want))

base_docs = {}
for f in glob.glob(os.path.join(ROOT, "village-*.yaml")):
    d = yaml.safe_load(io.open(f, encoding="utf-8"))
    for s in d.get("sources", []):
        loc_ = s.get("locator", "")
        m = re.match(r"bannerlord\.villages#(.+)$", loc_)
        if m and m.group(1) in want:
            base_docs[m.group(1)] = (os.path.basename(f), d)

missing, bad = [], []
for sid, first in want.items():
    if sid not in base_docs:
        missing.append(sid); continue
    fn, d = base_docs[sid]
    src = [s for s in d["sources"] if s.get("locator") == f"bannerlord.villages#{sid}"][0]
    if src.get("quote") != first:
        bad.append((fn, sid, src.get("quote"), first))
    # 别名核验
    zh = d["aliases"]["zh-CN"]
    en = d["aliases"]["en"]
    if zh[-1] != "村庄" or en[-1] != sid:
        bad.append((fn, sid, "alias", zh, en))

print("缺档:", len(missing), missing[:10])
print("不符:", len(bad))
for b in bad[:15]:
    print("  ", b)
