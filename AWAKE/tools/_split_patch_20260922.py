# -*- coding: utf-8 -*-
"""把 Program.cs 的完整补丁切成"只含我这条线那几个 hunk"的子补丁。

背景（2026-09-22）：AWAKE.Tests/Program.cs 同时承载两条线的在途改动 ——
  本线（主干 · P1-05）：注册行 letter-half-commit（旧起始行 80）＋ 用例与辅助（旧起始行 2719）；
  他线（世界书 · P1-04）：注册行 worldbook-overlay-import-partial（旧起始行 130）＋ 其用例（旧起始行 1731）。
仓库纪律：不许把别线的在途改动带进自己的提交。一个文件里混编 ⇒ pathspec 挡不住，
只能按 hunk 切，再 `git apply --cached` 只把这几个 hunk 放进索引（**不动工作区**，他线无感）。

只用标准库；输出子补丁到 _patch_mine_20260922.patch。
"""

import io
import re

SRC = r"D:\AWAKE-Dev\AWAKE\tools\_patch_full_20260922.patch"
DST = r"D:\AWAKE-Dev\AWAKE\tools\_patch_mine_20260922.patch"

# 只保留这两个 hunk 的旧起始行（本线的两段）。
KEEP_OLD_START = (80, 2719)

HUNK = re.compile(r"^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@(.*)$")


def main():
    with io.open(SRC, "r", encoding="utf-8", newline="") as handle:
        lines = handle.readlines()

    head = []
    index = 0
    while index < len(lines) and not lines[index].startswith("@@"):
        head.append(lines[index])
        index += 1

    kept = []
    dropped = []
    while index < len(lines):
        header = lines[index]
        match = HUNK.match(header.rstrip("\n"))
        if match is None:
            raise SystemExit("unparsable hunk header: " + header)
        old_start = int(match.group(1))
        body = []
        index += 1
        while index < len(lines) and not lines[index].startswith("@@"):
            if lines[index].startswith("diff --git"):
                raise SystemExit("unexpected second file in patch")
            body.append(lines[index])
            index += 1
        if old_start in KEEP_OLD_START:
            kept.append((header.rstrip("\n"), body))
        else:
            dropped.append(old_start)

    out = list(head)
    offset = 0
    for header, body in kept:
        match = HUNK.match(header)
        old_start = int(match.group(1))
        old_len = int(match.group(2) or "1")
        added = sum(1 for line in body if line.startswith("+"))
        removed = sum(1 for line in body if line.startswith("-"))
        if old_len != len(body) - added + removed:
            # 只有 U3 的上下文计数才是这个关系；不成立就直接报出来，别猜。
            print("WARN hunk %d 计数可疑: header=%d body=%d" % (old_start, old_len, len(body)))
        new_start = old_start + offset
        new_len = old_len - removed + added
        out.append("@@ -%d,%d +%d,%d @@%s\n" % (old_start, old_len, new_start, new_len, match.group(5)))
        out.extend(body)
        offset += added - removed

    with io.open(DST, "w", encoding="utf-8", newline="") as handle:
        handle.writelines(out)

    print("保留 hunk（旧起始行）：" + ", ".join(str(int(HUNK.match(h).group(1))) for h, _ in kept))
    print("丢弃 hunk（旧起始行）：" + ", ".join(str(x) for x in dropped))
    print("累计行偏移 = %d" % offset)
    print("子补丁 -> " + DST)


main()
