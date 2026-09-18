# -*- coding: utf-8 -*-
"""城堡铺开批（P1 全量 67 座）六步：
   register 增量（内容哈希不符的档，含 67 城堡）→ select 全量 → approve → proof → compile。
OPBASE=castle1a20260914；输出 geo1-castles1。
register 因 operation-journal 回放近二次方变慢 ⇒ 后台跑，用文件系统判进度。
"""
import os, json, subprocess, sys, glob, re, hashlib

ROOT = r"D:/AWAKE-Dev/AWAKE"
WS = os.path.join(ROOT, "tools/worldbook-studio/workspace/full-geo1")
WS_REL = "tools/worldbook-studio/workspace/full-geo1"
DLL = os.path.join(ROOT, "tools/worldbook-studio/src/Awake.WorldbookStudio.Cli/bin/Release/net10.0/worldbook-studio.dll")
OPBASE = "castle1a20260914"
OUT_PKG = "tools/worldbook-studio/workspace/full-geo1/compiled/geo1-castles1"


def run(args, timeout=3600):
    full = args[:1] + ["--workspace", WS_REL] + args[1:]
    r = subprocess.run(["dotnet", DLL] + full, capture_output=True, text=True, timeout=timeout, cwd=ROOT)
    out = r.stdout.strip() or r.stderr.strip()
    try:
        return json.loads(out), r.returncode
    except Exception:
        return {"_raw": out[:900], "_stderr": r.stderr[:900]}, r.returncode


def main():
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
    print("档数:", len(docs), " 需注册:", len(need), flush=True)

    ok = 0
    for i, (path, docid) in enumerate(need):
        res, _ = run(["authoring-register", "--operation", f"cs1-reg-{i}-{OPBASE}", "--path", path], timeout=300)
        if res.get("ok"):
            ok += 1
        else:
            print("REGISTER FAIL:", os.path.basename(path), json.dumps(res, ensure_ascii=False)[:400], flush=True)
            sys.exit(1)
        if (i + 1) % 10 == 0:
            print(f"  register {i+1}/{len(need)}", flush=True)
    print(f"[1/5] register {ok}/{len(need)}", flush=True)

    ids = [re.search(r"^id:\s*(\S+)", open(f, encoding="utf-8").read(2000), re.M).group(1) for f in docs]
    res, _ = run(["authoring-select", "--operation", f"customer.select.v1.{OPBASE}", "--document-id", ",".join(ids)])
    assert res.get("ok"), json.dumps(res, ensure_ascii=False)[:500]
    sel_id = res["selection"]["selectionId"]
    json.dump(res, open(os.path.join(WS, "_castles1_sel.json"), "w", encoding="utf-8"), ensure_ascii=False)
    print("[2/5] select ->", sel_id, "items:", res["selection"]["itemCount"], flush=True)

    res, _ = run(["authoring-approve", "--operation", f"customer.approve.v1.{OPBASE}", "--selection", sel_id])
    assert res.get("ok"), json.dumps(res, ensure_ascii=False)[:500]
    app = res["approval_proof"]
    aid = app.get("approvalId") or app.get("approval_id") or app.get("id")
    print("[3/5] approve ->", aid, flush=True)

    res, _ = run(["authoring-proof", "--operation", f"customer.proof.v1.{OPBASE}", "--approval", aid])
    assert res.get("ok"), json.dumps(res, ensure_ascii=False)[:500]
    pf = res["compile_proof"]
    pid = pf.get("compileProofId") or pf.get("proofId") or pf.get("proof_id") or pf.get("id")
    print("[4/5] proof ->", pid, flush=True)

    res, code = run(["compile", "--proof", pid, "--out", OUT_PKG])
    if not res.get("manifest_hash"):
        print("compile FAIL:", json.dumps(res, ensure_ascii=False)[:1200], flush=True)
        print("STDERR:", str(res.get("_stderr", ""))[:900], flush=True)
        sys.exit(1)
    json.dump(res, open(os.path.join(WS, "_castles1_compile.json"), "w", encoding="utf-8"), ensure_ascii=False)
    diag = res.get("validation", [])
    errs = [x for x in diag if str(x.get("Severity", "")).lower() == "error"]
    print("[5/5] compile ->", OUT_PKG, "manifest:", res.get("manifest_hash", "")[:16],
          "validation:", len(diag), "errors:", len(errs), flush=True)
    for e in errs[:10]:
        print("   ERR:", json.dumps(e, ensure_ascii=False)[:300], flush=True)
    print("DONE", flush=True)


if __name__ == "__main__":
    main()
