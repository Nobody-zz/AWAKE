# -*- coding: utf-8 -*-
"""把「他不该知道」这一类题补进检索题集（09-17，甲方「补啊」）。

题集是唯一数据源（Python 探针与 C# 验台都读它），所以这一类题也放同一份里。
形状：deniedCases.cases[]，每条 = 同一问句换一个问的人，并且**必须带阳性对照**：
  · allowedIdentity        够格的身份 ⇒ 必须拿到 target；
  · forbiddenText          被禁的那一档正文里的独有串 ⇒ 必须在 forbiddenControlIdentity 那里**真能出现**
                           （否则"他没漏"是空转）；在 deniedIdentity 那里必须**不出现**；
  · requiredText           被禁身份**该拿到**的糙版本独有串 ⇒ 必须出现（证明他是"只知道个大概"，不是"啥都不知道"）；
  · forbiddenControlIdentity  可选，默认同 allowedIdentity（给"分层"用：够细的那一档另有一个身份能拿到）。
判据跑在 `tools/worldbook-runtime-sim`（子命令 identity-gate）。
"""
import io
import json

CASES = "tools/_retrieval_cases_20260916.json"

DENIED = {
    "_note": (
        "第三类题：「他不该知道」。与上面那些题的差别是 —— **问法不变，只换问的人**。"
        "判据跑在 `tools/worldbook-runtime-sim` 的 identity-gate；带上语义臂跑：AWAKE_SIM_SEMANTIC=1。"
        "每条都带阳性对照：够格的身份必须拿到、被禁那一档必须在他那里真能出现 —— 否则「他没漏」是空转。"
    ),
    "cases": [
        {
            "query": "毛皮值钱吗？",
            "target": "awake:entry:economy.goods-fur",
            "allowedIdentity": "awake:identity:noble",
            "deniedIdentity": "awake:identity:commoner",
            "forbiddenText": "毛皮基准价 400",
            "requiredText": "一张好皮子能顶半个月嚼谷",
            "dimension": "详细度分层",
            "note": "同一问句：贵族拿 detail 档（有基准价与南北价差），平民只配 rumor 档（只有「一张好皮子顶半个月嚼谷」）。查的是「他知道的是哪一档」，不只是「知不知道」。",
        },
        {
            "query": "盐是从哪儿来的？",
            "target": "awake:entry:economy.goods-salt",
            "allowedIdentity": "awake:identity:noble",
            "deniedIdentity": "awake:identity:commoner",
            "forbiddenText": "盐基准价 40",
            "requiredText": "盐比肉还金贵",
            "dimension": "详细度分层",
            "note": "同 D1 的形状，换一条（防单点）。平民知道「盐贵、产地集中在海边盐湖」，不该知道「基准价 40、翻倍卖」这种行市话。",
        },
        {
            "query": "戴·科尔坦家靠什么发财？",
            "target": "awake:entry:politics.clans-charas-cortain-secret",
            "allowedIdentity": "awake:identity:noble",
            "deniedIdentity": "awake:identity:commoner",
            "forbiddenText": "操办科尔坦家的图谋",
            "dimension": "整条不可见",
            "note": "这条条目**没有任何给平民的授权**（grant 只到 townsfolk/merchant/headman/soldier/noble/notable/tavernkeeper）⇒ 平民应当整条拿不到，不是拿个糙版本。",
        },
        {
            "query": "戴·科尔坦家靠什么发财？",
            "target": "awake:entry:politics.clans-charas-cortain-secret",
            "allowedIdentity": "awake:identity:noble",
            "deniedIdentity": "awake:identity:merchant",
            "forbiddenText": "操办科尔坦家的图谋",
            "requiredText": "科尔坦家族",
            "dimension": "详细度分层（公开面 vs 秘密面）",
            "note": "同一条、同一问句：商人拿 summary 档（「沙拉斯的海务财富尽归戴·科尔坦家族」＝账面是公开的），secret 档（「这些财富正被拿来操办图谋」）只给贵族。这正是初衷说的「等级管后果、公开程度管能不能出门」。",
        },
        {
            "query": "林·莫德里斯是什么湖？",
            "target": "awake:entry:geography.lakes-llyn-modris",
            "allowedIdentity": "awake:identity:villager",
            "deniedIdentity": "awake:identity:commoner",
            "forbiddenControlIdentity": "awake:identity:townsfolk",
            "forbiddenText": "火山口湖",
            "dimension": "地域 + 详细度",
            "note": (
                "★ 这条最尖：`villager`（村民，local/rumor）**拿得到**，而 `commoner`（无地缘的平民）**整条拿不到** —— "
                "同一条条目、同一问句。⇒ 证明「谁知道」是按身份 id 与它的父链判的，不是按「地位高低」排的。"
                "同时它带三档对照：villager=rumor（巨人踩出来的脚印）／townsfolk=summary（火山口湖）／commoner=无。"
                "⚠️ 因为 commoner 是整条不可见，所以这条**不设 requiredText**（设了＝题写反了，判据会当场判红）。"
            ),
        },
    ],
}

with io.open(CASES, encoding="utf-8") as handle:
    data = json.load(handle)

data["deniedCases"] = DENIED

data.setdefault("changes", []).append({
    "date": "2026-09-17",
    "what": "新增第三类题 `deniedCases`（5 条）：「他不该知道」—— 问法不变、只换问的人；每条带阳性对照。",
    "why": "甲方 09-17「照着我的初衷核验一遍」核出：现有 24 条题**在「谁知道」这一维上是盲的**（门禁验台 `PickIdentity` 从目标条目自己的 grants 里挑身份，题集里 `identity` 0 命中）。初衷第一层就是「谁知道」，不补这一类题，它永远没有判据。",
    "effect": "**不动**原有 24 条的分母与分子、不动任何门禁常量；这是**新增的第二条门禁**（跑在 `tools/worldbook-runtime-sim identity-gate`），与 `RETRIEVAL_GATE` / `MERGE_GATE` 并列。",
    "evidence": "待补：`tools/_identity_gate_20260917*.txt`（挂/不挂语义臂两跑 + 变异检验）。",
})

data.setdefault("rulings", {})["2026-09-17c"] = (
    "甲方「补啊」⇒ 把核验里提的两件补上：① 给验收补一列「该不该知道」（＝本文件 `deniedCases` ＋ "
    "`tools/worldbook-runtime-sim` 的 identity-gate 子命令）；② 题集补一类「他不该知道」的题（本题 5 条）。"
    "口径：**阳性对照不可省** —— 被禁的那一档必须在够格的身份那里真能出现，否则「他没漏」是空转；"
    "**同一个问句换人问**，不另造问法；**不改语料**（沿用 09-17 三条裁定）。"
)

with io.open(CASES, "w", encoding="utf-8") as handle:
    json.dump(data, handle, ensure_ascii=False, indent=2)
    handle.write("\n")

print("已写入", CASES, "；deniedCases =", len(DENIED["cases"]), "条；顶层键:", list(data.keys()))
