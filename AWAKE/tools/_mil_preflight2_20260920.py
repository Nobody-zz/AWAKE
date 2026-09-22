# -*- coding: utf-8 -*-
"""只看两件事：① 全 25 个「只在磁盘」档；② head 为什么大范围不一致（抽样 5 档看 hash）。"""
import glob
import hashlib
import io
import json
import os

REPO = r"D:\AWAKE-Dev\AWAKE"
WS = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1")
LIVE_PKG = os.path.join(REPO, r"ModuleData\Worldbook\packages\calradia\runtime.json")


def docid_of(path):
    for ln in io.open(path, encoding="utf-8").read(6000).splitlines():
        if ln.startswith("id:"):
            return ln.split(":", 1)[1].strip()
    return None


def sha(p):
    return hashlib.sha256(open(p, "rb").read()).hexdigest().upper()


docs = [p for p in sorted(glob.glob(os.path.join(WS, "authoring", "*.yaml")))
        if not os.path.basename(p).startswith(("_", "source-"))]
disk = {docid_of(p): p for p in docs}
live = json.load(io.open(LIVE_PKG, encoding="utf-8"))
live_ids = {"doc." + e["id"].replace("awake:entry:", "", 1) for e in live["entries"]}

print("只在磁盘的全部档：")
for d in sorted(set(disk) - live_ids):
    print("   ", d, "  <-", os.path.basename(disk[d]))

hpath = os.path.join(WS, r"authoring-v1\workspace-head.json")
hraw = io.open(hpath, encoding="utf-8").read()
print("\nhead 文件字节:", len(hraw), " mtime:", os.path.getmtime(hpath))
head = json.loads(hraw)["documents"]
print("head documents:", len(head))
sample = sorted(head)[:3]
for d in sample:
    rec = head[d]
    p = os.path.join(WS, rec.get("path", ""))
    print("  head[%s] keys=%s" % (d, sorted(rec.keys())))
    print("     head hash:", str(rec.get("content_hash"))[:24], " disk exists:", os.path.exists(p),
          " disk hash:", (sha(p)[:24] if os.path.exists(p) else "-"))

# 统计：head 里有多少档 path 指向的文件存在
exist = sum(1 for d, r in head.items() if os.path.exists(os.path.join(WS, r.get("path", ""))))
print("head 档中 path 存在:", exist, "/", len(head))
