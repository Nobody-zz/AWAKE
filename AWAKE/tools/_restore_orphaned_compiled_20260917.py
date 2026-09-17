# -*- coding: utf-8 -*-
"""事故恢复：把我误触发的「孤儿回收」从 `compiled/quarantine/orphans/` 里移回 `compiled/`。

事故链（我造成的，必须留痕）：
  ① 18:16 我跑恢复脚本第 1 版时，用 `k1_op_*` 通配把 operations/ 里 **52 个历史 op 记录**移到了别处；
  ② 18:17 重跑 compile 触发了 studio 的 recovery 流程（`AcquireCompileRecoveryLease` 会把 lease 写成
     `recovery:<owner>`，正是现场看到的内容）；
  ③ recovery 调 `QuarantineCompileOrphans()`（`AuthorityGate.cs:1330`）—— 它把 `compiled/` 下
     **所有没有 op 记录（tracked）的一级条目**移进 `compiled/quarantine/orphans/`。
     此时 operations/ 被搬空 ⇒ 43 个产物目录 + 4695 个历史报告目录全被隔离。
  ④ 52 个 op 记录已还原 ✔；本脚本负责把**产物目录**移回。

判定依据（为什么不慌）：
  `MoveToQuarantine` 在目标同名时会给名字加 `.<guid>` 后缀。orphans 里的 43 个 `geo1-*` **全是干净名字**
  ⇒ 它们是**首次**被隔离，即全部来自本次事故（历史被隔离过同名的话，这次会带后缀）。

恢复规则（复刻 `QuarantineCompileOrphans` 的 tracked 定义）：
  扫 `authoring-v1/operations/*.json`，取 `kind == "compile_runtime"` 且 `state != "quarantined"` 的
  `target_relative`（为空且 state ∈ {reserved, prepared, executing} 时退回 `output_root`），
  凡 `orphans/<name>` 落在 tracked 里的 ⇒ 移回 `compiled/<name>`。

⚠️ `reports.<guid>`（4695 个）**不恢复**：它们是每次编译生成的报告目录，永远不在 tracked 里，
   被隔离是机制的正常结果（只是时点被我提前触发）。东西没丢，都在 orphans 里，随时可取。
"""
import io
import json
import os
import shutil
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = r"D:/AWAKE-Dev/AWAKE"
WS = os.path.join(ROOT, "tools/worldbook-studio/workspace/full-geo1")
OPS = os.path.join(WS, "authoring-v1/operations")
COMPILED = os.path.join(WS, "compiled")
ORPHANS = os.path.join(COMPILED, "quarantine", "orphans")

# ---- 1. 复刻 tracked 集合 ----
tracked = set()
n_ops = 0
import collections
_names = os.listdir(OPS)
print("诊断：OPS=%s" % OPS)
print("诊断：存在=%s 条目数=%d" % (os.path.isdir(OPS), len(_names)))
_kinds = collections.Counter()
_errs = collections.Counter()
for name in _names:
    if not name.endswith(".json"):
        continue
    path = os.path.join(OPS, name)
    try:
        op = json.load(io.open(path, encoding="utf-8"))
        _kinds[str(op.get("kind"))] += 1
    except Exception as e:
        _errs[type(e).__name__] += 1
        continue
    if op.get("kind") != "compile_runtime":
        continue
    if op.get("state") == "quarantined":
        continue
    n_ops += 1
    target = op.get("target_relative") or ""
    if not target and op.get("state") in ("reserved", "prepared", "executing"):
        target = op.get("output_root") or ""
    if target:
        tracked.add(os.path.normpath(os.path.join(WS, target.replace("/", os.sep))))
print("诊断：kind 分布 %s；解析失败 %s" % (dict(_kinds), dict(_errs)))
print("诊断：tracked 样例 %s" % sorted(tracked)[:3])
print("operations/*.json 里 compile_runtime 记录 %d 个 ⇒ tracked 目标 %d 个" % (n_ops, len(tracked)))

# ---- 2. 找 orphans 里该恢复的产物目录 ----
items = sorted(os.listdir(ORPHANS))
cand = [n for n in items if n.startswith("geo1") and os.path.isdir(os.path.join(ORPHANS, n))]
print("orphans 里 geo1-* 目录 %d 个；其中带 guid 后缀的 %d 个"
      % (len(cand), sum(1 for n in cand if len(n) > 40)))

restore, keep, conflict = [], [], []
for n in cand:
    src = os.path.join(ORPHANS, n)
    dst = os.path.join(COMPILED, n)
    if os.path.normpath(dst).lower() in {t.lower() for t in tracked}:
        (restore if not os.path.exists(dst) else conflict).append(n)
    else:
        keep.append(n)

print()
print("该移回（tracked 命中且目标不存在）：%d 个" % len(restore))
print("留在 orphans（不在 tracked）：%s" % (keep or "无"))
print("目标已存在、跳过：%s" % (conflict or "无"))

# ---- 3. 移动 ----
moved = []
for n in restore:
    shutil.move(os.path.join(ORPHANS, n), os.path.join(COMPILED, n))
    moved.append(n)
print()
print("已移回 %d 个：" % len(moved))
for n in moved:
    print("   %s" % n)

# ---- 4. 验收：v12 基线包回来且 hash 与上线包一致 ----
import hashlib


def sha(p):
    return hashlib.sha256(open(p, "rb").read()).hexdigest()


LIVE = os.path.join(ROOT, "ModuleData/Worldbook/packages/calradia/runtime.json")
PEER = os.path.join(COMPILED, "geo1-v12-cortain-summary/runtime.json")
print()
print("compiled/ 下现在的一级目录 %d 个" % len([x for x in os.listdir(COMPILED)
                                                 if os.path.isdir(os.path.join(COMPILED, x))]))
if os.path.exists(PEER):
    same = sha(PEER) == sha(LIVE)
    print("v12 基线包已回位：hash 与上线包一致 = %s" % same)
else:
    print("❌ v12 基线包仍未回位")
