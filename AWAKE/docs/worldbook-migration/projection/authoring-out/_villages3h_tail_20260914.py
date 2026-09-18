# -*- coding: utf-8 -*-
"""六步链「后半段」：select→approve→proof→compile（跳过 register）。
前提：workspace-head 已与磁盘 246/246 一致（`_check_head_vs_disk_20260914.py` 验过）。
理由：register 是逐档 CLI 调用（~4.5s/档，全库 ~19min），而本轮只需刷新被
prose-qc 改动的档；已确认 head 无分叉 ⇒ 直接进后半段。

OPBASE=vill3h20260914；compile --out 相对仓库根（CWD=仓库根）。
输出 geo1-full21；判定字段＝manifest_hash。
"""
import os, json, subprocess, sys, glob, re

ROOT = r"D:/AWAKE-Dev/AWAKE"
WS = os.path.join(ROOT, "tools/worldbook-studio/workspace/full-geo1")
DOTNET = "dotnet"
DLL = os.path.join(ROOT, "tools/worldbook-studio/src/Awake.WorldbookStudio.Cli/bin/Release/net10.0/worldbook-studio.dll")
OPBASE = "vill3h20260914"

def run(args):
    WS_REL = "tools/worldbook-studio/workspace/full-geo1"
    full = args[:1] + ["--workspace", WS_REL] + args[1:]
    r = subprocess.run([DOTNET, DLL] + full, capture_output=True, text=True, timeout=900, cwd=ROOT)
    out = r.stdout.strip() or r.stderr.strip()
    try:
        return json.loads(out), r.returncode
    except Exception:
        return {"_raw": out[:800], "_stderr": r.stderr[:800]}, r.returncode

# 取当前 246 档 id
docs = [f for f in sorted(glob.glob(os.path.join(WS, "authoring", "*.yaml")))
        if not os.path.basename(f).startswith(("_", "source-"))]
ids = []
for f in docs:
    m = re.search(r"^id:\s*(\S+)", open(f, encoding="utf-8").read(2000), re.M)
    assert m, f
    ids.append(m.group(1))
print("档数:", len(docs))

res, _ = run(["authoring-select", "--operation", f"customer.select.v1.{OPBASE}",
              "--document-id", ",".join(ids)])
assert res.get("ok"), json.dumps(res, ensure_ascii=False)[:400]
sel_id = res["selection"]["selectionId"]
json.dump(res, open(os.path.join(WS, "_vill3h_sel.json"), "w", encoding="utf-8"), ensure_ascii=False)
print("[2/5] select ->", sel_id, "items:", res["selection"]["itemCount"])

res, _ = run(["authoring-approve", "--operation", f"customer.approve.v1.{OPBASE}", "--selection", sel_id])
assert res.get("ok"), json.dumps(res, ensure_ascii=False)[:400]
app = res["approval_proof"]
approval_id = app.get("approvalId") or app.get("approval_id") or app.get("id")
json.dump(res, open(os.path.join(WS, "_vill3h_app.json"), "w", encoding="utf-8"), ensure_ascii=False)
print("[3/5] approve ->", approval_id)

res, _ = run(["authoring-proof", "--operation", f"customer.proof.v1.{OPBASE}", "--approval", approval_id])
assert res.get("ok"), json.dumps(res, ensure_ascii=False)[:400]
proof = res["compile_proof"]
proof_id = proof.get("compileProofId") or proof.get("proofId") or proof.get("proof_id") or proof.get("id")
json.dump(res, open(os.path.join(WS, "_vill3h_prf.json"), "w", encoding="utf-8"), ensure_ascii=False)
print("[4/5] proof ->", proof_id)

out_pkg = "tools/worldbook-studio/workspace/full-geo1/compiled/geo1-full21"
res, code = run(["compile", "--proof", proof_id, "--out", out_pkg])
if not res.get("manifest_hash"):
    print("compile FAIL:", json.dumps(res, ensure_ascii=False)[:800])
    print("STDERR:", str(res.get("_stderr", ""))[:800])
    sys.exit(1)
json.dump(res, open(os.path.join(WS, "_vill3h_compile.json"), "w", encoding="utf-8"), ensure_ascii=False)
diag = res.get("validation", [])
errs = [x for x in diag if x.get("Severity") == "error"]
print("[5/5] compile ->", out_pkg, "manifest:", res.get("manifest_hash", "")[:16],
      "validation:", len(diag), "errors:", len(errs))
print("DONE")
