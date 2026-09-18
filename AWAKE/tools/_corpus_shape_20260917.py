# -*- coding: utf-8 -*-
"""看世界书语料真实长什么样（v2：title/summary 是 {en, zh-CN} 字典）。"""
import json, statistics, collections

SRC = r"D:\AWAKE-Dev\AWAKE\ModuleData\Worldbook\packages\calradia\runtime.json"
d = json.load(open(SRC, encoding="utf-8"))
entries = d.get("entries") or []
print("entries=%d  identities=%d  referrals=%d  contentTier=%s"
      % (len(entries), len(d.get("identities") or []), len(d.get("referrals") or []),
         (d.get("extensions") or {}).get("contentTier")))


def pick(v, lang):
    """从 {en, zh-CN} 里取值；已经是字符串就直接返回。"""
    if isinstance(v, dict):
        return v.get(lang) or v.get("zh-CN") or v.get("en") or ""
    return v or ""


def langs(v):
    return sorted(v.keys()) if isinstance(v, dict) else ["<str>"]


# 各字段的语言键分布
for f in ("title", "summary"):
    c = collections.Counter()
    for e in entries:
        c[tuple(langs(e.get(f)))] += 1
    print("%-9s 语言键组合：%s" % (f, dict(c)))

ec = collections.Counter()
etext_langs = collections.Counter()
for e in entries:
    for x in (e.get("expressions") or []):
        if isinstance(x, dict):
            ec[tuple(sorted(x.keys()))] += 1
            etext_langs[tuple(langs(x.get("text")))] += 1
print("expression 键组合：%s" % dict(ec))
print("expression.text 语言键：%s" % dict(etext_langs))

domain = collections.Counter(e.get("domain") for e in entries)
print("\ndomain 分布：%s" % dict(domain))

extkeys = collections.Counter()
for e in entries:
    for k in ((e.get("extensions") or {}).keys()):
        extkeys[k] += 1
print("extensions 子键（top 20）：%s" % dict(extkeys.most_common(20)))


def stat(name, xs):
    xs = [x for x in xs if x is not None]
    if not xs:
        print("   %-26s (empty)" % name); return
    s = sorted(xs)
    print("   %-26s n=%d 中位=%d 均值=%.1f 最小=%d 最大=%d p90=%d"
          % (name, len(s), statistics.median(s), statistics.mean(s), s[0], s[-1], s[int(len(s) * 0.9)]))


print("\n长度分布（字符数）：")
for lang in ("zh-CN", "en"):
    stat("title[%s]" % lang, [len(pick(e.get("title"), lang)) for e in entries])
for lang in ("zh-CN", "en"):
    stat("summary[%s]" % lang, [len(pick(e.get("summary"), lang)) for e in entries])
stat("keywords 个数", [len(e.get("keywords") or []) for e in entries])
stat("keywords 单条长度", [len(k) for e in entries for k in (e.get("keywords") or [])])
stat("expressions 个数", [len(e.get("expressions") or []) for e in entries])
for lang in ("zh-CN", "en"):
    stat("expression.text[%s]" % lang,
         [len(pick(x.get("text"), lang)) for e in entries for x in (e.get("expressions") or []) if isinstance(x, dict)])

# 拼装方案对比：几种候选喂法各有多长
print("\n★ 候选输入拼法（zh-CN，拼起来多少字符 / 大约多少 token*）：")
plans = {
    "A title": lambda e: pick(e.get("title"), "zh-CN"),
    "B title+summary": lambda e: pick(e.get("title"), "zh-CN") + " " + pick(e.get("summary"), "zh-CN"),
    "C title+summary+keywords": lambda e: " ".join(
        [pick(e.get("title"), "zh-CN"), pick(e.get("summary"), "zh-CN")
         ] + [k for k in (e.get("keywords") or []) if any("\u4e00" <= ch <= "\u9fff" for ch in k)]),
    "D C + 首条 expression": lambda e: " ".join(
        [pick(e.get("title"), "zh-CN"), pick(e.get("summary"), "zh-CN")]
        + [k for k in (e.get("keywords") or []) if any("\u4e00" <= ch <= "\u9fff" for ch in k)]
        + [pick(x.get("text"), "zh-CN") for x in (e.get("expressions") or []) if isinstance(x, dict)][:1]),
    "E C + 全部 expression": lambda e: " ".join(
        [pick(e.get("title"), "zh-CN"), pick(e.get("summary"), "zh-CN")]
        + [k for k in (e.get("keywords") or []) if any("\u4e00" <= ch <= "\u9fff" for ch in k)]
        + [pick(x.get("text"), "zh-CN") for x in (e.get("expressions") or []) if isinstance(x, dict)]),
}
for name, fn in plans.items():
    lens = [len(fn(e)) for e in entries]
    s = sorted(lens)
    print("   %-24s 中位=%4d 均值=%6.1f 最大=%5d  （中文约 %d~%d token）"
          % (name, statistics.median(s), statistics.mean(s), s[-1],
             int(statistics.median(s) * 0.9), int(s[-1] * 0.9)))

print("\n样本（3 条）：")
for e in entries[:3] + entries[200:201] + entries[-1:]:
    print("\n--- %s  [domain=%s]" % (e.get("id"), e.get("domain")))
    print("   title    : %s" % e.get("title"))
    print("   summary  : %s" % e.get("summary"))
    print("   keywords : %s" % (e.get("keywords") or [])[:8])
    for x in (e.get("expressions") or [])[:1]:
        print("   expr     : id=%s enabled=%s deny=%s grant=%s"
              % (x.get("id"), x.get("enabled"), len(x.get("denies") or []), len(x.get("grants") or [])))
        print("     text   : %s" % str(x.get("text"))[:200])
    print("   extensions: %s" % json.dumps(e.get("extensions"), ensure_ascii=False)[:200])
