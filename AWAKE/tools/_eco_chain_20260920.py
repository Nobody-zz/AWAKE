# -*- coding: utf-8 -*-
"""经济批（09-20）链式编译：全量磁盘档 register → select → approve → proof → compile。

产物：tools/worldbook-studio/workspace/full-geo1/compiled/geo1-v20-goods
⚠️ 不部署到 ModuleData（甲方「先不急」仍有效）。

op_id 规则：`eco20260920-<用途>`，一律用新 id（op_id 幂等：同 id 重跑短路、不重读磁盘）。

运行（仓库根为 CWD）：
  python -u tools/_eco_chain_20260920.py
"""
import hashlib
import io
import json
import os
import subprocess
import sys
import time
import glob

ROOT = r"D:/AWAKE-Dev/AWAKE"
WS_REL = "tools/worldbook-studio/workspace/full-geo1"
WS = os.path.join(ROOT, WS_REL)
DLL = os.path.join(ROOT, "tools/worldbook-studio/src/Awake.WorldbookStudio.Cli/bin/Release/net10.0/worldbook-studio.dll")
OPBASE = "eco20260920d"
OUT_PKG = WS_REL + "/compiled/geo1-v20-goods"
LIVE_PKG = os.path.join(ROOT, "ModuleData/Worldbook/packages/calradia/runtime.json")

LOG_PATH = os.path.join(WS, "_eco_chain_log.txt")
MANIFEST = os.path.join(WS, "_eco_register_batch.json")
BATCH_OUT = os.path.join(WS, "_eco_register_out.txt")

logf = io.open(LOG_PATH, "a", encoding="utf-8", newline="\n")


def log(msg):
    line = "[%s] %s" % (time.strftime("%H:%M:%S"), msg)
    print(line, flush=True)
    logf.write(line + "\n")
    logf.flush()


def run(args, timeout=7200):
    """⚠️ 不给 text=，自己按 utf-8 解码。真因常在 stderr：凡 ok=false / 非零返回码，
    都必须把 stderr 打到日志里（铁律）。"""
    full = args[:1] + ["--workspace", WS_REL] + args[1:]
    r = subprocess.run(["dotnet", DLL] + full, capture_output=True, timeout=timeout, cwd=ROOT)
    so = (r.stdout or b"").decode("utf-8", "replace").strip()
    se = (r.stderr or b"").decode("utf-8", "replace").strip()
    out = so or se
    try:
        res = json.loads(out)
        if res.get("ok") is False or r.returncode != 0:
            log("  [STDERR] %s" % se[:800])
        return res, r.returncode
    except Exception:
        log("  [STDERR] %s" % se[:800])
        return {"_raw": out[:900], "_stderr": se[:900]}, r.returncode


def sha(p):
    return hashlib.sha256(open(p, "rb").read()).hexdigest().upper()


def docid_of(path):
    for ln in io.open(path, encoding="utf-8").read(6000).splitlines():
        if ln.startswith("id:"):
            return ln.split(":", 1)[1].strip()
    return None


def main():
    t0 = time.time()
    docs = [p for p in sorted(glob.glob(os.path.join(WS, "authoring", "*.yaml")))
            if not os.path.basename(p).startswith(("_", "source-"))]
    disk = {}
    for p in docs:
        disk[docid_of(p)] = p
    log("==== 经济批链式编译：磁盘 %d 档 ====" % len(docs))

    live = json.load(io.open(LIVE_PKG, encoding="utf-8"))
    live_by_id = {"doc." + e["id"].replace("awake:entry:", "", 1): e for e in live["entries"]}
    live_ids = set(live_by_id)
    log("[0/6] 现役包 %d 档；磁盘多出 %d 档" % (len(live_ids), len(set(disk) - live_ids)))

    head = json.load(io.open(os.path.join(WS, "authoring-v1/workspace-head.json"),
                             encoding="utf-8"))["documents"]
    need = []
    for d, p in sorted(disk.items()):
        rec = head.get(d)
        if rec is None or str(rec.get("content_hash", "")).upper() != sha(p):
            need.append((p, d))
    log("[1/6] 需重登记 %d 档" % len(need))

    # ⚠️ 登记操作的 op_id 必须带 OPBASE：否则同内容重跑会被幂等短路、不重读磁盘，
    #    导致 head 记的还是旧 hash，proof 与磁盘不一致 ⇒ compile 报 WB-AUTHORITY-CAS-409。
    manifest = [{"operation": "%s-reg-%d-%s" % (OPBASE, i, d.replace(".", "_")), "path": p}
                for i, (p, d) in enumerate(need)]
    with io.open(MANIFEST, "w", encoding="utf-8") as mf:
        json.dump(manifest, mf, ensure_ascii=False, indent=1)
    with io.open(BATCH_OUT, "w", encoding="utf-8") as of:
        p2 = subprocess.Popen(["dotnet", DLL, "authoring-register-batch", "--manifest", MANIFEST,
                               "--workspace", WS_REL], cwd=ROOT, stdout=of, stderr=subprocess.PIPE)
        # ⚠️ 铁律：真因在 stderr，自己按 utf-8 解码
        for line in iter(p2.stderr.readline, b""):
            s = line.decode("utf-8", "replace").strip()
            if s:
                log("  " + s)
        p2.wait()
    if p2.returncode != 0:
        log("FATAL batch register rc=%d" % p2.returncode)
        logf.close()
        sys.exit(1)
    log("[2/6] register %d 档完成" % len(need))

    ids = sorted(disk)
    res, _ = run(["authoring-select", "--operation", "customer.select.v1." + OPBASE, "--document-id", ",".join(ids)])
    if not res.get("ok"):
        log("FATAL select: %s" % json.dumps(res, ensure_ascii=False)[:500])
        logf.close()
        sys.exit(1)
    sel_id = res["selection"]["selectionId"]
    json.dump(res, io.open(os.path.join(WS, "_eco_select.json"), "w", encoding="utf-8"),
              ensure_ascii=False, indent=1)
    log("[3/6] select -> %s itemCount=%s" % (sel_id, res["selection"].get("itemCount")))
    if int(res["selection"].get("itemCount") or 0) != len(ids):
        log("FATAL itemCount != 磁盘档数")
        logf.close()
        sys.exit(1)

    res, _ = run(["authoring-approve", "--operation", "customer.approve.v1." + OPBASE, "--selection", sel_id])
    if not res.get("ok"):
        log("FATAL approve: %s" % json.dumps(res, ensure_ascii=False)[:500])
        logf.close()
        sys.exit(1)
    app = res.get("approval_proof") or {}
    aid = app.get("approvalId") or app.get("approval_id") or app.get("id")
    log("[4/6] approve -> %s" % aid)

    res, _ = run(["authoring-proof", "--operation", "customer.proof.v1." + OPBASE, "--approval", aid])
    if not res.get("ok"):
        log("FATAL proof: %s" % json.dumps(res, ensure_ascii=False)[:500])
        logf.close()
        sys.exit(1)
    pf = res.get("compile_proof") or {}
    pid = pf.get("compileProofId") or pf.get("proofId") or pf.get("proof_id") or pf.get("id")
    log("[5/6] proof -> %s" % pid)

    res, _ = run(["compile", "--proof", pid, "--out", OUT_PKG], timeout=7200)
    json.dump(res, io.open(os.path.join(WS, "_eco_compile.json"), "w", encoding="utf-8"),
              ensure_ascii=False, indent=1)
    if not res.get("result_hash"):
        log("FATAL compile: %s" % json.dumps(res, ensure_ascii=False)[:1200])
        logf.close()
        sys.exit(1)
    val = res.get("validation") or []
    errs = [x for x in val if str(x.get("severity") or x.get("Severity") or "").lower() == "error"]
    log("[6/6] compile OK manifest_hash=%s result_hash=%s" % (res.get("manifest_hash"), res.get("result_hash")))
    log("      validation total=%d error=%d" % (len(val), len(errs)))
    for e in errs[:12]:
        log("      ERROR %s %s" % (e.get("code"), str(e.get("message"))[:180]))

    # ---- 验收 ----
    out_path = os.path.join(ROOT, OUT_PKG.replace("/", os.sep), "runtime.json")
    out_raw = io.open(out_path, encoding="utf-8").read()
    out = json.loads(out_raw)
    oe = out["entries"]
    by_new = {"doc." + e["id"].replace("awake:entry:", "", 1): e for e in oe}
    log("READBACK entries=%d（磁盘 %d，现役 %d）" % (len(oe), len(disk), len(live_ids)))
    out_ids = {"doc." + e["id"].replace("awake:entry:", "", 1) for e in oe}
    log("READBACK id 集合 == 磁盘 id 集合：%s" % (out_ids == set(disk)))

    added = sorted(out_ids - live_ids)
    removed = sorted(live_ids - out_ids)
    log("DIFF vs 现役：新增 %d 档，消失 %d 档" % (len(added), len(removed)))
    log("  新增 %d 档（前 45）：%s" % (len(added), added[:45]))
    if removed:
        log("  ⚠️ 消失：%s" % removed[:10])

    chg = []
    for i in sorted(out_ids & live_ids):
        if by_new[i] != live_by_id[i]:
            chg.append(i)
    log("DIFF vs 现役：既有档内容变化 %d 条" % len(chg))
    for i in chg[:60]:
        a, b = live_by_id[i], by_new[i]
        fields = [f for f in ("summary", "keywords", "expressions", "title") if a.get(f) != b.get(f)]
        log("   %s 变化字段=%s" % (i, fields))

    log("READBACK 'doc.' 出现 %d 次（期望 %d）" % (out_raw.count("doc."), len(oe)))
    log("==== 完成，用时 %.1f 分钟；产物 %s ====" % ((time.time() - t0) / 60.0, OUT_PKG))
    logf.close()


if __name__ == "__main__":
    main()
