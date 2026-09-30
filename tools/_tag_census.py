# -*- coding: utf-8 -*-
"""按正典档 id 的第三段前缀（tag）统计覆盖面，并关联 domain/subdomain。
用法： python tools/_tag_census.py
"""
import os
import re
import collections

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, ".."))
SRC = os.path.join(REPO, "AWAKE", "tools", "worldbook-studio",
                   "workspace", "full-geo1", "authoring")

RE_ID = re.compile(r"^id:\s*(\S+)", re.M)
RE_DOM = re.compile(r"^domain:\s*(\S+)", re.M)
RE_SUB = re.compile(r"^subdomain:\s*(\S+)", re.M)

cnt = collections.Counter()
pair = collections.defaultdict(collections.Counter)
sub_tag = collections.defaultdict(collections.Counter)
ids = {}

for fn in sorted(os.listdir(SRC)):
    if not fn.endswith(".yaml"):
        continue
    with open(os.path.join(SRC, fn), encoding="utf-8-sig", errors="replace") as fh:
        txt = fh.read(6000)
    mid = RE_ID.search(txt)
    mdom = RE_DOM.search(txt)
    msub = RE_SUB.search(txt)
    did = mid.group(1) if mid else ""
    dom = mdom.group(1) if mdom else "?"
    sub = msub.group(1) if msub else "?"
    parts = did.split(".")
    tag = parts[2].split("-", 1)[0] if len(parts) >= 3 else "?"
    ids[fn] = did
    cnt[tag] += 1
    pair[tag][f"{dom}/{sub}"] += 1
    sub_tag[sub][tag] += 1

print(f"TOTAL={sum(cnt.values())} TAGS={len(cnt)}")
print("=" * 70)
for t, n in cnt.most_common():
    ps = "; ".join(f"{k}:{v}" for k, v in pair[t].most_common())
    print(f"{n:4d}  {t:14s} -> {ps}")
print("=" * 70)
print("SUBDOMAIN -> TAGS")
for s, c in sorted(sub_tag.items(), key=lambda kv: -sum(kv[1].values())):
    tot = sum(c.values())
    print(f"{tot:4d}  {s:16s} <- " + ", ".join(f"{k}({v})" for k, v in c.most_common()))
