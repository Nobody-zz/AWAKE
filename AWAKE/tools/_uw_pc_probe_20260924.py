# -*- coding: utf-8 -*-
"""
§六-4 阳性对照（2026-09-24，只读）

上一发的探针发现：《小阵营》档的 rumor 层**根本没有 culture_ids**（我纸上写的方案从没落档）。
但"culture 条件到底筛不筛"这件事必须用**真有 culture 条件的档**来验 —— 否则我证明的只是
"没条件 ⇒ 不筛"，是废话。

阳性对照档：`military-empire-north`（北帝国的军事实力）——
  它的 grants 明写 `culture_ids: [entity.culture.empire]` ＋ `kingdom_ids: [entity.kingdom.empire]`。
用它验：
  阳性：帝国 + 城镇居民（townsfolk, regional/summary）→ 应拿到
  阴性：瓦兰迪亚 + 城镇居民 → 应拿不到（culture 在筛）
  阴性：帝国 + 平民（commoner, local/rumor）→ 应拿不到（**能力不够**，验能力闸）
  阳性对照：帝国 + 士兵（national/summary）→ 应拿到

⇒ 三个方向都测，才能说明「culture ＋ 能力」这两道闸各自都在管事。
"""
import io
import json
import os
import subprocess

REPO = r"D:\AWAKE-Dev\AWAKE"
SIM_DLL = os.path.join(REPO, r"tools\worldbook-runtime-sim\bin\Release\net10.0-windows\WorldbookRuntimeSim.dll")
PKG = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1\compiled\geo1-v26-uw-rumor")
MANIFEST = os.path.join(PKG, "manifest.json")
SPEC = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1\_uw_pc_spec_20260924.json")
OUT = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1\_uw_pc_out_20260924.json")

TARGET = "awake:entry:war.military-empire-north"
TEXT = "北帝国"

queries = [
    {"name": "P1_empire_townsfolk", "identity": "profile.townsfolk", "role": "townsfolk",
     "culture": "empire", "kingdom": "empire", "text": TEXT},
    {"name": "N1_vlandia_townsfolk", "identity": "profile.townsfolk", "role": "townsfolk",
     "culture": "vlandia", "kingdom": "vlandia", "text": TEXT},
    {"name": "N2_empire_commoner", "identity": "profile.commoner", "role": "commoner",
     "culture": "empire", "kingdom": "empire", "text": TEXT},
    {"name": "P2_empire_soldier", "identity": "profile.soldier", "role": "soldier",
     "culture": "empire", "kingdom": "empire", "text": TEXT},
    {"name": "N3_noculture_townsfolk", "identity": "profile.townsfolk", "role": "townsfolk",
     "text": TEXT},
]

json.dump({"queries": queries}, io.open(SPEC, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
proc = subprocess.run(["dotnet", SIM_DLL, "probe", MANIFEST, SPEC, OUT], capture_output=True)
rows = json.load(io.open(OUT, encoding="utf-8")) if os.path.exists(OUT) else []

print("=" * 92)
print("阳性对照档：military-empire-north（grants 挂 culture_ids:[empire] + kingdom_ids:[empire]）")
print("目标条目：%s" % TARGET)
print("=" * 92)
for r in rows:
    hits = r.get("hits") or []
    ids = [(h.get("id") if isinstance(h, dict) else h) for h in hits]
    hit_target = TARGET in ids
    print("── %-24s culture=%-8s kingdom=%-8s identity=%-18s scope=%-9s detail=%-7s"
          % (r.get("name"), r.get("culture") or "(无)", r.get("kingdom") or "(无)",
             r.get("identity"), r.get("scope"), r.get("detail")))
    print("     目标条目命中 = %s   state=%s   命中数=%d" % (hit_target, r.get("state"), len(ids)))
    for i in ids[:6]:
        print("       %s%s" % (i, "   ★目标" if i == TARGET else ""))
    print()


def hit(n):
    for r in rows:
        if r.get("name") == n:
            ids = [(h.get("id") if isinstance(h, dict) else h) for h in (r.get("hits") or [])]
            return TARGET in ids
    return None


print("=" * 92)
print("判定")
print("=" * 92)
p1, n1, n2, p2 = hit("P1_empire_townsfolk"), hit("N1_vlandia_townsfolk"), \
                  hit("N2_empire_commoner"), hit("P2_empire_soldier")
print("P1 帝国+townsfolk  =", p1, "（期望 True）")
print("N1 瓦兰迪亚+townsfolk =", n1, "（期望 False ⇒ culture 在筛）")
print("N2 帝国+commoner   =", n2, "（期望 False ⇒ 能力不够，验能力闸）")
print("P2 帝国+soldier    =", p2, "（期望 True）")
print()
ok_culture = (p1 is True and n1 is False)
ok_cap = (p1 is True and n2 is False)
print("culture 闸在管事？", "✅ 是" if ok_culture else "❌ 否/无读数")
print("能力闸在管事？   ", "✅ 是" if ok_cap else "❌ 否/无读数")
