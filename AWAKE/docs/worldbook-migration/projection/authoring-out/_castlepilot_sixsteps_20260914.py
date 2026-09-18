# -*- coding: utf-8 -*-
"""城堡 PILOT 六步：注册 3 座新城堡档 → select 全量 → compile 出 pilot 包。
用于验证新档型（doc.geography.castle-*）/ 新来源登记 / entity_ids / quote 全链。
OPBASE=castlepilot20260914；输出 geo1-castlepilot。
"""
import os, json, subprocess, sys, glob, re, hashlib

ROOT = r"D:/AWAKE-Dev/AWAKE"
WS = os.path.join(ROOT, "tools/worldbook-studio/workspace/full-geo1")
DLL = os.path.join(ROOT, "tools/worldbook-studio/src/Awake.WorldbookStudio.Cli/bin/Release/net10.0/worldbook-studio.dll")
OPBASE = "castlepilot20260914"


def run(args, timeout=1800):
    WS_REL = "tools/worldbook-studio/workspace/full-geo1"
    full = args[:1] + ["--workspace", WS_REL] + args[1:]
    r = subprocess.run(["dotnet", DLL] + full, capture_output=True, text=True, timeout=timeout, cwd=ROOT)
    out = r.stdout.strip() or r.stderr.strip()
    try:
        return json.loads(out), r.returncode
    except Exception:
        return {"_raw": out[:900], "_stderr": r.stderr[:900]}, r.returncode


head = json.load(open(os.path.join(WS, "authoring-v1/workspace-head.json"), encoding="utf-8"))["documents"]
docs = [f for f in sorted(glob.glob(os.path.join(WS, "authoring", "*.yaml")))
        if not os.path.basename(f).startswith(("_", "source-"))]
need = []
for f in docs:
    b = open(f, "rb").read()
    h = hashlib.sha256(b).hexdigest().upper()
    docid = re.search(r"^id:\s*(\S+)", b.decode("utf-8"), re.M).group(1)
    rec = head.get(docid)
    if rec is None or rec.get("content_hash", "").upper() != h:
        need.append((f, docid))
print("当前档数:", len(docs), " 需注册:", len(need))

ok = 0
for i, (path, docid) in enumerate(need):
    res, _ = run(["authoring-register", "--operation", f"cp-reg-{i}-{OPBASE}", "--path", path], timeout=300)
    if res.get("ok"):
        ok += 1
    else:
        print("REGISTER FAIL:", os.path.basename(path), json.dumps(res, ensure_ascii=False)[:400]); sys.exit(1)
print(f"[1/5] register {ok}/{len(need)}")

ids = [re.search(r"^id:\s*(\S+)", open(f, encoding="utf-8").read(2000), re.M).group(1) for f in docs]
res, _ = run(["authoring-select", "--operation", f"customer.select.v1.{OPBASE}", "--document-id", ",".join(ids)])
assert res.get("ok"), json.dumps(res, ensure_ascii=False)[:500]
sel_id = res["selection"]["selectionId"]
json.dump(res, open(os.path.join(WS, "_castlepilot_sel.json"), "w", encoding="utf-8"), ensure_ascii=False)
print("[2/5] select ->", sel_id, "items:", res["selection"]["itemCount"])

res, _ = run(["authoring-approve", "--operation", f"customer.approve.v1.{OPBASE}", "--selection", sel_id])
assert res.get("ok"), json.dumps(res, ensure_ascii=False)[:500]
app = res["approval_proof"]
aid = app.get("approvalId") or app.get("approval_id") or app.get("id")
print("[3/5] approve ->", aid)

res, _ = run(["authoring-proof", "--operation", f"customer.proof.v1.{OPBASE}", "--approval", aid])
assert res.get("ok"), json.dumps(res, ensure_ascii=False)[:500]
pf = res["compile_proof"]
pid = pf.get("compileProofId") or pf.get("proofId") or pf.get("proof_id") or pf.get("id")
print("[4/5] proof ->", pid)

out_pkg = "tools/worldbook-studio/workspace/full-geo1/compiled/geo1-castlepilot"
res, code = run(["compile", "--proof", pid, "--out", out_pkg])
if not res.get("manifest_hash"):
    print("compile FAIL:", json.dumps(res, ensure_ascii=False)[:1200]); print("STDERR:", str(res.get("_stderr", ""))[:900]); sys.exit(1)
json.dump(res, open(os.path.join(WS, "_castlepilot_compile.json"), "w", encoding="utf-8"), ensure_ascii=False)
diag = res.get("validation", [])
errs = [x for x in diag if str(x.get("Severity", "")).lower() == "error"]
print("[5/5] compile ->", out_pkg, "manifest:", res.get("manifest_hash", "")[:16], "validation:", len(diag), "errors:", len(errs))
for e in errs[:10]:
    print("   ERR:", json.dumps(e, ensure_ascii=False)[:300])
print("DONE")
