# -*- coding: utf-8 -*-
"""三类别名 · 检索后果实测。

复刻运行时检索（src/WorldKnowledgeQueryService.cs:131 + :449 MatchQuality）：
  Search      : Id / Title / Keywords 上的双向子串命中（OrdinalIgnoreCase）
  MatchQuality: title 全等→Rank0；keyword 全等→Rank1；title 子串→Rank2；keyword 子串→Rank3
输入 = 编译产物 runtime.json（唯一真值）。
对每个查询，跑「现状」与「模拟删掉该类别名后」两遍，看命中集与 Rank 的差异。
"""
import io
import json
import os
import re
from collections import defaultdict

import yaml

ROOT = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1"
AO = os.path.join(ROOT, "authoring")
RUNTIME = os.path.join(ROOT, "compiled", "geo1-v18-status-trust", "runtime.json")
REG = r"D:/AWAKE-Dev/AWAKE/docs/mappings/persona-entity/generations/b1-7590085e3662512d7c1c6646c529694bda9850146234827b2dafbd9f20c6a2b3/entity-registry.v1.json"
PAT_ID = re.compile(r"(village_|castle_|town_|Settlement\.|Culture\.|Clan\.|Kingdom\.|NPCCharacter\.|Item\.|_EW\d|_ES\d|_B\d+_|_S\d+_|_[A-Z]{2}\d+_\d+)")

# --- 锚点可读名 ---
reg = json.load(io.open(REG, encoding="utf-8"))
anchor_names = {}
for e in reg.get("entities", []):
    eid = (e.get("entity_id") or "").strip().lower()
    vals = []
    for f in ("display_name_zh", "english_name", "family_name_zh"):
        v = e.get(f)
        if isinstance(v, str) and v.strip():
            vals.append(v.strip())
    for a in (e.get("aliases") or []):
        if isinstance(a, str) and a.strip():
            vals.append(a.strip())
    if eid and vals:
        anchor_names[eid] = vals

# --- 真产物 ---
rt = json.load(io.open(RUNTIME, encoding="utf-8"))
entries = []
for e in rt["entries"]:
    s = str(e.get("id", ""))
    for pre in ("awake:entry:", "doc."):
        if s.startswith(pre):
            s = s[len(pre):]
            break
    entries.append({
        "id": "doc." + s,
        "title": e.get("title") or {},
        "keywords": [str(x).strip() for x in (e.get("keywords") or []) if str(x).strip()],
    })

def title_of(e):
    t = e["title"]
    for k in ("zh-CN", "en"):
        v = t.get(k)
        if isinstance(v, str) and v.strip():
            return v.strip()
    return ""

def search(ents, text):
    """返回 [(rank, id, title)]，按 Rank 再 Id 排序（照运行时）"""
    hits = []
    for e in ents:
        tid = e["id"]
        tt = title_of(e)
        kws = e["keywords"]
        if (text.lower() not in tid.lower()) and (text.lower() not in tt.lower()) \
           and not any(text.lower() in k.lower() for k in kws):
            continue
        # MatchQuality
        if tt and tt.lower() == text.lower():
            r = 0
        else:
            exact_kw = [k for k in kws if k.lower() == text.lower()]
            if exact_kw:
                r = 1
            elif tt and (text.lower() in tt.lower() or tt.lower() in text.lower()):
                r = 2
            else:
                r = 3
        hits.append((r, tid, tt))
    return sorted(hits, key=lambda x: (x[0], x[1]))

def brief(hits, n=8):
    if not hits:
        return "  （无命中）"
    return "\n".join("    Rank%d  %-46s %s" % (r, i, t) for r, i, t in hits[:n])

# --- 读现役档，建「删哪条别名 → keywords 里去掉哪个串」的模拟 ---
docs = {}
for f in sorted(os.listdir(AO)):
    if not f.endswith(".yaml"):
        continue
    d = yaml.safe_load(io.open(os.path.join(AO, f), encoding="utf-8"))
    if isinstance(d, dict) and str(d.get("id", "")).startswith("doc."):
        docs[d["id"]] = d

def loc_vals(obj):
    out = []
    if not isinstance(obj, dict):
        return out
    for k, v in obj.items():
        if isinstance(v, str) and v.strip():
            out.append(v.strip())
        elif isinstance(v, list):
            out += [x.strip() for x in v if isinstance(x, str) and x.strip()]
    return out

def classify(did, a, title2docs):
    d = docs[did]
    tv = set(loc_vals(d.get("title") or {}))
    if PAT_ID.search(a):
        return "A"
    if a in title2docs and did not in title2docs[a]:
        return "B"
    if a in tv:
        return "C"
    return None

title2docs = defaultdict(list)
for did, d in docs.items():
    t = (d.get("title") or {}).get("zh-CN", "")
    if t:
        title2docs[t].append(did)

# --- 模拟删除：该类别名里「唯一来源=别名」的串，从 keywords 去掉 ---
def build_after(cls):
    ents = json.loads(json.dumps(entries))
    byid = {e["id"]: e for e in ents}
    removed = 0
    for did, d in docs.items():
        if did not in byid:
            continue
        tv = set(loc_vals(d.get("title") or {}))
        anc = set()
        for eid in (d.get("entity_ids") or []):
            anc.update(anchor_names.get(str(eid).strip().lower(), []))
        for a in loc_vals(d.get("aliases") or {}):
            if classify(did, a, title2docs) != cls:
                continue
            if a in tv or a in anc:
                continue  # 有其他来源，删了 keywords 不变
            kw = byid[did]["keywords"]
            if a in kw:
                kw.remove(a)
                removed += 1
    return ents, removed

QUERIES = [
    ("A 类：拿实体 id 问", "castle_B3", "A"),
    ("B 类：拿下辖村名问", "托·梅利纳", "B"),
    ("B 类：拿下辖村名问", "德鲁伊莫尔", "B"),
    ("C 类：拿本档标题问", "德鲁伊莫尔堡", "C"),
]

for label, q, cls in QUERIES:
    print("=" * 78)
    print("%s　查询「%s」" % (label, q))
    print("  ── 现状 ──")
    print(brief(search(entries, q)))
    after, removed = build_after(cls)
    print("  ── 删掉 %s 类别名后（该类共移除 keywords 串 %d 个）──" % (cls, removed))
    print(brief(search(after, q)))
