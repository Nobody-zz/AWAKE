# -*- coding: utf-8 -*-
"""权重专项探针：同名聚落（堡 vs 村）谁排前。
期望：查村名 → 该村档首发；查堡名 → 该堡档首发。
用法：python _weight_probe_20260914.py [run]
"""
import io, os, json, subprocess, sys

ROOT = r"D:/AWAKE-Dev/AWAKE"
HERE = os.path.join(ROOT, "docs/worldbook-migration/projection/authoring-out")
MANIFEST = os.path.join(ROOT, "tools/worldbook-studio/workspace/full-geo1/compiled/geo1-castles1/manifest.json")

CASES = [
    # (name, query, 期望首发 entry)
    ("W1-村名(于桑克)", "于桑克", "awake:entry:geography.village-usanc"),
    ("W2-堡名(于桑克堡)", "于桑克堡", "awake:entry:geography.castle-usanc-castle"),
    ("W3-村名(德鲁伊莫尔)", "德鲁伊莫尔", "awake:entry:geography.village-druimmor"),
    ("W4-堡名(德鲁伊莫尔堡)", "德鲁伊莫尔堡", "awake:entry:geography.castle-druimmor-castle"),
    ("W5-村名(瓦拉戈斯)", "瓦拉戈斯", "awake:entry:geography.village-varagos"),
    ("W6-堡名(瓦拉戈斯堡)", "瓦拉戈斯堡", "awake:entry:geography.castle-varagos-castle"),
    ("W7-城镇(瓦尔切格)", "瓦尔切格", "awake:entry:geography.town-varcheg"),
]


def main():
    queries = [{"name": n, "identity": "profile.merchant", "text": t, "requested_detail": "detail"}
               for n, t, _ in CASES]
    spec = os.path.join(HERE, "_weight_spec_20260914.json")
    json.dump({"queries": queries}, io.open(spec, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print("spec:", len(queries))
    if "run" not in sys.argv:
        return
    out = os.path.join(HERE, "_weight_result_20260914.json")
    cmd = ["dotnet", "run", "--project", "tools/worldbook-runtime-sim", "-c", "Release", "--",
           "probe", MANIFEST, spec, out]
    r = subprocess.run(cmd, capture_output=True, text=True, cwd=ROOT, timeout=3600)
    print("sim rc:", r.returncode)
    res = json.load(io.open(out, encoding="utf-8"))
    by = {x["name"]: x for x in res}
    npass = nfail = 0
    for name, q, want in CASES:
        row = by.get(name, {})
        hits = row.get("hits", []) or []
        first = hits[0] if hits else "(none)"
        ok = first == want
        npass += ok
        nfail += (not ok)
        print(("  PASS " if ok else "  FAIL ") + f"{name}  「{q}」 first={first}" + ("" if ok else f"  期望={want}"))
    print(f"===== 权重探针 {npass+nfail} 行: PASS {npass} / FAIL {nfail} =====")


if __name__ == "__main__":
    main()
