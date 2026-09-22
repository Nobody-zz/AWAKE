# -*- coding: utf-8 -*-
"""军事批 · 撞车处置（2026-09-20）。

按 MILITARY-BATCH-SPEC-20260920.md §二：
  military-vlandia 撤 alias「方旗骑士」        （留给 doc.war.troops-banner-knight）
  military-khuzait 撤 alias「怯薛」「可汗卫士」 （留给 doc.war.troops-khans-guard）
判据：这两条不是本档的别称，是本档正文里提到的另一个事物的名字；本尊档一出现，挂名让位。

工序：先备份（现役 + 镜像）→ 行级删 alias → revision+1 → 写前自检 → 双写 → 复验。
"""
import io
import os
import shutil

import yaml

WS = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring"
AO = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out"
BU = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1/_backup_20260920-military-collision"

TASKS = [
    ("military-vlandia.yaml", ["方旗骑士"]),
    ("military-khuzait.yaml", ["怯薛", "可汗卫士"]),
]


def strip_alias(lines, drop):
    """行级删除 aliases 段里的指定条目；段删空则删段头。返回 (新行表, 删了几条)。"""
    out, removed = [], 0
    lang_stack = []          # 当前所处的语言段缩进（如 zh-CN 行）
    for ln in lines:
        s = ln.strip()
        # 记录语言段（形如 '  zh-CN:' / '  en:'）
        if s.endswith(":") and ln.startswith("  ") and not ln.startswith("  -") and not ln.startswith("    "):
            lang_stack = [(len(ln) - len(ln.lstrip()), s[:-1])]
        # 删除命中条目
        if s.startswith("- ") and s[2:].strip() in drop:
            removed += 1
            continue
        out.append(ln)
    return out, removed


def prune_empty_alias_block(text):
    """若某语言段被删空，删该段头；若两段全空，删整个 aliases 键。"""
    lines = text.split("\n")
    out = []
    i = 0
    while i < len(lines):
        ln = lines[i]
        if ln.rstrip() == "aliases:":
            j = i + 1
            block = []
            while j < len(lines) and (lines[j].startswith("  ") or lines[j].strip() == ""):
                if lines[j].strip() == "":
                    break
                block.append(lines[j])
                j += 1
            keep, k = [], 0
            while k < len(block):
                cur = block[k]
                if cur.startswith("  ") and not cur.startswith("  -") and cur.strip().endswith(":"):
                    m = k + 1
                    items = []
                    while m < len(block) and block[m].startswith("  -"):
                        items.append(block[m])
                        m += 1
                    if items:
                        keep.append(cur)
                        keep.extend(items)
                    k = m
                else:
                    keep.append(cur)
                    k += 1
            if keep:
                out.append(ln)
                out.extend(keep)
            i = j
            continue
        out.append(ln)
        i += 1
    return "\n".join(out)


def bump_revision(text):
    lines = text.split("\n")
    for idx, ln in enumerate(lines):
        if ln.startswith("revision: "):
            n = int(ln.split(":", 1)[1].strip())
            lines[idx] = "revision: %d" % (n + 1)
            return "\n".join(lines), n + 1
    raise SystemExit("no revision line")


os.makedirs(BU, exist_ok=True)

for fn, drop in TASKS:
    src = os.path.join(WS, fn)
    text = io.open(src, encoding="utf-8").read()
    before = yaml.safe_load(text)
    b_aliases = (before.get("aliases") or {}).get("zh-CN") or []

    for d in (WS, AO):
        shutil.copy2(os.path.join(d, fn), os.path.join(BU, fn.replace(".yaml", ".before.yaml")))

    lines = text.split("\n")
    new_lines, removed = strip_alias(lines, drop)
    assert removed == len(drop), "%s: 删了 %d 条，期望 %d" % (fn, removed, len(drop))
    text2 = "\n".join(new_lines)
    text2 = prune_empty_alias_block(text2)
    text2, newrev = bump_revision(text2)

    chk = yaml.safe_load(text2)
    a2 = (chk.get("aliases") or {}).get("zh-CN") or []
    assert a2, "%s: zh-CN 段被删空（schema minProperties=1）" % fn
    assert not (set(drop) & set(a2)), "%s: 命中词仍在" % fn
    assert chk["revision"] == newrev and chk["id"] == before["id"]

    for d in (WS, AO):
        io.open(os.path.join(d, fn), "w", encoding="utf-8", newline="\n").write(text2)

    print("%-24s rev %d->%d  zh-CN %s -> %s" % (fn, before["revision"], newrev, b_aliases, a2))

# 双写复验：逐字节
print("\n--- 双写复验（逐字节）---")
bad = 0
for fn, _ in TASKS:
    b1 = io.open(os.path.join(WS, fn), "rb").read()
    b2 = io.open(os.path.join(AO, fn), "rb").read()
    ok = b1 == b2
    bad += 0 if ok else 1
    print("%-24s %s" % (fn, "IDENTICAL" if ok else "DIFF"))
print("不一致档数:", bad, "| 备份:", BU)
