# -*- coding: utf-8 -*-
"""临时：梳理分类体系三源对照
 A) 权威定义 knowledge-taxonomy.v1.json（域 / 子域 定义）
 B) schema awake.worldbook.authoring.v1.schema.json（domain / subdomain 枚举）
 C) 实际 448 档（domain / subdomain / doc_id 第三段 token）
输出：taxonomy 中未用（预留）、实际用但未登记（漂移）。
"""
import io, os, json, glob, sys
from collections import Counter, defaultdict

try:
    import yaml
except ImportError:
    sys.exit("need pyyaml")

ROOT = "D:/AWAKE-Dev/AWAKE"
TAX = os.path.join(ROOT, "docs/worldbook-studio-plan/knowledge-taxonomy.v1.json")
SCH = os.path.join(ROOT, "docs/worldbook-studio-plan/awake.worldbook.authoring.v1.schema.json")
AUTH = os.path.join(ROOT, "docs/worldbook-migration/projection/authoring-out")

print("=" * 70)
print("A) 权威定义 knowledge-taxonomy.v1.json")
print("=" * 70)
tax = json.load(io.open(TAX, encoding="utf-8"))
print("schema_version:", tax.get("schema_version"), "| taxonomy_version:", tax.get("taxonomy_version"))
tax_dom = {}
tax_subdom = defaultdict(list)
for d in tax["domains"]:
    did = d["id"]
    tax_dom[did] = d["label"]["zh-CN"]
    print(f"\n[{did}] {d['label']['zh-CN']} — {d['help']['zh-CN']}")
    print(f"    examples: {d.get('examples', {}).get('zh-CN')}")
    for ch in d.get("conflict_hints", {}).get("zh-CN", []):
        print(f"    ⚠ {ch}")
    for s in d["subdomains"]:
        tax_subdom[did].append(s["id"])
        print(f"    - {s['id']:<14} {s['label']['zh-CN']:<6} {s['help']['zh-CN']}")

print("\n" + "=" * 70)
print("B) schema domain / subdomain 枚举")
print("=" * 70)
sch = json.load(io.open(SCH, encoding="utf-8"))

def find_enum(node, key, trail=""):
    out = []
    if isinstance(node, dict):
        for k, v in node.items():
            if k == key and isinstance(v, dict) and "enum" in v:
                out.append((trail + "/" + k, v["enum"]))
            out += find_enum(v, key, trail + "/" + str(k))
    elif isinstance(node, list):
        for i, v in enumerate(node):
            out += find_enum(v, key, f"{trail}[{i}]")
    return out

for k in ("domain", "subdomain"):
    for path, enum in find_enum(sch, k):
        print(f"{k} @ {path}\n    {enum}")

print("\n" + "=" * 70)
print("C) 实际 448 档使用情况")
print("=" * 70)
dom_cnt = Counter(); sd_cnt = Counter(); tok_cnt = Counter()
pair = defaultdict(Counter)   # (domain) -> (subdomain) -> n
tok_by_sd = defaultdict(Counter)  # domain -> subdomain -> token counts
files = sorted(glob.glob(os.path.join(AUTH, "*.yaml")))
for p in files:
    d = yaml.safe_load(io.open(p, encoding="utf-8"))
    if not str(d.get("id", "")).startswith("doc."):
        continue
    dom = d.get("domain", ""); sd = d.get("subdomain", "")
    tok = d["id"].split(".", 2)[2].split("-", 1)[0]
    dom_cnt[dom] += 1; sd_cnt[(dom, sd)] += 1; tok_cnt[(dom, tok)] += 1
    pair[dom][sd] += 1
    tok_by_sd[dom][sd] += 1
print("档数:", sum(dom_cnt.values()))
for dom in sorted(dom_cnt, key=lambda x: -dom_cnt[x]):
    print(f"\n[{dom}] total={dom_cnt[dom]}")
    for sd in sorted(pair[dom], key=lambda x: -pair[dom][x]):
        toks = Counter({t: n for (dd, t), n in tok_cnt.items() if dd == dom})
        print(f"    - {sd:<14} n={pair[dom][sd]}")

print("\n域 × 子类 token 全表:")
for (dom, tok), n in sorted(tok_cnt.items()):
    print(f"  {dom:<10} {tok:<12} {n}")

print("\n" + "=" * 70)
print("D) 对照：taxonomy 子域 vs 实际 subdomain")
print("=" * 70)
tax_all_sd = {(d, s) for d, ss in tax_subdom.items() for s in ss}
used_all_sd = set(sd_cnt)
print("taxonomy 有、实际未用（预留）:")
for d, s in sorted(tax_all_sd - used_all_sd):
    print(f"  {d}.{s}")
print("实际用、taxonomy 无（漂移/需登记）:")
for d, s in sorted(used_all_sd - tax_all_sd):
    print(f"  {d}.{s}  n={sd_cnt[(d, s)]}")
print("\ntaxonomy 有、实际已用:")
for d, s in sorted(tax_all_sd & used_all_sd):
    print(f"  {d}.{s}  n={sd_cnt[(d, s)]}")

print("\n" + "=" * 70)
print("E) doc_id 第三段 token（子类）一览")
print("=" * 70)
tok_all = defaultdict(Counter)
for (dom, tok), n in tok_cnt.items():
    tok_all[dom][tok] += n
for dom in sorted(tok_all):
    print(f"  [{dom}] " + ", ".join(f"{t}({n})" for t, n in tok_all[dom].most_common()))
