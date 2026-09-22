# -*- coding: utf-8 -*-
"""把三组手写判层套到 21 档聚落上：改断言可信度分层 ＋ 补对应口吻表达。

做法（不动别的字段一个字节）：
  读**现役**档（`workspace/full-geo1/authoring/`，权威）→ 只替换 `assertions:` 段 →
  写回现役 ＋ 同步镜像（`projection/authoring-out/`）→ revision +1。
  ⇒ 头部（registry hash／sources／entity_ids…）**整段照抄现役原文**，不经过 dump，避免格式漂移。

硬断言（任一条不过即中止，不留半成品）：
  ① 每个 quote 必须能在来源文件里逐字定位（连续子串）；
  ② 引用的 grants 名单必须能从该档原有表达里按 layer 复制到；
  ③ 新文本里不得出现 YAML 锚点 `&id`；不得出现 ASCII 直引号（会截断 YAML）。
"""
import glob
import hashlib
import io
import json
import os
import shutil
import sys
import time

import yaml

REPO = r"D:\AWAKE-Dev\AWAKE"
LIVE = os.path.join(REPO, r"tools\worldbook-studio\workspace\full-geo1\authoring")
MIRR = os.path.join(REPO, r"docs\worldbook-migration\projection\authoring-out")
SRC = os.path.join(LIVE, "sources")
L2DIR = MIRR
STAMP = time.strftime("%Y%m%d-%H%M%S")
BACKUP = os.path.join(REPO, r"artifacts\status-trust-backup-" + STAMP)

APPLY = "--apply" in sys.argv

# ---- 来源文件表 ----
REG = {}
for f in glob.glob(os.path.join(SRC, "source-*.yaml")):
    d = yaml.safe_load(io.open(f, encoding="utf-8"))
    r = d.get("locator_root")
    p = os.path.join(SRC, r) if r else None
    if p and os.path.exists(p):
        REG[d["source_id"]] = io.open(p, encoding="utf-8").read()


def qh(text):
    return hashlib.sha256(text.encode("utf-8")).hexdigest().upper()


class NoAlias(yaml.dumper.Dumper):
    def ignore_aliases(self, data):
        return True


# ---- 读三组手写判层 ----
JUDGE = {}
for n in (1, 2, 3):
    p = os.path.join(L2DIR, "_status_l2_towns_%d_20260919.json" % n)
    JUDGE.update(json.load(io.open(p, encoding="utf-8")))

docs = sorted(k for k in JUDGE if not k.startswith("_"))
print("判层档数:", len(docs))

if APPLY:
    os.makedirs(os.path.join(BACKUP, "live"))
    os.makedirs(os.path.join(BACKUP, "mirror"))
    print("备份根:", BACKUP)

rows = []
problems = []
for fname in docs:
    spec = JUDGE[fname]
    live_p = os.path.join(LIVE, fname)
    mirr_p = os.path.join(MIRR, fname)
    raw = io.open(live_p, encoding="utf-8", newline="").read()
    doc = yaml.safe_load(raw)

    # grants 模板：按 layer 从该档原有表达复制
    tpl = {}
    for a in doc["assertions"]:
        for e in a.get("expressions") or []:
            tpl.setdefault(str(e.get("layer")), e.get("grants") or [])
    if "rumor" not in tpl or "detail" not in tpl:
        problems.append((fname, "缺 grants 模板: " + str(sorted(tpl))))
        continue
    TPL = {"low": tpl["rumor"], "high": tpl["detail"]}

    s0 = doc["sources"][0]
    sid, locator = s0["source_id"], s0["locator"]
    sver, shash = s0["source_version"], s0["source_content_hash"]
    body = REG[sid]

    asserts = []
    for a in spec["asserts"]:
        srcs = []
        for q in a["quotes"]:
            if q not in body:
                problems.append((fname, "quote 定位不到: " + q[:40]))
            srcs.append({"source_id": sid, "source_version": sver,
                         "source_content_hash": shash, "locator": locator,
                         "quote_hash": qh(q), "quote": q})
        exprs = []
        for e in a["exprs"]:
            exprs.append({"id": e["id"], "revision": 1, "layer": e["layer"],
                          "text": {"zh-CN": e["text"]},
                          "sources": [dict(srcs[0])],
                          "grants": [dict(g) for g in TPL[e["grants"]]],
                          "denies": []})
        asserts.append({"id": a["id"], "revision": 1, "kind": a["kind"],
                        "text": {"zh-CN": a["text"]}, "sources": srcs,
                        "expressions": exprs})

    # 只替换 assertions 段：头部照抄原文
    marker = "\nassertions:\n"
    idx = raw.find(marker)
    if idx < 0:
        problems.append((fname, "找不到 assertions 段"))
        continue
    head = raw[:idx + 1]
    head = head.replace("revision: %d\n" % doc["revision"], "revision: %d\n" % (doc["revision"] + 1), 1)
    block = yaml.dump({"assertions": asserts}, Dumper=NoAlias, allow_unicode=True,
                      default_flow_style=False, sort_keys=False, width=100000, indent=2)
    new = head + block
    assert "&id" not in new
    assert '"' not in "".join([a["text"] for a in spec["asserts"]]
                              + [e["text"] for a in spec["asserts"] for e in a["exprs"]]), \
        fname + ": 新文本里出现 ASCII 直引号"

    chk = yaml.safe_load(new)
    assert chk["revision"] == doc["revision"] + 1
    for k in doc:
        if k in ("assertions", "revision"):
            continue
        assert chk[k] == doc[k], (fname, k)
    assert [a["kind"] for a in chk["assertions"]] == [a["kind"] for a in spec["asserts"]]

    if APPLY:
        shutil.copy(live_p, os.path.join(BACKUP, "live", fname))
        shutil.copy(mirr_p, os.path.join(BACKUP, "mirror", fname))
        io.open(live_p, "w", encoding="utf-8", newline="").write(new)
        io.open(mirr_p, "w", encoding="utf-8", newline="").write(new)

    kinds = [a["kind"] for a in spec["asserts"]]
    rows.append((fname, doc["revision"], doc["revision"] + 1,
                 len(doc["assertions"]), len(asserts),
                 sum(len(a["expressions"]) for a in asserts),
                 ",".join(kinds)))

print()
print("%-30s %-7s %-9s %-11s %s" % ("档", "rev", "断言 旧→新", "表达数", "分层"))
for f, r0, r1, a0, a1, ne, kinds in rows:
    print("%-30s %d→%-5d %d→%-8d %-11d %s" % (f, r0, r1, a0, a1, ne, kinds))
print()
print("合计:", len(rows), "档;  断言", sum(r[3] for r in rows), "→", sum(r[4] for r in rows),
      ";  表达", sum(r[5] for r in rows))
if problems:
    print()
    print("！！问题", len(problems))
    for f, msg in problems:
        print("   ", f, msg)
print()
print("模式:", "已写入" if APPLY else "预演（加 --apply 才落盘）")
