# -*- coding: utf-8 -*-
"""前缀归一 · 最终复核（v2 口径：B 案）。

五道（比上一版换了口径 —— 上一版把"id 不许动"当对的，那是错的）：
  1. 两侧 558 档，8 档逐字节一致
  2. 档内：`doc` id 首段=新 slug ；`assertion`/`expr` 子 id=旧形态（B 案）
  3. 全仓无旧档名/旧 doc id 当引用（快照类除外）
  4. 产物：8 个新条目 id 全在、8 个旧 id 退场、子 id 保留旧形态
  5. 现役包（ModuleData）未动
"""
import hashlib
import io
import json
import os
import re

ROOT = r"D:\AWAKE-Dev\AWAKE"
WS = os.path.join(ROOT, "tools", "worldbook-studio", "workspace", "full-geo1", "authoring")
AO = os.path.join(ROOT, "docs", "worldbook-migration", "projection", "authoring-out")
OUT = os.path.join(ROOT, "tools", "worldbook-studio", "workspace", "full-geo1",
                   "compiled", "geo1-v25-underworld")
REPORT = os.path.join(ROOT, "tools", "_underworld_final_check2_20260924.txt")

TBL = [
    ("underworld-alleys.yaml",       "politics", "underworld-alleys",        "town-alleys"),
    ("underworld-gang-leaders.yaml", "politics", "underworld-gang-leaders",  "alley-gang-leaders"),
    ("underworld-struggle.yaml",     "politics", "underworld-struggle",      "alley-struggle"),
    ("underworld-gangs.yaml",        "politics", "underworld-gangs",         "town-gangs"),
    ("underworld-crime-rating.yaml", "politics", "underworld-crime-rating",  "crime-rating"),
    ("underworld-blood-money.yaml",  "politics", "underworld-blood-money",   "blood-money"),
    ("underworld-bandits.yaml",      "politics", "underworld-bandits",       "bandits"),
    ("underworld-smuggling.yaml",    "economy",  "underworld-smuggling",     "smuggling"),
]

lines, fails = [], []


def log(s):
    lines.append(s)


def fail(s):
    fails.append(s)
    log("  !! " + s)


def sha(p):
    return hashlib.sha256(open(p, "rb").read()).hexdigest().upper()


log("===== 前缀归一 · 最终复核（B 案口径）=====")
log("")

# 1
log("[1] 两侧档数 / 双写")
for nm, d in (("WS", WS), ("AO", AO)):
    n = len([x for x in os.listdir(d) if x.endswith(".yaml") and not x.startswith(("_", "source-"))])
    log("   %s 档数 = %d  %s" % (nm, n, "OK" if n == 558 else "!!"))
    if n != 558:
        fail("%s 档数 %d" % (nm, n))
b = 0
for fn, _, _, _ in TBL:
    wp, ap = os.path.join(WS, fn), os.path.join(AO, fn)
    if not (os.path.exists(wp) and os.path.exists(ap)) or sha(wp) != sha(ap):
        b += 1
        fail("双写不一致 %s" % fn)
log("   8 档双写：不一致 %d  %s" % (b, "OK" if b == 0 else "!!"))
log("")

# 2
log("[2] 档内 id 层（B 案：doc 换新 / 子 id 留旧）")
for fn, dom, new, old in TBL:
    t = io.open(os.path.join(WS, fn), encoding="utf-8").read()
    a = ("id: doc.%s.%s\n" % (dom, new)) in t
    b2 = ("id: doc.%s.%s\n" % (dom, old)) in t
    c = ("assertion.%s-1" % old) in t
    dd = ("expr.%s-" % old) in t
    e = ("assertion.underworld" in t) or ("expr.underworld" in t)
    tag = "OK" if (a and not b2 and c and dd and not e) else "!!"
    log("   %-30s doc新=%s doc旧残留=%s 子id旧=%s 子id被误改=%s  %s" % (
        fn, "Y" if a else "N", "Y" if b2 else "N", "Y" if (c and dd) else "N",
        "Y" if e else "N", tag))
    if tag == "!!":
        fail("%s id 层不符" % fn)
log("")

# 3
log("[3] 全仓旧引用残留（排除快照 / 备份 / 产物 / 本批脚本）")
SKIP = ("/compiled/", "/_archive", "\\compiled\\", "\\_archive", "\\authoring-v1\\",
        "/authoring-v1/", "_underworld_", "_alley_", "should-link", "link-registry",
        "_dark_A_", "\\_dark_", "/_dark_",
        # WS/full-geo1 下 `_` 前缀的一次性探针产物（该目录整体被 .gitignore 排除，
        # 内容是 09-20 那批旧档名的快照，不该按"引用残留"计）
        "/full-geo1/_", "\\full-geo1\\_", "_link_probe", "_village_", "_geo_", "_probe")
pat = re.compile(r"(politics|economy)\.(town-alleys|alley-gang-leaders|alley-struggle|town-gangs|crime-rating|blood-money|bandits|smuggling)\b")
found = []
for base, dirs, files in os.walk(ROOT):
    dirs[:] = [d for d in dirs if d not in (".git", "bin", "obj", "node_modules", "__pycache__")]
    for fn in files:
        if not fn.endswith((".md", ".py", ".ps1", ".json")):
            continue
        p = os.path.join(base, fn)
        if any(s in p for s in SKIP):
            continue
        try:
            t = io.open(p, encoding="utf-8", errors="replace").read()
        except Exception:
            continue
        for m in pat.finditer(t):
            found.append((os.path.relpath(p, ROOT).replace("\\", "/"), m.group(0)))
if found:
    for rel, w in found[:12]:
        log("   !! %s -> %s" % (rel, w))
    fail("仍有 %d 处旧 id 引用" % len(found))
else:
    log("   无残留  OK")
log("")

# 4
log("[4] 编译产物")
op = os.path.join(OUT, "runtime.json")
if os.path.exists(op):
    o = json.load(io.open(op, encoding="utf-8"))
    ids = set("doc." + e["id"].replace("awake:entry:", "", 1) for e in o["entries"])
    NEW = ["doc.%s.%s" % (d, n) for _, d, n, _ in TBL]
    OLD = ["doc.%s.%s" % (d, s) for _, d, _, s in TBL]
    miss = [x for x in NEW if x not in ids]
    left = [x for x in OLD if x in ids]
    rt = io.open(op, encoding="utf-8").read()
    sub = all(("assertion.%s-1" % s) in rt for _, _, _, s in TBL)
    bad = ("assertion.underworld" in rt) or ("awake:expression:underworld" in rt)
    log("   档数 = %d" % len(ids))
    log("   8 个新条目 id 全在：%s" % ("是" if not miss else "否 %s" % miss))
    log("   8 个旧条目 id 已退场：%s" % ("是" if not left else "否 %s" % left))
    log("   子 id 保留旧形态：%s ；子 id 被误改：%s" % ("是" if sub else "否", "有" if bad else "无"))
    if miss or left or not sub or bad:
        fail("产物 id 层不符")
else:
    fail("缺产物 %s" % op)
log("")

# 5
log("[5] 现役包")
lp = os.path.join(ROOT, "ModuleData", "Worldbook", "packages", "calradia", "runtime.json")
d = json.load(io.open(lp, encoding="utf-8"))
log("   档数 = %d（应 482）" % len(d["entries"]))
if len(d["entries"]) != 482:
    fail("现役包档数变了")
log("")

log("===== 结论 =====")
if fails:
    log("!! %d 项不合格" % len(fails))
    for f in fails:
        log("   - " + f)
else:
    log("全部通过。")

io.open(REPORT, "w", encoding="utf-8", newline="\n").write("\n".join(lines) + "\n")
print("\n".join(lines))
raise SystemExit(1 if fails else 0)
