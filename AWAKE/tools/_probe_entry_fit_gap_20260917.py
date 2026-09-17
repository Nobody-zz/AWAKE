# -*- coding: utf-8 -*-
"""把「中文入口词自命中失败」那 36% 拆开看：是哪些词、被谁抢走、为什么。

输入：判定脚本产出的 *_judge.json（per_q）＋ 数据侧普查的 kw_owner 索引。
"""
import collections
import io
import json
import sys

sys.stdout.reconfigure(encoding="utf-8")

SIDE_JUDGE = "tools/_entry_fit_off_20260917_judge.json"
DATA = "tools/_probe_entry_fit_data_20260917.json"
PKG = "ModuleData/Worldbook/packages/calradia/runtime.json"

jq = json.load(io.open(SIDE_JUDGE, encoding="utf-8"))["per_q"]
data = json.load(io.open(DATA, encoding="utf-8"))
kw_owner = {k: v for k, v in data["kw_owner"].items()}
pkg = json.load(io.open(PKG, encoding="utf-8"))
title = {}
for e in pkg["entries"]:
    t = e.get("title")
    title[e["id"].replace("awake:entry:", "")] = (t or {}).get("zh-CN") if isinstance(t, dict) else t

fail = [x for x in jq if x["kind"] == "kw_zh" and not x["self_hit"]]
ok = [x for x in jq if x["kind"] == "kw_zh" and x["self_hit"]]
print("中文入口词：自命中 %d ／ 失败 %d（共 %d）" % (len(ok), len(fail), len(ok) + len(fail)))

# ---------- 分类 ----------
b = collections.Counter()
samples = collections.defaultdict(list)
for x in fail:
    w, exp = x["text"], x["entry"]
    owners = set(o.replace("awake:entry:", "") for o in kw_owner.get(w, []))
    cov = len(owners)
    is_sub_of_own_title = w in (title.get(exp) or "")
    hit_own_domain_sibling = any(h.split(".")[0] == exp.split(".")[0] for h in x["hits"])
    if not x["hits"]:
        k = "① 一个都没命中（链路空手）"
    elif hit_own_domain_sibling:
        k = "② 命中了，但被**同域邻居**抢先"
    else:
        k = "③ 命中了，但被**别的域**抢走"
    if cov > 1:
        k += "｜且这词本身覆盖 %d 条" % cov
    b[k] += 1
    if len(samples[k]) < 6:
        samples[k].append((w, exp, x["hits"][:3], cov, is_sub_of_own_title))

print("\n按失败形态分组：")
for k in sorted(b):
    print("   %-58s %d" % (k, b[k]))

print("\n样例：")
for k in sorted(samples):
    print("  ── %s" % k)
    for w, exp, hits, cov, sub in samples[k]:
        print("     「%s」 属于 %s%s" % (w, exp, "（是本条目名的子串）" if sub else ""))
        print("        命中 -> %s" % ("、".join(hits) or "—"))

# ---------- 覆盖度视角 ----------
print("\n失败词的「覆盖条目数」分布（覆盖高的词本来就该被更强的条目拿走）：")
cb = collections.Counter()
for x in fail:
    cov = len(set(kw_owner.get(x["text"], [])))
    cb["1 条（只属于它自己）" if cov <= 1 else "2-5 条" if cov <= 5 else "6-20 条" if cov <= 20 else ">20 条"] += 1
for k in ["1 条（只属于它自己）", "2-5 条", "6-20 条", ">20 条"]:
    if cb[k]:
        print("   %-22s %d" % (k, cb[k]))

print("\n★ 只属于它自己、却仍打不中自己 的中文入口词（这是最像「缺陷」的一类）：")
hard = [x for x in fail if len(set(kw_owner.get(x["text"], []))) <= 1]
print("   共 %d 个" % len(hard))
for x in hard[:20]:
    print("     「%-18s」 %-42s -> %s" % (x["text"], x["entry"][:42],
                                        "、".join(x["hits"][:2]) if x["hits"] else "空手"))

# ---------- 有效入口数最少的域 ----------
print("\n按域看「每条有效中文入口词数」中位：")
by_dom = collections.defaultdict(list)
per = collections.defaultdict(lambda: [0, 0])
for x in jq:
    if x["kind"] != "kw_zh":
        continue
    dom = x["entry"].split(".")[0]
    per[x["entry"]][1] += 1
    if x["self_hit"]:
        per[x["entry"]][0] += 1
for e, (o, n) in per.items():
    by_dom[e.split(".")[0]].append(o)
import statistics
for d, v in sorted(by_dom.items()):
    print("   %-11s %d 条，有效中文入口中位 %.1f ／ 最少 %d" % (d, len(v), statistics.median(v), min(v)))
