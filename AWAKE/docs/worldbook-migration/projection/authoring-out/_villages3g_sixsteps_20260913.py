# -*- coding: utf-8 -*-
"""六步编译编排（register→select→approve→proof→compile）— DLL 直调版。
与 _villages3f 等价，仅把 `dotnet run --project` 换成直调已构建的 worldbook-studio.dll，
把每档 5~6s 的 MSBuild 开销降到 ~0.3s（246 档从 ~24min → ~1min）。

用途：prose-QC 线在 23:53 改写 7 档正文后，重新 register 刷新 head hash，
让 CompileProof 快照与磁盘一致（此前 full20 因 WB-AUTHORITY-CAS-409 失败）。

OPBASE=vill3g20260913；compile --out 相对仓库根（进程 CWD=仓库根）。
输出 geo1-full21；compile 判定字段＝manifest_hash（response 无 ok 键）。
"""
import os, json, subprocess, sys, glob, re

ROOT = r"D:/AWAKE-Dev/AWAKE"
WS = os.path.join(ROOT, "tools/worldbook-studio/workspace/full-geo1")
DOTNET = "dotnet"
DLL = os.path.join(ROOT, "tools/worldbook-studio/src/Awake.WorldbookStudio.Cli/bin/Release/net10.0/worldbook-studio.dll")
OPBASE = "vill3g20260913"

def run(args):
    WS_REL = "tools/worldbook-studio/workspace/full-geo1"
    full = args[:1] + ["--workspace", WS_REL] + args[1:]
    r = subprocess.run([DOTNET, DLL] + full,
                       capture_output=True, text=True, timeout=600, cwd=ROOT)
    out = r.stdout.strip() or r.stderr.strip()
    try:
        return json.loads(out), r.returncode
    except Exception:
        return {"_raw": out[:500], "_stderr": r.stderr[:800]}, r.returncode

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
    op = f"vill1-reg-{i}-{OPBASE}"
    res, code = run(["authoring-register", "--operation", op, "--path", path])
    if res.get("ok"):
        ok += 1
    else:
        print("FAIL:", os.path.basename(path), json.dumps(res, ensure_ascii=False)[:200])
        sys.exit(1)
print(f"[1/5] register {ok}/{len(docs)} 档")

# 2) select
ids = []
for f in docs:
    t = open(f, encoding="utf-8").read(2000)
    m = re.search(r"^id:\s*(\S+)", t, re.M)
    assert m, f
    ids.append(m.group(1))
res, _ = run(["authoring-select", "--operation", f"customer.select.v1.{OPBASE}",
              "--document-id", ",".join(ids)])
assert res.get("ok"), json.dumps(res, ensure_ascii=False)[:300]
sel_id = res["selection"]["selectionId"]
json.dump(res, open(os.path.join(WS, "_vill3g_sel.json"), "w", encoding="utf-8"), ensure_ascii=False)
print("[2/5] select ->", sel_id, "items:", res["selection"]["itemCount"])

# 3) approve
res, _ = run(["authoring-approve", "--operation", f"customer.approve.v1.{OPBASE}", "--selection", sel_id])
assert res.get("ok"), json.dumps(res, ensure_ascii=False)[:300]
app = res["approval_proof"]
approval_id = app.get("approvalId") or app.get("approval_id") or app.get("id")
json.dump(res, open(os.path.join(WS, "_vill3g_app.json"), "w", encoding="utf-8"), ensure_ascii=False)
print("[3/5] approve ->", approval_id)

# 4) proof
res, _ = run(["authoring-proof", "--operation", f"customer.proof.v1.{OPBASE}", "--approval", approval_id])
assert res.get("ok"), json.dumps(res, ensure_ascii=False)[:300]
proof = res["compile_proof"]
proof_id = proof.get("compileProofId") or proof.get("proofId") or proof.get("proof_id") or proof.get("id")
json.dump(res, open(os.path.join(WS, "_vill3g_prf.json"), "w", encoding="utf-8"), ensure_ascii=False)
print("[4/5] proof ->", proof_id)

# 5) compile（--out 按进程 CWD=仓库根解析）
out_pkg = "tools/worldbook-studio/workspace/full-geo1/compiled/geo1-full21"
res, code = run(["compile", "--proof", proof_id, "--out", out_pkg])
if not res.get("manifest_hash"):
    print("compile FAIL:", json.dumps(res, ensure_ascii=False)[:600])
    print("STDERR:", str(res.get("_stderr", ""))[:800])
    sys.exit(1)
json.dump(res, open(os.path.join(WS, "_vill3g_compile.json"), "w", encoding="utf-8"), ensure_ascii=False)
diag = res.get("validation", [])
errs = [x for x in diag if x.get("Severity") == "error"]
print("[5/5] compile ->", out_pkg, "manifest:", res.get("manifest_hash", "")[:16], "validation:", len(diag), "errors:", len(errs))
print("DONE")
