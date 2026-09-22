# -*- coding: utf-8 -*-
"""上线前的预检（只读，不写任何东西）：
  ① 磁盘档数 / doc id 集合 与 在挂包 entries 集合 是否同一集合；
  ② head 与磁盘不一致的档 = 恰为本批 21 档；
  ③ 基线 documents.json（v17 产物）的档集合 与 磁盘一致；
  ④ op 记录里有没有撞名的（防幂等短路）。
"""
import glob
import hashlib
import io
import json
import os
import re

REPO = r"D:\AWAKE-Dev\AWAKE"
WS = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1")
WS_REL = "tools/worldbook-studio/workspace/full-geo1"
MIRR = os.path.join(REPO, r"docs\worldbook-migration\projection\authoring-out")
LIVE_PKG = os.path.join(REPO, r"ModuleData\Worldbook\packages\calradia\runtime.json")
PREV = os.path.join(WS, r"compiled\geo1-v17-armor-prose\documents.json")
OPBASE = "v18st20260919"

J = {}
for n in (1, 2, 3):
    J.update(json.load(io.open(os.path.join(MIRR, "_status_l2_towns_%d_20260919.json" % n), encoding="utf-8")))
targets = sorted(k for k in J if not k.startswith("_"))


def docid_of(path):
    for ln in io.open(path, encoding="utf-8").read(4000).splitlines():
        if ln.startswith("id:"):
            return ln.split(":", 1)[1].strip()
    return None


docs = [p for p in sorted(glob.glob(os.path.join(WS, "authoring", "*.yaml")))
        if not os.path.basename(p).startswith(("_", "source-"))]
disk_ids = {}
for p in docs:
    disk_ids[docid_of(p)] = p
print("① 磁盘档:", len(docs), " doc id 唯一:", len(disk_ids))

live = json.load(io.open(LIVE_PKG, encoding="utf-8"))
live_entries = {e["id"] for e in live["entries"]}
live_docids = {}
for e in live["entries"]:
    live_docids["doc." + e["id"].replace("awake:entry:", "", 1)] = e["id"]
print("   在挂包 entries:", len(live_entries))

only_disk = sorted(set(disk_ids) - set(live_docids))
only_pkg = sorted(set(live_docids) - set(disk_ids))
print("   只在磁盘:", len(only_disk), only_disk[:5])
print("   只在包  :", len(only_pkg), only_pkg[:5])

# ② head vs 磁盘
head = json.load(io.open(os.path.join(WS, r"authoring-v1\workspace-head.json"), encoding="utf-8"))["documents"]


def sha(p):
    return hashlib.sha256(open(p, "rb").read()).hexdigest().upper()


need = []
for d, p in sorted(disk_ids.items()):
    rec = head.get(d)
    if rec is None or str(rec.get("content_hash", "")).upper() != sha(p):
        need.append(os.path.basename(p))
print("② head 与磁盘不一致:", len(need))
print("   与本批 21 档集合相同:", sorted(need) == targets)
print("   多出的:", sorted(set(need) - set(targets))[:8])
print("   少了的:", sorted(set(targets) - set(need))[:8])

# ③ 基线 documents.json
prev = json.load(io.open(PREV, encoding="utf-8"))
pd = prev["documents"] if isinstance(prev, dict) else prev
pd_ids = {x["id"] for x in pd}
print("③ v17 产物 documents:", len(pd_ids), " 与磁盘同集合:", pd_ids == set(disk_ids))
print("   只在基线:", sorted(pd_ids - set(disk_ids))[:5])
print("   只在磁盘:", sorted(set(disk_ids) - pd_ids)[:5])

# ④ op 记录撞名
opsdir = os.path.join(WS, r"authoring-v1\operations")
n_ops = len(glob.glob(os.path.join(opsdir, "*"))) if os.path.isdir(opsdir) else 0
print("④ operations 目录条目:", n_ops, " 目录:", os.path.isdir(opsdir))
hit = 0
if os.path.isdir(opsdir):
    for f in os.listdir(opsdir):
        if OPBASE in f:
            hit += 1
print("   含本批 OPBASE 前缀的记录:", hit, "（期望 0）")
