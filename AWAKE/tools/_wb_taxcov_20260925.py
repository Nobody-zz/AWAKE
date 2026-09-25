"""世界书知识条目 × 分类法 覆盖普查（09-25）
口径：authoring 源档的 domain/subdomain 字段 vs knowledge-taxonomy.v1.json 的 43 个子域。
只用 Write 落文件执行（bash heredoc 会吃反斜杠，已踩过）。
"""
import os
import re
import json
import collections

AU = r"D:\AWAKE-Dev\AWAKE\tools\worldbook-studio\workspace\full-geo1\authoring"
TAX = r"D:\AWAKE-Dev\AWAKE\docs\worldbook-studio-plan\knowledge-taxonomy.v1.json"

tax = json.load(open(TAX, encoding="utf-8"))
valid = set()
for dom in tax["domains"]:
    for s in dom.get("subdomains", []):
        valid.add((dom["id"], s["id"]))

cnt = collections.Counter()
no_domain = []
offtax = []

for f in sorted(os.listdir(AU)):
    if not f.endswith(".yaml"):
        continue
    t = open(os.path.join(AU, f), encoding="utf-8").read()
    md = re.search(r"^domain:\s*(\S+)\s*$", t, re.M)
    ms = re.search(r"^subdomain:\s*(\S+)\s*$", t, re.M)
    if not md:
        no_domain.append(f)
        continue
    key = (md.group(1), ms.group(1) if ms else "(none)")
    cnt[key] += 1
    if key not in valid:
        offtax.append((f, key))

print("=== 实际使用的 (domain, subdomain) ===")
for k, v in cnt.most_common():
    flag = "" if k in valid else "   <<不在分类法内"
    print("  %-12s / %-26s %4d%s" % (k[0], k[1], v, flag))

print()
print("=== 分类法有、实档为 0 的子域 ===")
miss = [x for x in sorted(valid) if x not in cnt]
for d, s in miss:
    print("  [空] %s/%s" % (d, s))
print("  => 共 %d/%d 个子域为空" % (len(miss), len(valid)))

print()
print("=== 解析不到 domain 的档 ===", len(no_domain))
for f in no_domain[:20]:
    print("  ", f)

print()
print("=== domain/subdomain 不在分类法内的档 ===", len(offtax))
for f, k in offtax[:20]:
    print("  ", f, k)
