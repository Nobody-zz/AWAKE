# -*- coding: utf-8 -*-
"""取交付报告需要的一组读数（只读，不改任何东西）。"""
import json, io, os, collections

ROOT = r"D:\AWAKE-Dev\AWAKE"
PKG = os.path.join(ROOT, "tools/worldbook-studio/workspace/full-geo1/compiled/geo1-v22-links")
PREV = os.path.join(ROOT, "tools/worldbook-studio/workspace/full-geo1/compiled/geo1-v21-dark")

out = io.StringIO()
def p(*a):
    print(*a)
    print(*a, file=out)

sr = json.load(open(os.path.join(PKG, "source-report.json"), encoding="utf-8"))
p("== source-report ==")
p(json.dumps(sr, ensure_ascii=False, indent=2))

v = json.load(open(os.path.join(PKG, "validation.json"), encoding="utf-8"))
diags = v.get("Diagnostics") or v.get("diagnostics") or []

def fld(d, *names):
    for n in names:
        if n in d:
            return d[n]
    return None

errs = [d for d in diags if (fld(d, "Severity", "severity") or "").lower() == "error"]
warns = [d for d in diags if (fld(d, "Severity", "severity") or "").lower() == "warning"]
p("\n== validation ==")
p("total=%d error=%d warning=%d" % (len(diags), len(errs), len(warns)))
p("codes: %s" % sorted(set(fld(d, "Code", "code") for d in diags)))
p("Valid=%s" % v.get("Valid", v.get("valid")))

def load_runtime(path):
    return json.load(open(os.path.join(path, "runtime.json"), encoding="utf-8"))

cur = load_runtime(PKG)
prev = load_runtime(PREV)

def entries(doc):
    return doc.get("entries") or doc.get("Entries") or []

ce = entries(cur)
pe = entries(prev)
p("\n== entries ==")
p("cur=%d prev=%d" % (len(ce), len(pe)))

# 带 links 的条目
with_links = [e for e in ce if (e.get("extensions") or {}).get("links")]
p("带 extensions.links 的条目 = %d" % len(with_links))
edge_count = sum(len((e.get("extensions") or {}).get("links") or []) for e in with_links)
p("包内 links 边总数 = %d" % edge_count)

# 端点全在包内
ids = set(e.get("id") for e in ce)
bad = []
for e in with_links:
    for l in e["extensions"]["links"]:
        if l["to"] not in ids:
            bad.append((e["id"], l["to"]))
p("端点不在包内的边 = %d" % len(bad))

# 与上一包逐条比：只有 extensions 变
def strip_ext(e):
    d = dict(e)
    d.pop("extensions", None)
    return json.dumps(d, ensure_ascii=False, sort_keys=True)

mm = {e.get("id"): strip_ext(e) for e in pe}
changed_only_ext = 0
changed_other = []
for e in ce:
    i = e.get("id")
    if i not in mm:
        continue
    if strip_ext(e) != mm[i]:
        changed_other.append(i)
    else:
        if (e.get("extensions") or {}).get("links"):
            changed_only_ext += 1
p("\n== 与 v21-dark 逐条比 ==")
p("除 extensions 外有变化的条目 = %d %s" % (len(changed_other), changed_other[:5]))
p("id 集合相同 = %s" % (ids == set(mm.keys())))
new_ids = ids - set(mm.keys())
gone_ids = set(mm.keys()) - ids
p("新增 id=%d 消失 id=%d" % (len(new_ids), len(gone_ids)))

# 边表 registry 读数
lr = json.load(open(os.path.join(ROOT, "docs/worldbook-studio-plan/link-registry.v1.json"), encoding="utf-8"))
p("\n== link-registry.v1.json ==")
p("registry_version=%s edges=%d" % (lr.get("registry_version"), len(lr.get("edges", []))))
buckets = collections.Counter(e["bucket"] for e in lr["edges"])
p("bucket 分布: %s" % dict(buckets))
usable = collections.Counter()
for e in lr["edges"]:
    for u in e.get("usableAs", []):
        usable[u] += 1
p("usableAs 分布: %s" % dict(usable))
via = collections.Counter(e["strength"] for e in lr["edges"])
p("strength 分布: %s" % dict(via))
srcs = set(e["from"] for e in lr["edges"])
p("有出边的档 = %d" % len(srcs))

v3 = json.load(open(os.path.join(ROOT, "docs/mappings/worldbook-should-link/20260920/should-link.v3.json"), encoding="utf-8"))
def count_edges(o):
    if isinstance(o, list):
        return len(o)
    if isinstance(o, dict):
        for k in ("edges", "links", "items"):
            if k in o and isinstance(o[k], list):
                return len(o[k])
        return None
    return None
p("\n== should-link.v3.json ==")
p("top keys: %s" % list(v3.keys())[:12] if isinstance(v3, dict) else type(v3))
p("边数（自动探测）= %s" % count_edges(v3))

open(os.path.join(ROOT, "tools/_link_report_numbers_20260920.txt"), "w", encoding="utf-8").write(out.getvalue())
print("\n[written] tools/_link_report_numbers_20260920.txt")
