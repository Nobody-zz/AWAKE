# -*- coding: utf-8 -*-
"""量记忆档规模 ＋ 逐行字数控（用于守 MEMORY.md 的 3000 字限）。可复用，不带日期。

为什么需要它：**Git Bash 的 `wc -m` 在本机按字节报**（`LC_ALL` 怎么设都一样），
会把 3350 字的 `MEMORY.md` 报成 6321 ⇒ 据此判断"没超限"就是错的。

用法：
    python tools/_mem_stats.py                 # 总览
    python tools/_mem_stats.py MEMORY.md       # 逐行（含超限提示）
"""
import io, os, sys

MEMDIR = r"D:\AWAKE-Dev\.workbuddy\memory"
LIMIT = 3000          # MEMORY.md 的字数上限（项目约定，见其 §五）


def chars(s):
    return len(s)


def main(argv):
    if len(argv) > 1:
        name = argv[1]
        p = os.path.join(MEMDIR, name)
        lines = io.open(p, encoding="utf-8").read().split("\n")
        tot = 0
        for i, l in enumerate(lines, 1):
            tot += len(l) + 1
            print("%3d  %4d  %s" % (i, len(l), l[:52]))
        print("-" * 60)
        print("总字符(含换行) %d   目标 ≤%d   需削 %d" % (tot, LIMIT, max(0, tot - LIMIT)))
        return 0

    for name in ("MEMORY.md", "TOPIC-CODE.md", "TOPIC-WORLDBOOK.md", "TOPIC-PERSONA.md",
                 "TOPIC-UI.md", "TOPIC-ART.md", "CROSSLINE.md"):
        p = os.path.join(MEMDIR, name)
        if not os.path.exists(p):
            continue
        s = io.open(p, encoding="utf-8").read()
        flag = ""
        if name == "MEMORY.md":
            flag = "  ← 限 %d，%s" % (LIMIT, "达标" if chars(s) <= LIMIT else "**超限 %d**" % (chars(s) - LIMIT))
        print("%-22s 字符 %7d   字节 %8d%s" % (name, chars(s), len(s.encode("utf-8")), flag))

    d = sorted(f for f in os.listdir(MEMDIR) if f.endswith(".md") and f[0].isdigit())
    for name in d[-2:]:
        s = io.open(os.path.join(MEMDIR, name), encoding="utf-8").read()
        print("%-22s 字符 %7d   字节 %8d   （当日流水）" % (name, chars(s), len(s.encode("utf-8"))))
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
