# -*- coding: utf-8 -*-
"""红测一：章法 §2 档型封闭清单套测现有 40 档。
按立档三问顺序自动归类，报告：无法归类 / 判据重叠 / 一实体多本档 / secret 档无 deny。
"""
import os, re, json
import yaml

DIR = r"D:\AWAKE-Dev\AWAKE\docs\worldbook-migration\projection\authoring-out"
OUT = os.path.join(DIR, "_redtest1_result_20260913.json")

results = []
for fn in sorted(os.listdir(DIR)):
    if not fn.endswith(".yaml") or fn.startswith("_"):
        continue
    path = os.path.join(DIR, fn)
    with open(path, encoding="utf-8") as f:
        doc = yaml.safe_load(f)
    doc_id = doc.get("id", "?")
    entity_ids = doc.get("entity_ids") or []
    grants = doc.get("grants") or []
    has_deny = any(
        (g.get("effect") == "deny" or g.get("deny") or "deny" in str(g.get("kind", "")).lower())
        for g in grants
    )
    # 也扫全文兜底
    raw = open(path, encoding="utf-8").read()
    if not has_deny and re.search(r"(?i)deny", raw):
        has_deny = "keyword"
    entries = doc.get("entries") or doc.get("assertions") or []
    n_expr = 0
    for e in entries:
        exprs = e.get("expressions") or []
        n_expr += len(exprs)
    # 立档三问自动归类
    slug_secret = "-secret" in fn
    if slug_secret:
        cls = "秘密档"
    elif entity_ids:
        cls = "实体本档或事件档(判据重叠)"
    else:
        cls = "专题档"
    results.append({
        "file": fn, "id": doc_id, "classified": cls,
        "entity_ids": entity_ids, "n_assertions": len(entries),
        "n_expressions": n_expr, "n_grants": len(grants), "has_deny": has_deny,
    })

print("=== 自动归类结果 ===")
by_cls = {}
for r in results:
    by_cls.setdefault(r["classified"], []).append(r["file"])
for cls, files in by_cls.items():
    print(f"\n[{cls}] ({len(files)})")
    for x in sorted(files):
        print("  ", x)

# 检查 A：一实体多本档（entity_ids 交集的非 secret 档）
print("\n=== 检查A：同一实体挂在多个非 secret 档 ===")
ent_map = {}
for r in results:
    if r["classified"] == "秘密档":
        continue
    for e in r["entity_ids"]:
        ent_map.setdefault(e, []).append(r["file"])
viol_a = {e: fs for e, fs in ent_map.items() if len(fs) > 1}
print(json.dumps(viol_a, ensure_ascii=False, indent=1) if viol_a else "  无违规")

# 检查 B：secret 档 deny 情况
print("\n=== 检查B：secret 档 deny 检测 ===")
for r in results:
    if r["classified"] == "秘密档":
        print(f"  {r['file']}: deny={r['has_deny']}")

# 检查 C：规模硬限（断言 1-3、每断言表达 ≤4）
print("\n=== 检查C：规模硬限（粗扫，表达数/断言数比值仅供参考）===")
for r in results:
    n_a = r["n_assertions"]
    if n_a == 0:
        print(f"  {r['file']}: 断言数=0(结构字段名可能不符，需人工看)")
    elif r["n_expressions"] > n_a * 4:
        print(f"  {r['file']}: 断言{n_a}/表达{r['n_expressions']} 超4:1 需人工复核")

with open(OUT, "w", encoding="utf-8") as f:
    json.dump(results, f, ensure_ascii=False, indent=1)
print(f"\n明细已存 {OUT}")
