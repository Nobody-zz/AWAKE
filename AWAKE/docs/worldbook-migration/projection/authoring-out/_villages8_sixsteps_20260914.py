# -*- coding: utf-8 -*-
"""村庄铺开批（瓦兰迪亚35＋库赛特35＋巴旦尼亚33＋斯特吉亚32＝135 档）六步编译编排。
- register：只注册 head 中缺失/不符的 135 档（其余 246 档 head 已一致，免注册）。
- select→approve→proof→compile：全量 381 档（273 村＋108 其他）出包。
- OPBASE=vill8a20260914；compile --out 相对仓库根（进程 CWD）；输出 geo1-full23。
- compile 判定字段＝manifest_hash（response 无 ok 键）。
"""
import os, json, subprocess, sys, glob, re, hashlib

ROOT = r"D:/AWAKE-Dev/AWAKE"
WS = os.path.join(ROOT, "tools/worldbook-studio/workspace/full-geo1")
DOTNET = "dotnet"
DLL = os.path.join(ROOT, "tools/worldbook-studio/src/Awake.WorldbookStudio.Cli/bin/Release/net10.0/worldbook-studio.dll")
OPBASE = "vill8a20260914"

def run(args, timeout=1800):
    WS_REL = "tools/worldbook-studio/workspace/full-geo1"
    full = args[:1] + ["--workspace", WS_REL] + args[1:]
    r = subprocess.run([DOTNET, DLL] + full, capture_output=True, text=True, timeout=timeout, cwd=ROOT)
    out = r.stdout.strip() or r.stderr.strip()
    try:
        return json.loads(out), r.returncode
    except Exception:
        return {"_raw": out[:800], "_stderr": r.stderr[:800]}, r.returncode

# 0) 判定需注册的档 = head 缺失或 hash 不符
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

# 1) register 逐档（每档独立 op id）
ok = 0
for i, (path, docid) in enumerate(need):
    op = f"vill8a-reg-{i}-{OPBASE}"
    res, code = run(["authoring-register", "--operation", op, "--path", path], timeout=300)
    if res.get("ok"):
        ok += 1
    else:
        print("FAIL:", os.path.basename(path), json.dumps(res, ensure_ascii=False)[:300])
        sys.exit(1)
print(f"[1/5] register {ok}/{len(need)} 档")

# 2) select 全量
ids = [re.search(r"^id:\s*(\S+)", open(f, encoding="utf-8").read(2000), re.M).group(1) for f in docs]
res, _ = run(["authoring-select", "--operation", f"customer.select.v1.{OPBASE}",
              "--document-id", ",".join(ids)])
assert res.get("ok"), json.dumps(res, ensure_ascii=False)[:400]
sel_id = res["selection"]["selectionId"]
json.dump(res, open(os.path.join(WS, "_vill8a_sel.json"), "w", encoding="utf-8"), ensure_ascii=False)
print("[2/5] select ->", sel_id, "items:", res["selection"]["itemCount"])

# 3) approve
res, _ = run(["authoring-approve", "--operation", f"customer.approve.v1.{OPBASE}", "--selection", sel_id])
assert res.get("ok"), json.dumps(res, ensure_ascii=False)[:400]
app = res["approval_proof"]
approval_id = app.get("approvalId") or app.get("approval_id") or app.get("id")
json.dump(res, open(os.path.join(WS, "_vill8a_app.json"), "w", encoding="utf-8"), ensure_ascii=False)
print("[3/5] approve ->", approval_id)

# 4) proof
res, _ = run(["authoring-proof", "--operation", f"customer.proof.v1.{OPBASE}", "--approval", approval_id])
assert res.get("ok"), json.dumps(res, ensure_ascii=False)[:400]
proof = res["compile_proof"]
proof_id = proof.get("compileProofId") or proof.get("proofId") or proof.get("proof_id") or proof.get("id")
json.dump(res, open(os.path.join(WS, "_vill8a_prf.json"), "w", encoding="utf-8"), ensure_ascii=False)
print("[4/5] proof ->", proof_id)

# 5) compile
out_pkg = "tools/worldbook-studio/workspace/full-geo1/compiled/geo1-full23"
res, code = run(["compile", "--proof", proof_id, "--out", out_pkg], timeout=1800)
if not res.get("manifest_hash"):
    print("compile FAIL:", json.dumps(res, ensure_ascii=False)[:1000])
    print("STDERR:", str(res.get("_stderr", ""))[:1000])
    sys.exit(1)
json.dump(res, open(os.path.join(WS, "_vill8a_compile.json"), "w", encoding="utf-8"), ensure_ascii=False)
diag = res.get("validation", [])
errs = [x for x in diag if str(x.get("Severity", "")).lower() == "error"]
print("[5/5] compile ->", out_pkg, "manifest:", res.get("manifest_hash", "")[:16],
      "validation:", len(diag), "errors:", len(errs))
print("DONE")
