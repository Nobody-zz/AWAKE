# -*- coding: utf-8 -*-
"""临时：输出 类别 × 子域 × 子类token 交叉表 + 子域→token 配对。"""
import io, os, glob, sys, json
from collections import Counter, defaultdict

ROOT = "D:/AWAKE-Dev/AWAKE"
TAX = os.path.join(ROOT, "docs/worldbook-studio-plan/knowledge-taxonomy.v1.json")
AUTH = os.path.join(ROOT, "docs/worldbook-migration/projection/authoring-out")

tax = json.load(io.open(TAX, encoding="utf-8"))
sub2dom = {}
sub_label = {}
sub_help = {}
dom_label = {}
dom_help = {}
sub_examples = {}
for d in tax["domains"]:
    dom_label[d["id"]] = d["label"]["zh-CN"]
    dom_help[d["id"]] = d["help"]["zh-CN"]
    for s in d["subdomains"]:
        sub2dom[s["id"]] = d["id"]
        sub_label[s["id"]] = s["label"]["zh-CN"]
        sub_help[s["id"]] = s["help"]["zh-CN"]
        sub_examples[s["id"]] = s.get("examples", {}).get("zh-CN", [])

sd_tok = defaultdict(Counter)
sd_n = Counter()
dom_n = Counter()
for p in sorted(glob.glob(os.path.join(AUTH, "*.yaml"))):
    d = yaml_d = None
    import yaml
    y = yaml.safe_load(io.open(p, encoding="utf-8"))
    if not str(y.get("id", "")).startswith("doc."):
        continue
    dom = y.get("domain", ""); sd = y.get("subdomain", "")
    tok = y["id"].split(".", 2)[2].split("-", 1)[0]
    sd_tok[sd][tok] += 1
    sd_n[sd] += 1
    dom_n[dom] += 1

print("=== 子域 → 子类token 配对（已用子域）===")
for sd in sorted(sd_tok):
    toks = ", ".join(f"{t}({n})" for t, n in sd_tok[sd].most_common())
    print(f"  {sub2dom[sd]:<10} {sd:<20} {sub_label[sd]:<6} n={sd_n[sd]:<4} token: {toks}")

print("\n=== 未用子域（预留）===")
for d in tax["domains"]:
    for s in d["subdomains"]:
        if s["id"] not in sd_n:
            print(f"  {d['id']:<10} {s['id']:<20} {s['label']['zh-CN']:<6} {s['help']['zh-CN']}")

print("\n=== 已用 token 汇总 ===")
tok_total = Counter()
for sd, c in sd_tok.items():
    for t, n in c.items():
        tok_total[t] += n
for t, n in tok_total.most_common():
    print(f"  {t:<12} {n}")
