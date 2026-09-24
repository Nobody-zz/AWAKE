# -*- coding: utf-8 -*-
"""clan 绑定全链探针（2026-09-24）。

问的是同一件事：**一条说法绑了 clan 之后，真能不能按"他是不是这家人"分流？**

治法（零替身）：照真实五步（register → select → approve → proof → compile）把带 clan_ids
条件的档编译成运行包，再用 tools/worldbook-runtime-sim（它把 src/*.cs 整编进自己的程序集，
`CapOf` 与判定器都是真件）跑 query 矩阵。

判据（缺一即判红）：
  ① 同 clan    -> 必须拿到（阳性对照；没有这条，②说不准是"查询恒返空"）
  ② 别的 clan  -> 必须拿不到
  ③ 无 clan    -> 必须拿不到（"不知道他是谁家的人"不得回退成"给他吧"）
  ④ 空转护栏   -> 三行都出现过
  ⑤ 编译落点   -> 运行包里真出现 clan_ids（否则①②只是"条件根本没进去"）

**变异检验（⑥⑦⑧，2026-09-24 补）**：光有 ① 只排掉了"查询恒返空"。
  ②的"恒真"嫌疑还在——万一判定器压根没读 clan_ids，随便什么查询都拿不到，
  ②也照样 PASS。⇒ 把**条件侧**也做一次对照：同一份档、同一套 query，
  只把 grant 里的 clan_ids 从 TARGET 换成 OTHER，重跑一遍。
    ⑥ 条件换成 OTHER 后，用 TARGET 查 -> 必须翻成 **0**（原先是 1）
    ⑦ 条件换成 OTHER 后，用 OTHER  查 -> 必须翻成 **>0**（原先是 0）
    ⑧ 变异轮里无 clan 查 -> 仍为 0
  ⑥⑦ 一起构成"读数真的跟着 clan 走"的证据：不翻转，说明这道闸没在测。

输出：tools/_uw_clan_gate_probe_20260924.json
"""
import json, os, subprocess, sys

ROOT = r"D:\AWAKE-Dev\AWAKE"
SIM = os.path.join(ROOT, "tools", "worldbook-runtime-sim", "bin", "Release", "net10.0-windows", "WorldbookRuntimeSim.dll")
STUDIO = os.path.join(ROOT, "tools", "worldbook-studio", "src", "Awake.WorldbookStudio.Cli", "bin", "Release", "net10.0", "worldbook-studio.dll")

OPBASE = "uwclan" + __import__("time").strftime("%m%d%H%M%S")
WS = os.path.join(ROOT, "tools", "worldbook-studio", "workspace", "full-geo1")
WS_REL = "tools/worldbook-studio/workspace/full-geo1"
OUT = os.path.join(ROOT, "tools", "_uw_clan_gate_probe_20260924.json")
LOG = os.path.join(WS, "_clan_probe_log.txt")

TARGET_CLAN = "clan_empire_south_1"
OTHER_CLAN = "clan_empire_south_2"
DOC_ID = "doc.politics.clan-probe"
DOC_FILE = "clan-probe.yaml"
# 变异轮：同一份档，只把 grant 里的 clan_ids 换成 OTHER_CLAN。档 id/文件名都另起。
DOC_ID_MUT = "doc.politics.clan-probe-mut"
DOC_FILE_MUT = "clan-probe-mut.yaml"

logf = None


def log(msg):
    print(msg, flush=True)
    if logf:
        logf.write(msg + "\n")
        logf.flush()


def studio(args, timeout=7200):
    full = args[:1] + ["--workspace", WS_REL] + args[1:]
    r = subprocess.run(["dotnet", STUDIO] + full, capture_output=True, timeout=timeout, cwd=ROOT)
    so = (r.stdout or b"").decode("utf-8", "replace").strip()
    se = (r.stderr or b"").decode("utf-8", "replace").strip()
    out = so or se
    try:
        return json.loads(out), r.returncode, se
    except Exception:
        return {"_raw": out[:1500]}, r.returncode, se


def step(n, msg):
    log("\n[%s] %s" % (n, msg))


# 一份最小但**逐字段照 schema 填全**的档。
# 出处走 sources（全库 558 档走的都是这条；author_created 全库零用例，别当第一个）。
# status 不写 canon ⇒ 不触发 schema 里 canon 专属的 approved_author_created 严要求。
SRC = {
    "source_id": "source.calradia.game.urban-dark",
    "source_version": "bannerlord-1.3.15.110062",
    "source_content_hash": "b4d5c8f431ccd2464129a318bab6b9243926a93f7df82b01799fd50fa3242f47",
    "locator": "bannerlord.db#localization.pKMVhx2v",
    "quote_hash": "723CCEE8A0C7230111A56D1C5C77142761482399A3982BCE547B0883539DE753",
    "quote": "那些后巷子里的渣滓所说的龌龊事情总是花样繁多。",
}


def build_doc(clan_code=TARGET_CLAN, doc_id=DOC_ID):
    return {
        "schema_version": "awake.worldbook.authoring.v1",
        "id": doc_id,
        "revision": 1,
        "title": {"zh-CN": "家族探针条目", "en": "Clan Probe"},
        "status": "accepted_variant",
        "domain": "politics",
        "subdomain": "law",
        "universe": "awake_current",
        "era": {"key": "current", "certainty": "bounded"},
        "content_tier": "base",
        "aliases": {"zh-CN": [], "en": []},
        "summary": {"zh-CN": "家族绑定探针：这条知识只送给某个家族的人。"},
        "registry_bindings": {
            "profile_registry_version": "1.0.0",
            "profile_registry_hash": "309D10583E7B4AF05F28DE2C2E67A2528CB3D6C45752369C0F7D160FABC3F3A5",
            "referral_registry_version": "1.0.0",
            "referral_registry_hash": "6E17075F1F967BE28C0437121DB50E5C30809C4EFC3D2A5F22AD9BB0B3B926CD",
        },
        "sources": [dict(SRC)],
        "authority": {"owner": "awake_canon", "conflict_policy": "canon_wins"},
        "assertions": [
            {
                "id": "assertion.clan-probe-1",
                "revision": 1,
                "kind": "fact",
                "text": {"zh-CN": "同族人说得出口的那句话。"},
                "sources": [dict(SRC)],
                "expressions": [
                    {
                        "id": "expr.clan-probe-detail",
                        "revision": 1,
                        "layer": "detail",
                        "text": {"zh-CN": "同族人说得出口的那句话。"},
                        "sources": [dict(SRC)],
                        "grants": [
                            {
                                "profile_id": "profile.noble",
                                "scope": "elite",
                                "min_detail": "detail",
                                # ★ 值必须是 entity.clan.<code> 三段
                                "clan_ids": ["entity.clan." + clan_code],
                            }
                        ],
                        "denies": [],
                    }
                ],
            }
        ],
    }


def to_yaml(doc):
    """把 dict 直译成 yaml（只用标量/映射/数组三种形态，缩进 2）。"""
    L = []

    def emit(obj, ind, key=None):
        pad = " " * ind
        if isinstance(obj, dict):
            if key is not None:
                L.append("%s%s:" % (pad, key))
                pad = " " * (ind + 2)
            for k, v in obj.items():
                emit(v, ind + 2 if key is not None else ind, k)
        elif isinstance(obj, list):
            if key is not None:
                L.append("%s%s:" % (pad, key))
            if not obj:
                L[-1] = L[-1] + " []"
                return
            item_ind = ind + 2 if key is not None else ind
            for it in obj:
                if isinstance(it, dict):
                    first = True
                    for k, v in it.items():
                        if first:
                            if isinstance(v, (dict, list)):
                                L.append("%s- %s:" % (" " * item_ind, k))
                                emit(v, item_ind + 4)
                            else:
                                L.append("%s- %s: %s" % (" " * item_ind, k, scalar(v)))
                            first = False
                        else:
                            emit(v, item_ind + 2, k)
                else:
                    L.append("%s- %s" % (" " * item_ind, scalar(it)))
        else:
            L.append("%s%s: %s" % (pad, key, scalar(obj)))

    def scalar(v):
        if isinstance(v, bool):
            return "true" if v else "false"
        if v is None:
            return "null"
        return str(v)

    for k, v in doc.items():
        emit(v, 0, k)
    return "\n".join(L) + "\n"


def run_chain(tag, doc_id, doc_file, clan_code, pkg_name, opbase):
    """跑一遍全链：写档 → register → select → approve → proof → compile → probe。

    返回 (rows, n_clan_in_pkg, ok)。
    `clan_code` 是**条件侧**写的 clan；查询侧固定用 TARGET/OTHER/空 三行
    （这样两轮之间只有条件侧变，读数翻转就只可能是条件侧引起的）。
    """
    p = os.path.join(WS, "authoring", doc_file)
    with open(p, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(to_yaml(build_doc(clan_code, doc_id)))
    log("  档: %s  (条件 clan=%s)" % (doc_file, clan_code))

    man = os.path.join(WS, "_clan_reg_%s.json" % pkg_name)
    json.dump([{"operation": opbase + "-reg-1", "path": p}],
              open(man, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    r = subprocess.run(["dotnet", STUDIO, "authoring-register-batch", "--manifest", man, "--workspace", WS_REL],
                       capture_output=True, cwd=ROOT)
    log("  register rc=%d" % r.returncode)
    if r.returncode != 0:
        log("  " + ((r.stdout or b"").decode("utf-8", "replace") or (r.stderr or b"").decode("utf-8", "replace"))[:800])
        return [], 0, False

    res, rc, se = studio(["authoring-select", "--operation", "customer.select.v1." + opbase, "--document-id", doc_id])
    if not res.get("ok"):
        log("  select FAIL: " + json.dumps(res, ensure_ascii=False)[:400])
        return [], 0, False
    sel = res["selection"]["selectionId"]

    res, rc, se = studio(["authoring-approve", "--operation", "customer.approve.v1." + opbase, "--selection", sel])
    if not res.get("ok"):
        log("  approve FAIL: " + json.dumps(res, ensure_ascii=False)[:400])
        return [], 0, False
    app = res.get("approval_proof") or {}
    aid = app.get("approvalId") or app.get("approval_id") or app.get("id")

    res, rc, se = studio(["authoring-proof", "--operation", "customer.proof.v1." + opbase, "--approval", aid])
    if not res.get("ok"):
        log("  proof FAIL: " + json.dumps(res, ensure_ascii=False)[:500])
        return [], 0, False
    pf = res.get("compile_proof") or {}
    pid = pf.get("compileProofId") or pf.get("proofId") or pf.get("proof_id") or pf.get("id")
    log("  proof -> %s" % pid)

    outpkg = WS_REL + "/compiled/" + pkg_name
    res, rc, se = studio(["compile", "--proof", pid, "--out", outpkg])
    if not res.get("result_hash"):
        log("  compile FAIL: " + json.dumps(res, ensure_ascii=False)[:600])
        if se:
            log("  stderr(真因): " + se[:900])
        return [], 0, False
    val = res.get("validation") or []
    errs = [x for x in val if str(x.get("severity") or "").lower() == "error"]
    log("  compile OK validation total=%d error=%d" % (len(val), len(errs)))

    pkgdir = os.path.join(ROOT, WS_REL, "compiled", pkg_name)
    man_out = os.path.join(pkgdir, "manifest.json")
    if not os.path.isfile(man_out):
        log("  找不到 manifest: " + man_out)
        return [], 0, False

    rj = json.load(open(os.path.join(pkgdir, "runtime.json"), encoding="utf-8"))
    raw = json.dumps(rj, ensure_ascii=False)
    n_clan = raw.count("clan_ids")
    log("  运行包里 clan_ids 出现 %d 次" % n_clan)
    i = raw.find("clan_ids")
    if i >= 0:
        log("  片段: " + raw[max(0, i - 90):i + 140])

    spec = {"queries": [
        {"name": "P1-同clan", "identity": "profile.noble", "role": "Lord", "clan": TARGET_CLAN,
         "text": "家族探针", "requested_detail": "secret"},
        {"name": "N1-别的clan", "identity": "profile.noble", "role": "Lord", "clan": OTHER_CLAN,
         "text": "家族探针", "requested_detail": "secret"},
        {"name": "N2-无clan", "identity": "profile.noble", "role": "Lord", "clan": "",
         "text": "家族探针", "requested_detail": "secret"},
    ]}
    specp = os.path.join(WS, "_clan_spec_%s.json" % pkg_name)
    outp = os.path.join(WS, "_clan_probe_out_%s.json" % pkg_name)
    json.dump(spec, open(specp, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    pr = subprocess.run(["dotnet", SIM, "probe", man_out, specp, outp], capture_output=True, cwd=ROOT, timeout=1800)
    pout = (pr.stdout or b"").decode("utf-8", "replace")
    for ln in pout.splitlines():
        if ln.strip():
            log("  " + ln.strip()[:280])

    rows = []
    if os.path.isfile(outp):
        d = json.load(open(outp, encoding="utf-8"))
        rows = d if isinstance(d, list) else (d.get("queries") or d.get("results") or [])
    return rows, n_clan, True


def main():
    global logf
    result = {"doc": DOC_ID, "target_clan": TARGET_CLAN, "other_clan": OTHER_CLAN}
    logf = open(LOG, "w", encoding="utf-8", newline="\n")

    step("1", "写探针档（进 full-geo1 工作区，用它现成的来源索引）")
    p = os.path.join(WS, "authoring", DOC_FILE)
    with open(p, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(to_yaml(build_doc(TARGET_CLAN, DOC_ID)))
    log("  " + p)
    log("  含 clan_ids 行: " + str(any("clan_ids" in l for l in open(p, encoding="utf-8"))))

    step("2", "validate（判据：**只看本探针档的诊断**）")
    r = subprocess.run(["dotnet", STUDIO, "validate", "--workspace", WS_REL, "--file", p],
                       capture_output=True, cwd=ROOT)
    so = (r.stdout or b"").decode("utf-8", "replace")
    try:
        v = json.loads(so)
    except Exception:
        v = {"_raw": so[:1500]}
    diag = v.get("Diagnostics") or []
    mine = [d for d in diag if "clan-probe" in str(d.get("Path", ""))]
    others = len(diag) - len(mine)
    log("  诊断总数 %d，其中与本档相关 %d，其余（工作区旧档）%d" % (len(diag), len(mine), others))
    for d in mine[:15]:
        log("   %s | %s | %s" % (d.get("Code"), d.get("Path"), str(d.get("Detail"))[:200]))
    result["validate_own_diag"] = [{"code": d.get("Code"), "path": d.get("Path"),
                                    "detail": str(d.get("Detail"))[:300]} for d in mine[:20]]
    result["validate_other_diag"] = others
    if mine:
        log("  ⇒ 本档有诊断，停")
        result["pass"] = False
        return finish(result)
    log("  ⇒ 本档零诊断（clan_ids 过 schema）")

    step("3-5", "第一轮：条件写 TARGET_CLAN（全链 register→…→compile→probe）")
    rows, n_clan, ok = run_chain("主", DOC_ID, DOC_FILE, TARGET_CLAN, "probe-clan", OPBASE)
    if not ok:
        result["pass"] = False
        return finish(result)
    result["clan_ids_in_package"] = n_clan
    result["rows"] = rows

    step("6", "变异轮：**同一份档**，只把条件侧的 clan 换成 OTHER_CLAN")
    pm = os.path.join(WS, "authoring", DOC_FILE_MUT)
    with open(pm, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(to_yaml(build_doc(OTHER_CLAN, DOC_ID_MUT)))
    r = subprocess.run(["dotnet", STUDIO, "validate", "--workspace", WS_REL, "--file", pm],
                       capture_output=True, cwd=ROOT)
    try:
        vm = json.loads((r.stdout or b"").decode("utf-8", "replace"))
    except Exception:
        vm = {}
    minem = [d for d in (vm.get("Diagnostics") or []) if "clan-probe-mut" in str(d.get("Path", ""))]
    log("  变异档诊断 %d 条" % len(minem))
    if minem:
        for d in minem[:10]:
            log("   %s | %s | %s" % (d.get("Code"), d.get("Path"), str(d.get("Detail"))[:200]))
        result["pass"] = False
        return finish(result)

    rows_m, n_clan_m, ok = run_chain("变异", DOC_ID_MUT, DOC_FILE_MUT, OTHER_CLAN, "probe-clan-mut", OPBASE + "m")
    if not ok:
        result["pass"] = False
        return finish(result)
    result["mut_clan_ids_in_package"] = n_clan_m
    result["rows_mut"] = rows_m

    step("7", "判定")
    by = {r.get("name"): r for r in rows}
    bym = {r.get("name"): r for r in rows_m}

    def hits(n, table=None):
        r = (table or by).get(n)
        return None if not r else len(r.get("hits") or [])

    h1, hn1, hn2 = hits("P1-同clan"), hits("N1-别的clan"), hits("N2-无clan")
    m_t, m_o, m_n = hits("P1-同clan", bym), hits("N1-别的clan", bym), hits("N2-无clan", bym)
    v = []
    v.append(("①同clan必须拿到(阳性对照)", h1 is not None and h1 > 0, h1))
    v.append(("②别的clan必须拿不到", hn1 == 0, hn1))
    v.append(("③无clan必须拿不到", hn2 == 0, hn2))
    v.append(("④空转护栏(三行都在)", all(x is not None for x in (h1, hn1, hn2)), [h1, hn1, hn2]))
    v.append(("⑤编译真落下了clan_ids", n_clan > 0, n_clan))
    # 变异：条件换成 OTHER 后，读数必须**跟着 clan 翻转**
    v.append(("⑥变异:条件换OTHER后TARGET查=0", m_t == 0, m_t))
    v.append(("⑦变异:条件换OTHER后OTHER查>0", m_o is not None and m_o > 0, m_o))
    v.append(("⑧变异:无clan查仍为0", m_n == 0, m_n))
    v.append(("⑨变异轮也真落了clan_ids", n_clan_m > 0, n_clan_m))
    log("")
    log("  主轮  [TARGET查询, OTHER查询, 无clan查询] = %s" % [h1, hn1, hn2])
    log("  变异轮[条件=OTHER→ TARGET查询, OTHER查询, 无clan查询] = %s" % [m_t, m_o, m_n])
    log("")
    for name, ok, val2 in v:
        log("  %-34s %s  (实测 %s)" % (name, "PASS" if ok else "FAIL", val2))
    allpass = all(x[1] for x in v)
    # 额外：主轮与变异轮必须**真的不一致**，否则说明条件侧没生效
    flipped = (h1 != m_t) and (hn1 != m_o)
    log("  翻转检验(主轮≠变异轮): %s" % ("真翻转" if flipped else "★没翻转⇒条件侧没生效"))
    allpass = allpass and flipped
    log("\n  CLAN_GATE %s" % ("PASS" if allpass else "FAIL"))
    result["verdict"] = [{"name": n, "pass": o, "value": val2} for n, o, val2 in v]
    result["flipped"] = flipped
    result["pass"] = allpass
    return finish(result)


def finish(result):
    with open(OUT, "w", encoding="utf-8", newline="\n") as fh:
        json.dump(result, fh, ensure_ascii=False, indent=1)
    log("\nwritten " + OUT)
    if logf:
        logf.close()
    return 0 if result.get("pass") else 1


if __name__ == "__main__":
    sys.exit(main())
