# -*- coding: utf-8 -*-
"""按「原文带存疑标记」扫全库：有多少档的官方原文里本来就写着『据说/传说/人们相信』。

只读。判据可核：标记必须出现在该档 locator 指向的那一行原文里。
"""
import glob
import io
import os
import sys
from collections import Counter

import yaml

AUTH = r"D:\AWAKE-Dev\AWAKE\docs\worldbook-migration\projection\authoring-out"
SRC = r"D:\AWAKE-Dev\AWAKE\tools\worldbook-studio\workspace\full-geo1\authoring\sources"

# 来源登记：source_id -> locator_root 的原文行表
src_lines = {}
src_root = {}
for f in glob.glob(os.path.join(SRC, "source-*.yaml")):
    d = yaml.safe_load(io.open(f, encoding="utf-8"))
    root = d.get("locator_root")
    if not root:
        continue
    p = os.path.join(SRC, root)
    if not os.path.exists(p):
        continue
    src_root[d.get("source_id")] = root
    lines = io.open(p, encoding="utf-8").read().splitlines()
    src_lines[d.get("source_id")] = lines
    print("source %-46s %-40s %d 行" % (d.get("source_id"), root, len(lines)))

MARK = ["据说", "据传说", "传说", "据传", "听说", "让人们相信", "人们相信",
        "没有任意两种版本", "宣传说法", "故事", "口里的传言", "名声", "老辈"]

def find_line(sid, locator, quote):
    """locator 形如 bannerlord.villages#village_B2_1 / chronicle-x.txt#/Variants/0/Content

    ⚠️ 必须精确比对 id 末段：子串匹配会让 `village_B2_1` 命中 `castle_village_B2_1`
    （同一来源文件里两行都有），张冠李戴、静默少报。09-19 实测（迪安托格麦尔整条被判漏）。
    编年史档的 locator 是 JSON 路径、行内找不到 ⇒ 回退用本档自己的 `quote` 定位。
    """
    lines = src_lines.get(sid) or []
    if not lines:
        return None
    if "#" in locator:
        key = locator.split("#", 1)[1].strip("/")
        parts = [p for p in key.split("/") if p]
        if parts and not (parts[0] == "Variants" or len(parts) > 1):
            tok = parts[0]
            for ln in lines:
                if "=>" in ln and ln.split("=>", 1)[0].strip().split(".")[-1] == tok:
                    return ln
    # 回退：用本档 quote 的起始片段在文件里找行
    if quote:
        probe = quote.strip()[:24]
        for ln in lines:
            if probe and probe in ln.replace("\\n", " "):
                return ln
    return None

hit = Counter()
hit_docs = []
scanned = 0
no_line = 0
miss = []
for path in sorted(glob.glob(os.path.join(AUTH, "*.yaml"))):
    doc = yaml.safe_load(io.open(path, encoding="utf-8"))
    if not isinstance(doc, dict):
        continue
    scanned += 1
    loc = None
    q0 = ""
    for s in (doc.get("sources") or []):
        if isinstance(s, dict) and s.get("locator"):
            loc = (str(s["source_id"]), str(s["locator"]))
            q0 = str(s.get("quote") or "")
            break
    if not loc:
        for a in (doc.get("assertions") or []):
            if isinstance(a, dict):
                for s in (a.get("sources") or []):
                    if isinstance(s, dict) and s.get("locator"):
                        loc = (str(s["source_id"]), str(s["locator"]))
                        q0 = str(s.get("quote") or "")
                        break
            if loc:
                break
    if not loc:
        no_line += 1
        continue
    line = find_line(loc[0], loc[1], q0)
    if line is None:
        no_line += 1
        miss.append(os.path.basename(path))
        continue
    marks = sorted({m for m in MARK if m in line})
    if marks:
        hit[doc.get("subdomain")] += 1
        hit_docs.append((os.path.basename(path), doc.get("subdomain"), marks))

print()
print("扫过档数:", scanned, " 取不到原文行:", no_line)
print()
print("=== 原文带存疑标记的档，按 subdomain ===")
tot = 0
for k, v in hit.most_common():
    print("  %-22s %d" % (k, v))
    tot += v
print("  合计:", tot)
print()
print("=== 前 40 条明细 ===")
for name, sub, marks in hit_docs[:40]:
    print("  %-46s %-16s %s" % (name, sub, marks))
print()

geo = [x for x in hit_docs if x[1] == "settlements"]
print("=== settlements（聚落）里够格的 ===", len(geo))
for name, sub, marks in geo:
    print("  %-46s %s" % (name, marks))
print()
print("=== 取不到原文行的档（前 50） ===", len(miss))
for n in miss[:50]:
    print("   ", n)
