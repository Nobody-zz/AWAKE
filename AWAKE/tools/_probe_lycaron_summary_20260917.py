# -*- coding: utf-8 -*-
"""看 mines-lycaron 的完整结构，并把正文里的「工程话」普查一遍。"""
import io
import json
import re
import collections

PKG = "ModuleData/Worldbook/packages/calradia/runtime.json"
with io.open(PKG, encoding="utf-8") as fh:
    data = json.load(fh)
entries = data["entries"]

by_id = {}
for e in entries:
    by_id[e.get("id") or ""] = e

t = by_id.get("awake:entry:geography.mines-lycaron")
print("=== 目标条目（缩进打印全文）===")
print(json.dumps(t, ensure_ascii=False, indent=2)[:3000])
print()

# 收正文：把 expressions 里的每段文本摊平
def _flatten(val, out):
    """`title`/`summary`/`text` 都是本地化字典 `{"zh-CN": "..."}`，别只认 str。"""
    if isinstance(val, str):
        if val:
            out.append(val)
    elif isinstance(val, dict):
        for k, v in val.items():
            _flatten(v, out)
    elif isinstance(val, list):
        for v in val:
            _flatten(v, out)


def texts_of(e):
    out = []
    ex = e.get("expressions")
    if isinstance(ex, list):
        for i, item in enumerate(ex):
            if isinstance(item, dict):
                for k in ("text", "detail", "id"):
                    if k in item:
                        buf = []
                        _flatten(item[k], buf)
                        for v in buf:
                            out.append(("expr.%s" % k, i, v))
            elif isinstance(item, str):
                out.append(("expr", i, item))
    for k in ("title", "summary"):
        buf = []
        _flatten(e.get(k), buf)
        for v in buf:
            out.append((k, -1, v))
    return out

print("=== 目标条目的每段文本 ===")
for tag, i, v in texts_of(t):
    print("  %s[%d]: %s" % (tag, i, v[:200]))
print()

# 工程话普查
PATTERNS = [
    ("IMPL", re.compile(r"IMPL")),
    ("§", re.compile(r"§")),
    ("留痕", re.compile(r"留痕")),
    ("TODO/FIXME", re.compile(r"TODO|FIXME|XXX")),
    ("档承载", re.compile(r"档承载")),
    ("英文字段名(chk/gate/PASS)", re.compile(r"\b(chk|PASS|FAIL|gate)\b")),
    ("半角括号里带 .cs/.md/.py", re.compile(r"[\w\-]+\.(cs|md|py|json)\b")),
    ("commit/sha 样式", re.compile(r"\b[0-9a-f]{7,40}\b")),
    ("英文内部域名单词", re.compile(r"\b(economy|geography|politics|military|culture|war|entry|doc)\b")),
]
hits = collections.defaultdict(list)
for e in entries:
    eid = e.get("id") or ""
    for tag, i, v in texts_of(e):
        for name, pat in PATTERNS:
            if pat.search(v):
                hits[name].append((eid, tag, i, v))

print("=== 工程话普查（448 条）===")
for name, _pat in PATTERNS:
    rows = hits.get(name, [])
    print("  %-28s %d 处" % (name, len(rows)))

print()
print("=== 「IMPL | § | 留痕 | 档承载」这四条最硬的，逐条列出（去重）===")
seen = set()
for name in ("IMPL", "§", "留痕", "档承载", "半角括号里带 .cs/.md/.py"):
    for eid, tag, i, v in hits.get(name, []):
        key = (eid, tag, i)
        if key in seen:
            continue
        seen.add(key)
        print("  [%s] %s %s[%d]" % (name, eid, tag, i))
        print("      %s" % v[:300].replace("\n", " "))
