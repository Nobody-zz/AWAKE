# -*- coding: utf-8 -*-
"""反推两个 hash 的口径：
  quote_hash       —— 已入库例：villages-ab-comer 的 quote 及其 hash
  source_content_hash —— 已入库例：source.calradia.game.villages-desc-battania
"""
import hashlib
import io
import json
import sqlite3
import sys

sys.stdout.reconfigure(encoding="utf-8")

DB = r"C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db"


def sha_text(s):
    return hashlib.sha256(s.encode("utf-8")).hexdigest().upper()


print("=" * 78)
print("① quote_hash 口径：拿 villages-ab-comer 那条试")
q = "阿布·科梅尔坐落于穿过乌卡利翁高原的特朗河上游的陡峭处。"
print("   目标哈希        = DBBEC3155DC3163325C2D18A31BBDE394D02E0AA26EC20D2B430D6210DB004AA")
print("   sha256(quote)   =", sha_text(q), "  ✔" if sha_text(q) == "DBBEC3155DC3163325C2D18A31BBDE394D02E0AA26EC20D2B430D6210DB004AA" else "  ✘")

print("\n" + "=" * 78)
print("② source_content_hash 口径：试几种拼法，找 3A9E1641DAFB70CAEDE9E6EFF11D2A9981C2F4256ECFB28DB7C1E591DE58CEEE")
TARGET = "3A9E1641DAFB70CAEDE9E6EFF11D2A9981C2F4256ECFB28DB7C1E591DE58CEEE"
con = sqlite3.connect(f"file:{DB}?mode=ro", uri=True)
con.row_factory = sqlite3.Row

# 巴旦尼亚所有村庄（castle_village_B*）的 CNs 描述文
rows = list(con.execute("""
  SELECT s.settlementId sid, s.descriptionText dt
  FROM bannerlord_settlements s
  WHERE s.settlementType='village' AND s.settlementId LIKE 'castle_village_B%'
  ORDER BY s.settlementId"""))
print("   巴旦尼亚村庄数：%d" % len(rows))

def cns_of(desc):
    if not desc:
        return ""
    tok = ""
    if desc.startswith("{="):
        tok = desc[2:desc.index("}")]
    if tok:
        r = con.execute("SELECT text FROM localization_entries WHERE language='CNs' AND stringId=? LIMIT 1", (tok,)).fetchone()
        if r:
            return r["text"]
    return desc

texts = [cns_of(r["dt"]) for r in rows]
cands = {
    "逐条 CNs 文本直接拼接": "".join(texts),
    "逐条 CNs 文本 \\n 拼接": "\n".join(texts),
    "逐条 CNs 文本 \\n\\n 拼接": "\n\n".join(texts),
    "逐条「sid + CNs」\\n 拼": "\n".join(r["sid"] + t for r, t in zip(rows, texts)),
    "逐条「sid#CNs」\\n 拼": "\n".join(r["sid"] + "#" + t for r, t in zip(rows, texts)),
    "原始英文 descriptionText \\n 拼": "\n".join((r["dt"] or "") for r in rows),
}
for name, s in cands.items():
    h = sha_text(s)
    print("   %-28s %s %s" % (name, h[:16], "✔" if h == TARGET else ""))

print("\n   样例 CNs 文本：")
for t in texts[:3]:
    print("      " + t[:90])

print("\n" + "=" * 78)
print("③ 换成 bannerlord.db 整库 hash 对比")
print("   db sha256 = e43d1df3439af81a4d99e57f19ab06bdfd5dc394bb9ea3cd20bfe2bf4b10a23a")
