# -*- coding: utf-8 -*-
"""A 项：把三个泛词从「泛用关键词」降下来。

改什么（**两个目录同步**：studio 工作区 authoring/ ＝编译输入；projection/authoring-out ＝仓库留档）：
  ① 388 条聚落条目 aliases[zh-CN] 里的**裸类别词**删掉
     村庄 ×272 / 城堡 ×67 / 城镇 ×49
  ② villages-deriat 的复合别名「德里亚特·村庄」删掉（这条是手写的，正是劫持 272 条的元凶）

为什么删：
  - 这三个词现在**指不到任何一条**（被索引卫生 R1 按覆盖>40 剔了），是**纯死重**；
  - 更要紧的是它留了个"谁都能沾的坑"：德里亚特那条手写复合词 `德里亚特·村庄` 覆盖只有 1、
    绕过了 R1，于是成了「村庄」在索引里的**唯一主人**，把 272 条村庄的泛问全劫走。
  - 甲方 09-17 裁决：这三个词**升格为概念词条**（`doc.geography.settlement-types-{village,castle,town}`），
    所以它们应当只挂在概念词条上（覆盖 1 ⇒ 不被 R1 剔 ⇒ 正常进索引 ⇒ 查询有明确所指）。

删完的期望：三个词在两个目录里的挂载数都各为 1（都在 aliases 上的只有它们的标题/别名），
且 `德里亚特·村庄` 全库为 0。

用法：
  python -u tools/_clean_generic_settlement_keywords_20260917.py          # 只预览
  python -u tools/_clean_generic_settlement_keywords_20260917.py --write  # 落盘
"""
import io
import os
import re
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = r"D:/AWAKE-Dev/AWAKE"
DIRS = [
    os.path.join(ROOT, "tools/worldbook-studio/workspace/full-geo1/authoring"),
    os.path.join(ROOT, "docs/worldbook-migration/projection/authoring-out"),
]
DROP_EXACT = {"村庄", "城堡", "城镇"}          # 裸类别词：整行删
DROP_VALUES = {"德里亚特·村庄"}                # 复合词：整行删

ALIAS_HEAD = re.compile(r"^aliases:\s*$", re.M)


def alias_span(text):
    """返回 aliases 段的 [start, end)（到下一个顶层键为止）。"""
    m = ALIAS_HEAD.search(text)
    if not m:
        return None
    start = m.end()
    nxt = re.search(r"^[A-Za-z_][A-Za-z0-9_]*:", text[start:], re.M)
    end = start + (nxt.start() if nxt else len(text) - start)
    return start, end


def clean(text):
    span = alias_span(text)
    if not span:
        return text, []
    s, e = span
    seg = text[s:e]
    out, dropped = [], []
    for line in seg.split("\n"):
        st = line.strip()
        if st.startswith("- "):
            val = st[2:].strip().strip('"').strip("'")
            if val in DROP_EXACT or val in DROP_VALUES:
                dropped.append(val)
                continue
        out.append(line)
    return text[:s] + "\n".join(out) + text[e:], dropped


def main():
    write = "--write" in sys.argv
    total = 0
    per_dir = {}
    for d in DIRS:
        n_files, n_lines = 0, 0
        counter = {}
        for name in sorted(os.listdir(d)):
            if not name.endswith(".yaml"):
                continue
            p = os.path.join(d, name)
            raw = io.open(p, encoding="utf-8", newline="").read()
            new, dropped = clean(raw)
            if not dropped:
                continue
            n_files += 1
            n_lines += len(dropped)
            for x in dropped:
                counter[x] = counter.get(x, 0) + 1
            if write:
                # 保持原有换行风格（这两个目录是 LF）
                io.open(p, "w", encoding="utf-8", newline="").write(new)
        per_dir[d] = (n_files, n_lines, counter)
        total += n_lines
        print("=== %s" % d)
        print("   命中文件 %d，删行 %d：%s" % (n_files, n_lines,
                                          {k: v for k, v in sorted(counter.items())}))
    print()
    print("合计删行 %d" % total)
    if write:
        print("已落盘。")
    else:
        print("（未加 --write，只做预览）")


if __name__ == "__main__":
    main()
