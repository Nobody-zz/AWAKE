# -*- coding: utf-8 -*-
"""广铺批验收探针：验证 hero（含八君主）/ clan / 概念档在真链路上可检索且门控正确。

判据（每条都有"应当命中/应当不命中"两侧，否则恒真恒假都不算证据）：
  T1 君主档：村民/kd 本地问"德泰尔"→ 应命中；异国人问 → 看是否受 kingdom 门控
  T2 君主档：贵族 vs 村民问同一句 → 分层可见性
  T3 家族档：问家族名 → 命中
  T4 概念档：问"第纳尔" / "税" / "围城" → 命中对应概念档
  T5 阴性对照：问一个**不存在**的词 → 应 0 命中（证探针有分辨力）

用法：
  python tools/_uw_gate_probe_20260925.py
"""
import io
import json
import os
import subprocess
import sys

ROOT = r"D:\AWAKE-Dev\AWAKE"
SIM = os.path.join(ROOT, "tools", "worldbook-runtime-sim")
MANIFEST = os.path.join(ROOT, "tools/worldbook-studio/workspace/full-geo1",
                        "compiled/geo1-v29-uw-wide/runtime.json")
OUT = os.path.join(ROOT, "tools", "_uw_gate_probe_20260925.json")
SPEC = os.path.join(ROOT, "tools", "_uw_gate_spec_20260925.json")

QUERIES = [
    # ── T1 君主档：本地 vs 异地 ──
    {"name": "T1a-瓦兰迪亚村民问德泰尔", "identity": "profile.villager",
     "culture": "vlandia", "kingdom": "vlandia", "text": "德泰尔"},
    {"name": "T1b-斯特吉亚村民问德泰尔(异地)", "identity": "profile.villager",
     "culture": "sturgia", "kingdom": "sturgia", "text": "德泰尔"},
    {"name": "T1c-库赛特村民问蒙楚格", "identity": "profile.villager",
     "culture": "khuzait", "kingdom": "khuzait", "text": "蒙楚格"},
    {"name": "T1d-巴旦尼亚村民问卡拉多格", "identity": "profile.villager",
     "culture": "battania", "kingdom": "battania", "text": "卡拉多格"},

    # ── T2 同句不同身份：分层可见性 ──
    {"name": "T2a-村民问德泰尔", "identity": "profile.villager", "text": "德泰尔"},
    {"name": "T2b-贵族问德泰尔", "identity": "profile.noble", "text": "德泰尔"},
    {"name": "T2c-酒馆老板问德泰尔", "identity": "profile.tavernkeeper", "text": "德泰尔"},

    # ── T3 家族档 ──
    {"name": "T3a-问库洛夫家", "identity": "profile.villager", "text": "库洛夫家"},
    {"name": "T3b-问库吉特家", "identity": "profile.villager", "text": "库吉特家"},
    {"name": "T3c-问戴·梅罗克家", "identity": "profile.noble", "text": "戴·梅罗克家"},

    # ── T4 概念档 ──
    {"name": "T4a-问第纳尔", "identity": "profile.merchant", "text": "第纳尔"},
    {"name": "T4b-问税赋", "identity": "profile.villager", "text": "税"},
    {"name": "T4c-问围城补给", "identity": "profile.soldier", "text": "补给"},
    {"name": "T4d-问步兵骑兵", "identity": "profile.soldier", "text": "步兵"},

    # ── T5 阴性对照（不存在的词，应 0 命中）──
    {"name": "T5a-阴性对照-不存在的词", "identity": "profile.villager",
     "text": "紫金葫芦宝塔"},
]

io.open(SPEC, "w", encoding="utf-8").write(
    json.dumps({"queries": QUERIES}, ensure_ascii=False, indent=1))

r = subprocess.run(
    ["dotnet", "run", "-c", "Release", "--no-build", "--",
     "probe", MANIFEST, SPEC, OUT],
    cwd=SIM, capture_output=True)
out = (r.stdout or b"").decode("utf-8", "replace")
err = (r.stderr or b"").decode("utf-8", "replace")
if err.strip():
    print("[stderr]")
    for ln in err.strip().splitlines()[-25:]:
        print("  " + ln)
if not os.path.exists(OUT):
    print("FATAL 探针未产出 %s（rc=%d）" % (OUT, r.returncode))
    print(out[-1500:])
    sys.exit(1)

d = json.load(io.open(OUT, encoding="utf-8"))
rows = d if isinstance(d, list) else (d.get("results") or d.get("cases") or [])
print("\n%-38s %6s  %s" % ("用例", "命中", "命中的档"))
print("-" * 104)
neg_ok = None
for row in rows:
    nm = row.get("name") or row.get("query") or "?"
    hits = row.get("hits") or row.get("Hits") or []
    ids = [str(h) if not isinstance(h, dict) else str(h.get("id") or h.get("entryId"))
           for h in (hits if isinstance(hits, list) else [])]
    print("%-38s %6d  %s" % (nm, len(ids), "、".join(ids[:3])[:62]))
    if nm.startswith("T5a"):
        neg_ok = (len(ids) == 0)

print("\n阳性对照：本批新档是否真被捞到 ——")
shown = 0
for row in rows:
    nm = row.get("name") or ""
    hits = row.get("hits") or row.get("Hits") or []
    ids = [str(h) if not isinstance(h, dict) else str(h.get("id") or h.get("entryId"))
           for h in (hits if isinstance(hits, list) else [])]
    if ids and any(("hero-" in i or "clan-" in i or "economy-" in i
                    or "culture-" in i or "war-" in i or "geography-" in i
                    or "politics-" in i) for i in ids):
        shown += 1
print("  有本批新档命中的用例数 = %d" % shown)
print("  阴性对照（不存在的词）应为 0 命中：%s" % ("PASS" if neg_ok else "FAIL（探针无分辨力）"))
