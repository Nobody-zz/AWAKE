# -*- coding: utf-8 -*-
"""量「可信度」这一轴在作者层有没有内容：status / assertions[].kind / expressions[].layer。

只读。用于回答「流言体到底存不存在、现在长什么样」。
"""
import glob
import io
import os
import re
from collections import Counter

ROOT = r"D:\AWAKE-Dev\AWAKE\docs\worldbook-migration\projection\authoring-out"
files = sorted(glob.glob(os.path.join(ROOT, "*.yaml")))
print("作者层档数:", len(files))

import yaml

status_c = Counter()
kind_c = Counter()
layer_c = Counter()
subdomain_c = Counter()
fact_text_eq_quote = 0     # 断言正文 == 引文（原样搬运）
fact_text_quote_ratio = [] # 断言正文与引文的最长公共前缀占比
multi_assert = 0
assert_counts = Counter()
kind_set_per_doc = Counter()
docs_with_interpretation = []
docs_with_rumor_kind = []

for path in files:
    try:
        doc = yaml.safe_load(io.open(path, encoding="utf-8"))
    except Exception:                                # noqa: BLE001
        continue
    if not isinstance(doc, dict):
        continue
    status_c[str(doc.get("status"))] += 1
    subdomain_c[str(doc.get("subdomain"))] += 1

    assertions = doc.get("assertions") or []
    assert_counts[len(assertions)] += 1
    if len(assertions) > 1:
        multi_assert += 1
    kinds = []
    for a in assertions:
        if not isinstance(a, dict):
            continue
        k = str(a.get("kind"))
        kinds.append(k)
        kind_c[k] += 1
        text = ((a.get("text") or {}).get("zh-CN") or "").strip()
        srcs = a.get("sources") or []
        if srcs and isinstance(srcs[0], dict):
            quote = (srcs[0].get("quote") or "").strip()
            if quote:
                if text == quote:
                    fact_text_eq_quote += 1
                n = 0
                for x, y in zip(text, quote):
                    if x == y:
                        n += 1
                    else:
                        break
                fact_text_quote_ratio.append((os.path.basename(path), round(n / max(len(quote), 1), 3)))
        for e in (a.get("expressions") or []):
            if isinstance(e, dict):
                layer_c[str(e.get("layer"))] += 1
    kind_set_per_doc[tuple(sorted(set(kinds)))] += 1
    if "interpretation" in kinds:
        docs_with_interpretation.append(os.path.basename(path))
    if "rumor" in kinds:
        docs_with_rumor_kind.append(os.path.basename(path))

print()
print("=== status（文档级可信度） ===")
for k, v in status_c.most_common():
    print("  %-20s %d" % (k, v))
print()
print("=== assertions[].kind（断言级可信度） ===")
for k, v in kind_c.most_common():
    print("  %-20s %d" % (k, v))
print()
print("=== 每档 assertions 条数分布 ===")
for k, v in sorted(assert_counts.items()):
    print("  %d 条: %d 档" % (k, v))
print()
print("=== 每档出现过的 kind 组合 ===")
for k, v in kind_set_per_doc.most_common():
    print("  %-30s %d 档" % (str(list(k)), v))
print()
print("=== expressions[].layer（语体层） ===")
for k, v in layer_c.most_common():
    print("  %-20s %d" % (k, v))
print()
print("=== 断言正文 vs 引文 ===")
print("  正文与引文逐字相同:", fact_text_eq_quote)
low = [x for x in fact_text_quote_ratio if x[1] < 0.9]
print("  有引文的断言数:", len(fact_text_quote_ratio))
print("  与引文公共前缀低于九成的条数:", len(low))
print("  样例（前缀占比 / 档名）:")
for name, r in sorted(fact_text_quote_ratio)[:8]:
    print("    %.3f  %s" % (r, name))
print()
print("=== subdomain 分布（前 10） ===")
for k, v in subdomain_c.most_common(10):
    print("  %-30s %d" % (k, v))
print()
print("带 kind=interpretation 的档:", len(docs_with_interpretation), docs_with_interpretation[:5])
print("带 kind=rumor 的档:", len(docs_with_rumor_kind), docs_with_rumor_kind[:5])
