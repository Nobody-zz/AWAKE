# -*- coding: utf-8 -*-
"""量「ii-b」这条出口的真实发生率。

背景：WorldKnowledgeQueryService 里，身份受限有两个出口
  b1  有更低档的表达可给  -> Expression != null -> state = partial（已知：给了糙版本）
  b2  一条都给不了        -> PermissionLimited -> state = blocked(reason=permission) -> 不让 AI 开口
A5 要改的是 b2。动手前先量它到底有多常见——罕见的事不值得为它改模板。

数据源：IDENTITY_GATE 自己导出的报告（真包 × 12 身份 × 全部题）。
"""
import io
import json
import sys
from collections import Counter

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

REPORT = r"C:\Users\26811\AppData\Local\Temp\awake-sim-identity-gate.json"

with open(REPORT, encoding="utf-8") as fh:
    data = json.load(fh)

print("包：%s ｜ 身份数 %d ｜ 语义臂 %s" % (data.get("package"), data.get("identityCount"), data.get("semantic")))
print("扫描：被排除行 %s（泄漏 %s）｜ 该知道行 %s（真拿到 %s）｜ 点名题失败 %s"
      % (data.get("sweepDeniedRows"), data.get("sweepLeaks"),
         data.get("shouldKnowRows"), data.get("shouldKnowHit"), data.get("deniedCasesFail")))
print("=" * 78)

print("顶层键：" + ", ".join(sorted(data.keys())))

denied_states = data.get("deniedStates") or {}
print("【被排除行】（他不该知道、但问了）的状态分布 —— 门禁自己报的：")
if not denied_states:
    print("   (门禁没报这个字段：老版本的 worldbook-runtime-sim)")
for name, count in sorted(denied_states.items(), key=lambda kv: -kv[1]):
    print("   %-26s %d" % (name, count))
print("   ⤷ blocked/permission ＝「材料在、一条都不给 ⇒ 替他闭嘴、吐写死台词」，也就是要改的那一档")
print("-" * 78)

misses = data.get("shouldKnowMisses") or []
print("shouldKnowMisses 条数 = %d" % len(misses))

cases = data.get("cases") or []
print("cases 条数 = %d" % len(cases))
if cases:
    print("首个 case 的字段：" + ", ".join(sorted(cases[0].keys())))
print("-" * 78)

# 把所有 state 值收集起来（结构未知，先递归找）
states = Counter()
blocked_samples = []
def walk(node, ctx):
    if isinstance(node, dict):
        if "state" in node and isinstance(node["state"], str):
            states[node["state"]] += 1
            if node["state"] == "blocked" and len(blocked_samples) < 10:
                blocked_samples.append(dict(ctx=ctx, node=node))
        for key, value in node.items():
            walk(value, ctx + "." + key)
    elif isinstance(node, list):
        for i, value in enumerate(node):
            walk(value, ctx + "[%d]" % i)

walk(data, "")
print("全报告的 state 取值分布：")
for name, count in states.most_common():
    print("   %-18s %d" % (name, count))

print("-" * 78)
print("【该知道却没拿到】的那些行，按 state 拆开：")
miss_states = Counter(str(m.get("state")) for m in misses)
for name, count in miss_states.most_common():
    print("   %-18s %d" % (name, count))
print("-" * 78)
print("其中 state=blocked 的样本（A5 要改的就是它）：")
shown = 0
for m in misses:
    if str(m.get("state")) != "blocked":
        continue
    shown += 1
    if shown > 15:
        break
    print("   identity=%s" % m.get("identity"))
    print("      query=%s" % m.get("query"))
    print("      target=%s" % m.get("target"))
print("   （blocked 合计 %d 条）" % miss_states.get("blocked", 0))
print("-" * 78)
print("非 blocked 的样本（前 8 条，看它们是不是走了糙版本那条路）：")
shown = 0
for m in misses:
    if str(m.get("state")) == "blocked":
        continue
    shown += 1
    if shown > 8:
        break
    print("   [%s] identity=%s query=%s" % (m.get("state"), m.get("identity"), m.get("query")))

# 显式找 permitted/blocked 之类的布尔字段
flags = Counter()
def walk_flags(node):
    if isinstance(node, dict):
        for key, value in node.items():
            if isinstance(value, bool) and any(t in key.lower() for t in ("block", "permit", "allow", "limit")):
                flags["%s=%s" % (key, value)] += 1
            walk_flags(value)
    elif isinstance(node, list):
        for value in node:
            walk_flags(value)

walk_flags(cases)
if flags:
    print("相关布尔字段分布：")
    for name, count in flags.most_common():
        print("   %-28s %d" % (name, count))
