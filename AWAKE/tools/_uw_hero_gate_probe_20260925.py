# -*- coding: utf-8 -*-
"""hero 绑定全链探针（2026-09-25）。

问的是同一件事：**一条说法绑了 hero 之后，真能不能按"他是不是这个人"分流？**

与 `_uw_clan_gate_probe_20260924.py` 同构（那个测 clan，这个测 hero）。
治法（零替身）：照真实五步（register → select → approve → proof → compile）把带 hero_ids
条件的档编译成运行包，再用 tools/worldbook-runtime-sim 跑 query 矩阵。

判据（缺一即判红）：
  ① 是本人      -> 必须拿到（阳性对照）
  ② 不是本人    -> 必须拿不到（同家族、不同人 —— 这是 hero 与 clan 的关键分界）
  ③ 无本人      -> 必须拿不到
  ④ 空转护栏    -> 三行都出现过
  ⑤ 编译落点    -> 运行包里真出现 hero_ids

**变异检验（⑥⑦⑧）**：把条件侧的 hero 换成别人，读数必须跟着翻。
    ⑥ 条件换成 OTHER 后，用 TARGET 查 -> 必须翻成 0
    ⑦ 条件换成 OTHER 后，用 OTHER  查 -> 必须翻成 >0
    ⑧ 变异轮里无本人查 -> 仍为 0
  不翻转 ⇒ 这道闸没在测。

**★ ②的取值最关键**：拿"同家族里的另一个人"当阴性，才能证明这道闸分得出
「一族人」与「这一个人」—— 如果 ② 用的是"完全不相干的人"，那连 clan 都能挡住它，
测出来的是 clan 不是 hero。

输出：tools/_uw_hero_gate_probe_20260925.json
"""
import json, os, subprocess, sys, time

ROOT = r"D:\AWAKE-Dev\AWAKE"
SIM = os.path.join(ROOT, "tools", "worldbook-runtime-sim", "bin", "Release", "net10.0-windows", "WorldbookRuntimeSim.dll")
STUDIO = os.path.join(ROOT, "tools", "worldbook-studio", "src", "Awake.WorldbookStudio.Cli", "bin", "Release", "net10.0", "worldbook-studio.dll")

OPBASE = "uwhero" + time.strftime("%m%d%H%M%S")
WS = os.path.join(ROOT, "tools", "worldbook-studio", "workspace", "full-geo1")
WS_REL = "tools/worldbook-studio/workspace/full-geo1"
OUT = os.path.join(ROOT, "tools", "_uw_hero_gate_probe_20260925.json")
LOG = os.path.join(WS, "_hero_probe_log.txt")

# 目标：南帝国珀特洛斯家的拉盖娅（官方中文「拉盖娅」，`lord_1_14`）。
TARGET_HERO = "lord_1_14"
# ★★ 阴性必须选「**同一个家族里**的另一个人」——
#   拉盖娅的妹妹伊拉（`lord_1_37`，官方中文「伊拉」），同属珀特洛斯家（`clan_empire_south_1`）。
#   这一条是整份探针的要害：若阴性取"不相干的人"，连 clan 都能挡住它，
#   测出来的就是 clan 而不是 hero —— 白测。
SAME_CLAN_OTHER_HERO = "lord_1_37"
TARGET_CLAN = "clan_empire_south_1"

DOC_ID = "doc.politics.hero-probe"
DOC_FILE = "hero-probe.yaml"
DOC_ID_MUT = "doc.politics.hero-probe-mut"
DOC_FILE_MUT = "hero-probe-mut.yaml"

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


SRC = {
    "source_id": "source.calradia.game.urban-dark",
    "source_version": "bannerlord-1.3.15.110062",
    "source_content_hash": "b4d5c8f431ccd2464129a318bab6b9243926a93f7df82b01799fd50fa3242f47",
    "locator": "bannerlord.db#localization.pKMVhx2v",
    "quote_hash": "723CCEE8A0C7230111A56D1C5C77142761482399A3982BCE547B0883539DE753",
    "quote": "那些后巷子里的渣滓所说的龌龊事情总是花样繁多。",
}


def build_doc(hero_code=TARGET_HERO, doc_id=DOC_ID):
    return {
        "schema_version": "awake.worldbook.authoring.v1",
        "id": doc_id,
        "revision": 1,
        "title": {"zh-CN": "本人探针条目", "en": "Hero Probe"},
        "status": "accepted_variant",
        "domain": "politics",
        "subdomain": "law",
        "universe": "awake_current",
        "era": {"key": "current", "certainty": "bounded"},
        "content_tier": "base",
        "aliases": {"zh-CN": [], "en": []},
        "summary": {"zh-CN": "本人绑定探针：这条知识只送给这一个人。"},
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
                "id": "assertion.hero-probe-1",
                "revision": 1,
                "kind": "fact",
                "text": {"zh-CN": "只有本人才知道的那句话。"},
                "sources": [dict(SRC)],
                "expressions": [
                    {
                        "id": "expr.hero-probe-detail",
                        "revision": 1,
                        "layer": "detail",
                        "text": {"zh-CN": "只有本人才知道的那句话。"},
                        "sources": [dict(SRC)],
                        "grants": [
                            {
                                "profile_id": "profile.noble",
                                "scope": "elite",
                                "min_detail": "detail",
                                # ★ 值必须是 entity.hero.<hero_code> 三段
                                "hero_ids": ["entity.hero." + hero_code],
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

    def scalar(v):
        if isinstance(v, bool):
            return "true" if v else "false"
        if v is None:
            return "null"
        return str(v)

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

    for k, v in doc.items():
        emit(v, 0, k)
    return "\n".join(L) + "\n"


def run_chain(tag, doc_id, doc_file, hero_code, pkg_name, opbase):
    """跑一遍全链：写档 → register → select → approve → proof → compile → probe。

    返回 (rows, n_hero_in_pkg, ok)。
    查询侧固定三行：TARGET / 同clan的另一个人 / 无本人。
    """
    p = os.path.join(WS, "authoring", doc_file)
    with open(p, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(to_yaml(build_doc(hero_code, doc_id)))
    log("  档: %s  (条件 hero=%s)" % (doc_file, hero_code))

    man = os.path.join(WS, "_hero_reg_%s.json" % pkg_name)
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
    n_hero = raw.count("hero_ids")
    log("  运行包里 hero_ids 出现 %d 次" % n_hero)
    i = raw.find("hero_ids")
    if i >= 0:
        log("  片段: " + raw[max(0, i - 90):i + 140])

    spec = {"queries": [
        {"name": "P1-是本人", "identity": "profile.noble", "role": "Lord", "hero": TARGET_HERO,
         "clan": TARGET_CLAN, "text": "本人探针", "requested_detail": "secret"},
        {"name": "N1-同clan的另一个人", "identity": "profile.noble", "role": "Lord", "hero": SAME_CLAN_OTHER_HERO,
         "clan": TARGET_CLAN, "text": "本人探针", "requested_detail": "secret"},
        {"name": "N2-无本人", "identity": "profile.noble", "role": "Lord", "hero": "",
         "clan": TARGET_CLAN, "text": "本人探针", "requested_detail": "secret"},
    ]}
    specp = os.path.join(WS, "_hero_spec_%s.json" % pkg_name)
    outp = os.path.join(WS, "_hero_probe_out_%s.json" % pkg_name)
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
    return rows, n_hero, True


def main():
    global logf
    result = {"doc": DOC_ID, "target_hero": TARGET_HERO,
              "same_clan_other_hero": SAME_CLAN_OTHER_HERO, "target_clan": TARGET_CLAN}
    logf = open(LOG, "w", encoding="utf-8", newline="\n")

    step("1", "写探针档（进 full-geo1 工作区，用它现成的来源索引）")
    p = os.path.join(WS, "authoring", DOC_FILE)
    with open(p, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(to_yaml(build_doc(TARGET_HERO, DOC_ID)))
    log("  " + p)
    log("  含 hero_ids 行: " + str(any("hero_ids" in l for l in open(p, encoding="utf-8"))))

    step("2", "validate（判据：**只看本探针档的诊断**）")
    r = subprocess.run(["dotnet", STUDIO, "validate", "--workspace", WS_REL, "--file", p],
                       capture_output=True, cwd=ROOT)
    so = (r.stdout or b"").decode("utf-8", "replace")
    try:
        v = json.loads(so)
    except Exception:
        v = {"_raw": so[:1500]}
    diag = v.get("Diagnostics") or []
    mine = [d for d in diag if "hero-probe" in str(d.get("Path", ""))]
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
    log("  ⇒ 本档零诊断（hero_ids 过 schema）")

    step("3-5", "第一轮：条件写 TARGET_HERO（全链 register→…→compile→probe）")
    rows, n_hero, ok = run_chain("主", DOC_ID, DOC_FILE, TARGET_HERO, "probe-hero", OPBASE)
    if not ok:
        result["pass"] = False
        return finish(result)
    result["hero_ids_in_package"] = n_hero
    result["rows"] = rows

    step("6", "变异轮：**同一份档**，只把条件侧的 hero 换成别一个人")
    pm = os.path.join(WS, "authoring", DOC_FILE_MUT)
    with open(pm, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(to_yaml(build_doc(SAME_CLAN_OTHER_HERO, DOC_ID_MUT)))
    r = subprocess.run(["dotnet", STUDIO, "validate", "--workspace", WS_REL, "--file", pm],
                       capture_output=True, cwd=ROOT)
    try:
        vm = json.loads((r.stdout or b"").decode("utf-8", "replace"))
    except Exception:
        vm = {}
    minem = [d for d in (vm.get("Diagnostics") or []) if "hero-probe-mut" in str(d.get("Path", ""))]
    log("  变异档诊断 %d 条" % len(minem))
    if minem:
        for d in minem[:10]:
            log("   %s | %s | %s" % (d.get("Code"), d.get("Path"), str(d.get("Detail"))[:200]))
        result["pass"] = False
        return finish(result)

    rows_m, n_hero_m, ok = run_chain("变异", DOC_ID_MUT, DOC_FILE_MUT, SAME_CLAN_OTHER_HERO, "probe-hero-mut", OPBASE + "m")
    if not ok:
        result["pass"] = False
        return finish(result)
    result["mut_hero_ids_in_package"] = n_hero_m
    result["rows_mut"] = rows_m

    step("7", "判定")
    by = {r.get("name"): r for r in rows}
    bym = {r.get("name"): r for r in rows_m}

    def hits(n, table=None):
        r = (table or by).get(n)
        return None if not r else len(r.get("hits") or [])

    h1, hn1, hn2 = hits("P1-是本人"), hits("N1-同clan的另一个人"), hits("N2-无本人")
    m_t, m_o, m_n = hits("P1-是本人", bym), hits("N1-同clan的另一个人", bym), hits("N2-无本人", bym)
    v = []
    v.append(("①是本人必须拿到(阳性对照)", h1 is not None and h1 > 0, h1))
    v.append(("②同clan的另一个人必须拿不到", hn1 == 0, hn1))
    v.append(("③无本人必须拿不到", hn2 == 0, hn2))
    v.append(("④空转护栏(三行都在)", all(x is not None for x in (h1, hn1, hn2)), [h1, hn1, hn2]))
    v.append(("⑤编译真落下了hero_ids", n_hero > 0, n_hero))
    v.append(("⑥变异:条件换别人后TARGET查=0", m_t == 0, m_t))
    v.append(("⑦变异:条件换别人后别人查>0", m_o is not None and m_o > 0, m_o))
    v.append(("⑧变异:无本人查仍为0", m_n == 0, m_n))
    v.append(("⑨变异轮也真落了hero_ids", n_hero_m > 0, n_hero_m))
    log("")
    log("  主轮  [TARGET查询, 同clan别人查询, 无本人查询] = %s" % [h1, hn1, hn2])
    log("  变异轮[条件=别人→ TARGET查询, 别人查询, 无本人查询] = %s" % [m_t, m_o, m_n])
    log("")
    for name, ok, val2 in v:
        log("  %-36s %s  (实测 %s)" % (name, "PASS" if ok else "FAIL", val2))
    allpass = all(x[1] for x in v)
    flipped = (h1 != m_t) and (hn1 != m_o)
    log("  翻转检验(主轮≠变异轮): %s" % ("真翻转" if flipped else "★没翻转⇒条件侧没生效"))
    allpass = allpass and flipped
    log("\n  HERO_GATE %s" % ("PASS" if allpass else "FAIL"))
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
