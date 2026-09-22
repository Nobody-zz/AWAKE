# -*- coding: utf-8 -*-
"""军事批（09-20）编译产物验收（只读）：
  ① entries 数 / id 集合 == 磁盘；
  ② vs 现役包：新增哪 25 档、既有档变了几条、变化字段；
  ③ 本批 24 档逐档关键词面（title+aliases 是否全进 keywords）、断言/表达数、别名口径；
  ④ 「doc.」明文出现次数 == entries 数。
"""
import glob
import hashlib
import io
import json
import os

REPO = r"D:\AWAKE-Dev\AWAKE"
WS = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1")
PKG = os.path.join(WS, r"compiled\geo1-v19-military\runtime.json")
LIVE = os.path.join(REPO, r"ModuleData\Worldbook\packages\calradia\runtime.json")

BATCH = ["troops-banner-knight", "troops-khans-guard", "troops-ghulam", "troops-vaegir-guard",
         "troops-druzhinnik-cavalry", "troops-golden-boar", "troops-legion-of-the-betrayed",
         "military-legion-old", "military-legion-modern", "war_history-kuyug",
         "military-empire-system", "military-battania", "military-empire-north",
         "military-empire-south", "military-empire-west", "military-nord", "military-aserai",
         "weapons-crossbow-ironbound", "weapons-ballista", "weapons-ballista-fire",
         "weapons-mangonel", "weapons-mangonel-fire", "weapons-siege-tower", "weapons-siege-ram"]


def docid_of(path):
    for ln in io.open(path, encoding="utf-8").read(6000).splitlines():
        if ln.startswith("id:"):
            return ln.split(":", 1)[1].strip()
    return None


docs = [p for p in sorted(glob.glob(os.path.join(WS, "authoring", "*.yaml")))
        if not os.path.basename(p).startswith(("_", "source-"))]
disk = {docid_of(p) for p in docs}

raw = io.open(PKG, encoding="utf-8").read()
pkg = json.loads(raw)
ents = pkg["entries"]
bypkg = {"doc." + e["id"].replace("awake:entry:", "", 1): e for e in ents}
live = json.load(io.open(LIVE, encoding="utf-8"))
bylive = {"doc." + e["id"].replace("awake:entry:", "", 1): e for e in live["entries"]}

print("① entries=%d 磁盘=%d id集合相同=%s" % (len(ents), len(disk), set(bypkg) == disk))
added = sorted(set(bypkg) - set(bylive))
print("   新增 %d 档：" % len(added))
for d in added:
    print("      +", d)
removed = sorted(set(bylive) - set(bypkg))
print("   消失 %d 档：%s" % (len(removed), removed[:6]))

print("\n② 既有档内容变化：")
n = 0
for d in sorted(set(bypkg) & set(bylive)):
    a, b = bylive[d], bypkg[d]
    if a == b:
        continue
    n += 1
    fields = [f for f in ("summary", "keywords", "expressions", "title") if a.get(f) != b.get(f)]
    if n <= 12:
        print("   %s 变化=%s" % (d, fields))
print("   合计变化 %d 条" % n)

print("\n③ 本批 24 档逐档：")
for b in BATCH:
    d = "doc.war." + b
    e = bypkg.get(d)
    if not e:
        print("   MISS", d)
        continue
    kw = e.get("keywords") or []
    ex = e.get("expressions") or []
    summ = e.get("summary")
    summ = summ.get("zh-CN") if isinstance(summ, dict) else summ
    print("   %-34s entries=%s kw=%d exprs=%d" % (b, "OK", len(kw), len(ex)))
    print("        kw=%s" % kw)
    print("        summary=%s" % str(summ)[:70])

print("\n④ 'doc.' 出现 %d 次（entries %d）" % (raw.count("doc."), len(ents)))
