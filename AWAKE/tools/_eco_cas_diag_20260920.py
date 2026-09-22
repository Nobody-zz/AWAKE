# -*- coding: utf-8 -*-
"""查 CAS-409 真因：磁盘 yaml 的 revision/hash vs head 记的 vs proof 记的。"""
import io, json, os, glob, hashlib, yaml as Y

REPO = r"D:\AWAKE-Dev\AWAKE"
WS = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1")
SAMPLE = ["goods-fish", "goods-iron", "items-hog"]

def sha(p):
    return hashlib.sha256(open(p, "rb").read()).hexdigest().upper()

print("== 磁盘 yaml 的 revision ==")
for s in SAMPLE:
    p = os.path.join(WS, "authoring", s + ".yaml")
    d = Y.safe_load(io.open(p, encoding="utf-8"))
    print("  %-14s revision=%s  sha=%s" % (s, d.get("revision"), sha(p)[:16]))

print()
head = json.load(io.open(os.path.join(WS, "authoring-v1/workspace-head.json"), encoding="utf-8"))
hd = head.get("documents") or {}
print("== head 里记的 ==")
for s in SAMPLE:
    rec = hd.get("doc.economy." + s)
    if rec is None:
        print("  %-14s HEAD 无记录" % s)
    else:
        print("  %-14s revision=%s hash=%s" % (s, rec.get("revision"), str(rec.get("content_hash"))[:16]))

print()
print("== 找 compile proof 文件 ==")
for pat in ["**/*1d5dab55*", "**/*39a320c2*", "**/*1cefd908*"]:
    for f in glob.glob(os.path.join(WS, pat), recursive=True):
        print("  ", f)

print()
print("== authoring-v1 目录结构 ==")
for root, dirs, files in os.walk(os.path.join(WS, "authoring-v1")):
    lvl = root.replace(os.path.join(WS, "authoring-v1"), "").count(os.sep)
    if lvl > 2:
        continue
    print("  " * lvl + os.path.basename(root) + "/  (%d files)" % len(files))
