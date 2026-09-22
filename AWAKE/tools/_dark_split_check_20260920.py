# -*- coding: utf-8 -*-
"""暗面批 ·「说法分层」阳性对照。

要证的一件事：**同一档、同一句问话，不同身份拿到的正文不一样。**
（若三种身份拿回同一段文字，则「信不信」这一轴在本批上等于没做。）

做法：拿 serfs / bandits 两档，各造 3 条问话（身份 = 村民 / 城镇居民 / 贵族），跑真 probe，
逐条比对返回正文的首句，并断言**三者互不相等**。
"""
import io
import json
import os
import subprocess

REPO = r"D:\AWAKE-Dev\AWAKE"
WS = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1")
PKG = os.path.join(WS, r"compiled\geo1-v21-dark")
SIM = os.path.join(REPO, r"tools\worldbook-runtime-sim")

CASES = [("农奴", "serfs"), ("强盗", "bandits")]
IDS = [("villager", "profile.villager", "村民"),
       ("townsfolk", "profile.townsfolk", "城镇居民"),
       ("noble", "profile.noble", "贵族")]

queries = []
for q, slug in CASES:
    for key, prof, label in IDS:
        queries.append({"name": "%s|%s" % (q, key), "identity": prof, "text": q})

SPEC = os.path.join(WS, "_dark_split_spec_20260920.json")
OUT = os.path.join(WS, "_dark_split_result_20260920.json")
json.dump({"queries": queries}, io.open(SPEC, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("造问 %d 条 -> 跑 probe" % len(queries), flush=True)

r = subprocess.run(["dotnet", "run", "-c", "Release", "--", "probe",
                    os.path.join(PKG, "manifest.json"), SPEC, OUT], cwd=SIM, capture_output=True)
if r.returncode != 0:
    print(r.stdout.decode("utf-8", "replace")[-2000:])
    print(r.stderr.decode("utf-8", "replace")[-2000:])
    raise SystemExit(1)

res = {row["name"]: row for row in json.load(io.open(OUT, encoding="utf-8"))}
bad = 0
for q, slug in CASES:
    print()
    print("=== 问「%s」 ===" % q)
    seen = {}
    for key, prof, label in IDS:
        row = res["%s|%s" % (q, key)]
        txt = (row.get("text") or "").strip()
        print("  [%s] state=%s" % (label, row.get("state")))
        print("      %s" % (txt[:150] or "(空)"))
        seen[label] = txt
    uniq = len({v for v in seen.values() if v})
    print("  → 不同说法数：%d （期望 3）" % uniq)
    if uniq < 3:
        bad += 1
        print("  ⚠️ 未达 3 —— 说法分层没有真正生效")

print()
print("阳性对照结论：%s（%d/%d 档通过）" % ("通过" if bad == 0 else "未通过", len(CASES) - bad, len(CASES)))
