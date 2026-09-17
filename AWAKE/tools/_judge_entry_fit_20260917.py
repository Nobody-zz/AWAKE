# -*- coding: utf-8 -*-
"""判定「入口词 → 自己」自命中（真代码 probe 的输出，零替身判定）。

只做一件事：**这个入口词，能不能把提问带到它自己那一条？**
  · title       —— 条目名自问（最基础的可用性）
  · kw_zh       —— 中文入口词（玩家真可能说出口的那一类）
  · kw_ascii    —— 英文名／内部 id 分片（今天索引卫生 R2/R3 的对象）

输出：总表 → 按类 → 条目级最差名单 → 被打通到别处的去向。
"""
import collections
import io
import json
import sys

sys.stdout.reconfigure(encoding="utf-8")

SIDE = "tools/_entry_fit_side_20260917.json"
PKG = "ModuleData/Worldbook/packages/calradia/runtime.json"
ARM = sys.argv[1] if len(sys.argv) > 1 else "tools/_entry_fit_off_20260917.json"
LABEL = sys.argv[2] if len(sys.argv) > 2 else ARM

side = json.load(io.open(SIDE, encoding="utf-8"))["side"]
res = json.load(io.open(ARM, encoding="utf-8"))
rows = res["queries"] if isinstance(res, dict) and "queries" in res else res
pkg = json.load(io.open(PKG, encoding="utf-8"))
zhtitle = {}
for e in pkg["entries"]:
    t = e.get("title")
    zhtitle[e["id"].replace("awake:entry:", "")] = (t or {}).get("zh-CN") if isinstance(t, dict) else t

by_name = {}
for r in rows:
    by_name[r.get("name")] = r

# ---------- 逐问判定 ----------
# ⚠️ 口径：结果里的 hits 带 `awake:entry:` 前缀，side 里的 entry 是**去了前缀**的短名。
#    两边必须归一化再比 —— 第一版就是拿短名去比带前缀的 hits，报出"全 0 命中"的假象。
#    （恒 0 命中＝没验过；阳性对照＝title 必须能自命中。）
PREFIX = "awake:entry:"
per_q = []
for name, s in side.items():
    r = by_name.get(name)
    hits = [h.replace(PREFIX, "") if isinstance(h, str) else h for h in ((r or {}).get("hits") or [])]
    exp = s["entry"]
    per_q.append(dict(s, hits=hits, self_hit=(exp in hits),
                      state=(r or {}).get("state"),
                      hit_first=(hits[0] if hits else ""),
                      n_hits=len(hits), mode=(r or {}).get("match_mode")))

# 阳性对照：title 这一类若一个都不自命中，说明是判定/仪器坏了，不是产品坏了
_t = [x for x in per_q if x["kind"] == "title"]
_ok = sum(1 for x in _t if x["self_hit"])
print("=" * 78)
print("入口词 → 自己 · 自命中判定（%s）" % LABEL)
print("=" * 78)
print("\n[阳性对照] 条目名自命中 %d/%d —— " % (_ok, len(_t))
      + ("正常，继续读结论" if _ok else "**全 0 ⇒ 先怀疑判定脚本/仪器，别读下面的数**"))


def rate(rows_):
    if not rows_:
        return "n/a"
    ok = sum(1 for x in rows_ if x["self_hit"])
    return "%d/%d = %.1f%%" % (ok, len(rows_), 100.0 * ok / len(rows_))


print("\n【1】按类总表")
for k in ("title", "kw_zh", "kw_ascii"):
    sub = [x for x in per_q if x["kind"] == k]
    print("   %-9s %s" % (k, rate(sub)))

us = [x for x in per_q if x["kind"] == "kw_ascii" and "_" in x["text"]]
nous = [x for x in per_q if x["kind"] == "kw_ascii" and "_" not in x["text"]]
print("   ├ 含下划线 %s" % rate(us))
print("   └ 不含下划线 %s" % rate(nous))

# ---------- 条目级 ----------
ent = collections.defaultdict(lambda: dict(title=None, zh=[], zh_ok=0, ascii_n=0, ascii_ok=0, t_hit=None))
for x in per_q:
    e = ent[x["entry"]]
    if x["kind"] == "title":
        e["title"] = x["text"]
        e["t_hit"] = x["self_hit"]
    elif x["kind"] == "kw_zh":
        e["zh"].append((x["text"], x["self_hit"], x["hit_first"], x["n_hits"], x["mode"]))
    else:
        e["ascii_n"] += 1
        e["ascii_ok"] += 1 if x["self_hit"] else 0

print("\n【2】条目级")
no_title = [k for k, v in ent.items() if v["t_hit"] is False]
print("   用**条目名**问却打不中自己的：%d 条" % len(no_title))
for k in sorted(no_title)[:15]:
    v = ent[k]
    q = [x for x in per_q if x["entry"] == k and x["kind"] == "title"][0]
    print("     %-42s 「%s」 -> state=%s 命中 %d 条（首条 %s）"
          % (k[:42], v["title"], q["state"], q["n_hits"], q["hit_first"] or "—"))

zh_none = [k for k, v in ent.items() if v["zh"] and all(not z[1] for z in v["zh"])]
print("\n   中文入口词**一个都不自命中**的条目：%d 条" % len(zh_none))
for k in sorted(zh_none)[:15]:
    v = ent[k]
    print("     %-42s 入口：%s" % (k[:42], [z[0] for z in v["zh"]][:4]))

tot_zh = sum(len(v["zh"]) for v in ent.values())
tot_zh_ok = sum(v["zh_ok"] for v in ent.values())
tot_zh_ok = sum(1 for v in ent.values() for z in v["zh"] if z[1])
print("\n   全库中文入口词 %d 个，其中真能自命中的 %d 个（%.1f%%）"
      % (tot_zh, tot_zh_ok, 100.0 * tot_zh_ok / max(tot_zh, 1)))
per_entry = [(k, sum(1 for z in v["zh"] if z[1]), len(v["zh"])) for k, v in ent.items()]
per_entry.sort(key=lambda t: (t[1], t[2]))
print("   每条**有效中文入口词数**分布：")
dist = collections.Counter(t[1] for t in per_entry)
print("     " + "  ".join("%d个:%d条" % (k, dist[k]) for k in sorted(dist)))
print("   最薄的 12 条（有效中文入口 / 全部中文入口）：")
for k, ok, n in per_entry[:12]:
    print("     %-42s %d/%d  入口：%s" % (k[:42], ok, n, [z[0] for z in ent[k]["zh"]][:4]))

# ---------- 3 打不通时去了哪 ----------
print("\n【3】自命中失败时，提问被带到了哪一条（top 去向）")
miss = [x for x in per_q if not x["self_hit"] and x["hit_first"]]
dest = collections.Counter(x["hit_first"] for x in miss)
for k, v in dest.most_common(12):
    print("     %-42s 抢走 %d 次" % (k[:42], v))
print("   其中**一个都没命中**（空手而归）的：%d 次" % sum(1 for x in per_q if not x["self_hit"] and not x["hits"]))

out = dict(per_q=per_q)
with io.open(ARM.replace(".json", "_judge.json"), "w", encoding="utf-8") as fh:
    json.dump(out, fh, ensure_ascii=False, indent=1)
print("\n明细：%s" % ARM.replace(".json", "_judge.json"))
