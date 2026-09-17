# -*- coding: utf-8 -*-
"""重做「模型可见文字里有没有作者台账」的普查（09-17 第二版）。

⚠️ 为什么重做：第一版筛子只查 `IMPL／§／留痕／档承载／TODO／半角文件名`，
   结果报"孤例"——但那只证明**这几类**是孤例，不等于"没有别的台账"。
   随后肉眼就撞见 `throne-saneopa` 的 summary 里写着「定都史为 D 级裁定（Max 09-12，登记于研究稿）」
   ——「人名＋日期＋研究稿」这种形状，第一版筛子一个字也抓不到。
   ⇒ 教训：**筛子窄 ≠ 目标不存在；报"没有"之前先问"我的筛子能抓到哪几类"。**

模型可见文字 = FormatEntry 实际拼给模型的三段：title ＋ summary ＋ expressions[].text（按档）。
本脚本对每段跑一组**分类**的正则，逐类报数量并打印样例，让人自己判断哪些是真台账。
"""
import collections
import io
import json
import re

PKG = "ModuleData/Worldbook/packages/calradia/runtime.json"

# 分类筛子：宁可多报（误报我自己看），不要漏报。
PATTERNS = [
    ("A·工程缩略/章节号", re.compile(r"IMPL|§|§\s*\d|见\s*docs?/|RUNTIME-MAPPING|K1\b")),
    ("A·留痕类词", re.compile(r"留痕|登记于|已登记|归档|台账")),
    ("A·裁定/级别标记", re.compile(r"[A-D]\s*级裁定|裁定\(|裁定（|研究稿")),
    ("A·作者名（Max）", re.compile(r"\bMax\b")),
    ("A·日期串", re.compile(r"\b(20\d\d[-/])?\d{1,2}[-/月]\d{1,2}日?\b")),
    ("A·TODO/待办", re.compile(r"TODO|FIXME|待补|待定|待写|XXX")),
    ("A·档位/分层术语", re.compile(r"档承载|分档|分层分档|min_detail|scope|detail 档|summary 档|rumor 档")),
    ("A·半角文件名", re.compile(r"[\w\-]+\.(cs|md|py|json|yaml|ps1)\b")),
    ("A·commit/sha", re.compile(r"\b[0-9a-f]{7,40}\b")),
    ("B·英文内部域名", re.compile(r"\b(economy|geography|politics|military|culture|war|entry|doc)\b")),
    ("B·需要复核标记", re.compile(r"needs_review|待复核|未经复核")),
]


def flat(val, out):
    if isinstance(val, str):
        if val:
            out.append(val)
    elif isinstance(val, dict):
        for v in val.values():
            flat(v, out)
    elif isinstance(val, list):
        for v in val:
            flat(v, out)


def model_visible(e):
    """返回 [(字段标签, 文本)]，与 FormatEntry 拼给模型的三段一致。"""
    out = []
    for k in ("title", "summary"):
        buf = []
        flat(e.get(k), buf)
        for v in buf:
            out.append((k, v))
    for i, item in enumerate(e.get("expressions") or []):
        if isinstance(item, dict):
            tier = item.get("detail") or "?"
            buf = []
            flat(item.get("text"), buf)
            for v in buf:
                out.append(("expr[%s]" % tier, v))
    return out


with io.open(PKG, encoding="utf-8") as fh:
    data = json.load(fh)
entries = data["entries"]
print("包内条目数：", len(entries))
print()

per_class = collections.defaultdict(list)
for e in entries:
    eid = (e.get("id") or "").replace("awake:entry:", "")
    for tag, text in model_visible(e):
        for name, pat in PATTERNS:
            if pat.search(text):
                per_class[name].append((eid, tag, text))

print("=== 分类计数（模型可见文字）===")
for name, _p in PATTERNS:
    rows = per_class.get(name, [])
    uniq = {(a, b) for a, b, _t in rows}
    print("  %-22s %3d 段 / %3d 条" % (name, len(rows), len({a for a, _b, _t in rows})))
print()

print("=== A 组（工程台账嫌疑）逐条打印，人工判断 ===")
for name, _p in PATTERNS:
    if not name.startswith("A·"):
        continue
    rows = per_class.get(name, [])
    if not rows:
        continue
    print()
    print("--- %s（%d 段）" % (name, len(rows)))
    seen = set()
    for eid, tag, text in rows:
        key = (eid, tag, text[:40])
        if key in seen:
            continue
        seen.add(key)
        print("  [%s %s] %s" % (eid, tag, text[:220].replace("\n", " ")))
