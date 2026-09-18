# -*- coding: utf-8 -*-
"""城堡↔村庄 双向联系全量探针（67 堡 × 2 + 1 对照）。
每个堡两条 query：① 堡官方名 → 应命中该堡档；② 一个下辖村名 → 也应命中该堡档（联系）。
用法：python _castle_link_probe_20260914.py [run]
不带 run 只生成 spec；带 run 跑 sim 并判定。
"""
import io, os, json, re, subprocess, sys

ROOT = r"D:/AWAKE-Dev/AWAKE"
WS = os.path.join(ROOT, "tools/worldbook-studio/workspace/full-geo1")
PKG = os.path.join(WS, "compiled/geo1-castles1")
HERE = os.path.join(ROOT, "docs/worldbook-migration/projection/authoring-out")


def slug_of(en):
    return re.sub(r"[^a-z0-9]+", "-", en.lower()).strip("-")


def build_spec(inv):
    queries = []
    for sid in sorted(inv):
        c = inv[sid]
        eid = "awake:entry:geography.castle-" + slug_of(c["en"])
        queries.append({"name": f"L-{sid}-self", "identity": "profile.merchant",
                        "text": c["cn"], "requested_detail": "detail", "_want": eid})
        vcn = [v["cn"] for v in c["villages"] if v["cn"]]
        if vcn:
            queries.append({"name": f"L-{sid}-village", "identity": "profile.merchant",
                            "text": vcn[0], "requested_detail": "detail", "_want": eid})
    queries.append({"name": "L-control-invented", "identity": "profile.merchant",
                    "text": "子虚堡", "requested_detail": "detail", "_want": ""})
    return queries


def main():
    inv = json.load(io.open(os.path.join(HERE, "_castle_inventory_20260914.json"), encoding="utf-8"))
    queries = build_spec(inv)
    spec_path = os.path.join(HERE, "_castlelink_spec_20260914.json")
    json.dump({"queries": queries}, io.open(spec_path, "w", encoding="utf-8"),
              ensure_ascii=False, indent=1)
    print("spec queries:", len(queries))
    if "run" not in sys.argv:
        return
    out = os.path.join(HERE, "_castlelink_result_20260914.json")
    manifest = os.path.join(PKG, "manifest.json")
    cmd = ["dotnet", "run", "--project", "tools/worldbook-runtime-sim", "-c", "Release", "--",
           "probe", manifest, spec_path, out]
    r = subprocess.run(cmd, capture_output=True, text=True, cwd=ROOT, timeout=3600)
    print("sim rc:", r.returncode, (r.stdout or "")[-400:], (r.stderr or "")[-400:])
    res = json.load(io.open(out, encoding="utf-8"))
    want = {q["name"]: q["_want"] for q in queries}
    npass = nfail = 0
    fails = []
    for row in res:
        nm = row.get("name", "")
        hits = row.get("hits", []) or []
        want_id = want.get(nm, "")
        if want_id == "":
            # 对照：不应命中任何档
            ok = (len(hits) == 0)
            if not ok:
                fails.append(f"{nm}: 对照却命中 {hits[:3]}")
        else:
            ok = want_id in hits
            if not ok:
                fails.append(f"{nm}: 未见 {want_id} | hits={hits[:4]}")
        if ok:
            npass += 1
        else:
            nfail += 1
    print(f"===== 联系探针 {npass+nfail} 行: PASS {npass} / FAIL {nfail} =====")
    for f in fails[:40]:
        print("  FAIL", f)


if __name__ == "__main__":
    main()
