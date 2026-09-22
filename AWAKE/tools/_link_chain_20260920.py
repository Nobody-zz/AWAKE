# -*- coding: utf-8 -*-
"""互引边表入包（09-20）链式编译：register → select → approve → proof → compile。

与上一轮（暗面批）唯一的差别：**SchemaRoot 里多了一份 `link-registry.v1.json`**，
编译器会把它编进每个条目的 `extensions.links`。

验收（本脚本自己断言，不靠眼看）：
  1. `source-report.json` 的 `link_edges` == 边表条数；
  2. 带 `extensions.links` 的条目数 == 边表里有出边的条目数；
  3. **与上一包逐条比**：id 集合相同、除 `extensions` 外每个字段逐字节相同 ⇒ 证明这次只加了边、没动内容；
  4. 边表里出现的端点全部在包内 ⇒ 没有悬空引用。

op_id 规则：`link20260920a-<用途>`。⚠️ register 的 operation 也要带 OPBASE，否则幂等短路 ⇒ WB-AUTHORITY-CAS-409。
运行（仓库根为 CWD）：python -u tools/_link_chain_20260920.py
"""
import glob
import hashlib
import io
import json
import os
import subprocess
import sys
import time

ROOT = r"D:/AWAKE-Dev/AWAKE"
WS_REL = "tools/worldbook-studio/workspace/full-geo1"
WS = os.path.join(ROOT, WS_REL)
DLL = os.path.join(ROOT, "tools/worldbook-studio/src/Awake.WorldbookStudio.Cli/bin/Release/net10.0/worldbook-studio.dll")
OPBASE = "link20260920a"
OUT_PKG = WS_REL + "/compiled/geo1-v22-links"
PREV_PKG = os.path.join(WS, "compiled/geo1-v21-dark")
REGISTRY = os.path.join(ROOT, "docs/worldbook-studio-plan/link-registry.v1.json")

LOG_PATH = os.path.join(WS, "_link_chain_log.txt")
MANIFEST = os.path.join(WS, "_link_register_batch.json")
BATCH_OUT = os.path.join(WS, "_link_register_out.txt")

logf = io.open(LOG_PATH, "a", encoding="utf-8", newline="\n")


def log(msg):
    line = "[%s] %s" % (time.strftime("%H:%M:%S"), msg)
    print(line, flush=True)
    logf.write(line + "\n")
    logf.flush()


def run(args, timeout=7200):
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
    log("==== 边表入包链式编译：磁盘 %d 档 ====" % len(docs))

    reg = json.load(io.open(REGISTRY, encoding="utf-8"))
    reg_edges = reg["edges"]
    with_out = {}
    for e in reg_edges:
        with_out.setdefault(e["from"], 0)
        with_out[e["from"]] += 1
    log("[0/6] 边表 %d 条；有出边的条目 %d 个" % (len(reg_edges), len(with_out)))

    head = json.load(io.open(os.path.join(WS, "authoring-v1/workspace-head.json"),
                             encoding="utf-8"))["documents"]
    need = []
    for d, p in sorted(disk.items()):
        rec = head.get(d)
        if rec is None or str(rec.get("content_hash", "")).upper() != sha(p):
            need.append((p, d))
    log("[1/6] 需重登记 %d 档" % len(need))

    manifest = [{"operation": "%s-reg-%d-%s" % (OPBASE, i, d.replace(".", "_")), "path": p}
                for i, (p, d) in enumerate(need)]
    with io.open(MANIFEST, "w", encoding="utf-8") as mf:
        json.dump(manifest, mf, ensure_ascii=False, indent=1)
    with io.open(BATCH_OUT, "w", encoding="utf-8") as of:
        p2 = subprocess.Popen(["dotnet", DLL, "authoring-register-batch", "--manifest", MANIFEST,
                               "--workspace", WS_REL], cwd=ROOT, stdout=of, stderr=subprocess.PIPE)
        for line in iter(p2.stderr.readline, b""):
            s = line.decode("utf-8", "replace").strip()
            if s:
                log("  " + s)
        p2.wait()
    if p2.returncode != 0:
        log("FATAL batch register rc=%d" % p2.returncode)
        logf.close()
        sys.exit(1)
    log("[2/6] register 完成")

    ids = sorted(disk)
    res, _ = run(["authoring-select", "--operation", "customer.select.v1." + OPBASE, "--document-id", ",".join(ids)])
    if not res.get("ok"):
        log("FATAL select: %s" % json.dumps(res, ensure_ascii=False)[:500])
        logf.close()
        sys.exit(1)
    sel_id = res["selection"]["selectionId"]
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
    json.dump(res, io.open(os.path.join(WS, "_link_compile.json"), "w", encoding="utf-8"),
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

    out_dir = os.path.join(ROOT, OUT_PKG.replace("/", os.sep))
    out = json.load(io.open(os.path.join(out_dir, "runtime.json"), encoding="utf-8"))
    oe = out["entries"]
    log("READBACK entries=%d（磁盘 %d）" % (len(oe), len(disk)))

    # ---- 验收 1：source-report ----
    sr = json.load(io.open(os.path.join(out_dir, "source-report.json"), encoding="utf-8"))
    log("CHECK1 source-report.link_edges=%s（期望 %d）%s"
        % (sr.get("link_edges"), len(reg_edges), "OK" if sr.get("link_edges") == len(reg_edges) else "**FAIL**"))

    # ---- 验收 2：带边的条目数 ----
    carry = [e for e in oe if (e.get("extensions") or {}).get("links")]
    log("CHECK2 带 extensions.links 的条目=%d（期望 %d）%s"
        % (len(carry), len(with_out), "OK" if len(carry) == len(with_out) else "**FAIL**"))
    pkg_ids = {e["id"] for e in oe}
    log("CHECK3 边表端点是否全在包内：%s"
        % ("OK" if all(x in pkg_ids for x in list(with_out) + [e["to"] for e in reg_edges]) else "**FAIL**"))

    # ---- 验收 4：与上一包逐条比 ----
    prev = json.load(io.open(os.path.join(PREV_PKG, "runtime.json"), encoding="utf-8"))
    pb = {e["id"]: e for e in prev["entries"]}
    cb = {e["id"]: e for e in oe}
    log("CHECK4 id 集合相同：%s（上一包 %d，本包 %d）" % (set(pb) == set(cb), len(pb), len(cb)))
    diff_fields = {}
    for i in sorted(set(pb) & set(cb)):
        a, b = pb[i], cb[i]
        keys = set(a) | set(b)
        for k in keys:
            if a.get(k) != b.get(k):
                diff_fields[k] = diff_fields.get(k, 0) + 1
    log("CHECK5 与上一包比，各字段不同的条目数：%s" % json.dumps(diff_fields, ensure_ascii=False))
    only_links = all(k in ("extensions",) for k in diff_fields)
    log("       ⇒ 只动了 extensions（即只加了边，内容未变）：%s" % only_links)
    log("==== 完成，用时 %.1f 分钟；产物 %s ====" % ((time.time() - t0) / 60.0, OUT_PKG))
    logf.close()


if __name__ == "__main__":
    main()
