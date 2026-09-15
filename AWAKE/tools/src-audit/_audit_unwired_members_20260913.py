"""AWAKE src 审核 · 未接通成员扫描（只读）

目的：找出「声明了但没人调用」的 internal 类型与 internal 方法——
即上一轮在 CreateInboundAsync 上踩到的那类缺口（写入端写好了，
全仓库零调用者，功能实际从未发生）。

原理：文本级引用计数。对每个候选定义，统计其标识符在
  (a) src/  除定义行之外的其余位置
  (b) tools/ 全目录
  (c) ../AWAKE.Tests/ 全目录
的出现次数。生产代码零引用而夹具有引用 = 典型的「只有测试在用」。

⚠️ 修订（2026-09-13 晚）：初版只扫 src+tools，**漏了 AWAKE.Tests**，导致
`WorldbookLoader` / `WorldEventInboxFormatter` 被误判为"全仓零引用"——它们
其实只被测试工程调用。零引用必须三语料同时为零才成立。

已知局限（会带来误报/漏报，报告里必须标注）：
  - 反射按字符串调用（AwakeRuntime 大量用反射读游戏类型）→ 真调用者不计入。
  - 同名重载会互相计数。
  - 接口实现、事件订阅（+= MethodName）本身算引用，属正确。
  - 只扫文本，不解析语法树。
"""

import os
import re
import sys
from collections import defaultdict

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "src")
TOOLS = os.path.join(ROOT, "tools")

# 太通用的名字必然误报，直接跳过
STOPWORDS = {
    "Apply", "New", "Get", "Set", "Try", "Run", "Main", "Parse", "Load", "Save",
    "Reset", "Init", "Dispose", "Equals", "ToString", "Compare", "Contains",
    "Write", "Read", "Open", "Close", "Start", "Stop", "Update", "Create",
    "Value", "Name", "Key", "Count", "Item", "Error", "Result", "Status",
}

DEF_TYPE = re.compile(r"^\s*internal\s+(?:static\s+|sealed\s+|abstract\s+|partial\s+)*(?:class|struct|interface|enum)\s+(\w+)")
DEF_METHOD = re.compile(
    r"^\s*(?:internal|private|protected)\s+(?:static\s+)?(?:async\s+)?(?:unsafe\s+)?"
    r"(?:[\w<>,\[\]\.\?]+)\s+(\w+)\s*\("
)


SKIP_DIRS = {"out", "bin", "obj", "node_modules", ".git", "packages", "__pycache__", "dist", "release", "workspace"}


def read_all(directory, pattern=".cs"):
    docs = {}
    for base, dirs, files in os.walk(directory):
        dirs[:] = [d for d in dirs if d.lower() not in SKIP_DIRS]
        for name in files:
            if not name.endswith(pattern):
                continue
            path = os.path.join(base, name)
            try:
                with open(path, "r", encoding="utf-8", errors="replace") as handle:
                    docs[path] = handle.read()
            except OSError as exc:
                print("warn: cannot read %s (%s)" % (path, exc), file=sys.stderr)
    return docs


def main():
    src_docs = read_all(SRC)
    tool_docs = read_all(TOOLS)
    test_docs = read_all(TESTS)
    print("scanned: src=%d files, tools=%d files, tests=%d files"
          % (len(src_docs), len(tool_docs), len(test_docs)))

    # 定义收集
    definitions = []  # (kind, name, relpath, lineno)
    for path, text in src_docs.items():
        rel = os.path.relpath(path, ROOT).replace("\\", "/")
        for index, line in enumerate(text.splitlines(), start=1):
            m = DEF_TYPE.match(line)
            if m:
                definitions.append(("type", m.group(1), rel, index))
                continue
            m = DEF_METHOD.match(line)
            if m and not line.strip().startswith("//"):
                definitions.append(("method", m.group(1), rel, index))
                continue

    # 引用统计
    src_all = "\n".join(src_docs.values())
    tools_all = "\n".join(tool_docs.values())
    tests_all = "\n".join(test_docs.values())

    buckets = defaultdict(list)
    for kind, name, rel, lineno in definitions:
        if len(name) < 5 or name in STOPWORDS:
            continue
        pat = re.compile(r"\b%s\b" % re.escape(name))
        src_hits = len(pat.findall(src_all))
        tool_hits = len(pat.findall(tools_all))
        tests_hits = len(pat.findall(tests_all))
        # 定义自身算 1 次
        src_other = src_hits - 1
        fixture_hits = tool_hits + tests_hits
        if src_other <= 0 and fixture_hits == 0:
            buckets["ZERO"].append((kind, name, rel, lineno))
        elif src_other <= 0 and fixture_hits > 0:
            buckets["TESTONLY"].append((kind, name, rel, lineno, fixture_hits))

    print()
    print("=== A. 全仓库零引用（候选死代码）: %d ===" % len(buckets["ZERO"]))
    for kind, name, rel, lineno in sorted(buckets["ZERO"], key=lambda x: (x[0], x[1])):
        print("  [%s] %-46s %s:%d" % (kind, name, rel, lineno))

    print()
    print("=== B. 仅夹具引用（生产代码零调用）: %d ===" % len(buckets["TESTONLY"]))
    for kind, name, rel, lineno, hits in sorted(buckets["TESTONLY"], key=lambda x: (x[0], x[1])):
        print("  [%s] %-46s %s:%d  tools_hits=%d" % (kind, name, rel, lineno, hits))

    print()
    print("total definitions considered: %d" % len(definitions))


if __name__ == "__main__":
    main()
