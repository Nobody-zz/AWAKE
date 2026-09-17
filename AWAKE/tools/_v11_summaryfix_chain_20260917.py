# -*- coding: utf-8 -*-
"""摘要台账清理重编（v11）：把那两截作者台账从线上数据里去掉。

甲方 09-17 拍板「1不动 2修掉」的第 2 件。

改了什么（源档，两个目录同步）：
  doc.geography.mines-lycaron  summary  去「（沿革断言…承 IMPL §3.1 留痕：economy 档承载一条政治沿革）」
  doc.politics.throne-saneopa  summary  去「定都史为 D 级裁定（Max 09-12，登记于研究稿）。」
留痕（原值逐字）：docs/worldbook-migration/corrections_20260917/SUMMARY-AUTHORING-LEDGER-20260917.md
生成器防复发：_gen_new10_20260912.py / _gen_batch2_capitals_20260912.py 同步已改。

基线：上线包 ModuleData/Worldbook/packages/calradia/runtime.json
     == compiled/geo1-v6/runtime.json（md5 f0a5820b… 逐字节相同，448 条）
     → 选择快照 authoring-v1/selections/selection.c64bdf0dfa164daf9c43f49a3079d060.json
     （与 09-16 `_v2j_kwclean` 同基线，两次重编可直接互比）

预期待归因的差异（相对上线包）只有两类：
  (A) 这 2 条的 summary —— 本次要修的；
  (B) 448 条 keywords 里的 `doc.<id>` 消失 —— 编译器 K1 那笔（09-16 已在物理 DLL 里，
      只是上线包是 K1 之前编的）。运行时另有一道 R3 拦截（`doc.` 前缀不进索引），
      所以 (B) 对线上行为**净零**，不是本次引入的新行为。
      旁证：`compiled/geo1-v10-kwclean` 也是 K1 之后编的，与本次产物应当**只差这 2 条摘要**。

op_id 规则：`v11sf2026-<用途>`。⚠️ op_id 幂等：同 id 重跑会被短路成「返回上次结果」而**不重读磁盘**，故一律用新 id。

运行（仓库根为 CWD）：
  python -u tools/worldbook-studio/workspace/full-geo1/_v11_summaryfix_20260917.py
"""
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
DLL = os.path.join(
    ROOT,
    "tools/worldbook-studio/src/Awake.WorldbookStudio.Cli/bin/Release/net10.0/worldbook-studio.dll",
)
OPBASE = "v11sf20260917"
OUT_PKG = WS_REL + "/compiled/geo1-v11-summaryfix"
BASE_SELECTION = "authoring-v1/selections/selection.c64bdf0dfa164daf9c43f49a3079d060.json"
LIVE_PKG = os.path.join(ROOT, "ModuleData/Worldbook/packages/calradia/runtime.json")
PEER_PKG = os.path.join(WS, "compiled/geo1-v10-kwclean/runtime.json")
LIVE_V6 = os.path.join(WS, "compiled/geo1-v6/runtime.json")

# 本次要清的两档：doc id -> (台账特征串, 期望新值)
TARGETS = {
    "doc.geography.mines-lycaron": {
        "forbidden": ["IMPL", "留痕", "档承载", "§"],
        "expect": "吕卡隆银矿与兵祸的由来。",
    },
    "doc.politics.throne-saneopa": {
        "forbidden": ["D 级裁定", "Max", "研究稿", "登记于"],
        "expect": "萨涅俄帕：隘口之上的内陆商埠与涅雷采斯家的京城；旧都岁月与迁都后的迁回之争。",
    },
}

LOG_PATH = os.path.join(WS, "_v11_chain_log.txt")
MANIFEST = os.path.join(WS, "_v11_register_batch.json")
BATCH_OUT = os.path.join(WS, "_v11_register_out.txt")

logf = io.open(LOG_PATH, "a", encoding="utf-8", newline="\n")


def log(msg):
    line = "[%s] %s" % (time.strftime("%H:%M:%S"), msg)
    print(line, flush=True)
    logf.write(line + "\n")
    logf.flush()


def run(args, timeout=7200):
    full = args[:1] + ["--workspace", WS_REL] + args[1:]
    r = subprocess.run(["dotnet", DLL] + full, capture_output=True, text=True,
                       timeout=timeout, cwd=ROOT)
    out = (r.stdout or "").strip() or (r.stderr or "").strip()
    try:
        return json.loads(out), r.returncode
    except Exception:
        return {"_raw": out[:900], "_stderr": (r.stderr or "")[:900]}, r.returncode


def sha256(path):
    return hashlib.sha256(open(path, "rb").read()).hexdigest().upper()


def summary_of(entries_by_id, docid):
    e = entries_by_id.get("awake:entry:" + docid.replace("doc.", "", 1))
    if e is None:
        return None
    s = e.get("summary")
    if isinstance(s, dict):
        return s.get("zh-CN")
    return s


def diff_kinds(a, b):
    """a = 基线（字典 id->entry），b = 新包。返回分类计数。"""
    kinds = {"summary": [], "keywords": [], "exprs": [], "other": []}
    for i in sorted(set(a) | set(b)):
        ea, eb = a.get(i), b.get(i)
        if ea == eb:
            continue
        if ea is None or eb is None:
            kinds["other"].append(i)
            continue
        touched = False
        if ea.get("summary") != eb.get("summary"):
            kinds["summary"].append(i)
            touched = True
        if ea.get("keywords") != eb.get("keywords"):
            kinds["keywords"].append(i)
            touched = True
        if ea.get("expressions") != eb.get("expressions"):
            kinds["exprs"].append(i)
            touched = True
        if not touched:
            kinds["other"].append(i)
    return kinds


def main():
    t0 = time.time()

    # ---- [0/6] 基线 ----
    sel = json.load(io.open(os.path.join(WS, BASE_SELECTION), encoding="utf-8"))
    ids = [x["document_id"] for x in sel["items"]]
    log("==== v11 摘要台账清理重编：基线选择 %s，档数=%d ====" % (sel["selection_id"], len(ids)))

    live_raw = io.open(LIVE_PKG, encoding="utf-8").read()
    live = json.loads(live_raw)
    live_entries = live["entries"]
    live_ids = {e["id"] for e in live_entries}
    log("[0/6] 上线包 md5 与 geo1-v6 是否一致：%s"
        % (sha256(LIVE_PKG) == sha256(LIVE_V6)))
    log("      上线包条目数=%d，'doc.' 出现 %d 次" % (len(live_entries), live_raw.count("doc.")))
    for d in TARGETS:
        log("      上线包里 %s 的 summary = %s" % (d, summary_of({e["id"]: e for e in live_entries}, d)))

    # ---- [1/6] 找与注册表 hash 不一致的档（期望恰好 2） ----
    head = json.load(io.open(os.path.join(WS, "authoring-v1/workspace-head.json"),
                             encoding="utf-8"))["documents"]
    need, missing = [], []
    for docid in ids:
        rec = head.get(docid)
        if rec is None or not os.path.exists(os.path.join(WS, rec["path"])):
            missing.append(docid)
            continue
        if str(rec.get("content_hash", "")).upper() != sha256(os.path.join(WS, rec["path"])):
            need.append((os.path.join(WS, rec["path"]), docid))
    log("[1/6] 需重登记=%d %s；缺档=%d %s"
        % (len(need), [d for _p, d in need], len(missing), missing[:5]))

    if missing:
        log("FATAL 基线选择里有档已不在注册表/磁盘上，停止（避免悄悄改内容面）")
        logf.close()
        sys.exit(1)
    if sorted(d for _p, d in need) != sorted(TARGETS):
        log("FATAL 需重登记的档与预期两档不符 —— 磁盘上还有别的改动，停止以免混入")
        logf.close()
        sys.exit(1)

    # ---- [2/6] 批量 register（仅这 2 档） ----
    manifest = [{"operation": "v11sf-reg-" + d, "path": p} for p, d in need]
    with io.open(MANIFEST, "w", encoding="utf-8") as mf:
        json.dump(manifest, mf, ensure_ascii=False, indent=1)
    with io.open(BATCH_OUT, "w", encoding="utf-8") as of:
        p2 = subprocess.Popen(
            ["dotnet", DLL, "authoring-register-batch", "--manifest", MANIFEST,
             "--workspace", WS_REL],
            cwd=ROOT, stdout=of, stderr=subprocess.PIPE,
            text=True, encoding="utf-8", errors="replace")
        for line in p2.stderr:
            if line.strip():
                log("  " + line.strip())
        p2.wait()
    if p2.returncode != 0:
        log("FATAL batch register rc=%d" % p2.returncode)
        logf.close()
        sys.exit(1)
    log("[2/6] register %d 档完成" % len(need))

    # ---- [3/6] select 这 448 档 ----
    res, _ = run(["authoring-select", "--operation", "customer.select.v1." + OPBASE,
                  "--document-id", ",".join(ids)])
    if not res.get("ok"):
        log("FATAL select: %s" % json.dumps(res, ensure_ascii=False)[:500])
        logf.close()
        sys.exit(1)
    sel_id = res["selection"]["selectionId"]
    json.dump(res, io.open(os.path.join(WS, "_v11_select.json"), "w", encoding="utf-8"),
              ensure_ascii=False, indent=1)
    log("[3/6] select -> %s itemCount=%s" % (sel_id, res["selection"].get("itemCount")))
    if int(res["selection"].get("itemCount") or 0) != len(ids):
        log("FATAL itemCount 与基线不一致")
        logf.close()
        sys.exit(1)

    # ---- [4/6] approve ----
    res, _ = run(["authoring-approve", "--operation", "customer.approve.v1." + OPBASE,
                  "--selection", sel_id])
    if not res.get("ok"):
        log("FATAL approve: %s" % json.dumps(res, ensure_ascii=False)[:500])
        logf.close()
        sys.exit(1)
    app = res.get("approval_proof") or {}
    aid = app.get("approvalId") or app.get("approval_id") or app.get("id")
    json.dump(res, io.open(os.path.join(WS, "_v11_approve.json"), "w", encoding="utf-8"),
              ensure_ascii=False, indent=1)
    log("[4/6] approve -> %s" % aid)

    # ---- [5/6] proof ----
    res, _ = run(["authoring-proof", "--operation", "customer.proof.v1." + OPBASE,
                  "--approval", aid])
    if not res.get("ok"):
        log("FATAL proof: %s" % json.dumps(res, ensure_ascii=False)[:500])
        logf.close()
        sys.exit(1)
    pf = res.get("compile_proof") or {}
    pid = pf.get("compileProofId") or pf.get("proofId") or pf.get("proof_id") or pf.get("id")
    json.dump(res, io.open(os.path.join(WS, "_v11_proof.json"), "w", encoding="utf-8"),
              ensure_ascii=False, indent=1)
    log("[5/6] proof -> %s" % pid)

    # ---- [6/6] compile ----
    res, _ = run(["compile", "--proof", pid, "--out", OUT_PKG], timeout=7200)
    json.dump(res, io.open(os.path.join(WS, "_v11_compile.json"), "w", encoding="utf-8"),
              ensure_ascii=False, indent=1)
    if not res.get("result_hash"):
        log("FATAL compile: %s" % json.dumps(res, ensure_ascii=False)[:900])
        logf.close()
        sys.exit(1)
    val = res.get("validation") or []
    errs = [x for x in val
            if str(x.get("severity") or x.get("Severity") or "").lower() == "error"]
    log("[6/6] compile OK manifest_hash=%s result_hash=%s" % (res.get("manifest_hash"),
                                                             res.get("result_hash")))
    log("      validation total=%d error=%d" % (len(val), len(errs)))
    for e in errs[:10]:
        log("      ERROR %s %s" % (e.get("code"), str(e.get("message"))[:160]))

    # ---- 验收 ----
    out_path = os.path.join(ROOT, OUT_PKG.replace("/", os.sep), "runtime.json")
    out_raw = io.open(out_path, encoding="utf-8").read()
    out = json.loads(out_raw)
    oe = out["entries"]
    by_new = {e["id"]: e for e in oe}
    by_live = {e["id"]: e for e in live_entries}
    by_peer = ({e["id"]: e for e in json.load(io.open(PEER_PKG, encoding="utf-8"))["entries"]}
               if os.path.exists(PEER_PKG) else {})

    log("READBACK entries=%d（基线 %d）" % (len(oe), len(live_entries)))
    log("READBACK id 集合与基线相同：%s" % ({e["id"] for e in oe} == live_ids))

    ok = True
    for docid, spec in sorted(TARGETS.items()):
        got = summary_of(by_new, docid)
        bad = [t for t in spec["forbidden"] if t in (got or "")]
        match = (got or "").strip() == spec["expect"]
        log("READBACK %s summary=%s" % (docid, got))
        log("         → 台账串残留=%s，与期望逐字相同=%s" % (bad or "无", match))
        ok = ok and not bad and match

    kinds_live = diff_kinds(by_live, by_new)
    log("DIFF vs 上线包：summary 变 %d 条 %s；keywords 变 %d 条；expressions 变 %d 条；其它 %d %s"
        % (len(kinds_live["summary"]), kinds_live["summary"],
           len(kinds_live["keywords"]), len(kinds_live["exprs"]),
           len(kinds_live["other"]), kinds_live["other"][:5]))
    expect_summary = {"awake:entry:" + d.replace("doc.", "", 1) for d in TARGETS}
    ok = ok and set(kinds_live["summary"]) == expect_summary
    ok = ok and not kinds_live["exprs"] and not kinds_live["other"]

    if by_peer:
        kinds_peer = diff_kinds(by_peer, by_new)
        log("DIFF vs geo1-v10-kwclean（同为 K1 之后编的）：summary 变 %d 条 %s；keywords 变 %d 条；"
            "expressions 变 %d 条；其它 %d %s"
            % (len(kinds_peer["summary"]), kinds_peer["summary"],
               len(kinds_peer["keywords"]), len(kinds_peer["exprs"]),
               len(kinds_peer["other"]), kinds_peer["other"][:5]))

    log("READBACK 'doc.' 出现 %d 次（期望 %d = 每条 extensions.sourceDocumentId 一处）"
        % (out_raw.count("doc."), len(oe)))
    ok = ok and out_raw.count("doc.") == len(oe)

    log("VERDICT %s" % ("PASS" if ok else "FAIL"))
    log("==== v11 完成，总用时 %.1f 分钟；产物 %s ====" % ((time.time() - t0) / 60.0, OUT_PKG))
    logf.close()
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
