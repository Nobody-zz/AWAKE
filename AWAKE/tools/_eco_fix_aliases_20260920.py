# -*- coding: utf-8 -*-
"""按 K1 口径清理经济批别名：与本档同语言 title 相等或互为子串的，一律删（死条）。"""
import io, json, os, re

AO = r"D:\AWAKE-Dev\AWAKE\docs\worldbook-migration\projection\authoring-out"
FILES = ["_eco_A1_20260920.json", "_eco_A2B_20260920.json"]

def sub(a, b):
    return a in b or b in a

report = []
for fn in FILES:
    p = os.path.join(AO, fn)
    d = json.load(io.open(p, encoding="utf-8"))
    for doc in d["docs"]:
        tzh, ten = doc["title_zh"], doc["title_en"]
        for lang, tv in (("zh", tzh), ("en", ten)):
            key = "aliases_zh" if lang == "zh" else "aliases_en"
            keep = []
            for a in doc.get(key) or []:
                if a == tv or sub(a.lower(), tv.lower()):
                    report.append((doc["slug"], lang, a, tv))
                else:
                    keep.append(a)
            doc[key] = keep
    io.open(p, "w", encoding="utf-8", newline="\n").write(
        json.dumps(d, ensure_ascii=False, indent=1))

print("被删的死条别名（%d 条）:" % len(report))
for s, lang, a, tv in report:
    print("  %-28s %s  「%s」 vs title「%s」" % (s, lang, a, tv))
print()
# 复核
for fn in FILES:
    d = json.load(io.open(os.path.join(AO, fn), encoding="utf-8"))
    for doc in d["docs"]:
        print("%-30s zh=%s en=%s" % (doc["slug"], doc["aliases_zh"], doc["aliases_en"]))
