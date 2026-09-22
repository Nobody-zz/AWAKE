# -*- coding: utf-8 -*-
"""弄清「现役工作区」与「镜像档目录」到底差在哪，并基于**现役**重扫存疑标记。

背景：09-19 发现 482 档里 **322 档同名但字节不同**。镜像档看起来是旧投影。
判「谁新」的证据链要落在字段上，不能靠猜。
"""
import glob
import io
import os
from collections import Counter

import yaml

LIVE = r"tools\worldbook-studio\workspace\full-geo1\authoring"
MIRR = r"docs\worldbook-migration\projection\authoring-out"
SRC = r"tools\worldbook-studio\workspace\full-geo1\authoring\sources"

MARK = ["据传说", "据传", "据说", "听说", "名声", "宣传说法", "让人们相信", "人们相信",
        "没有任意两种版本", "口里的传言", "传说", "故事", "老辈"]
STRONG = {"据传说", "据传", "据说", "听说", "名声", "宣传说法", "让人们相信",
          "人们相信", "没有任意两种版本", "口里的传言"}


def load(p):
    return yaml.safe_load(io.open(p, encoding="utf-8"))


def sig(d):
    """一个档的「内容指纹」：只取会进包或影响判层的部分。"""
    return {
        "revision": d.get("revision"),
        "kind": [a.get("kind") for a in (d.get("assertions") or [])],
        "quotes": [s.get("quote") for a in (d.get("assertions") or []) for s in (a.get("sources") or [])],
        "expr_layers": [e.get("layer") for a in (d.get("assertions") or []) for e in (a.get("expressions") or [])],
        "expr_texts": [(e.get("text") or {}).get("zh-CN") for a in (d.get("assertions") or []) for e in (a.get("expressions") or [])],
        "entity_ids": d.get("entity_ids"),
        "subdomain": d.get("subdomain"),
        "title": (d.get("title") or {}).get("zh-CN"),
        "summary": (d.get("summary") or {}).get("zh-CN"),
    }


diffs = Counter()
changed = []
for f in sorted(os.listdir(LIVE)):
    if not f.endswith(".yaml"):
        continue
    a, b = load(os.path.join(LIVE, f)), load(os.path.join(MIRR, f))
    sa, sb = sig(a), sig(b)
    if sa == sb:
        continue
    changed.append(f)
    for k in sa:
        if sa[k] != sb[k]:
            diffs[k] += 1

print("==" * 3, "现役 vs 镜像：差异按字段统计")
print("  内容指纹不同的档:", len(changed))
for k, v in diffs.most_common():
    print("    %-12s %d 档不同" % (k, v))

# 逐字段举例
print()
print("==" * 3, "举例（每类取一档的现值 vs 镜像值）")
for k in diffs:
    for f in changed:
        a, b = sig(load(os.path.join(LIVE, f))), sig(load(os.path.join(MIRR, f)))
        if a[k] != b[k]:
            print("  [%s] %s" % (k, f))
            print("      现役:", str(a[k])[:150])
            print("      镜像:", str(b[k])[:150])
            break

# ---- 基于**现役**重扫存疑标记 ----
reg = {}
for f in glob.glob(os.path.join(SRC, "source-*.yaml")):
    d = load(f)
    r = d.get("locator_root")
    p = os.path.join(SRC, r) if r else None
    if p and os.path.exists(p):
        reg[d["source_id"]] = io.open(p, encoding="utf-8").read().splitlines()


def find_line(sid, locator, quote):
    L = reg.get(sid) or []
    if "#" in locator:
        key = locator.split("#", 1)[1].strip("/")
        parts = [x for x in key.split("/") if x]
        if parts and not (parts[0] == "Variants" or len(parts) > 1):
            for ln in L:
                if "=>" in ln and ln.split("=>", 1)[0].strip().split(".")[-1] == parts[0]:
                    return ln
    if quote:
        probe = quote.strip()[:24]
        for ln in L:
            if probe and probe in ln:
                return ln
    return None


rows = []
miss = 0
for f in sorted(os.listdir(LIVE)):
    if not f.endswith(".yaml"):
        continue
    d = load(os.path.join(LIVE, f))
    if d.get("subdomain") != "settlements":
        continue
    s = (d.get("sources") or [{}])[0]
    ln = find_line(str(s.get("source_id")), str(s.get("locator") or ""), str(s.get("quote") or ""))
    if ln is None:
        miss += 1
        continue
    m = sorted({x for x in MARK if x in ln})
    if m:
        rows.append((f, [x for x in m if x in STRONG], m))

print()
print("==" * 3, "基于**现役**重扫 settlements")
print("  定位不到原文行:", miss)
print("  带标记:", len(rows), "  其中强标记:", sum(1 for r in rows if r[1]))
for f, st, allm in rows:
    print("    %-32s 强=%-28s 全=%s" % (f, ",".join(st) or "-", ",".join(allm)))

# 与上一轮（基于镜像）的名单对比
prev = {"towns-amitatys.yaml", "towns-balgard.yaml", "towns-car-banseth.yaml", "towns-diathma.yaml",
        "towns-dunglanys.yaml", "towns-hubyar.yaml", "towns-husn-fulq.yaml", "towns-marunath.yaml",
        "towns-ocs-hall.yaml", "towns-onira.yaml", "towns-pen-cannoc.yaml", "towns-poros.yaml",
        "towns-qasira.yaml", "towns-razih.yaml", "towns-revyl.yaml", "towns-rhotae.yaml",
        "villages-diantogmail.yaml", "villages-enoisa.yaml", "villages-geunat-nal.yaml",
        "villages-horsger.yaml", "villages-lartusys.yaml"}
now = {r[0] for r in rows}
print()
print("  与基于镜像的名单差异：只在镜像有 =", sorted(prev - now), "；只在现役有 =", sorted(now - prev))
