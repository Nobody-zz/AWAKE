# -*- coding: utf-8 -*-
"""别名存活探针：档里写的 aliases，有多少真的进了编译产物 runtime.json 的 keywords（＝运行时检索面）。

判据（硬）：编译产物 = 检索面的唯一真值。档里写了、keywords 里没有 = 死条（白写）。
分类沿用 _alias_audit：
  A 含游戏实体 id 模式      B 别名 == 别档 title（跨档挂名）      C 别名 == 本档 title（冗余）
"""
import io
import json
import os
import re
from collections import Counter, defaultdict

import yaml

ROOT = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1"
AO = os.path.join(ROOT, "authoring")
PKG = os.path.join(ROOT, "compiled", "geo1-v18-status-trust")
RUNTIME = os.path.join(PKG, "runtime.json")

PAT_ID = re.compile(r"(village_|castle_|town_|Settlement\.|Culture\.|Clan\.|Kingdom\.|NPCCharacter\.|Item\.|_EW\d|_ES\d|_B\d+_|_S\d+_|_[A-Z]{2}\d+_\d+)")

# ---- 1. 读编译产物，建 id -> keywords ----
rt = json.load(io.open(RUNTIME, encoding="utf-8"))
if not isinstance(rt, dict):
    raise SystemExit("runtime.json 顶层不是 dict：%s" % type(rt))
print("runtime.json 顶层 keys：%s" % list(rt.keys()))
entries = None
for k in ("entries", "Entries", "documents", "Documents"):
    if isinstance(rt.get(k), list):
        entries = rt[k]
        print("entries 字段 = %s，条数 %d" % (k, len(entries)))
        break
if entries is None:
    raise SystemExit("找不到 entries 列表，顶层：%s" % list(rt.keys()))
print("entry[0] keys：%s" % list(entries[0].keys()))
k0 = None
for k in ("keywords", "Keywords"):
    if k in entries[0]:
        k0 = k
        break
print("keywords 字段 = %s" % k0)
print("entry[0] id=%s" % entries[0].get("id"))
print("entry[0] keywords=%s" % json.dumps(entries[0].get(k0), ensure_ascii=False))
print("=" * 76)

def to_doc_id(eid):
    """产物 id awake:entry:<tail>  <->  档 id doc.<tail>"""
    s = str(eid)
    for pre in ("awake:entry:", "doc."):
        if s.startswith(pre):
            s = s[len(pre):]
            break
    return "doc." + s


kw_of = {}
for e in entries:
    eid = e.get("id")
    if not eid:
        continue
    kw = e.get(k0) or []
    kw_of[to_doc_id(eid)] = set(str(x).strip() for x in kw if str(x).strip())

# ---- 2. 读现役档 ----
docs = {}
for f in sorted(os.listdir(AO)):
    if not f.endswith(".yaml"):
        continue
    try:
        d = yaml.safe_load(io.open(os.path.join(AO, f), encoding="utf-8"))
    except Exception:
        continue
    if not isinstance(d, dict) or not str(d.get("id", "")).startswith("doc."):
        continue
    docs[d["id"]] = (f, d)

title2docs = defaultdict(list)
for did, (f, d) in docs.items():
    t = (d.get("title") or {}).get("zh-CN", "")
    if t:
        title2docs[t].append(did)

print("现役档 %d 个；编译产物 %d 条" % (len(docs), len(kw_of)))
print("档 id 与产物 id 对不上（档里有、产物没有）：%d" % len(set(docs) - set(kw_of)))
print("产物里有、档里没有（动态/派生档）：%d" % len(set(kw_of) - set(docs)))
print("=" * 76)

# ---- 3. 逐条别名判存活 ----
stat = Counter()
cls_total = Counter()
cls_dead = Counter()
samples = defaultdict(list)
per_doc_dead = []

for did, (f, d) in sorted(docs.items()):
    al = d.get("aliases") or {}
    zh = al.get("zh-CN") or []
    en = al.get("en") or []
    t_zh = (d.get("title") or {}).get("zh-CN", "")
    t_en = (d.get("title") or {}).get("en", "")
    kw = kw_of.get(did)
    if kw is None:
        stat["档无产物"] += 1
        continue
    dead = []
    alive = []
    for a in [str(x).strip() for x in (zh + en) if str(x).strip()]:
        (alive if a in kw else dead).append(a)
    # 归类
    for a in dead:
        cls = None
        if PAT_ID.search(a):
            cls = "A"
        elif a in title2docs and did not in title2docs[a]:
            cls = "B"
        elif a in (t_zh, t_en):
            cls = "C"
        if cls:
            cls_total[cls] += 1
            cls_dead[cls] += 1
        else:
            cls_total["其他"] += 1
            cls_dead["其他"] += 1
        if len(samples[cls or "其他"]) < 10:
            samples[cls or "其他"].append((did, a))
    # 分类别总量（含存活）
    for a in [str(x).strip() for x in (zh + en) if str(x).strip()]:
        if PAT_ID.search(a):
            cls_total.setdefault("A_alive_check", 0)
    stat["总别名"] += len(alive) + len(dead)
    stat["存活"] += len(alive)
    stat["死条"] += len(dead)
    if dead:
        per_doc_dead.append((did, len(dead), len(alive) + len(dead), dead))

print("别名总条数 %d：存活 %d / 死条 %d（死条率 %.1f%%）" % (
    stat["总别名"], stat["存活"], stat["死条"],
    100.0 * stat["死条"] / max(1, stat["总别名"])))
print("档无产物：%d" % stat["档无产物"])
print()
print("按类看死条（分母＝该类死条数；注意：只统计了『死』的，未含『活』的同类）")
for k in ("A", "B", "C", "其他"):
    print("  %-4s 死条 %d" % (k, cls_dead.get(k, 0)))
    for did, a in samples.get(k, []):
        print("        %-44s %s" % (did, a))
print()
print("=" * 76)
print("死条最多的 20 档")
print("=" * 76)
for did, nd, na, dead in sorted(per_doc_dead, key=lambda x: -x[1])[:20]:
    print("  %-46s 死 %2d / 共 %2d   %s" % (did, nd, na, dead[:4]))
