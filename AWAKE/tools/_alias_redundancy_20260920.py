# -*- coding: utf-8 -*-
"""别名冗余探针：每条别名的『唯一来源』是谁。

编译器 K1 规则（RuntimePackageCompiler.cs:112-122）：
  keywords = title(多语言) → aliases(多语言) → entity_ids 的「可读名」（去重，保留首次）
⇒ 一条别名串若已在 title 或锚点可读名里，它对检索面**零贡献**（删了 keywords 不变）。
   若只在 aliases 里，才是真检索词。

阳性对照：本脚本重算的 keywords 必须与真产物 runtime.json 的 keywords **完全相同**，
          否则模型错、结论作废。
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
REG = r"D:/AWAKE-Dev/AWAKE/docs/mappings/persona-entity/generations/b1-7590085e3662512d7c1c6646c529694bda9850146234827b2dafbd9f20c6a2b3/entity-registry.v1.json"

PAT_ID = re.compile(r"(village_|castle_|town_|Settlement\.|Culture\.|Clan\.|Kingdom\.|NPCCharacter\.|Item\.|_EW\d|_ES\d|_B\d+_|_S\d+_|_[A-Z]{2}\d+_\d+)")

# ---- 锚点可读名 ----
reg = json.load(io.open(REG, encoding="utf-8"))
anchor_names = {}
for e in reg.get("entities", []):
    eid = (e.get("entity_id") or "").strip().lower()
    if not eid:
        continue
    vals = []
    for f in ("display_name_zh", "english_name", "family_name_zh"):
        v = e.get(f)
        if isinstance(v, str) and v.strip():
            vals.append(v.strip())
    for a in (e.get("aliases") or []):
        if isinstance(a, str) and a.strip():
            vals.append(a.strip())
    if vals:
        anchor_names[eid] = list(dict.fromkeys(vals))
print("锚点登记表：entities %d，有可读名的 %d" % (len(reg.get("entities", [])), len(anchor_names)))

# ---- 真产物 ----
rt = json.load(io.open(RUNTIME, encoding="utf-8"))
kw_of = {}
for e in rt["entries"]:
    s = str(e.get("id", ""))
    for pre in ("awake:entry:", "doc."):
        if s.startswith(pre):
            s = s[len(pre):]
            break
    kw_of["doc." + s] = [str(x).strip() for x in (e.get("keywords") or []) if str(x).strip()]

def loc_vals(obj):
    """多语言对象 -> 值列表（字符串与数组都算一个词），保持出现顺序"""
    out = []
    if not isinstance(obj, dict):
        return out
    for k, v in obj.items():
        if isinstance(v, str) and v.strip():
            out.append(v.strip())
        elif isinstance(v, list):
            for x in v:
                if isinstance(x, str) and x.strip():
                    out.append(x.strip())
    return out

def recompute(d):
    kw, seen = [], set()
    for v in loc_vals(d.get("title") or {}) + loc_vals(d.get("aliases") or {}):
        if v not in seen:
            seen.add(v)
            kw.append(v)
    for eid in (d.get("entity_ids") or []):
        for name in anchor_names.get(str(eid).strip().lower(), []):
            if name not in seen:
                seen.add(name)
                kw.append(name)
    return kw

# ---- 读档 + 阳性对照 ----
docs = {}
for f in sorted(os.listdir(AO)):
    if not f.endswith(".yaml"):
        continue
    try:
        d = yaml.safe_load(io.open(os.path.join(AO, f), encoding="utf-8"))
    except Exception:
        continue
    if isinstance(d, dict) and str(d.get("id", "")).startswith("doc."):
        docs[d["id"]] = (f, d)

title2docs = defaultdict(list)
for did, (f, d) in docs.items():
    t = (d.get("title") or {}).get("zh-CN", "")
    if t:
        title2docs[t].append(did)

match = mismatch = 0
for did, (f, d) in docs.items():
    real = kw_of.get(did)
    if real is None:
        continue
    if recompute(d) == real:
        match += 1
    else:
        mismatch += 1
print("阳性对照：重算 keywords == 真产物 的档 %d；不等 %d" % (match, mismatch))
print("=" * 76)

# ---- 逐条别名判来源 ----
src_stat = Counter()
cls_stat = defaultdict(Counter)
samples = defaultdict(list)
redundant_docs = Counter()

for did, (f, d) in sorted(docs.items()):
    t_vals = set(loc_vals(d.get("title") or {}))
    a_vals = set(loc_vals(d.get("aliases") or {}))
    anc = set()
    for eid in (d.get("entity_ids") or []):
        anc.update(anchor_names.get(str(eid).strip().lower(), []))
    t_zh = (d.get("title") or {}).get("zh-CN", "")
    t_en = (d.get("title") or {}).get("en", "")
    for a in sorted(a_vals):
        if a in t_vals:
            src = "冗余·title 已覆盖"
        elif a in anc:
            src = "冗余·锚点已覆盖"
        else:
            src = "唯一来源=别名"
        src_stat[src] += 1
        if a in title2docs and did not in title2docs[a]:
            cls = "B 跨档挂名"
        elif PAT_ID.search(a):
            cls = "A 含实体id"
        elif a in (t_zh, t_en):
            cls = "C 等于本档title"
        else:
            cls = "其他"
        cls_stat[cls][src] += 1
        if len(samples[cls]) < 8:
            samples[cls].append((did, a, src))
        if src != "唯一来源=别名":
            redundant_docs[did] += 0

print("全部别名 %d 条，按唯一来源：" % sum(src_stat.values()))
for k, v in src_stat.most_common():
    print("  %-22s %d" % (k, v))
print()
print("=" * 76)
print("按类别 × 来源")
print("=" * 76)
for cls in ("A 含实体id", "B 跨档挂名", "C 等于本档title", "其他"):
    c = cls_stat[cls]
    tot = sum(c.values())
    red = c.get("冗余·title 已覆盖", 0) + c.get("冗余·锚点已覆盖", 0)
    print("  %-16s 共 %4d  冗余可删 %4d  真检索词 %4d" % (cls, tot, red, c.get("唯一来源=别名", 0)))
    for did, a, src in samples[cls][:6]:
        print("        %-42s %-14s %s" % (did, src, a))
