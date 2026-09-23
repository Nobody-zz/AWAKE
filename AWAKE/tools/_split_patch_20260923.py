#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
把「多线共改的文件」里属于本线那几段改动单独切成一个补丁。

用法： _split_patch_20260923.py <完整补丁> <子补丁> <要保留的 hunk 序号,逗号分隔>
hunk 序号从 1 起，按补丁里 @@ 出现的顺序计。

存在理由（2026-09-22 起，2026-09-23 复用）：
`git commit -- <路径>` 提交的是**工作区整份**，不是索引里暂存的部分 ⇒
在「一个文件被多条线同时改」的情况下，用 --cached 暂存一部分再提交
仍会把整份工作区内容带走。唯一安全的做法是：只对挑出来的 hunk 建索引。
"""
import io
import sys

def main():
    if len(sys.argv) < 4:
        print("用法: _split_patch_20260923.py <完整补丁> <子补丁> <保留hunk序号>")
        return 2
    src, dst, keep_arg = sys.argv[1], sys.argv[2], sys.argv[3]
    keep = set(int(x) for x in keep_arg.split(",") if x.strip())

    lines = io.open(src, encoding="utf-8").read().split("\n")
    header = []
    hunks = []
    current = None
    for line in lines:
        if line.startswith("@@"):
            current = [line]
            hunks.append(current)
            continue
        if current is None:
            header.append(line)
        else:
            current.append(line)

    print("补丁里共 %d 段改动，保留第 %s 段" % (len(hunks), ",".join(str(x) for x in sorted(keep))))

    out = list(header)
    taken = 0
    for index, hunk in enumerate(hunks, start=1):
        if index not in keep:
            continue
        # 去掉上一段落末尾产生的空行，避免累积
        while hunk and hunk[-1] == "":
            hunk.pop()
        out.extend(hunk)
        taken += 1
    print("实际取出 %d 段" % taken)

    io.open(dst, "w", encoding="utf-8", newline="\n").write("\n".join(out) + "\n")
    print("已写入 " + dst)
    return 0


if __name__ == "__main__":
    sys.exit(main())
