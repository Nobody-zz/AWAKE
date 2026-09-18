# -*- coding: utf-8 -*-
"""把「山里有不让进的地方吗？」按 09-17 已立裁定移出计数（靶子任意），并登记甲方原话。

依据（都已存在的既有裁定，不是我新加的）：
  · 题集 `rulings["2026-09-17"]`（甲方原话）：「废题别留着。不让进本来就不应该成提示词，
    好马被多个词条占据也是正常现象」。
  · 现有先例：「这一带有好马吗？」就是标 `countInGate: false`（多个村都产好马，把拉迈萨
    定成唯一目标是任意的）——仍跑、仍打印，只不进分子分母。
  · 本次实测新增的证据：`geography.towns-odokh` 正文第一条写着「奥多赫背后那座山进不得，
    说是黑魔法的地方。」⇒ 这题至少有两条条目都答得上（黎明山脉·禁忌与传说 / 奥多赫）
    ⇒ **靶子任意**，与「好马」同一形状。

不做的事：不删题、不改问法、不给词条补题面词（沿用 09-17 三条裁定）。
"""
import json, io, sys

P = r"D:\AWAKE-Dev\AWAKE\tools\_retrieval_cases_20260916.json"
QUERY = "山里有不让进的地方吗？"

with io.open(P, encoding="utf-8") as f:
    d = json.load(f)

hit = None
for c in d["cases"]:
    if c.get("query") == QUERY:
        hit = c
        break
if hit is None:
    print("FAIL: case not found")
    sys.exit(1)
if hit.get("countInGate") is False:
    print("ALREADY 已经标过 countInGate=false，本次不改")
    sys.exit(0)

hit["countInGate"] = False
hit["note"] = ("靶子任意：`geography.towns-odokh` 正文写「奥多赫背后那座山进不得，说是黑魔法的地方」"
               "⇒ 至少两条条目都答得上，把「黎明山脉·禁忌与传说」定成唯一目标是任意的。"
               "按 09-17 裁定处理：仍跑、仍打印，不进分子分母。")

d["rulings"]["2026-09-18"] = (
    "甲方原话：「因为什么山里有不让进的话问出去本身就很扯淡」。"
    "落点：① 该题（`山里有不让进的地方吗？`）标 `countInGate=false` 移出计数——"
    "它问法本身就不成立，且 `geography.towns-odokh` 已直接写着「背后那座山进不得」，"
    "属**靶子任意**，与「这一带有好马吗？」同一形状；② 沿用 09-17 三条裁定："
    "**不为题面词补 keywords、不改问法、不拿自造题集去改语料**。"
    "⚠️ 移出后计分题 24 → 23（A 13 / B 10）。"
)

d["changes"].append({
    "date": "2026-09-18",
    "what": "把 `山里有不让进的地方吗？`（target = culture.tales-dawn-taboo）标为 `countInGate=false`。",
    "why": ("① 甲方：这题问出去本身就扯淡；② 实测靶子不唯一：`geography.towns-odokh` 正文第一条就是"
            "「奥多赫背后那座山进不得」，`geography.villages-deir-hawa` 也写「外人不许看」"
            "⇒ 多条条目都答得上，答哪条都对。"),
    "effect": ("计分题 24 → 23（A 13 / B 10）。该题在**字面臂与合并臂两处都是 0**"
               "（证据：`tools/_smoke_run_20260917_hit3.txt` 的 `RETRIEVAL_MISS B ... tales-dawn-taboo`；"
               "`_midlayer_probe_*` 里它到 K=100 都不在候选序）"
               "⇒ **分子不动、只缩分母**，两处验台的门禁常量（GateAHit3=10 / GateBHit3=9 / GateAllHit3=19）"
               "理论上不必动，但**必须复跑两闸核实**，不许只靠推。"),
    "gate": ("复跑：`worldbook-runtime-smoke`（RETRIEVAL_GATE）与 `worldbook-rag-merge`（MERGE_GATE），"
             "读数见 `tools/_retrieval_gate_after_taboo_20260918.txt` / `_merge_gate_after_taboo_20260918.txt`。")
})

with io.open(P, "w", encoding="utf-8", newline="\n") as f:
    json.dump(d, f, ensure_ascii=False, indent=2)
    f.write("\n")

print("OK patched. counted cases now =",
      sum(1 for c in d["cases"] if c.get("countInGate") is not False),
      "/ total", len(d["cases"]))
