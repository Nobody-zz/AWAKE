# -*- coding: utf-8 -*-
"""挖「谁不该知道什么」的缺口：为世界书线的补料单取证。

要回答三个问题，全部从上线包本身取数（不猜）：
  ① 现在内容侧**到底写了多少条**「硬拒绝」（denies）？长什么样？
  ② 有多少条是**人人可得**（grant 里有 public 或 commoner）？
  ③ 其中哪些，**按内容本身就该分层**（标题/正文里写着"秘密、内情、私账、图谋"这类东西）？
     —— 这一步只给候选，判不给机器下，交给世界书线定。

用法：python _probe_secrecy_gap_20260918.py
"""
import io
import json
import os
import re
import sys
from collections import Counter

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

PACKAGE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..",
                       "ModuleData", "Worldbook", "packages", "calradia", "runtime.json")

# 「这条内容本身就该分层」的线索词。只用来**筛候选**，不作为判定。
SECRECY_HINTS = [
    "秘密", "密档", "内情", "私事", "私账", "账目", "图谋", "勾结", "私通",
    "叛", "密议", "暗中", "私下", "不传", "只有", "无人知", "隐情", "把柄",
    "密约", "内应", "密信", "身世", "来历不明", "不能提", "忌讳",
]

with open(PACKAGE, encoding="utf-8") as fh:
    package = json.load(fh)

entries = package.get("entries") or package.get("documents") or []
print("包：%s/%s ｜ 条目 %d" % (package.get("packageId"), package.get("version"), len(entries)))
print("顶层键：" + ", ".join(sorted(package.keys())[:14]))
print("=" * 84)

# --- 先看一条 entry 的形状（只打印键，不打印正文）---
if entries:
    sample = entries[0]
    print("entry 键：" + ", ".join(sorted(sample.keys())))
    exprs = sample.get("expressions") or []
    if exprs:
        print("expression 键：" + ", ".join(sorted(exprs[0].keys())))
        grants = exprs[0].get("grants") or []
        if grants:
            print("grant 键：" + ", ".join(sorted(grants[0].keys())))
            print("grant 样例：" + json.dumps(grants[0], ensure_ascii=False))
        print("denies 字段存在？%s" % ("denies" in exprs[0]))
    print("-" * 84)

# --- ① 全部 denies ---
denies_total = 0
deny_rows = []
for entry in entries:
    for expr in entry.get("expressions") or []:
        rows = expr.get("denies") or []
        for rule in rows:
            denies_total += 1
            deny_rows.append((entry.get("id"), entry.get("title"), expr.get("id"), rule))

print("① 硬拒绝（denies）合计 = %d 条" % denies_total)
for entry_id, title, expr_id, rule in deny_rows:
    print("   %s ｜ %s" % (entry_id, title))
    print("       expression=%s" % expr_id)
    print("       rule=%s" % json.dumps(rule, ensure_ascii=False))
print("-" * 84)

# --- ② 覆盖面统计 ---
def grant_identities(entry):
    found = set()
    for expr in entry.get("expressions") or []:
        if expr.get("enabled") is False:
            continue
        for rule in expr.get("grants") or []:
            ident = str(rule.get("identity_id") or rule.get("identityId") or "")
            if ident:
                found.add(ident.split(":")[-1])
    return found

public_entries = []
all_identities = Counter()
for entry in entries:
    ids = grant_identities(entry)
    for ident in ids:
        all_identities[ident] += 1
    if "public" in ids:
        public_entries.append(entry)

print("② 身份覆盖（有多少条目给了这个身份）：")
for name, count in all_identities.most_common():
    print("   %-18s %d" % (name, count))
print("   带 public（人人可得）的条目 = %d / %d" % (len(public_entries), len(entries)))
print("-" * 84)

# --- ③ 结构性判据：这个条目在身份上到底有没有分层 ---
# 设计的本意是「一个条目挂多条说法，低档给底层、高档给上层」。
# 若一个条目的所有说法都落在同一个档位，那无论谁来问都是同一个答案 —— 这个条目在身份上**没有分层**。
detail_counts = Counter()
grant_min_detail = Counter()
single_band = []
multi_band = []

for entry in entries:
    bands = set()
    for expr in entry.get("expressions") or []:
        if expr.get("enabled") is False:
            continue
        band = str(expr.get("detail") or "")
        bands.add(band)
        detail_counts[band or "(empty)"] += 1
        for rule in expr.get("grants") or []:
            grant_min_detail[str(rule.get("min_detail") or "(empty)")] += 1
    if not bands:
        continue
    if len(bands) == 1:
        single_band.append((entry, bands.pop()))
    else:
        multi_band.append((entry, sorted(bands)))

print("说法档位（expression.detail）分布：")
for name, count in detail_counts.most_common():
    print("   %-12s %d" % (name, count))
print("授权门槛（grant.min_detail）分布：")
for name, count in grant_min_detail.most_common():
    print("   %-12s %d" % (name, count))
print("条目分层结构：**单档条目 %d**（无论谁来问都同一档）｜ 多档条目 %d"
      % (len(single_band), len(multi_band)))
print("-" * 84)

# 单档且是高细档、又发给了底层身份 —— 这才是"该拆没拆"的精确候选
LOW_IDENTITIES = {"commoner", "villager", "townsfolk", "soldier"}
HIGH_BANDS = {"detail", "secret"}
precise = []
for entry, band in single_band:
    if band not in HIGH_BANDS:
        continue
    ids = grant_identities(entry)
    if not (ids & LOW_IDENTITIES):
        continue
    precise.append((entry, band, sorted(ids)))

print("③ 精确候选：**只有一条说法、档位是 %s、却发给了底层身份** = %d 条"
      % ("/".join(sorted(HIGH_BANDS)), len(precise)))
for entry, band, ids in precise[:30]:
    title = entry.get("title")
    zh = title.get("zh-CN") if isinstance(title, dict) else title
    print("   %-46s %s  [%s]" % (entry.get("id"), zh, band))
    print("       现给：%s" % ",".join(ids))
if len(precise) > 30:
    print("   …（还有 %d 条）" % (len(precise) - 30))
print("-" * 84)

# --- ④ 纵深：上半段（细/密）到底有多薄 ---
print("④ 那 6 条「单档条目」（只有一种说法 ⇒ 谁问都是同一档）：")
for entry, band in single_band:
    title = entry.get("title")
    zh = title.get("zh-CN") if isinstance(title, dict) else title
    print("   %-46s %s  [%s]  现给：%s" % (entry.get("id"), zh, band, ",".join(sorted(grant_identities(entry)))))

secret_exprs = []
for entry in entries:
    for expr in entry.get("expressions") or []:
        if str(expr.get("detail") or "") == "secret":
            secret_exprs.append((entry, expr))
print("全包 detail=secret 的说法 = %d 条" % len(secret_exprs))
for entry, expr in secret_exprs:
    title = entry.get("title")
    zh = title.get("zh-CN") if isinstance(title, dict) else title
    print("   %s ｜ %s" % (entry.get("id"), zh))
    print("       说法 id=%s" % expr.get("id"))
    print("       授权=%s" % ",".join(json.dumps(r, ensure_ascii=False) for r in (expr.get("grants") or [])))
print("-" * 84)

# --- ⑤ 有多少条目对某身份**一条说法都不给**（＝真正的身份门槛）---
# ⚠️ 修订 09-18：**必须算身份继承**。人物同时拥有全部祖先身份
# （ransom_broker → merchant → townsfolk → commoner；noble → notable → commoner），
# 只按"直接写他名字"算会**严重低估覆盖**。
TREE = {}
for item in package.get("identities") or []:
    if isinstance(item, dict) and item.get("id"):
        TREE[str(item["id"])] = [str(p) for p in (item.get("parents") or [])]


def ancestors_of(ident, seen=None):
    seen = seen if seen is not None else set()
    if ident in seen:
        return set()
    seen.add(ident)
    out = {ident}
    for parent in TREE.get(ident, []):
        out |= ancestors_of(parent, seen)
    return out


def reachable(entry, identity, use_inheritance=True):
    reach = ancestors_of("awake:identity:" + identity) if use_inheritance else {"awake:identity:" + identity}
    for expr in entry.get("expressions") or []:
        if expr.get("enabled") is False:
            continue
        for rule in expr.get("grants") or []:
            if str(rule.get("identity_id") or "") in reach:
                return True
    return False


print("⑤ 对某身份「一条说法都不给」的条目数（含继承 ｜ [仅本层] 对照）：")
for identity in ["commoner", "villager", "townsfolk", "soldier", "merchant",
                 "tavernkeeper", "notable", "ransom_broker", "noble", "anonymous"]:
    blocked = sum(1 for entry in entries if not reachable(entry, identity))
    blocked_own = sum(1 for entry in entries if not reachable(entry, identity, use_inheritance=False))
    print("   %-16s 查不到 %3d / %d  (%4.1f%%)   [仅本层 %3d]"
          % (identity, blocked, len(entries), 100.0 * blocked / max(len(entries), 1), blocked_own))
print("-" * 84)

# --- ⑥ 唯一那条写了硬拒绝的条目，完整看一遍（当样板）---
for entry in entries:
    if entry.get("id") != "awake:entry:politics.clans-charas-cortain-secret":
        continue
    print("⑥ 全包唯一写了 denies 的条目（当样板）：")
    print(json.dumps(entry, ensure_ascii=False, indent=1)[:2600])
print("-" * 84)

# --- ⑦ 按 domain 汇总：哪些类别最薄，优先补哪里 ---
by_domain = {}
for entry in entries:
    domain = str(entry.get("domain") or "(none)")
    stats = by_domain.setdefault(domain, {"entries": 0, "secret": 0, "deny": 0,
                                          "single_band": 0, "highest": Counter()})
    stats["entries"] += 1
    bands = []
    for expr in entry.get("expressions") or []:
        if expr.get("enabled") is False:
            continue
        band = str(expr.get("detail") or "(empty)")
        bands.append(band)
        stats["highest"][band] += 1
        if band == "secret":
            stats["secret"] += 1
        stats["deny"] += len(expr.get("denies") or [])
    if len(set(bands)) == 1:
        stats["single_band"] += 1

print("⑦ 按 domain 汇总（条目 / 有 secret 档的说法 / denies / 单档条目）：")
for domain, s in sorted(by_domain.items(), key=lambda kv: -kv[1]["entries"]):
    print("   %-22s 条目 %3d ｜ secret %d ｜ denies %d ｜ 单档 %d"
          % (domain, s["entries"], s["secret"], s["deny"], s["single_band"]))
print("-" * 84)

# ⑧ 对 commoner **一条说法都不给**的条目（＝平民完全查不到的 30 条，最可能该扩展的方向）
print("⑧ 平民（commoner）完全查不到的 30 条（今天仅有的「完全挡」，也是唯一在跑的样本）：")
for entry in entries:
    if reachable(entry, "commoner"):
        continue
    title = entry.get("title")
    zh = title.get("zh-CN") if isinstance(title, dict) else title
    print("   %-48s %s" % (entry.get("id"), zh))
print("-" * 84)

# --- ⑨ 关键词筛（粗，仅供人工扫） ---
def text_of(entry):
    parts = [str(entry.get("title") or "")]
    for expr in entry.get("expressions") or []:
        parts.append(str(expr.get("text") or expr.get("summary") or ""))
        for value in (expr.get("metadata") or {}).values() if isinstance(expr.get("metadata"), dict) else []:
            parts.append(str(value))
    parts.append(str(entry.get("keywords") or ""))
    return " ".join(parts)

candidates = []
for entry in entries:
    ids = grant_identities(entry)
    # 只挑「广覆盖」的：给了 public，或者给了 commoner/villager/townsfolk 这类底层的
    broad = ids & {"public", "commoner", "villager", "townsfolk", "soldier"}
    if not broad:
        continue
    text = text_of(entry)
    hits = [word for word in SECRECY_HINTS if word in text]
    if not hits:
        continue
    candidates.append((entry.get("id"), entry.get("title"), sorted(ids), hits))

print("③ 「现在人人可得、但内容看着该分层」的候选 = %d 条" % len(candidates))
for entry_id, title, ids, hits in candidates[:40]:
    print("   %-46s %s" % (entry_id, title))
    print("       现给：%s" % ",".join(ids))
    print("       线索：%s" % "、".join(hits))
if len(candidates) > 40:
    print("   …（还有 %d 条，见落盘文件）" % (len(candidates) - 40))
