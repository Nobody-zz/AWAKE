# -*- coding: utf-8 -*-
"""暗面 8 档「能不能长出 rumor」的侦察。

判据（照 TOPIC-WORLDBOOK §23:3x 那张表）：
  rumor 的立身之本＝**说话人自认没底** —— 原句里必须有存疑/猜测/转述标记。
  没有这种原句的档，**不许补 rumor**（甲案）。

本脚本只读、只报，不改档。
"""
import io
import os
import re

import yaml

DIR = r"D:\AWAKE-Dev\AWAKE\docs\worldbook-migration\projection\authoring-out"
OUT = r"D:\AWAKE-Dev\AWAKE\tools\_uw_rumor_scout_20260924.txt"

FS = ["underworld-alleys", "underworld-gang-leaders", "underworld-struggle",
      "underworld-gangs", "underworld-crime-rating", "underworld-blood-money",
      "underworld-bandits", "underworld-smuggling"]

# 存疑标记：说话人不确定 / 转述别人说 / 打折扣
UNCERTAIN = re.compile(
    r"据说|听说|据传|传言|传闻|似乎|貌似|大概|大约|也许|或许|兴许|说不定|"
    r"不清楚|不确定|不知道|谁知道|说不准|未见得|想必|看来|看起来|"
    r"有人说|人家说|他们说|都说是|据我|以为|好像|是不是|当真|难道"
)
# 说话人在场（说明是"谁在说"，可以拿来挂在表达上）
VOICE = re.compile(r"我说|我说过|我们|你们|他们|人家|大伙|各位|先生|大人|老兄|你")

L = []


def p(s=""):
    L.append(s)


p("===== 暗面 8 档 · rumor 可行性侦察 =====")
p("判据：rumor 必须长在「说话人自认没底」的原句上（甲案）。")
p("")

verdict = []
for f in FS:
    d = yaml.safe_load(io.open(os.path.join(DIR, f + ".yaml"), encoding="utf-8"))
    seen, qs = set(), []
    for s in d.get("sources", []):
        q = s["quote"]
        if q in seen:
            continue
        seen.add(q)
        qs.append((s["locator"].split("#")[-1], q))

    hit = [(k, q) for k, q in qs if UNCERTAIN.search(q)]
    p("=" * 70)
    p("%s ｜ %s" % (f, d["title"]["zh-CN"]))
    p("  引文池 %d 条（去重）→ 带存疑标记 %d 条" % (len(qs), len(hit)))
    for k, q in hit:
        p("    ★ %-14s %s" % (k, q))
    if not hit:
        p("    （无）")
        p("    ⇒ 结论：**长不出 rumor**，本档不补。")
        verdict.append((f, d["title"]["zh-CN"], 0, "不补"))
    else:
        p("    ⇒ 结论：有底，**可以补** rumor（上限 %d 条）" % len(hit))
        verdict.append((f, d["title"]["zh-CN"], len(hit), "可补"))
    p("")

p("=" * 70)
p("===== 汇总 =====")
p("%-30s %-10s %s" % ("档", "标题", "结论"))
for f, t, n, v in verdict:
    p("%-30s %-10s %s（存疑原句 %d 条）" % (f, t, v, n))
p("")
p("可补 %d 档 ／ 不补 %d 档" % (
    len([x for x in verdict if x[3] == "可补"]),
    len([x for x in verdict if x[3] == "不补"])))

io.open(OUT, "w", encoding="utf-8", newline="\n").write("\n".join(L) + "\n")
print("\n".join(L))
