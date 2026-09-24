# -*- coding: utf-8 -*-
"""改名后全链复跑：用新 op 前缀重走 register → select → approve → proof → compile。

为什么必须重走全链而不是只跑 compile：
  `compile` 要求 compile proof；proof 由 approve 产；approve 由 select 产；select 由 register 产。
  而 **register 是按「对照 workspace-head 的 content_hash」判要不要重登记**——
  本次只改文件名、档内字节未动 ⇒ 按 hash 判定会认为「无需重登记」。
  ⇒ 所以这里用**新 op 前缀**强制走一遍全链，并且**只强制重登记那 8 档**（其余按需），
    以最小扰动验证「改名后的磁盘状态能编译出正确产物」。

产物：compiled/geo1-v25-underworld
⚠️ 不动 ModuleData（不部署）。⚠️ 不改分类表。
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
OPBASE = "uwrumor20260924a"
OUT_PKG = WS_REL + "/compiled/geo1-v26-uw-rumor"

LOG = os.path.join(WS, "_uw_rumor_chain_log.txt")
MANIFEST = os.path.join(WS, "_uw_rumor_register_batch.json")
BATCH_OUT = os.path.join(WS, "_uw_rumor_register_out.txt")

logf = io.open(LOG, "a", encoding="utf-8", newline="\n")


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
    docs = [p for p in sorted(glob.glob(os.path.join(WS, "authoring", "*.yaml")))
            if not os.path.basename(p).startswith(("_", "source-"))]
    disk = {}
    for p in docs:
        disk[docid_of(p)] = p
    log("==== 前缀归一后全链复跑：磁盘 %d 档 ====" % len(docs))

    head = json.load(io.open(os.path.join(WS, "authoring-v1/workspace-head.json"),
                             encoding="utf-8"))["documents"]
    need = []
    for d, p in sorted(disk.items()):
        rec = head.get(d)
        if rec is None or str(rec.get("content_hash", "")).upper() != sha(p):
            need.append((p, d))
    log("[1/6] 按 hash 判定需重登记 %d 档（改名不改字节 ⇒ 预期 0）" % len(need))

    # ★ 强制把 8 个改名档也登记一遍：新 op 前缀 ⇒ 不会幂等短路
    FORCE = [
        "doc.politics.underworld-alleys", "doc.politics.underworld-gang-leaders",
        "doc.politics.underworld-struggle", "doc.politics.underworld-gangs",
        "doc.politics.underworld-crime-rating", "doc.politics.underworld-blood-money",
        "doc.politics.underworld-bandits", "doc.economy.underworld-smuggling",
    ]
    forced = [(disk[d], d) for d in FORCE if d in disk and (disk[d], d) not in need]
    log("[1b/6] 强制重登记改名档 %d 档" % len(forced))
    batch = need + forced

    manifest = [{"operation": "%s-reg-%d-%s" % (OPBASE, i, d.replace(".", "_")), "path": p}
                for i, (p, d) in enumerate(batch)]
    with io.open(MANIFEST, "w", encoding="utf-8") as mf:
        json.dump(manifest, mf, ensure_ascii=False, indent=1)
    if batch:
        with io.open(BATCH_OUT, "w", encoding="utf-8", newline="\n") as of:
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
            return 1
    log("[2/6] register 完成（%d 档）" % len(batch))

    ids = sorted(disk)
    res, _ = run(["authoring-select", "--operation", "customer.select.v1." + OPBASE,
                  "--document-id", ",".join(ids)])
    if not res.get("ok"):
        log("FATAL select: %s" % json.dumps(res, ensure_ascii=False)[:500])
        logf.close()
        return 1
    sel_id = res["selection"]["selectionId"]
    log("[3/6] select -> %s itemCount=%s" % (sel_id, res["selection"].get("itemCount")))
    if int(res["selection"].get("itemCount") or 0) != len(ids):
        log("FATAL itemCount != 磁盘档数")
        logf.close()
        return 1

    res, _ = run(["authoring-approve", "--operation", "customer.approve.v1." + OPBASE, "--selection", sel_id])
    if not res.get("ok"):
        log("FATAL approve: %s" % json.dumps(res, ensure_ascii=False)[:500])
        logf.close()
        return 1
    app = res.get("approval_proof") or {}
    aid = app.get("approvalId") or app.get("approval_id") or app.get("id")
    log("[4/6] approve -> %s" % aid)

    res, _ = run(["authoring-proof", "--operation", "customer.proof.v1." + OPBASE, "--approval", aid])
    if not res.get("ok"):
        log("FATAL proof: %s" % json.dumps(res, ensure_ascii=False)[:500])
        logf.close()
        return 1
    pf = res.get("compile_proof") or {}
    pid = pf.get("compileProofId") or pf.get("proofId") or pf.get("proof_id") or pf.get("id")
    log("[5/6] proof -> %s" % pid)

    res, _ = run(["compile", "--proof", pid, "--out", OUT_PKG], timeout=7200)
    if not res.get("result_hash"):
        log("FATAL compile: %s" % json.dumps(res, ensure_ascii=False)[:1200])
        logf.close()
        return 1
    val = res.get("validation") or []
    errs = [x for x in val if str(x.get("severity") or x.get("Severity") or "").lower() == "error"]
    warn = [x for x in val if str(x.get("severity") or x.get("Severity") or "").lower() == "warning"]
    log("[6/6] compile OK manifest_hash=%s" % res.get("manifest_hash"))
    log("      validation total=%d error=%d warning=%d" % (len(val), len(errs), len(warn)))
    for e in errs[:12]:
        log("      ERROR %s %s" % (e.get("code"), str(e.get("message"))[:180]))

    # ---- 验收（B 案口径）：条目 id 换新、子 id 留旧 ----
    out_path = os.path.join(ROOT, OUT_PKG.replace("/", os.sep), "runtime.json")
    out = json.load(io.open(out_path, encoding="utf-8"))
    ids_in = set("doc." + e["id"].replace("awake:entry:", "", 1) for e in out["entries"])
    log("      新包档数 = %d" % len(ids_in))

    # ① 新 doc id 必须全在
    miss = [d for d in FORCE if d not in ids_in]
    log("      8 个新 doc id 全在：%s" % ("是" if not miss else "否 -> " + str(miss)))

    # ② 旧 doc id 必须已退场
    OLD_DOC = ["doc.politics.town-alleys", "doc.politics.alley-gang-leaders",
               "doc.politics.alley-struggle", "doc.politics.town-gangs",
               "doc.politics.crime-rating", "doc.politics.blood-money",
               "doc.politics.bandits", "doc.economy.smuggling"]
    left = [d for d in OLD_DOC if d in ids_in]
    log("      8 个旧 doc id 已退场：%s" % ("是" if not left else "否 -> 残留 %s" % left))

    # ③ 子 id 必须保留旧形态（B 案：assertion/expr 不跟首段走）
    #    ⚠️ 产物里子 id 的完整形态是 `assertion.<slug>-1` / `awake:expression:<slug>-<layer>`，
    #       上一次探针拿裸 `expr.<slug>` 去找、恒不命中 —— 那是探针写错，不是内容丢。
    rt = io.open(out_path, encoding="utf-8").read()
    sub_probes = ["assertion.town-alleys-1", "awake:expression:alley-struggle-rumor",
                  "assertion.bandits-1", "awake:expression:smuggling-detail"]
    sub_ok = all(p in rt for p in sub_probes)
    log("      子 id 保留旧形态（B 案）：%s" % ("是" if sub_ok else "否"))
    bad_sub = [p for p in ("assertion.underworld", "awake:expression:underworld") if p in rt]
    log("      子 id 被误改新前缀：%s" % ("无" if not bad_sub else "有 -> %s" % bad_sub))

    allok = (not miss) and (not left) and sub_ok and (not bad_sub)
    log("")
    log("结论：%s" % ("条目 id 已换新、子 id 按 B 案保留旧形态" if allok else "!! 有不符合项"))
    logf.close()
    return 0 if allok else 1


if __name__ == "__main__":
    raise SystemExit(main())
