# -*- coding: utf-8 -*-
"""
《小阵营》补料后 · 分层实测（2026-09-24，只读）

验四件事：
  ① 三层是否各自可达（每层都有人能听到）
  ② 层与层是否互斥（同一身份只听一层，不串）
  ③ 平民听得是 rumor（街面口吻），城镇居民听得是 summary（局内人自述），
     要人以上听得是 detail（朝廷视角）—— **文本内容要对得上层级**
  ④ 空转护栏：三层若返回同一段文本 ⇒ 分层没生效（恒真），必须报出来

用真件：WorldbookRuntimeSim probe（内部调真 WorldbookIdentityEvaluator）。
"""
import io
import json
import os
import subprocess

REPO = r"D:\AWAKE-Dev\AWAKE"
SIM_DLL = os.path.join(REPO, r"tools\worldbook-runtime-sim\bin\Release\net10.0-windows\WorldbookRuntimeSim.dll")
PKG = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1\compiled\geo1-v27-uw-flame")
MANIFEST = os.path.join(PKG, "manifest.json")
SPEC = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1\_uw_flame_layer_spec_20260924.json")
OUT = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1\_uw_flame_layer_out_20260924.json")

TARGET = "awake:entry:politics.small-factions"
TEXT = "小阵营"

# 各层代表句的**独有特征词**（用来判"这段文本属于哪一层"）
# ⚠️ 第一版用"老瘸子"判 rumor，结果 summary 里也写了"隔壁老瘸子家后院" ⇒ 判据自身撞车。
#    ⇒ 特征词必须是**该层独有**的串（跑前先核过：下面三个各只出现在自己那层）。
MARK = {
    "rumor": "没人来掀你的摊子",       # 变体 0：街坊看到的
    "summary": "被遗忘者的工会",        # 变体 2：他们自己的自称
    "detail": "癣疥之疾",              # 变体 1：领主的判断
}

queries = [
    # rumor 层受众
    {"name": "L1_commoner", "identity": "profile.commoner", "role": "commoner", "text": TEXT},
    {"name": "L1_villager", "identity": "profile.villager", "role": "villager", "text": TEXT},
    # summary 层受众
    {"name": "L2_townsfolk", "identity": "profile.townsfolk", "role": "townsfolk", "text": TEXT},
    # detail 层受众
    {"name": "L3_notable", "identity": "profile.notable", "role": "notable", "text": TEXT},
    {"name": "L3_merchant", "identity": "profile.merchant", "role": "merchant", "text": TEXT},
    {"name": "L3_noble", "identity": "profile.noble", "role": "noble", "text": TEXT},
    {"name": "L3_soldier", "identity": "profile.soldier", "role": "soldier", "text": TEXT},
]

json.dump({"queries": queries}, io.open(SPEC, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
proc = subprocess.run(["dotnet", SIM_DLL, "probe", MANIFEST, SPEC, OUT], capture_output=True)
rows = json.load(io.open(OUT, encoding="utf-8")) if os.path.exists(OUT) else []

print("=" * 96)
print("《小阵营》分层实测 · 包 geo1-v27-uw-flame")
print("=" * 96)


def layer_of(text):
    for lay, mark in MARK.items():
        if mark in text:
            return lay
    return "?"


seen = {}
for r in rows:
    name = r.get("name", "")
    text = (r.get("text") or "").replace("\n", " ")
    lay = layer_of(text)
    seen[name] = lay
    print("── %-16s scope=%-8s detail=%-7s → 听到的是 **%s** 层" % (
        name, r.get("scope"), r.get("detail"), lay))
    print("     %s" % text[:150])
    print()

print("=" * 96)
print("判定")
print("=" * 96)
exp = {"L1_commoner": "rumor", "L1_villager": "rumor", "L2_townsfolk": "summary",
       "L3_notable": "detail", "L3_merchant": "detail", "L3_noble": "detail", "L3_soldier": "detail"}
allok = True
for k, v in exp.items():
    got = seen.get(k)
    ok = got == v
    allok &= ok
    print("  %-16s 期望 %-8s 实得 %-8s %s" % (k, v, got, "✅" if ok else "❌"))

print()
l1 = {seen.get("L1_commoner"), seen.get("L1_villager")}
l2 = seen.get("L2_townsfolk")
l3 = {seen.get("L3_notable"), seen.get("L3_merchant"), seen.get("L3_noble"), seen.get("L3_soldier")}
print("① 三层都可达？", "✅" if (l1 <= {"rumor"} and l2 == "summary" and l3 <= {"detail"}) else "❌")
print("② 层间互斥（低能力不听 detail、高能力不听 rumor）？",
      "✅" if (l1 <= {"rumor"} and l3 <= {"detail"}) else "❌")
print("③ 空转护栏（三层不是同一段文本）？",
      "✅" if len(l1 | {l2} | l3) == 3 else "❌（三层返回数=%d，分层没生效）" % len(l1 | {l2} | l3))
print()
print("总结论：", "✅ 全绿" if allok else "❌ 有不符合项")
