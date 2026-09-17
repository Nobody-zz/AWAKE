# -*- coding: utf-8 -*-
"""识别链路 × 知识条目 契合度 · 数据侧普查（只读上线包，零改写）

想问清三件事（都从**条目侧**看，不看样本）：
  A. 每条条目的「入口面」有多少、是什么构成的（专名？通用词？内部词？）
  B. 入口词与条目自己正文的**关系** —— 是"正文里真出现过的词"，还是外挂别名？
  C. 正文里**藏着别家条目的入口词**吗 —— 玩家读了这条的内容、用里面的词去问，
     链路会把他带到哪一条去？（这是"契合缝隙"的直接形状）

读的真件：`ModuleData/Worldbook/packages/calradia/runtime.json`（440+ 条，已上线）
  ⚠️ 运行时条目**没有 aliases 字段**；别名是编译期折进 `keywords` 的（实例见下）。
"""
import collections
import io
import json
import re
import statistics
import sys

sys.stdout.reconfigure(encoding="utf-8")

PKG = "ModuleData/Worldbook/packages/calradia/runtime.json"
d = json.load(io.open(PKG, encoding="utf-8"))
es = d["entries"]


def zh(v):
    if isinstance(v, dict):
        return v.get("zh-CN") or ""
    return v or ""


def body_text(e):
    """该条目的全部正文（summary + 各表达 text），用于查入口词在不在正文里。"""
    parts = [zh(e.get("summary"))]
    for ex in e.get("expressions") or []:
        parts.append(zh(ex.get("text")))
    return "\n".join(p for p in parts if p)


# ---------- 全库关键词 → 覆盖到哪些条目（用于判"专有入口"还是"泛词"） ----------
kw_owner = collections.defaultdict(set)
for e in es:
    for k in e.get("keywords") or []:
        kw_owner[k].add(e["id"])

ASCII_ONLY = re.compile(r"^[\x20-\x7e]+$")
HAS_US = re.compile(r"_")

# ---------- 逐条 ----------
rows = []
for e in es:
    kws = e.get("keywords") or []
    body = body_text(e)
    eid = e["id"]
    zh_kw = [k for k in kws if not ASCII_ONLY.match(k)]
    ascii_kw = [k for k in kws if ASCII_ONLY.match(k)]
    us_kw = [k for k in kws if HAS_US.search(k)]
    # 入口词是否出现在自己的正文里（只对中文词判；ASCII 内部词不判）
    in_body = [k for k in zh_kw if k and k in body]
    not_in_body = [k for k in zh_kw if k and k not in body]
    # 这条入口词覆盖了多少条目（越少越"专"）
    cov = [len(kw_owner[k]) for k in kws if k]
    # 正文里出现的**别家**入口词（只取中文、长度>=2，避免噪声）
    others = set()
    for k, owners in kw_owner.items():
        if len(k) < 2 or ASCII_ONLY.match(k):
            continue
        if eid in owners:
            continue
        if k in body:
            others.add(k)
    rows.append(dict(
        id=eid.replace("awake:entry:", ""), domain=e.get("domain"),
        title=zh(e.get("title")), n_kw=len(kws), kws=kws,
        zh_kw=len(zh_kw), ascii_kw=len(ascii_kw), us_kw=len(us_kw),
        body_len=len(body), in_body=in_body, not_in_body=not_in_body,
        cov_min=min(cov) if cov else 0, cov_max=max(cov) if cov else 0,
        other_kw=sorted(others),
    ))

print("=" * 78)
print("识别链路 × 知识条目 · 入口面普查（真件 %s，%d 条）" % (PKG, len(rows)))
print("=" * 78)

# ---------- A. 入口面宽度 ----------
print("\n【A】入口面宽度（每条 keywords 数）")
n = [r["n_kw"] for r in rows]
print("   min %d ／ 中位 %.0f ／ 均值 %.1f ／ max %d ／ 合计 %d 个入口词"
      % (min(n), statistics.median(n), sum(n) / len(n), max(n), sum(n)))
hist = collections.Counter(n)
print("   分布：" + "  ".join("%d个:%d条" % (k, hist[k]) for k in sorted(hist)))
print("   子项：中文词合计 %d ／ ASCII 词合计 %d ／ 含下划线 %d"
      % (sum(r["zh_kw"] for r in rows), sum(r["ascii_kw"] for r in rows),
         sum(r["us_kw"] for r in rows)))
print("   ⇒ 平均每条只有 %.1f 个中文入口词" % (sum(r["zh_kw"] for r in rows) / len(rows)))

# ---------- B. 入口词 vs 自己的正文 ----------
print("\n【B】入口词是不是「正文里真有的词」")
tot_kw = sum(len(r["in_body"]) + len(r["not_in_body"]) for r in rows)
tot_not = sum(len(r["not_in_body"]) for r in rows)
print("   中文入口词 %d 个，其中出现在本条目正文里的 %d 个（%.0f%%），不在正文里的 %d 个"
      % (tot_kw, tot_kw - tot_not, 100.0 * (tot_kw - tot_not) / max(tot_kw, 1), tot_not))
bad = [r for r in rows if r["not_in_body"]]
print("   有「正文里没出现过的入口词」的条目：%d / %d" % (len(bad), len(rows)))
for r in sorted(bad, key=lambda x: -len(x["not_in_body"]))[:12]:
    print("     %-40s 不在正文：%s" % (r["id"][:40], r["not_in_body"][:4]))

# ---------- C. 正文里藏着别家入口词 ----------
print("\n【C】正文里出现**别条目的入口词** ⇒ 玩家用那些词问，会被引到别处")
withother = [r for r in rows if r["other_kw"]]
print("   命中条目：%d / %d（%.0f%%）" % (len(withother), len(rows),
                                     100.0 * len(withother) / len(rows)))
cnt = collections.Counter()
for r in rows:
    for k in r["other_kw"]:
        for o in kw_owner[k]:
            if o != "awake:entry:" + r["id"]:
                cnt[o.replace("awake:entry:", "")] += 1
print("   ⇒ 被别人的正文「点名」最多的条目（这些条目会抢走别人的提问）：")
for k, v in cnt.most_common(12):
    print("     %-40s 被 %d 条别的条目正文提到" % (k[:40], v))

# ---------- D. 入口词的"专有度" ----------
print("\n【D】入口词有多专：一个词覆盖多少条目")
covs = sorted(l for r in rows for l in [r["cov_min"]])
allcov = collections.Counter()
for k, owners in kw_owner.items():
    allcov[len(set(owners))] += 1
b = collections.Counter()
for c, num in allcov.items():
    b["1" if c == 1 else "2" if c == 2 else "3-5" if c <= 5 else "6-20" if c <= 20 else ">20"] += num
print("   独立入口词种数 %d；覆盖分布：%s"
      % (len(kw_owner), "  ".join("%s条:%d个" % (k, b[k]) for k in ["1", "2", "3-5", "6-20", ">20"] if b[k])))
only_one = sum(1 for k, o in kw_owner.items() if len(set(o)) == 1)
print("   ⇒ 只覆盖 1 条条目的「专属词」%d 个（占 %.0f%%）—— 这些词**只有知道名字的人说得出**"
      % (only_one, 100.0 * only_one / len(kw_owner)))

# ---------- E. 正文里"没有入口的词"规模（描述性内容 vs 入口） ----------
print("\n【E】描述性正文的规模（正文长度）—— 玩家的第一手材料在这，但入口不在这")
bl = [r["body_len"] for r in rows]
print("   正文长度：min %d ／ 中位 %.0f ／ max %d 字" % (min(bl), statistics.median(bl), max(bl)))

# ---------- 汇总导出 ----------
out = dict(entries=rows, kw_owner={k: sorted(set(v)) for k, v in kw_owner.items()})
with io.open("tools/_probe_entry_fit_data_20260917.json", "w", encoding="utf-8") as fh:
    json.dump(out, fh, ensure_ascii=False, indent=1)
print("\n明细导出：tools/_probe_entry_fit_data_20260917.json")
