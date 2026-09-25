# -*- coding: utf-8 -*-
"""生成本批（聚落别名批）精确名单：321 档 stem。"""
import io, json, os, sys, importlib.util

ROOT = r"D:\AWAKE-Dev\AWAKE"
spec = importlib.util.spec_from_file_location(
    "m", os.path.join(ROOT, "tools", "_uw_settle_aliases_probe_20260925.py"))
M = importlib.util.module_from_spec(spec)
spec.loader.exec_module(M)

ents = M.load_entries()
stems = []
for x in ents:
    head, za, ea = M.gen_aliases(x)
    if head is None:
        continue
    k = x.get("keywords") or []
    if len(k) > 3:
        continue
    slug = str(x.get("id")).split("entry:")[-1].split(".", 1)[-1]
    stems.append(slug)

out = os.path.join(ROOT, "tools", "_uw_settle_written_20260925.json")
io.open(out, "w", encoding="utf-8").write(json.dumps(stems, ensure_ascii=False, indent=1))
print("名单档数 =", len(stems), "->", out)
