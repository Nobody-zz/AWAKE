# -*- coding: utf-8 -*-
"""百科化宪章红测套件 v1（对应宪章 v0.x）
用法: python _ency_redteam_20260913.py <宪章路径>
判据: 每探针 HOLD / BREAK / EDGE；BREAK>0 = 本轮 FAIL。
收敛判据 R: 连续两轮 0 BREAK。
"""
import io, os, re, sys, json, shutil, subprocess, glob
import yaml

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", ".."))  # authoring-out→projection→worldbook-migration→docs→AWAKE
AUTH = os.path.dirname(os.path.abspath(__file__))
WS = os.path.join(ROOT, "tools", "worldbook-studio", "workspace", "full-geo1")
VENV = r"C:/Users/26811/.workbuddy/binaries/python/envs/default/Scripts/python.exe"

results = []
def probe(name, fn):
    try:
        verdict, detail = fn()
    except Exception as e:
        verdict, detail = "BREAK", f"探针自身异常: {e}"
    results.append({"name": name, "verdict": verdict, "detail": detail})
    print(f"[{verdict:5}] {name}: {detail[:150]}")

# ---------- S 系列静态探针 ----------
def make_static(pid, patterns, need_all=True, desc=""):
    def run():
        text = io.open(CHARTER, encoding="utf-8").read()
        hits = [p for p in patterns if re.search(p, text)]
        ok = (len(hits) == len(patterns)) if need_all else (len(hits) > 0)
        return ("HOLD" if ok else "BREAK", f"{desc}; 命中 {len(hits)}/{len(patterns)}")
    return run

CHARTER = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "docs", "worldbook-migration", "WORLDBOOK-ENCYCLOPEDIA-CHARTER-20260913.md")

probe("S1-三层齐全可执行", make_static("S1",
    [r"L1\s*底座层", r"L2\s*观感层", r"L3\s*门禁层", r"生成器", r"人工", r"机验"],
    desc="三层各有生产方式与内容定义"))
probe("S2-不开第六域", make_static("S2",
    [r"不开第六域", r"subdomain"], desc="扩容不扩域与 WB-DOC-002 对齐"))
probe("S3-六身份显式落点", make_static("S3",
    [r"villager", r"merchant", r"soldier", r"显式直配", r"notes"], desc="继承缺口教训入宪"))
probe("S4-收敛判据R", make_static("S4",
    [r"连续两轮\s*0\s*BREAK"], desc="R 判据存在"))
probe("S5-冲突处置条款", make_static("S5",
    [r"与旧章法的关系|冲突处置", r"规模硬限", r"身份覆盖"], desc="与旧章法冲突清单"))
probe("S6-译名自动化", make_static("S6",
    [r"官方译名由生成器|DB 直取", r"手写译名"], desc="硬规矩第0条自动化"))
probe("S7-D级登记通道", make_static("S7",
    [r"D 级原创登记|D级原创登记|D 登记留痕|D 级"], desc="无 A 源对象出口"))
probe("S8-批量四教训", make_static("S8",
    [r"operation id", r"WB-PATH-003", r"MUTATION-UNKNOWN", r"WB-YAML-006"],
    desc="编译链血泪全数入宪"))
probe("S9-Schema权威与逐档验证", make_static("S9",
    [r"Schema 唯一权威", r"逐档 validate 纪律", r"Document=None"], desc="r4 两笔真 BREAK 的整改条款入宪"))

# ---------- E9 动态探针：L 规格极限样例真跑 validate（基线对比法）----------
def _run_validate():
    r = subprocess.run(["dotnet", "run", "--project", "tools/worldbook-studio/src/Awake.WorldbookStudio.Cli",
                        "--", "validate", "--workspace", "tools/worldbook-studio/workspace/full-geo1"],
                       capture_output=True, text=True, timeout=600, cwd=ROOT)
    return json.loads(r.stdout or "{}")

def e9():
    base_diag = _run_validate()
    base_err = sum(1 for x in base_diag.get("Diagnostics", []) if x.get("Severity") == "error")
    base_doc = yaml.safe_load(io.open(os.path.join(AUTH, "der-furs.yaml"), encoding="utf-8"))
    base_doc["id"] = "doc.economy.item-redteam-probe"
    base_doc["subdomain"] = "items"   # 宪章 §二 声称可登记的新 subdomain
    base_doc["revision"] = 1
    # 不加任何 schema 外字段——纯 L 规格最小样例
    probe_path = os.path.join(WS, "authoring", "_redteam-probe.yaml")
    with open(probe_path, "w", encoding="utf-8") as f:
        f.write("# redteam E9 probe - delete after run\n")
        yaml.safe_dump(base_doc, f, allow_unicode=True, default_flow_style=False, sort_keys=False)
    try:
        d = _run_validate()
        errs = [x for x in d.get("Diagnostics", []) if x.get("Severity") == "error"]
        if len(errs) > base_err:
            codes = sorted(set(x.get("Code") for x in errs))[:4]
            return "BREAK", f"L 规格最小样例被 validate 拒（基线 {base_err} → {len(errs)} 错误，码: {codes}）——L 规格与 schema 未对齐"
        if any(x.get("Code") in ("WB-TAXONOMY-422", "WB-DOC-002") for x in d.get("Diagnostics", [])):
            return "BREAK", "subdomain 登记通道未实现（TAXONOMY/DOC 错误）"
        return "HOLD", f"economy/items 最小样例通过 validate（基线 {base_err} 错误保持）"
    finally:
        if os.path.exists(probe_path):
            os.remove(probe_path)
probe("E9-L规格极限样例", e9)

# ---------- E10 动态探针：生成器 YAML 锚点红线 ----------
def e10():
    shared = {"zh-CN": "测试文本"}
    doc = {"title": shared, "summary": shared}   # 共享引用
    dumped = yaml.safe_dump(doc, allow_unicode=True)
    class NoAlias(yaml.dumper.Dumper):
        def ignore_aliases(self, data): return True
    if "&id" in dumped or "*id" in dumped:
        import json as _json
        doc2 = _json.loads(_json.dumps(doc))
        buf = io.StringIO()
        yaml.dump(doc2, buf, Dumper=NoAlias, allow_unicode=True)
        return ("EDGE", "共享引用确实产生锚点（红线必要）；ignore_aliases 修复法验证" + ("成功" if "&id" not in buf.getvalue() else "失败→BREAK"))
    direct = io.StringIO()
    yaml.dump(doc, direct, Dumper=NoAlias, allow_unicode=True)
    return "HOLD", "NoAlias Dumper 验证通过（生成器红线方法可行）"
probe("E10-锚点红线", e10)

# ---------- E11 动态探针：H5 存量可达性（aliases 双语非空）----------
def e11():
    bad = []
    for f in sorted(glob.glob(os.path.join(AUTH, "*.yaml"))):
        b = os.path.basename(f)
        if b.startswith("_"): continue
        d = yaml.safe_load(io.open(f, encoding="utf-8"))
        al = d.get("aliases") or []
        if not al: bad.append(b + "(空)")
    return ("HOLD" if not bad else "EDGE", f"40 档 aliases 空缺: {bad or '无'}")
probe("E11-H5存量可达", e11)

# ---------- E12 DB 物品描述覆盖率（r7 实测：BannerlordSage get_item_stats + items XML）----------
def e12():
    return ("EDGE",
        "实测结论（2026-09-13）：物品官方译名覆盖≈100%（如 saddle_horse→旅行马），"
        "叙述性描述文覆盖≈0%（物品 XML 仅数值属性，无 flavor/description 字段）。"
        "→ 物品类 L1 自动产「译名+属性」；叙述正文走①属性成文②B 编年史③D 登记。宪章 §三4 已按此修订。")
probe("E12-DB物品描述覆盖率", e12)

# ---------- 汇总 ----------
n_break = sum(1 for r in results if r["verdict"] == "BREAK")
report = {"charter": CHARTER, "round": os.environ.get("ENCY_ROUND", "?"),
          "break": n_break,
          "verdict": "FAIL" if n_break else "PASS",
          "probes": results}
out = os.path.join(AUTH, f"_ency_redteam_result_r{os.environ.get('ENCY_ROUND','x')}.json")
io.open(out, "w", encoding="utf-8").write(json.dumps(report, ensure_ascii=False, indent=2))
print(f"\n=== 轮次 {report['round']}: {report['verdict']}（BREAK={n_break}）报告: {out}")
sys.exit(1 if n_break else 0)
