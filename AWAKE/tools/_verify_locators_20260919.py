# -*- coding: utf-8 -*-
"""核 21 档新断言里的每条 source：**quote 是否真在它自己声明的 locator 那一行上**。

为什么必须单独核：套用器为了不动头部，把所有 quote 的 locator 一律写成该档**第一个来源**
的 locator。若某条 quote 实际出自别的位置，就成了一条「引文张冠李戴」——
`validate` 只比 quote_hash↔quote 与 source_content_hash↔文件，**看不出 locator 错**。

判法（与 `_probe_doubt_marks` 同源）：
  · locator 形如 `bannerlord.villages#village_B2_1` ⇒ 取 `#` 后 key 的**首段**，
    在来源文件里找 `键 => 值` 的行，按 `.` 末段**全等**比对（防 castle_village_B2_1 张冠李戴）。
  · locator 是 JSON 路径（编年史，形如 `#/Variants/0/Content`）⇒ 行内找不到 key，
    回退用 quote 前 24 字在文件里定位。
并打印「取不到原文行」条数当阳性对照（应为 0）。
"""
import io
import json
import os
import sys

import yaml

REPO = r"D:\AWAKE-Dev\AWAKE"
LIVE = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1\authoring")
MIRR = os.path.join(REPO, r"docs\worldbook-migration\projection\authoring-out")
SRC = os.path.join(LIVE, "sources")

REG = {}
for f in sorted(os.listdir(SRC)):
    if f.startswith("source-") and f.endswith(".yaml"):
        d = yaml.safe_load(io.open(os.path.join(SRC, f), encoding="utf-8"))
        root = d.get("locator_root")
        p = os.path.join(SRC, root) if root else None
        if p and os.path.exists(p):
            REG[d["source_id"]] = io.open(p, encoding="utf-8").read().splitlines()

J = {}
for n in (1, 2, 3):
    J.update(json.load(io.open(os.path.join(MIRR, "_status_l2_towns_%d_20260919.json" % n), encoding="utf-8")))
targets = sorted(k for k in J if not k.startswith("_"))


def norm(s):
    return s.replace("\\n", " ").replace("\\u", "\\u")


def find_line(sid, locator, quote):
    lines = REG.get(sid) or []
    if "#" in locator:
        key = locator.split("#", 1)[1].strip("/")
        parts = [p for p in key.split("/") if p]
        if parts and not (parts[0] == "Variants" or len(parts) > 1):
            tok = parts[0]
            for ln in lines:
                if "=>" in ln and ln.split("=>", 1)[0].strip().split(".")[-1] == tok:
                    return ln
    if quote:
        probe = quote.strip()[:24]
        for ln in lines:
            if probe and probe in ln.replace("\\n", " "):
                return ln
    return None


miss = 0
bad = 0
total = 0
for t in targets:
    doc = yaml.safe_load(io.open(os.path.join(LIVE, t), encoding="utf-8"))
    for a in doc["assertions"]:
        for s in a["sources"]:
            total += 1
            ln = find_line(s["source_id"], s["locator"], s["quote"])
            if ln is None:
                miss += 1
                print("MISS  %-30s %-40s %s" % (t, s["locator"], s["quote"][:30]))
                continue
            if s["quote"] not in ln:
                bad += 1
                print("BAD   %-30s %-40s" % (t, s["locator"]))
                print("       quote : %s" % s["quote"][:70])
                print("       line  : %s" % ln.strip()[:110])

print()
print("断言来源条目 总=%d  locator 行取不到=%d  quote 不在该行=%d" % (total, miss, bad))

# ---- 阳性对照（内存里做，不落盘）：证这个判据分辨得出张冠李戴 ----
print()
print("--- 阳性对照 ---")
allpairs = []
for t in targets:
    d = yaml.safe_load(io.open(os.path.join(LIVE, t), encoding="utf-8"))
    for a in d["assertions"]:
        for s in a["sources"]:
            allpairs.append((t, s))
# 找一对：同一来源文件、但 locator 不同的两条引文
pair = None
for i in range(len(allpairs)):
    for j in range(i + 1, len(allpairs)):
        a, b = allpairs[i][1], allpairs[j][1]
        if a["source_id"] == b["source_id"] and a["locator"] != b["locator"]:
            pair = (a, b)
            break
    if pair:
        break
if pair:
    a, b = pair
    ln = find_line(a["source_id"], a["locator"], b["quote"])   # A 的 locator ＋ B 的 quote
    print("P1 拿 A 的 locator 配 B 的 quote -> 取到行:", ln is not None,
          "；quote 在该行:", (ln is not None and b["quote"] in ln), "（期望 True / False ⇒ 能判 BAD）")
    print("     A.locator=%s" % a["locator"])
    print("     B.locator=%s" % b["locator"])
else:
    print("P1 没找到同来源、不同 locator 的一对，跳过")
a0 = allpairs[0][1]
bogus_loc = a0["locator"].split("#")[0] + "#village_Z9_9"      # 不存在的 key，逼它走回退
ln = find_line(a0["source_id"], bogus_loc, "这句话绝不在来源文件里")
print("P2 不存在的 key ＋ 文件里没有的话 -> 取到行:", ln is not None, "（期望 False ⇒ 能判 MISS）")
ln = find_line(a0["source_id"], a0["locator"], a0["quote"])
print("P3 原样再跑一遍 -> quote 在该行:", (ln is not None and a0["quote"] in ln), "（期望 True）")
