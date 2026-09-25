# -*- coding: utf-8 -*-
"""把 authoring schema 的关键定义打出来，供生成器对齐。"""
import json
import io

P = r"D:\AWAKE-Dev\AWAKE\docs\worldbook-studio-plan\awake.worldbook.authoring.v1.schema.json"
s = json.load(io.open(P, encoding="utf-8"))
d = s.get("definitions") or s.get("$defs") or {}
print("defs:", list(d.keys()))
print()
for n in ["expression", "knowledge_rule", "source_ref", "localized_text",
          "era", "authority", "registry_bindings", "localized_aliases"]:
    if n not in d:
        print("### %s  <无>" % n)
        continue
    print("### " + n)
    print(json.dumps(d[n], ensure_ascii=False, indent=1)[:2200])
    print()
