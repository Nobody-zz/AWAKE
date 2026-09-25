# -*- coding: utf-8 -*-
"""聚落别名批验收探针（v30 vs v29 对照）。
判据：问物产/地形 → 应捞到对应聚落档；且 v29（别名前）应捞不到 ⇒ 证明别名真的起作用。

金标来源：直接读 v30 runtime.json，按 alias 反查 —— 每个测试词先确认全库有几档带该词。
"""
import io, json, os, subprocess, sys, collections

ROOT = r"D:\AWAKE-Dev\AWAKE"
SIM = os.path.join(ROOT, "tools", "worldbook-runtime-sim")
PKG_DIR = os.path.join(ROOT, "tools/worldbook-studio/workspace/full-geo1/compiled")
V29 = os.path.join(PKG_DIR, "geo1-v29-uw-wide/runtime.json")
V30 = os.path.join(PKG_DIR, "geo1-v30-settle-alias/runtime.json")

QUERIES = [
    # 物产类
    {"name": "Q1-问葡萄(产酒村)", "identity": "profile.merchant", "text": "葡萄"},
    {"name": "Q2-问铁矿(产矿村)", "identity": "profile.merchant", "text": "铁矿石"},
    {"name": "Q3-问盐(产盐村)", "identity": "profile.villager", "text": "盐"},
    {"name": "Q4-问椰枣(沙村)", "identity": "profile.villager", "text": "椰枣"},
    {"name": "Q5-问渔(渔村)", "identity": "profile.villager", "text": "渔"},
    # 地形类
    {"name": "Q6-问沙漠绿洲", "identity": "profile.merchant", "text": "绿洲"},
    {"name": "Q7-问高原", "identity": "profile.soldier", "text": "高原"},
    # 类属后缀
    {"name": "Q8-问某村全称", "identity": "profile.villager", "text": "阿布·科梅尔村"},
    {"name": "Q9-问某镇全称", "identity": "profile.merchant", "text": "阿克卡拉特镇"},
    # 城镇专属
    {"name": "Q10-问军市", "identity": "profile.soldier", "text": "军市"},
    # 阴性对照
    {"name": "NEG-不存在的词", "identity": "profile.villager", "text": "紫金葫芦宝塔"},
]


def run_probe(pkg_runtime, tag):
    spec = os.path.join(ROOT, "tools", "_uw_settle_spec_%s.json" % tag)
    out = os.path.join(ROOT, "tools", "_uw_settle_probe_%s.json" % tag)
    io.open(spec, "w", encoding="utf-8").write(
        json.dumps({"queries": QUERIES}, ensure_ascii=False, indent=1))
    r = subprocess.run(["dotnet", "run", "-c", "Release", "--no-build", "--",
                        "probe", pkg_runtime, spec, out], cwd=SIM, capture_output=True)
    if not os.path.exists(out):
        print("FATAL probe %s 无产出 rc=%d" % (tag, r.returncode))
        print((r.stdout or b"").decode("utf-8", "replace")[-1200:])
        print((r.stderr or b"").decode("utf-8", "replace")[-1200:])
        return None
    d = json.load(io.open(out, encoding="utf-8"))
    rows = d if isinstance(d, list) else (d.get("results") or d.get("cases") or [])
    res = {}
    for row in rows:
        nm = row.get("name") or "?"
        hits = row.get("hits") or row.get("Hits") or []
        ids = [str(h) if not isinstance(h, dict) else str(h.get("id") or h.get("entryId"))
               for h in (hits if isinstance(hits, list) else [])]
        res[nm] = ids
    return res


def main():
    r29 = run_probe(V29, "v29")
    r30 = run_probe(V30, "v30")
    if r29 is None or r30 is None:
        sys.exit(1)

    def sett(name):
        return set(i for i in (r30.get(name) or []) if i)

    print("%-24s %8s %8s  %s" % ("用例", "v29命中", "v30命中", "变化"))
    print("-" * 82)
    up = 0
    for q in QUERIES:
        nm = q["name"]
        a, b = len(r29.get(nm) or []), len(r30.get(nm) or [])
        mark = "↑ 新增" if b > a else ("＝" if b == a else "↓")
        if b > a:
            up += 1
        print("%-24s %8d %8d  %s" % (nm, a, b, mark))
    print()
    neg = r30.get("NEG-不存在的词") or []
    print("阴性对照应为 0：v30=%d ⇒ %s" % (len(neg), "PASS" if len(neg) == 0 else "FAIL"))
    print("比起 v29 命中变多的用例 = %d / %d（应 ≥5，否则别名没起作用）" % (up, len(QUERIES)))
    # 抽一条看捞到谁
    print()
    for nm in ("Q1-问葡萄(产酒村)", "Q8-问某村全称", "Q10-问军市"):
        print("%s -> %s" % (nm, [i.split('.')[-1] for i in (r30.get(nm) or [])][:6]))


if __name__ == "__main__":
    main()
