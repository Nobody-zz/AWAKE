# -*- coding: utf-8 -*-
"""规模收敛裁定辅助：打印超限档的断言结构（id/文本首句/表达数/表达层分布）。"""
import io, re, os, json

BASE = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out/"
FILES = ["charas-bay.yaml", "der-furs.yaml", "kach-land.yaml", "kach-tales.yaml",
         "lac-lake.yaml", "paravenos.yaml"]

def split_top_list(text, key):
    """取顶格 '- id:' 列表块的粗解析：返回断言块的字符串列表。"""
    lines = text.splitlines()
    start = None
    for i, ln in enumerate(lines):
        if ln.startswith(key + ":"):
            start = i + 1
            break
    if start is None:
        return []
    blocks, cur = [], None
    for ln in lines[start:]:
        if re.match(r"^[A-Za-z_]", ln) and cur is not None:
            break
        if ln.startswith("- id:"):
            if cur is not None:
                blocks.append(cur)
            cur = [ln]
        elif cur is not None:
            cur.append(ln)
    if cur is not None:
        blocks.append(cur)
    return blocks

for fn in FILES:
    text = io.open(BASE + fn, encoding="utf-8").read()
    title = re.search(r"  zh-CN: (.+)", text)
    print("=" * 70)
    print(fn, "|", title.group(1) if title else "?")
    for b in split_top_list(text, "assertions"):
        bid = b[0].split("id:")[1].strip()
        layer_counts = {}
        expr_ids = []
        for ln in b:
            m = re.match(r"\s+  - id: expr\.", ln)
            if m:
                expr_ids.append(ln.split("id: expr.")[1].strip())
            m2 = re.match(r"\s+    layer: (\w+)", ln)
            if m2:
                layer_counts[m2.group(1)] = layer_counts.get(m2.group(1), 0) + 1
        tmatch = re.search(r"    zh-CN: (.+)", "\n".join(b[:14]))
        t = (tmatch.group(1) if tmatch else "")[:60]
        n_expr = sum(1 for ln in b if re.match(r"\s+  - id: expr\.", ln))
        print(f"  {bid}  表达{n_expr} 层分布{layer_counts}")
        print(f"      文本: {t}")
        for e in expr_ids:
            print(f"        - expr.{e}")
