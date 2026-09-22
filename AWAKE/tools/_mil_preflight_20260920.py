# -*- coding: utf-8 -*-
"""军事批（09-20）编译前预检（只读）：
  ① 现役包档数 / 磁盘档数 / 需重登记档；
  ② 磁盘 vs 现役包 doc id 集合差；
  ③ 本批 24 档是否全在磁盘、且 head 里 hash 不一致（=待登记）；
  ④ 有没有别的无主改动混在待登记里（应恰为本批 24 档 + 撞车处置过的 2 档）。
"""
import glob
import hashlib
import io
import json
import os
import re

REPO = r"D:\AWAKE-Dev\AWAKE"
WS = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1")
MIRR = os.path.join(REPO, r"docs\worldbook-migration\projection\authoring-out")
LIVE_PKG = os.path.join(REPO, r"ModuleData\Worldbook\packages\calradia\runtime.json")

BATCH = ["troops-banner-knight", "troops-khans-guard", "troops-ghulam", "troops-vaegir-guard",
         "troops-druzhinnik-cavalry", "troops-golden-boar", "troops-legion-of-the-betrayed",
         "military-legion-old", "military-legion-modern", "war_history-kuyug",
         "military-empire-system", "military-battania", "military-empire-north",
         "military-empire-south", "military-empire-west", "military-nord", "military-aserai",
         "weapons-crossbow-ironbound", "weapons-ballista", "weapons-ballista-fire",
         "weapons-mangonel", "weapons-mangonel-fire", "weapons-siege-tower", "weapons-siege-ram"]
COLLISION = ["military-vlandia", "military-khuzait"]


def docid_of(path):
    for ln in io.open(path, encoding="utf-8").read(6000).splitlines():
        if ln.startswith("id:"):
            return ln.split(":", 1)[1].strip()
    return None


def sha(p):
    return hashlib.sha256(open(p, "rb").read()).hexdigest().upper()


docs = [p for p in sorted(glob.glob(os.path.join(WS, "authoring", "*.yaml")))
        if not os.path.basename(p).startswith(("_", "source-"))]
disk = {}   # docid -> path
for p in docs:
    disk[docid_of(p)] = p
print("① 磁盘档:", len(docs), " doc id 唯一:", len(disk))

live = json.load(io.open(LIVE_PKG, encoding="utf-8"))
live_ids = {"doc." + e["id"].replace("awake:entry:", "", 1) for e in live["entries"]}
print("   现役包 entries:", len(live["entries"]), " doc id:", len(live_ids))

both = set(disk) & live_ids
only_disk = sorted(set(disk) - live_ids)
only_live = sorted(live_ids - set(disk))
print("   交集:", len(both))
print("   只在磁盘:", len(only_disk), only_disk[:8])
print("   只在包  :", len(only_live), only_live[:8])

# ② 本批 24 档在不在磁盘
want = {"doc.war." + b for b in BATCH}
miss = sorted(want - set(disk))
print("② 本批 24 档全在磁盘:", not miss, " 缺:", miss[:5])

# ③ head 与磁盘不一致
head = json.load(io.open(os.path.join(WS, r"authoring-v1\workspace-head.json"),
                         encoding="utf-8"))["documents"]
need = []
for d, p in sorted(disk.items()):
    rec = head.get(d)
    if rec is None or str(rec.get("content_hash", "")).upper() != sha(p):
        need.append(os.path.basename(p))
print("③ head 不一致（待登记）:", len(need))
extra = sorted(set(need) - set(b + ".yaml" for b in BATCH) - set(c + ".yaml" for c in COLLISION))
print("   除本批 24 + 撞车 2 之外的额外待登记:", len(extra), extra[:10])

# ④ 撞车 2 档也是待登记（rev+1 已改）
for c in COLLISION:
    print("   撞车档 %s 待登记: %s" % (c, c + ".yaml" in need))
