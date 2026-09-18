# -*- coding: utf-8 -*-
"""① 验证 source_content_hash 的口径（拿 chronicle-kachar-peninsula 试：是不是那个文件的 sha256）
   ② 从 bannerlord.db 抽「村庄/城堡/城镇」三类聚落各十条**通用**说明串（政策/perk/提示），
      这是给三个概念词条当一手引文用的。
"""
import hashlib
import io
import json
import os
import sqlite3
import sys

sys.stdout.reconfigure(encoding="utf-8")

DB = r"C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db"
SRC = "docs/worldbook-migration/projection/sources-out/chronicle-kachar-peninsula.txt"
TARGET = "a306e8aec67d3665c1f10846236b414d1e1a0c16c76aec106f00cf62e69aaceb"


def sha_file(p):
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for c in iter(lambda: f.read(1 << 20), b""):
            h.update(c)
    return h.hexdigest()


def sha_text(s):
    return hashlib.sha256(s.encode("utf-8")).hexdigest()


print("=" * 78)
print("① source_content_hash 是不是「源文件本身」的 sha256")
if os.path.exists(SRC):
    print("   目标           =", TARGET[:24])
    print("   文件 sha256(raw)=", sha_file(SRC)[:24], "✔" if sha_file(SRC) == TARGET else "✘")
    b = open(SRC, "rb").read()
    print("   文本（utf-8-sig 解码后）sha256 =", sha_text(b.decode("utf-8-sig"))[:24],
          "✔" if sha_text(b.decode("utf-8-sig")) == TARGET else "✘")
else:
    print("   源文件不在:", SRC)

con = sqlite3.connect(f"file:{DB}?mode=ro", uri=True)
con.row_factory = sqlite3.Row

print("\n" + "=" * 78)
print("② 三类聚落的**通用**说明串（本地化里不含专名的短句）")
PAT = {
    "村庄": ["%村庄%"],
    "城堡": ["%城堡%"],
    "城镇": ["%城镇%"],
}
BAN = ["阿布", "德里亚特", "沙拉斯", "卡恰尔", "塔科尔", "阿克卡"]
for label, pats in PAT.items():
    print("\n── %s ──" % label)
    seen = set()
    n = 0
    for p in pats:
        for r in con.execute("""SELECT stringId, text, filePath FROM localization_entries
                                WHERE language='CNs' AND text LIKE ? AND LENGTH(text) BETWEEN 4 AND 60""", (p,)):
            t = r["text"]
            if any(b in t for b in BAN):
                continue
            key = (r["stringId"], t)
            if key in seen:
                continue
            seen.add(key)
            n += 1
            if n <= 18:
                print("   %-10s %s" % (r["stringId"], t))
    print("   （去掉专名后共 %d 条）" % n)
