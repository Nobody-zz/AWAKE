# -*- coding: utf-8 -*-
"""
世界书条目 id 稳定性探针（2026-09-20）
------------------------------------------------------------------
目的：回答「哪一类编辑会改掉运行时条目 id」。
规则出处（唯一落点）：
  AWAKE/tools/worldbook-studio/src/Awake.WorldbookStudio.Core/RuntimePackageCompiler.cs:504
      StableEntry(v) = "awake:entry:" + Sanitize(v.Replace("doc.", ""))
      Sanitize(v)    = v.Trim().ToLowerInvariant()，保留 [字母数字 _ - .]，其余换 _，两端去 _
  :110-111  sourceId = document["id"]；entryId = StableEntry(sourceId)

做法（同源复算，不引入第二套规则）：
  1. 取真产物 runtime.json 的每个 entries[].id（strip 掉 awake:entry: 前缀）=> 产物侧尾串集合
  2. 取 authoring 档 *.yaml 的 id: 字段，按同一条规则复算                  => 源侧尾串集合
  3. 三个读数：
     A. 产物 → 源：产物里有没有源侧复算不上来的 id（= 规则不可预测/手改过）
     B. 源 → 产物：源里有、产物里没有（= 未编译进包，属预期）
     C. 产物 id 是否全部落在「规则能算出的形状」上
  本探针只读，不写任何产物。
"""
import io
import json
import os
import re
import sys
from collections import Counter

ROOT = r"D:\AWAKE-Dev\AWAKE"
RUNTIME = os.path.join(ROOT, "ModuleData", "Worldbook", "packages", "calradia", "runtime.json")
AUTHORING = os.path.join(ROOT, "docs", "worldbook-migration", "projection", "authoring-out")
OUT = os.path.join(ROOT, "tools", "_wb_id_stability_probe_20260920.txt")

lines = []


def say(s):
    lines.append(s)
    print(s)


def sanitize(value):
    """同源于 RuntimePackageCompiler.Sanitize。"""
    s = str(value).strip().lower()
    out = []
    for ch in s:
        if ch.isalnum() or ch in ("_", "-", "."):
            out.append(ch)
        else:
            out.append("_")
    return "".join(out).strip("_")


def stable_entry(source_id):
    """同源于 RuntimePackageCompiler.StableEntry。"""
    return "awake:entry:" + sanitize(str(source_id).replace("doc.", ""))


say("=" * 76)
say("世界书条目 id 稳定性探针 · 2026-09-20")
say("同源规则：RuntimePackageCompiler.cs:504 / :508（StableEntry + Sanitize）")
say("=" * 76)

# ---------- 真产物 ----------
if not os.path.isfile(RUNTIME):
    say("!! 产物不存在：" + RUNTIME)
    sys.exit(2)

with io.open(RUNTIME, encoding="utf-8") as fh:
    rt = json.load(fh)

prod_ids = [str(e.get("id", "")) for e in rt.get("entries", [])]
prod_tail = []
bad_shape = []
for pid in prod_ids:
    if pid.startswith("awake:entry:"):
        prod_tail.append(pid[len("awake:entry:"):])
    else:
        bad_shape.append(pid)

say("")
say("[1] 产物侧（真包）")
say("    entries 总数            = %d" % len(prod_ids))
say("    带 awake:entry: 前缀     = %d" % len(prod_tail))
say("    前缀不符（异形 id）      = %d %s" % (len(bad_shape), bad_shape[:5]))

# 产物 id 是否全部满足「sanitize 后等于自身」（即已是规范形）
not_canonical = [t for t in prod_tail if sanitize(t) != t]
say("    非规范形（sanitize 会改写它） = %d %s" % (len(not_canonical), not_canonical[:5]))

# 产物 id 的域分布（形状 awake:entry:<域>.<类型>-<名>）
domain_counter = Counter()
for t in prod_tail:
    head = t.split(".", 1)[0] if "." in t else "(无点)"
    domain_counter[head] += 1
say("    域分布 top: " + ", ".join("%s=%d" % (k, v) for k, v in domain_counter.most_common(12)))

# ---------- authoring 源 ----------
src_doc_ids = []
if os.path.isdir(AUTHORING):
    for name in sorted(os.listdir(AUTHORING)):
        if not name.endswith(".yaml"):
            continue
        path = os.path.join(AUTHORING, name)
        try:
            with io.open(path, encoding="utf-8") as fh:
                for line in fh:
                    if line.startswith("id:"):
                        src_doc_ids.append(line[3:].strip())
                        break
        except Exception as exc:
            say("    !! 读不动 " + name + " : " + str(exc))

say("")
say("[2] 源侧（authoring 档）")
say("    档数（有 id: 的 .yaml） = %d" % len(src_doc_ids))
prefix_counter = Counter(d.split(".", 1)[0] for d in src_doc_ids)
say("    前缀分布: " + ", ".join("%s=%d" % (k, v) for k, v in prefix_counter.most_common(8)))

src_tail = {}
for d in src_doc_ids:
    tail = stable_entry(d)[len("awake:entry:"):]
    src_tail.setdefault(tail, []).append(d)

# ---------- 对照 ----------
say("")
say("[3] 对照（产物 ← 源，按同源规则复算尾串）")
prod_set = set(prod_tail)
src_set = set(src_tail.keys())

orphan_in_prod = sorted(prod_set - src_set)      # 产物有、源侧复算不上
missing_in_prod = sorted(src_set - prod_set)     # 源有、产物无（未编译，预期）
both = prod_set & src_set

say("    交集（源与产物对得上）  = %d" % len(both))
say("    产物有 / 源侧算不出     = %d" % len(orphan_in_prod))
say("    源有 / 产物没有（未编译）= %d" % len(missing_in_prod))
if orphan_in_prod:
    say("    -- 产物有但源侧复算不上（前 10）：")
    for t in orphan_in_prod[:10]:
        say("       " + t)

# ---------- 关键词：slug 里含 doc. 或会被 sanitize 动的档 ----------
say("")
say("[4] 危险样本：源档 id 里含会被规则改写的内容")
danger = []
for d in src_doc_ids:
    body = d.replace("doc.", "")
    if sanitize(body) != body:
        danger.append((d, sanitize(body)))
say("    会被 sanitize 改写的源 id = %d" % len(danger))
for d, s in danger[:10]:
    say("       %s  ->  %s" % (d, s))

say("")
say("[5] 若改 slug / 改 domain，谁跟着动（示例）")
sample = sorted(prod_tail)[:3]
for t in sample:
    doc = "doc." + t
    say("    产物 %s  ⇔ 源 %s" % ("awake:entry:" + t, doc))
say("    ⇒ 改 slug（点号后那段）或改 domain（点号前那段），两边同一串一起变，id 随之变。")
say("    ⇒ 改 title / aliases / summary / assertions：不在这条链上，id 不变。")

with io.open(OUT, "w", encoding="utf-8", newline="\n") as fh:
    fh.write("\n".join(lines) + "\n")

say("")
say("落盘：" + OUT)
