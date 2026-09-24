# -*- coding: utf-8 -*-
"""
§六 待查核验探针（2026-09-24，只读）

要回答的三件事：
  2) role_ids 的合法值域 —— 到底哪些 role 名能被运行时认出来（而不是落 anonymous）
  3) is_clan_leader / min_skill 能不能表达「这人属于某个小派系」
  4) 变体 2（火焰余烬自述）的近似挂法，跑真探针看它到底送不送得到

做法：把 spec 灌给 WorldbookRuntimeSim 的 probe 模式（内部直接调真件
WorldbookIdentityCapabilityRules.Resolve / WorldbookIdentityEvaluator），
不重写任何匹配逻辑 ⇒ 是读数，不是另写引擎。

用法：python _uw_ident_probe_20260924.py
"""
import io
import json
import os
import subprocess
import sys

REPO = r"D:\AWAKE-Dev\AWAKE"
SIM_DLL = os.path.join(REPO, r"tools\worldbook-runtime-sim\bin\Release\net10.0-windows\WorldbookRuntimeSim.dll")
PKG = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1\compiled\geo1-v26-uw-rumor")
MANIFEST = os.path.join(PKG, "manifest.json")
SPEC = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1\_uw_ident_spec_20260924.json")
OUT = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1\_uw_ident_out_20260924.json")

# ── 要测的 role 名：从代码里读出来的全部合法值 + 几个可疑值做阴性对照 ──────────
ROLES = [
    # IdentitiesForRole + WorldbookIdentityCapabilityRules 里显式写到的
    "villager", "farmer", "commoner",
    "headman", "village_headman", "town_headman",
    "merchant", "tavernkeeper", "tavern_keeper", "ransom_broker",
    "soldier", "trooper",
    "noble", "lord", "clan_leader",
    # ResolveHeroRole 会产出的
    "rural_notable", "wanderer", "gang_leader",
    # 规则表里写了但 adapter 未必产出的
    "notable", "arena_master", "gangster", "bandit", "preacher", "musician",
    "goods_trader", "weaponsmith", "guard", "caravan_guard", "mercenary",
    # ── 阴性对照：这几个**不该**被认出来 ──
    "embers_of_flame", "hidden_hand", "FLAME", "cultist", "bogus_role",
]

queries = []
for r in ROLES:
    queries.append({"name": "role:" + r, "identity": "", "role": r, "text": "巷子"})

# 「属于某小派系」能不能表达：测 is_clan_leader 这一位
for flag in (True, False):
    queries.append({
        "name": "clanleader:" + str(flag), "identity": "", "role": "noble",
        "is_clan_leader": flag, "text": "巷子"})

spec = {"queries": queries}
with io.open(SPEC, "w", encoding="utf-8") as h:
    json.dump(spec, h, ensure_ascii=False, indent=2)

print("spec 写入：" + SPEC)
print("sim dll：" + SIM_DLL)
print("package：" + PKG)
print("-" * 70)

proc = subprocess.run(
    ["dotnet", SIM_DLL, "probe", MANIFEST, SPEC, OUT],
    capture_output=True)
raw = proc.stdout
# ★ 不许给 text=，自己按 utf-8 解（GBK 环境下中文字节会炸）
out = raw.decode("utf-8", errors="replace")
err = proc.stderr.decode("utf-8", errors="replace")
print(out)
if err.strip():
    print("--- stderr ---")
    print(err[:3000])
print("exit =", proc.returncode)
