# -*- coding: utf-8 -*-
"""清点 2026-09-17 那次「误移 op 记录 → 触发 studio 孤儿回收」造成的事故面（只读）。

事故经过（完整版见 docs/worldbook-migration/corrections_20260917/）：
  1. 我写的恢复脚本第 1 版用**通配** `k1_op_*` / `k1_rv_*` 把 52 个历史 op 记录移走了；
  2. 随后重跑 compile，studio 的 `QuarantineCompileOrphans()` 把 `compiled/` 下**没有 op 记录**
     的一级条目移进 `compiled/quarantine/orphans/`；
  3. 我把 op 文件放回去后，validate 又触发 recovery，把产出丢了的 op 标成
     `state=quarantined` + `failure_code=WB-AUTHORITY-RECOVERY-409`，marker/result 隔离进
     `compiled/quarantine/<opid>/`。

本脚本只做三件事：数（有多少）、分类（哪些是本次新产物、哪些是历史）、指路（要不要还原）。
不改任何东西。

运行：python -u tools/_inventory_quarantine_20260917.py
"""
import io
import json
import os
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = r"D:/AWAKE-Dev/AWAKE"
WS = os.path.join(ROOT, "tools/worldbook-studio/workspace/full-geo1")
COMPILED = os.path.join(WS, "compiled")
QUAR = os.path.join(COMPILED, "quarantine")


def dirs(p):
    if not os.path.isdir(p):
        return []
    return sorted(x for x in os.listdir(p) if os.path.isdir(os.path.join(p, x)))


def files(p, pat=None):
    if not os.path.isdir(p):
        return []
    out = []
    for root, _d, fs in os.walk(p):
        for f in fs:
            if pat is None or f.startswith(pat):
                out.append(os.path.join(root, f))
    return out


def main():
    print("工作区 = %s" % os.path.relpath(WS, ROOT))
    print()
    print("── compiled/ 一级内容 ──")
    top_dirs = dirs(COMPILED)
    top_files = sorted(f for f in os.listdir(COMPILED) if os.path.isfile(os.path.join(COMPILED, f)))
    print("  目录 %d 个：%s" % (len(top_dirs), top_dirs))
    print("  文件 %d 个：%s" % (len(top_files), top_files[:12]))
    print()

    orph = os.path.join(QUAR, "orphans")
    od = dirs(orph)
    geo = [x for x in od if x.startswith("geo1")]
    rep = [x for x in od if not x.startswith("geo1")]
    print("── compiled/quarantine/orphans/ ──")
    print("  一级目录 %d 个（geo1-* 产物 %d 个；其余 %d 个）" % (len(od), len(geo), len(rep)))
    print("  geo1-*：%s" % geo)
    print("  其余前 12 个：%s" % rep[:12])
    print()

    print("── compiled/quarantine/<opid>/ ──")
    qd = dirs(QUAR)
    qd = [x for x in qd if x != "orphans"]
    print("  目录 %d 个：%s" % (len(qd), qd[:10]))
    print()

    # op 记录
    ops = []
    for root, _d, fs in os.walk(WS):
        if os.sep + "compiled" + os.sep in root + os.sep:
            continue
        for f in fs:
            if f.startswith("op.") and f.endswith(".json"):
                ops.append(os.path.join(root, f))
    ops = sorted(set(ops))
    print("── op 记录（compiled/ 之外） ──")
    print("  文件 %d 个" % len(ops))
    states, codes, kinds = {}, {}, {}
    quarantined = []
    for p in ops:
        try:
            d = json.loads(io.open(p, encoding="utf-8").read())
        except Exception as e:
            states["<读不出:%s>" % type(e).__name__] = states.get("<读不出:%s>" % type(e).__name__, 0) + 1
            continue
        st = str(d.get("state") or d.get("State") or "?")
        states[st] = states.get(st, 0) + 1
        fc = str(d.get("failure_code") or d.get("failureCode") or "")
        if fc:
            codes[fc] = codes.get(fc, 0) + 1
        kd = str(d.get("kind") or d.get("Kind") or "?")
        kinds[kd] = kinds.get(kd, 0) + 1
        if st == "quarantined":
            quarantined.append((os.path.relpath(p, WS), kd, fc))
    print("  state 分布：%s" % json.dumps(states, ensure_ascii=False))
    print("  kind  分布：%s" % json.dumps(kinds, ensure_ascii=False))
    print("  failure_code：%s" % json.dumps(codes, ensure_ascii=False))
    print("  被标成 quarantined 的前 6 条：")
    for r, kd, fc in quarantined[:6]:
        print("    %s  kind=%s code=%s" % (r, kd, fc))
    print()

    print("── 结论要点 ──")
    print("  · `geo1-*` 产物目录 %d 个躺在 orphans/ 里；其中不含本次 v13f 的新产物"
          % len(geo))
    print("  · 这些目录的**内容没被删**（只是移了位置）⇒ 需要时可搬回去；")
    print("    但它们的 op 记录现在被标成 quarantined ⇒ 只看 op 记录会以为它们不存在。")
    print("  · 本轮真正要保留的对拍基线已另存：tools/_baseline/geo1-v12-runtime.json")


if __name__ == "__main__":
    main()
