# -*- coding: utf-8 -*-
"""改 docs/DECISION-20260917-两条通道怎么合.md 的 §7.8 数字（26 → 24 分母）＋ §7.7 探针行 ＋ §7.3 兜底说明。

为什么用脚本而不是 Edit：文件里有全角引号，Read 显示成直引号，Edit 锚不上。
做法：按"行首特征"定位，断言恰好命中一行，逐行替换；任何一条对不上就整篇不写。
"""
import io
import sys

PATH = r"D:\AWAKE-Dev\AWAKE\docs\DECISION-20260917-两条通道怎么合.md"

# (行首特征, 新行)  —— 行首特征必须唯一命中
RULES = [
    ("| 现状（合并） |", "| 现状（合并） | 19/24 |"),
    ("| 语义单独 |", "| 语义单独 | 20/24 |"),
    ("| ★ **天花板：目标在语义前 20 里** |", "| ★ **天花板：目标在语义前 20 里** | **23/24** |"),
    ("| oracle 限定到目标的**域** |", "| oracle 限定到目标的**域** | 23/24（域内前 3 也是 23） |"),
    ("| oracle 限定到目标的**类型** |", "| oracle 限定到目标的**类型** | 23/24 |"),
    ("| **自动选域**（取前 20 里成员最多的域） |", "| **自动选域**（取前 20 里成员最多的域） | **13/24 —— 比不用还差** |"),
    ("**实测（26 条，语义臂前 20 当池；",
     "**实测（24 条 —— 与门禁**同分母**（09-17 删掉一条废题、另有一条不计入），语义臂前 20 当池；"),
]


def main():
    with io.open(PATH, "r", encoding="utf-8") as fh:
        lines = fh.read().split("\n")

    changed = 0
    for prefix, newline in RULES:
        idx = [i for i, ln in enumerate(lines) if ln.startswith(prefix)]
        if len(idx) != 1:
            sys.exit("行首特征命中 %d 次（需要 1 次）：%r" % (len(idx), prefix))
        print("第 %d 行：%s" % (idx[0] + 1, lines[idx[0]]))
        print("     => %s" % newline)
        lines[idx[0]] = newline
        changed += 1

    with io.open(PATH, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("\n".join(lines))
    print("改了 %d 行，写回 %d 行" % (changed, len(lines)))


main()
