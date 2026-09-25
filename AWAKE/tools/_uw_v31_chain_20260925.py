# -*- coding: utf-8 -*-
"""世界书「广铺批」编译链：validate -> register(批量,分批) -> select -> approve -> proof -> compile。

本批 = 123 档：
  · hero-*        27（含八位君主）
  · clan-*        74
  · 概念档 22（economy-*/culture-*/geography-*/politics-*/war-* 通识）

从 _head1_chain_20260916.py 派生，四处不同：
  · OPBASE = uwb1a20260925（全新命名空间，避开旧幂等态）
  · 输出包 compiled/geo1-v29-uw-wide（保留 v27）
  · register **分批**（每批 ≤40，宪章硬要求；单进程长跑撞超时）
  · run() 不给 text=，自按 utf-8 解码（CLI 的 WB-AUTHORITY-MUTATION-UNKNOWN 真因在 stderr，
    给 text= 会按 cp936 解坏 ⇒ 丢掉真因）

运行（仓库根为 CWD）：
  python -u tools/_uw_chain_20260925.py
"""
import glob
import hashlib
import io
import json
import os
import re
import subprocess
import sys
import time

ROOT = r"D:/AWAKE-Dev/AWAKE"
WS_REL = "tools/worldbook-studio/workspace/full-geo1"
WS = os.path.join(ROOT, WS_REL)
AUTH = os.path.join(WS, "authoring")
DLL = os.path.join(
    ROOT,
    "tools/worldbook-studio/src/Awake.WorldbookStudio.Cli/bin/Release/net10.0/worldbook-studio.dll",
)
OPBASE = "uwb1d20260925"
OUT_PKG = WS_REL + "/compiled/geo1-v31-settle-hero-alias"
BATCH_SIZE = 40

LOG_PATH = os.path.join(WS, "_uw_v31_chain_log.txt")
MANIFEST = os.path.join(WS, "_uw_v31_manifest.json")
BATCH_OUT = os.path.join(WS, "_uw_v31_register_batch.json")

logf = io.open(LOG_PATH, "a", encoding="utf-8", newline="\n")


def log(msg):
    line = "[%s] %s" % (time.strftime("%H:%M:%S"), msg)
    print(line, flush=True)
    logf.write(line + "\n")
    logf.flush()


def run(args, timeout=7200):
    """⚠️ 不给 text=：CLI 的 stderr 含中文（真因所在），交给调用方按 utf-8 解。"""
    full = args[:1] + ["--workspace", WS_REL] + args[1:]
    r = subprocess.run(["dotnet", DLL] + full, capture_output=True,
                       timeout=timeout, cwd=ROOT)
    out = (r.stdout or b"").decode("utf-8", "replace").strip()
    err = (r.stderr or b"").decode("utf-8", "replace").strip()
    if err:
        for ln in err.splitlines():
            if ln.strip():
                log("    stderr| " + ln.strip()[:200])
    try:
        return json.loads(out), r.returncode
    except Exception:
        return {"_raw": out[:2000]}, r.returncode


def read_docid(path):
    with io.open(path, encoding="utf-8") as fh:
        m = re.search(r"^id:\s*(\S+)", fh.read(), re.M)
    return m.group(1) if m else None


LIST = os.path.join(ROOT, "tools", "_uw_settle_hero_written_20260925.json")


def main():
    t0 = time.time()
    docs = [f for f in sorted(glob.glob(os.path.join(AUTH, "*.yaml")))
            if not os.path.basename(f).startswith(("_", "source-"))]
    # 本批 = 生成器落盘的**精确名单**（不用前缀猜：前缀会误吞 hero-probe 之类旧探针档）
    stems = set(json.load(io.open(LIST, encoding="utf-8")))
    newdocs = [f for f in docs if os.path.basename(f)[:-5] in stems]
    log("==== v31 settle+hero chain 全工作区档数=%d（其中本批 %d / 名单 %d）===="
        % (len(docs), len(newdocs), len(stems)))
    if len(newdocs) != len(stems):
        log("FATAL 名单对不上：磁盘 %d vs 名单 %d；缺=%s"
            % (len(newdocs), len(stems),
               sorted(stems - set(os.path.basename(f)[:-5] for f in newdocs))[:5]))
        logf.close()
        sys.exit(1)

    # ---- [0/6] validate：只看本批诊断 ----
    res, rc = run(["validate"])
    val = res.get("Diagnostics") or res.get("validation") or []
    if isinstance(val, dict):
        val = val.get("diagnostics") or []
    mine = [x for x in val if any(os.path.basename(f)[:-5] in json.dumps(x, ensure_ascii=False)
                                  for f in newdocs)]
    errs = [x for x in mine if str(x.get("Severity") or x.get("severity") or "").lower() == "error"]
    log("[0/6] validate total=%d 本批诊断=%d 本批 error=%d" % (len(val), len(mine), len(errs)))
    for e in errs[:20]:
        log("  ERROR %s | %s | %s" % (e.get("Code"), str(e.get("Message"))[:160],
                                      str(e.get("Path"))[-90:]))
    if errs:
        log("FATAL 本批 schema 不过，已停在 register 之前")
        logf.close()
        sys.exit(1)

    # ---- [1/6] 批量 register（分批）----
    head = json.load(io.open(os.path.join(WS, "authoring-v1/workspace-head.json"),
                             encoding="utf-8"))["documents"]
    need = []
    for f in docs:
        b = open(f, "rb").read()
        h = hashlib.sha256(b).hexdigest().upper()
        docid = read_docid(f)
        rec = head.get(docid) if docid else None
        if rec is None or str(rec.get("content_hash", "")).upper() != h:
            need.append((f, docid))
    # 只注册本批 + 顺带任何未注册的（保持工作区一致），但分批跑
    need_new = [(f, d) for f, d in need if os.path.basename(f)[:-5] in stems]
    # 先注册本批（保证在途优先），再补其余欠账
    rest = [(f, d) for f, d in need if os.path.basename(f)[:-5] not in stems]
    ordered = need_new + rest
    log("[1/6] 待注册总 %d（本批 %d / 其余欠账 %d）；分 %d 批（每批 ≤%d）"
        % (len(need), len(need_new), len(rest),
           (len(ordered) + BATCH_SIZE - 1) // BATCH_SIZE, BATCH_SIZE))

    done = 0
    for bi in range(0, len(ordered), BATCH_SIZE):
        chunk = ordered[bi:bi + BATCH_SIZE]
        manifest = [{"operation": "%s-reg-b%d-%s" % (OPBASE, bi // BATCH_SIZE + 1, d or os.path.basename(f)),
                     "path": f} for f, d in chunk]
        with io.open(MANIFEST, "w", encoding="utf-8") as mf:
            json.dump(manifest, mf, ensure_ascii=False, indent=1)
        tb = time.time()
        with io.open(BATCH_OUT, "w", encoding="utf-8") as of:
            p = subprocess.Popen(
                ["dotnet", DLL, "authoring-register-batch", "--manifest", MANIFEST,
                 "--workspace", WS_REL],
                cwd=ROOT, stdout=of, stderr=subprocess.PIPE,
            )
            for line in p.stderr:
                s = line.decode("utf-8", "replace").strip()
                if s:
                    log("  b%d| %s" % (bi // BATCH_SIZE + 1, s[:200]))
            p.wait()
        if p.returncode != 0:
            log("FATAL batch register[%d] rc=%d（详见 %s）" % (bi // BATCH_SIZE + 1,
                                                              p.returncode, BATCH_OUT))
            logf.close()
            sys.exit(1)
        rr = json.load(io.open(BATCH_OUT, encoding="utf-8"))
        if not rr.get("ok"):
            log("FATAL batch register[%d]: %s" % (bi // BATCH_SIZE + 1,
                                                  json.dumps(rr, ensure_ascii=False)[:500]))
            logf.close()
            sys.exit(1)
        done += rr.get("count") or len(chunk)
        log("[1/6] 批 %d/%d 完成：%d 档，%.1f 秒"
            % (bi // BATCH_SIZE + 1, (len(ordered) + BATCH_SIZE - 1) // BATCH_SIZE,
               rr.get("count"), time.time() - tb))
    log("[1/6] register 合计 %d 档" % done)

    # ---- [2/6] select（★必须全量）----
    # 🚨 09-25 血的教训：这里若只选「本批新增」，编译出的包就只有本批 —— v28 因此
    #    从 558 档掉到 123 档，等于把已有内容全删。select 选的是**要进包的全部档**。
    ids = [read_docid(f) for f in docs]
    res, _ = run(["authoring-select", "--operation", "customer.select.v1." + OPBASE,
                  "--document-id", ",".join(ids)])
    if not res.get("ok"):
        log("FATAL select: %s" % json.dumps(res, ensure_ascii=False)[:600])
        logf.close()
        sys.exit(1)
    sel_id = res["selection"]["selectionId"]
    json.dump(res, io.open(os.path.join(WS, "_uw_v31_select.json"), "w", encoding="utf-8"),
              ensure_ascii=False, indent=1)
    item_cnt = res["selection"].get("itemCount")
    log("[2/6] select -> %s itemCount=%s（全量 %d 档；本批 %d 档已含在内）"
        % (sel_id, item_cnt, len(ids), len(newdocs)))
    if not item_cnt or int(item_cnt) < len(ids):
        log("FATAL select 只选中 %s 档，少于工作区 %d 档 —— 会掉档，停。" % (item_cnt, len(ids)))
        logf.close()
        sys.exit(1)

    # ---- [3/6] approve ----
    res, _ = run(["authoring-approve", "--operation", "customer.approve.v1." + OPBASE,
                  "--selection", sel_id])
    if not res.get("ok"):
        log("FATAL approve: %s" % json.dumps(res, ensure_ascii=False)[:600])
        logf.close()
        sys.exit(1)
    app = res.get("approval_proof") or {}
    aid = app.get("approvalId") or app.get("approval_id") or app.get("id")
    log("[3/6] approve -> %s" % aid)

    # ---- [4/6] proof ----
    res, _ = run(["authoring-proof", "--operation", "customer.proof.v1." + OPBASE,
                  "--approval", aid])
    if not res.get("ok"):
        log("FATAL proof: %s" % json.dumps(res, ensure_ascii=False)[:600])
        logf.close()
        sys.exit(1)
    pf = res.get("compile_proof") or {}
    pid = pf.get("compileProofId") or pf.get("proofId") or pf.get("proof_id") or pf.get("id")
    log("[4/6] proof -> %s" % pid)

    # ---- [5/6] compile ----
    res, _ = run(["compile", "--proof", pid, "--out", OUT_PKG])
    json.dump(res, io.open(os.path.join(WS, "_uw_v31_compile.json"), "w", encoding="utf-8"),
              ensure_ascii=False, indent=1)
    if not res.get("result_hash"):
        log("FATAL compile: %s" % json.dumps(res, ensure_ascii=False)[:900])
        logf.close()
        sys.exit(1)
    val2 = res.get("validation") or []
    if isinstance(val2, dict):
        val2 = val2.get("diagnostics") or []
    errs2 = [x for x in val2 if str(x.get("severity") or x.get("Severity") or "").lower() == "error"]
    log("[5/6] compile OK manifest_hash=%s" % res.get("manifest_hash"))
    log("  result_hash=%s" % res.get("result_hash"))
    log("  validation total=%d error=%d" % (len(val2), len(errs2)))

    # ---- [6/6] 回读产物 ----
    out_dir = os.path.join(ROOT, OUT_PKG.replace("/", os.sep))
    # 掉档守门：拿「上一个包」当基线，entries 少一个就是事故
    prev_entries = None
    compiled_root = os.path.join(WS, "compiled")
    sibs = [d for d in os.listdir(compiled_root)
            if d.startswith("geo1-v") and os.path.isdir(os.path.join(compiled_root, d))]
    sibs.sort(key=lambda s: int(re.findall(r"v(\d+)", s)[0]) if re.findall(r"v(\d+)", s) else -1)
    my_ver = int(re.findall(r"v(\d+)", OUT_PKG)[0])
    older = [s for s in sibs
             if re.findall(r"v(\d+)", s) and int(re.findall(r"v(\d+)", s)[0]) < my_ver]
    if older:
        pp = os.path.join(compiled_root, older[-1], "runtime.json")
        if os.path.exists(pp):
            prev_entries = len(json.load(io.open(pp, encoding="utf-8")).get("entries") or [])
            log("基线：%s entries=%d" % (older[-1], prev_entries))

    for name in ("manifest.json", "runtime.json"):
        pth = os.path.join(out_dir, name)
        if not os.path.exists(pth):
            log("READBACK missing %s" % name)
            continue
        d = json.load(io.open(pth, encoding="utf-8"))
        ents = d.get("entries") or []
        refs = sum(1 for e in ents if (e.get("extensions") or {}).get("entityRefs"))
        log("READBACK %s packageId=%s worldId=%s entries=%d 带锚点=%d"
            % (name, d.get("packageId"), d.get("worldId"), len(ents), refs))
        if name == "runtime.json":
            if prev_entries is not None:
                if len(ents) < prev_entries:
                    log("🚨 掉档！本包 %d < 基线 %d（少 %d）—— 该包不可用"
                        % (len(ents), prev_entries, prev_entries - len(ents)))
                else:
                    log("✅ 档数未掉：%d ≥ 基线 %d（+%d）"
                        % (len(ents), prev_entries, len(ents) - prev_entries))
            for pref in ("hero-", "clan-", "economy-", "culture-", "geography-",
                         "politics-", "war-"):
                c = sum(1 for e in ents if pref in str(e.get("id", "")))
                if c:
                    log("  本批入包 %-12s %d" % (pref, c))

    log("==== v31 settle+hero chain 完成，总用时 %.1f 分钟 ====" % ((time.time() - t0) / 60.0))
    logf.close()


if __name__ == "__main__":
    main()
