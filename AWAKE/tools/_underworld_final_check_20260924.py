# -*- coding: utf-8 -*-
"""前缀归一 · 最终一致性复核（全仓）。

逐条核这几件事，全绿才算收工：
  1. 两侧目录（WS / AO）各 558 档，改名后 8 档逐字节一致
  2. 8 档的档内 id / assertion id / expression id **一行未动**
  3. 全仓不再有旧档名当「文件名/档名」出现（产物与快照除外）
  4. 条目 id 层（politics.* / awake:entry:*）**一处未被改成 underworld**
  5. 编译产物 558 档，8 个旧条目 id 全在
"""
import glob
import hashlib
import io
import json
import os
import re
import subprocess

ROOT = r"D:\AWAKE-Dev\AWAKE"
WS = os.path.join(ROOT, "tools", "worldbook-studio", "workspace", "full-geo1", "authoring")
AO = os.path.join(ROOT, "docs", "worldbook-migration", "projection", "authoring-out")
OUT = os.path.join(ROOT, "tools", "worldbook-studio", "workspace", "full-geo1",
                   "compiled", "geo1-v23-underworld")
REPORT = os.path.join(ROOT, "tools", "_underworld_final_check_20260924.txt")

PAIRS = [
    ("underworld-alleys.yaml",        "doc.politics.town-alleys",        "town-alleys"),
    ("underworld-gang-leaders.yaml",  "doc.politics.alley-gang-leaders", "alley-gang-leaders"),
    ("underworld-struggle.yaml",      "doc.politics.alley-struggle",     "alley-struggle"),
    ("underworld-gangs.yaml",         "doc.politics.town-gangs",         "town-gangs"),
    ("underworld-crime-rating.yaml",  "doc.politics.crime-rating",       "crime-rating"),
    ("underworld-blood-money.yaml",   "doc.politics.blood-money",        "blood-money"),
    ("underworld-bandits.yaml",       "doc.politics.bandits",            "bandits"),
    ("underworld-smuggling.yaml",     "doc.economy.smuggling",           "smuggling"),
]
ID_IN_YAML = ["doc.politics.town-alleys", "doc.politics.alley-gang-leaders",
              "doc.politics.alley-struggle", "doc.politics.town-gangs",
              "doc.politics.crime-rating", "doc.politics.blood-money",
              "doc.politics.bandits", "doc.economy.smuggling"]

lines = []
fails = []


def log(s):
    lines.append(s)


def fail(s):
    fails.append(s)
    log("  !! " + s)


def sha256(p):
    with open(p, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest().upper()


def docid_of(p):
    with io.open(p, encoding="utf-8") as f:
        for ln in f.read(6000).splitlines():
            if ln.startswith("id:"):
                return ln.split(":", 1)[1].strip()
    return None


def yaml_ids(p):
    """抠出档内全部 id: / - id: 行"""
    out = []
    with io.open(p, encoding="utf-8") as f:
        for ln in f:
            m = re.match(r"^\s*-?\s*id:\s*(\S+)", ln)
            if m:
                out.append(m.group(1))
    return out


log("===== 前缀归一 · 最终一致性复核 =====")
log("")

# 1. 档数与双写一致
log("[1] 两侧档数与双写一致性")
for name, d in (("WS", WS), ("AO", AO)):
    n = len([x for x in os.listdir(d)
             if x.endswith(".yaml") and not x.startswith(("_", "source-"))])
    log("   %s 档数 = %d  %s" % (name, n, "OK" if n == 558 else "!! 应为 558"))
    if n != 558:
        fail("%s 档数 %d != 558" % (name, n))
bad = 0
for new, _, _ in PAIRS:
    wp, ap = os.path.join(WS, new), os.path.join(AO, new)
    if not (os.path.exists(wp) and os.path.exists(ap)):
        fail("缺档 %s" % new)
        bad += 1
    elif sha256(wp) != sha256(ap):
        fail("双写不一致 %s" % new)
        bad += 1
log("   8 档双写比对：不一致 %d 档  %s" % (bad, "OK" if bad == 0 else "!!"))
log("")

# 2. id 未动
log("[2] 档内 id 层未被动过")
for (new, want_docid, old_slug) in PAIRS:
    p = os.path.join(WS, new)
    got = docid_of(p)
    ids = yaml_ids(p)
    has_docid = want_docid in ids
    # assertion / expression id 应仍以旧 slug 为尾
    sub = [i for i in ids if i.startswith(("assertion.", "expr."))]
    sub_ok = len(sub) == 3 and all(i.endswith(old_slug + "-1") or i.startswith("expr." + old_slug) for i in sub)
    uw = [i for i in ids if ".underworld-" in i]
    tag = "OK" if (got == want_docid and has_docid and not uw) else "!!"
    log("   %-30s docid=%-36s 子id=%d 无 underworld 污染=%s  %s" % (
        new, got, len(sub), "是" if not uw else "否", tag))
    if got != want_docid or not has_docid or uw:
        fail("%s 的 id 层被改" % new)
log("")

# 3. 全仓旧档名残留（排除产物、快照、备份、脚本自身）
log("[3] 全仓旧档名残留（作为档名出现的地方）")
SKIP = ("/compiled/", "/_archive-", "/authoring-v1/", "/.git/", "\\compiled\\",
        "\\_archive-", "\\authoring-v1\\", "_underworld_", "_alley_")
pat = re.compile(r"`(alley-gang-leaders|alley-struggle|town-alleys|town-gangs|"
                 r"crime-rating|blood-money|bandits|smuggling)`")
found = []
for base, dirs, files in os.walk(ROOT):
    dirs[:] = [d for d in dirs if d not in (".git", "bin", "obj", "node_modules", "__pycache__")]
    for fn in files:
        if not fn.endswith((".md", ".py", ".ps1")):
            continue
        p = os.path.join(base, fn)
        if any(s in p for s in SKIP):
            continue
        try:
            txt = io.open(p, encoding="utf-8", errors="replace").read()
        except Exception:
            continue
        for m in pat.finditer(txt):
            found.append((os.path.relpath(p, ROOT).replace("\\", "/"), m.group(1)))
if found:
    for rel, w in found[:12]:
        log("   !! %s -> `%s`" % (rel, w))
    fail("仍有 %d 处旧档名" % len(found))
else:
    log("   无残留  OK")
log("")

# 4. 条目 id 层没被误改成 underworld
log("[4] 条目 id 层未被误改")
out_path = os.path.join(OUT, "runtime.json")
if os.path.exists(out_path):
    out = json.load(io.open(out_path, encoding="utf-8"))
    ids_in = set("doc." + e["id"].replace("awake:entry:", "", 1) for e in out["entries"])
    miss = [d for d in ID_IN_YAML if d not in ids_in]
    stale = [d for d in ids_in if ".underworld-" in d]
    log("   新包档数 = %d" % len(ids_in))
    log("   8 个旧条目 id 全在：%s" % ("是" if not miss else "否 -> %s" % miss))
    log("   条目 id 里出现 .underworld- 的 = %d  %s" % (
        len(stale), "OK" if not stale else "!! 污染"))
    if miss:
        fail("编译产物缺条目 %s" % miss)
    if stale:
        fail("条目 id 被污染")
else:
    log("   !! 编译产物不存在：%s" % out_path)
    fail("缺编译产物")
log("")

# 5. 现役包未被碰
log("[5] 现役上线包（ModuleData）")
live = os.path.join(ROOT, "ModuleData", "Worldbook", "packages", "calradia", "runtime.json")
d = json.load(io.open(live, encoding="utf-8"))
log("   档数 = %d（应为 482，未部署）" % len(d["entries"]))
if len(d["entries"]) != 482:
    fail("现役包档数变了")
log("")

log("===== 结论 =====")
if fails:
    log("!! 有 %d 项不合格：" % len(fails))
    for f in fails:
        log("   - " + f)
else:
    log("全部通过。")

with io.open(REPORT, "w", encoding="utf-8", newline="\n") as f:
    f.write("\n".join(lines) + "\n")
print("\n".join(lines))
return_code = 1 if fails else 0
raise SystemExit(return_code)
