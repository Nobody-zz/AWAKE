# -*- coding: utf-8 -*-
"""清之前先扫全量源档：authoring/*.yaml 里「模型可见文字」有没有作者台账。

为什么扫源档而不只扫上线包：上线包只有 448 档，工作区有 945 份；只清上线包等于
把同样的东西留给下一批发出去。

「模型可见文字」＝ yaml 的 `summary.zh-CN` ＋ 每条 `assertions[].expressions[].text.zh-CN`
（对应 FormatEntry 拼给模型的三段：title ＋ summary ＋ 该档正文）。
"""
import collections
import glob
import io
import os
import re

AUTH = "tools/worldbook-studio/workspace/full-geo1/authoring"

PATTERNS = [
    ("A·工程缩略/章节号", re.compile(r"IMPL|§|见\s*docs?/|RUNTIME-MAPPING|\bK1\b")),
    ("A·留痕类词", re.compile(r"留痕|登记于|已登记|归档|台账")),
    ("A·裁定/级别标记", re.compile(r"[A-D]\s*级裁定|裁定\(|裁定（|研究稿")),
    ("A·作者名（Max）", re.compile(r"\bMax\b")),
    ("A·日期串", re.compile(r"\b(20\d\d[-/])?\d{1,2}[-/月]\d{1,2}日?\b")),
    ("A·TODO/待办", re.compile(r"TODO|FIXME|待补|待定|待写|XXX")),
    ("A·档位/分层术语", re.compile(r"档承载|分层分档|min_detail|\bscope\b|detail 档|summary 档|rumor 档")),
    ("A·半角文件名", re.compile(r"[\w\-]+\.(cs|md|py|json|yaml|ps1)\b")),
    ("A·commit/sha", re.compile(r"\b[0-9a-f]{7,40}\b")),
    ("B·需要复核标记", re.compile(r"needs_review|待复核|未经复核")),
]


def block_scalar(text, key, indent):
    """取 `key:` 下缩进为 indent 的 `zh-CN: ...` 行（本仓 yaml 都是单行标量）。"""
    m = re.search(r"^%s%s:\s*\n((?:\s+.*\n)+)" % (" " * indent, re.escape(key)), text, re.M)
    if not m:
        return None
    body = m.group(1)
    m2 = re.search(r"^\s+zh-CN:\s*(.+)$", body, re.M)
    return m2.group(1).strip() if m2 else None


def scan(path):
    text = io.open(path, encoding="utf-8").read()
    out = []
    s = block_scalar(text, "summary", 0)
    if s:
        out.append(("summary", s))
    # 表达式正文：抓所有 `zh-CN:` 行里、缩进较深（属于 expressions[].text）的
    for ln in text.splitlines():
        m = re.match(r"^(\s+)zh-CN:\s*(.+)$", ln)
        if not m:
            continue
        indent, val = len(m.group(1)), m.group(2).strip()
        # summary 块缩进 2；expression 的 text 缩进更深
        if indent >= 6:
            out.append(("text", val))
    return out


per_class = collections.defaultdict(list)
files = sorted(glob.glob(os.path.join(AUTH, "*.yaml")))
print("源档数：", len(files))

for path in files:
    base = os.path.basename(path)
    for tag, val in scan(path):
        for name, pat in PATTERNS:
            if pat.search(val):
                per_class[name].append((base, tag, val))

print()
print("=== 分类计数（源档的模型可见文字）===")
for name, _p in PATTERNS:
    rows = per_class.get(name, [])
    print("  %-22s %3d 段 / %3d 份档" % (name, len(rows), len({a for a, _b, _c in rows})))
print()

print("=== A 组逐条（人工判断）===")
for name, _p in PATTERNS:
    if not name.startswith("A·"):
        continue
    rows = per_class.get(name, [])
    if not rows:
        continue
    print()
    print("--- %s（%d 段 / %d 份）" % (name, len(rows), len({a for a, _b, _c in rows})))
    seen = set()
    for base, tag, val in rows:
        key = (base, tag, val[:40])
        if key in seen:
            continue
        seen.add(key)
        print("  %-42s [%s] %s" % (base, tag, val[:200]))
