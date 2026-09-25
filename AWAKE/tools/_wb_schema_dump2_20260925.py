# -*- coding: utf-8 -*-
"""读 author_created / approved_author_created / authority / status 的真实约束。"""
import json
import io

P = r"D:\AWAKE-Dev\AWAKE\docs\worldbook-studio-plan\awake.worldbook.authoring.v1.schema.json"
s = json.load(io.open(P, encoding="utf-8"))
d = s["$defs"]
for n in ["author_created", "approved_author_created", "authority", "assertion", "redirect", "lifecycle"]:
    print("### " + n)
    print(json.dumps(d[n], ensure_ascii=False, indent=1))
    print()

print("### 顶层 allOf（status 与 author_created 的关系）")
print(json.dumps(s["allOf"], ensure_ascii=False, indent=1)[:3000])
