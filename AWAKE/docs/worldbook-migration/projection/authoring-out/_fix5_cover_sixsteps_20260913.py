# -*- coding: utf-8 -*-
"""身份覆盖修复批：六步编译编排（register→select→approve→proof→compile）。
每步新 operation id；compile --out 相对工作区根。输出 geo1-full7。
"""
import os, json, subprocess, sys, glob

ROOT = r"D:/AWAKE-Dev/AWAKE"
WS = os.path.join(ROOT, "tools/worldbook-studio/workspace/full-geo1")
CLI = os.path.join(ROOT, "tools/worldbook-studio/src/Awake.WorldbookStudio.Cli")
DOTNET = "dotnet"
OPBASE = "scale7a20260913"

def run(args):
    WS_REL = "tools/worldbook-studio/workspace/full-geo1"
    full = args[:1] + ["--workspace", WS_REL] + args[1:]
    r = subprocess.run([DOTNET, "run", "--project", CLI, "--"] + full,
                       capture_output=True, text=True, timeout=600, cwd=ROOT)
    out = r.stdout.strip() or r.stderr.strip()
    try:
        return json.loads(out), r.returncode
    except Exception:
        return {"_raw": out[:500]}, r.returncode

# 1) register 逐档（每档独立 op id）
docs = []
for f in sorted(glob.glob(os.path.join(WS, "authoring", "*.yaml"))):
    fn = os.path.basename(f)
    if fn.startswith("_") or fn.startswith("source-"):
        continue
    docs.append(f)
print("待 register 档数:", len(docs))
ok = 0
for i, path in enumerate(docs):
    op = f"scale7-reg-{i}-{OPBASE}"
    res, code = run(["authoring-register", "--operation", op, "--path", path])
    if res.get("ok"):
        ok += 1
    else:
        print("FAIL:", os.path.basename(path), json.dumps(res, ensure_ascii=False)[:200])
        sys.exit(1)
print(f"[1/5] register {ok}/{len(docs)} 档")

# 2) select
ids = []
import re
for f in docs:
    t = open(f, encoding="utf-8").read(2000)
    m = re.search(r"^id:\s*(\S+)", t, re.M)
    assert m, f
    ids.append(m.group(1))
res, _ = run(["authoring-select", "--operation", f"customer.select.v1.{OPBASE}",
              "--document-id", ",".join(ids)])
assert res.get("ok"), json.dumps(res, ensure_ascii=False)[:300]
sel_id = res["selection"]["selectionId"]
json.dump(res, open(os.path.join(WS, "_scale7_sel.json"), "w", encoding="utf-8"), ensure_ascii=False)
print("[2/5] select ->", sel_id, "items:", res["selection"]["itemCount"])

# 3) approve
res, _ = run(["authoring-approve", "--operation", f"customer.approve.v1.{OPBASE}", "--selection", sel_id])
assert res.get("ok"), json.dumps(res, ensure_ascii=False)[:300]
app = res["approval_proof"]
approval_id = app.get("approvalId") or app.get("approval_id") or app.get("id")
json.dump(res, open(os.path.join(WS, "_scale7_app.json"), "w", encoding="utf-8"), ensure_ascii=False)
print("[3/5] approve ->", approval_id)

# 4) proof
res, _ = run(["authoring-proof", "--operation", f"customer.proof.v1.{OPBASE}", "--approval", approval_id])
assert res.get("ok"), json.dumps(res, ensure_ascii=False)[:300]
proof = res["compile_proof"]
proof_id = proof.get("compileProofId") or proof.get("proofId") or proof.get("proof_id") or proof.get("id")
json.dump(res, open(os.path.join(WS, "_scale7_prf.json"), "w", encoding="utf-8"), ensure_ascii=False)
print("[4/5] proof ->", proof_id)

# 5) compile（--out 按进程 CWD=仓库根解析，须写从仓库根起的完整相对路径）
out_pkg = "tools/worldbook-studio/workspace/full-geo1/compiled/geo1-full7"
res, code = run(["compile", "--proof", proof_id, "--out", out_pkg])
if not res.get("manifest_hash") and res.get("_raw"):
    print("compile raw:", res["_raw"]); sys.exit(1)
json.dump(res, open(os.path.join(WS, "_scale7_compile.json"), "w", encoding="utf-8"), ensure_ascii=False)
diag = res.get("validation", [])
errs = [x for x in diag if x.get("Severity") == "error"]
print("[5/5] compile ->", out_pkg, "manifest:", res.get("manifest_hash", "")[:16], "validation:", len(diag), "errors:", len(errs))
print("DONE")
