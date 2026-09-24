# -*- coding: utf-8 -*-
"""
§六-4 真探针（2026-09-24，只读）——「变体 2 近似挂法」到底送不送得到？

背景（UW-RUMOR-MECHANICS §五-② ）：
  系统**没有 clan_ids** ⇒ 「说给火焰余烬的人听」这个条件表达不了。
  §五-③ 的近似方案：**culture_ids:[empire] + 低能力身份层**，
  靠**内容本身**（第一人称"我们"）让读者知道是他们在说话。

本探针要证的不是"能编译"，而是：
  A. 挂上 culture_ids:[empire] 后，**帝国地界的平民**能不能收到 → 能收（阳性）
  B. **非帝国地界的平民**能不能收到 → 不能收（阴性对照，证明 culture 真在筛）
  C. **低能力身份**收到的是 rumor 层、**高能力身份**收到的是 detail 层（分层没串）
  D. ★ 空转护栏：如果 A/B 的结果一样，说明 culture 条件**没生效**（恒真），
     那这条"近似"就是假的 —— 探针必须能报出来。

做法：把 spec 灌给 WorldbookRuntimeSim 的 probe 模式（内部直接调真件）。
     本探针的包用现役 geo1-v26-uw-rumor（small-factions 档已有的两层 + 我造的临时变体）。
"""
import io
import json
import os
import subprocess

REPO = r"D:\AWAKE-Dev\AWAKE"
SIM_DLL = os.path.join(REPO, r"tools\worldbook-runtime-sim\bin\Release\net10.0-windows\WorldbookRuntimeSim.dll")
PKG = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1\compiled\geo1-v26-uw-rumor")
MANIFEST = os.path.join(PKG, "manifest.json")
SPEC = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1\_uw_v2_spec_20260924.json")
OUT = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1\_uw_v2_out_20260924.json")

# ── 探针问题：用小阵营档的**现有文本**（不含七变体，先验机制） ──────────────
# 小阵营 rumor 层那句里「领地」＋ detail 层「兄弟会」都是可检索实词。
TEXT = "小阵营"

queries = [
    # A. 阳性：帝国地界 + 平民（应拿到 rumor 层）
    {"name": "A_empire_commoner", "identity": "profile.commoner", "role": "commoner",
     "culture": "empire", "text": TEXT},
    {"name": "A_empire_villager", "identity": "profile.villager", "role": "villager",
     "culture": "empire", "text": TEXT},
    # B. 阴性对照：非帝国地界 + 平民（**不该**拿到，culture 在筛）
    {"name": "B_vlandia_commoner", "identity": "profile.commoner", "role": "commoner",
     "culture": "vlandia", "text": TEXT},
    {"name": "B_sturgia_commoner", "identity": "profile.commoner", "role": "commoner",
     "culture": "sturgia", "text": TEXT},
    {"name": "B_aserai_villager", "identity": "profile.villager", "role": "villager",
     "culture": "aserai", "text": TEXT},
    # C. 分层：高能力身份（应拿到 detail 层，不该拿 rumor）
    {"name": "C_empire_noble", "identity": "profile.noble", "role": "noble",
     "culture": "empire", "text": TEXT},
    {"name": "C_empire_headman", "identity": "profile.headman", "role": "headman",
     "culture": "empire", "text": TEXT},
    # D. 无文化（兜底）：不限文化，平民该拿 rumor 层
    {"name": "D_noculture_commoner", "identity": "profile.commoner", "role": "commoner",
     "text": TEXT},
    # E. 阳性对照总量：不给文化、给高身份，应能拿到
    {"name": "E_noculture_noble", "identity": "profile.noble", "role": "noble",
     "text": TEXT},
]

spec = {"queries": queries}
with io.open(SPEC, "w", encoding="utf-8") as h:
    json.dump(spec, h, ensure_ascii=False, indent=2)

proc = subprocess.run(["dotnet", SIM_DLL, "probe", MANIFEST, SPEC, OUT], capture_output=True)
out = proc.stdout.decode("utf-8", errors="replace")
err = proc.stderr.decode("utf-8", errors="replace")

# 把结果读出来自己排（不依赖终端编码）
rows = json.load(io.open(OUT, encoding="utf-8")) if os.path.exists(OUT) else []

print("=" * 90)
print("§六-4 变体 2 近似挂法 · 真探针读数")
print("=" * 90)
print("包：geo1-v26-uw-rumor  问题文本：「%s」" % TEXT)
print()
for r in rows:
    name = r.get("name", "")
    state = r.get("state", "")
    hits = r.get("hits") or r.get("hit_ids") or []
    text = (r.get("text") or "").replace("\n", " ")
    print("── %s" % name)
    print("   culture=%-8s identity=%-18s scope=%-9s detail=%-7s state=%s"
          % (r.get("culture") or "(无)", r.get("identity"), r.get("scope"),
             r.get("detail"), state))
    if hits:
        for h in hits[:5]:
            hid = h.get("id") if isinstance(h, dict) else h
            print("       命中: %s" % hid)
    print("   文本: %s" % (text[:110] if text else "(空)"))
    print()

print("=" * 90)
print("判定")
print("=" * 90)


def find(n):
    for r in rows:
        if r.get("name") == n:
            return r
    return None


a1, b1 = find("A_empire_commoner"), find("B_vlandia_commoner")
if a1 and b1:
    a_hit = bool(a1.get("hits") or a1.get("hit_ids"))
    b_hit = bool(b1.get("hits") or b1.get("hit_ids"))
    print("A(帝国平民)收到 = %s ；B(瓦兰迪亚平民)收到 = %s" % (a_hit, b_hit))
    if a_hit and not b_hit:
        print("⇒ ✅ culture 真在筛：帝国收得到、别国收不到。近似挂法有效。")
    elif a_hit and b_hit:
        print("⇒ ⚠️ 两边都收到 ⇒ culture 条件**恒真**（没在筛），这道近似是假的。")
    elif not a_hit and not b_hit:
        print("⇒ ❌ 两边都收不到 ⇒ 这条表达在当前挂法下**送不到任何人**（空转）。")
    else:
        print("⇒ ⚠️ 反常：非帝国收到、帝国收不到。")
else:
    print("⇒ 缺读数，无法判定")
