# -*- coding: utf-8 -*-
"""软项整改后收口：只重注册被改的 6 档 → select→approve→proof→compile。
（register 只收单个 --path；只注册变更档，不跑全量 246。）
输出 geo1-full22。DLL 直调；CWD=仓库根。
"""
import os, json, subprocess, sys, glob, re

ROOT = r"D:/AWAKE-Dev/AWAKE"
WS = os.path.join(ROOT, "tools/worldbook-studio/workspace/full-geo1")
DOTNET = "dotnet"
DLL = os.path.join(ROOT, "tools/worldbook-studio/src/Awake.WorldbookStudio.Cli/bin/Release/net10.0/worldbook-studio.dll")
OPBASE = "vill3i20260914"

CHANGED = ["item-western_plated_helmet.yaml", "troop-mamluk.yaml", "village-atrion.yaml",
           "village-deir-hawa.yaml", "village-lavenia.yaml", "weapon-crossbow.yaml"]

def run(args):
    WS_REL = "tools/worldbook-studio/workspace/full-geo1"
    full = args[:1] + ["--workspace", WS_REL] + args[1:]
    r = subprocess.run([DOTNET, DLL] + full, capture_output=True, text=True, timeout=900, cwd=ROOT)
    out = r.stdout.strip() or r.stderr.strip()
    try:
        return json.loads(out), r.returncode
    except Exception:
        return {"_raw": out[:800], "_stderr": r.stderr[:800]}, r.returncode

# 1) 只注册变更档
for i, fn in enumerate(CHANGED):
    p = os.path.join(WS, "authoring", fn)
    res, _ = run(["authoring-register", "--operation", f"vill1-reg-soft{i}-{OPBASE}", "--path", p])
    if not res.get("ok"):
        print("register FAIL:", fn, json.dumps(res, ensure_ascii=False)[:300]); sys.exit(1)
    print("[reg]", fn, "OK")

# 2) select 全 246
docs = [f for f in sorted(glob.glob(os.path.join(WS, "authoring", "*.yaml")))
        if not os.path.basename(f).startswith(("_", "source-"))]
ids = []
for f in docs:
    m = re.search(r"^id:\s*(\S+)", open(f, encoding="utf-8").read(2000), re.M)
    assert m, f
    ids.append(m.group(1))
res, _ = run(["authoring-select", "--operation", f"customer.select.v1.{OPBASE}", "--document-id", ",".join(ids)])
assert res.get("ok"), json.dumps(res, ensure_ascii=False)[:400]
sel_id = res["selection"]["selectionId"]
print("[2/5] select ->", sel_id, "items:", res["selection"]["itemCount"])

res, _ = run(["authoring-approve", "--operation", f"customer.approve.v1.{OPBASE}", "--selection", sel_id])
assert res.get("ok"), json.dumps(res, ensure_ascii=False)[:400]
app = res["approval_proof"]
approval_id = app.get("approvalId") or app.get("approval_id") or app.get("id")
print("[3/5] approve ->", approval_id)

res, _ = run(["authoring-proof", "--operation", f"customer.proof.v1.{OPBASE}", "--approval", approval_id])
assert res.get("ok"), json.dumps(res, ensure_ascii=False)[:400]
proof = res["compile_proof"]
proof_id = proof.get("compileProofId") or proof.get("proofId") or proof.get("proof_id") or proof.get("id")
print("[4/5] proof ->", proof_id)

out_pkg = "tools/worldbook-studio/workspace/full-geo1/compiled/geo1-full22"
res, code = run(["compile", "--proof", proof_id, "--out", out_pkg])
if not res.get("manifest_hash"):
    print("compile FAIL:", json.dumps(res, ensure_ascii=False)[:800]); sys.exit(1)
json.dump(res, open(os.path.join(WS, "_vill3i_compile.json"), "w", encoding="utf-8"), ensure_ascii=False)
diag = res.get("validation", [])
errs = [x for x in diag if x.get("Severity") == "error"]
print("[5/5] compile ->", out_pkg, "manifest:", res.get("manifest_hash", "")[:16],
      "validation:", len(diag), "errors:", len(errs))
print("DONE")
