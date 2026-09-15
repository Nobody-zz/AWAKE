"""AWAKE src 审核 · 静默 catch 扫描（只读）

目的：找出「异常被吞掉且无任何痕迹」的 catch —— 即 F5 关注的那类静默失败。

原理：把**连续的 catch 子句合并成一条链**，只有**整条链的块体全为空**
（忽略注释）才报告。逐链输出被保护代码的前若干行，便于判断危害。

⚠️ 为什么必须按「链」判定（2026-09-13，初版踩到）：
本项目有一个固定写法——

    try { 危险操作(); }
    catch (OperationCanceledException) { }                        // 空，但有意
    catch (Exception ex) { AwakeLog.Write("..._error error=" + …); }  // 兄弟分支已留痕

按「任何空块即报」的口径会把它报成缺陷，**完全看不见紧随其后的兄弟 catch**。
初版报了 62 处，其中 4 处（AwakeEventEngine / AwakeEventBehavior /
AwakeLetterService / NpcProactiveService）都是这个模式，**且恰好是初版认定
"最值得修"的那 4 个** ⇒ 差点去改本就正确的代码。
⇒ 教训：工具盲区 ≠ 代码缺陷。判定前必须读完整上下文。

已知局限（报告里须标注）：
  - 只看单个 try 的 catch 链，不跨方法分析；嵌套 try 各自成链。
  - 只扫文本，不解析语法树；块体"非空"即视为已处理（哪怕只是 `_ = 1;`）。
  - 不判断调用频率、不判断是否死代码——这些须人工结合引用扫描与上下文。
  - `catch` 后紧跟 `finally` 不影响链判定。
"""

import io
import os
import re

_SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(_SCRIPT_DIR, "..", "..", "src"))
OUT = os.path.join(_SCRIPT_DIR, "_result_silent_catch_20260913.txt")
CTX_BEFORE = 12

catch_pat = re.compile(r"\bcatch\s*(\([^)]*\))?\s*\{")


def find_block_end(text, open_idx):
    """open_idx 指向 '{'，返回配对 '}' 的下标。跳过字符串/字符/注释。"""
    depth = 0
    i = open_idx
    n = len(text)
    in_str = in_char = in_line = in_blk = False
    while i < n:
        c = text[i]
        nxt = text[i + 1] if i + 1 < n else ""
        if in_line:
            if c == "\n":
                in_line = False
        elif in_blk:
            if c == "*" and nxt == "/":
                in_blk = False
                i += 1
        elif in_str:
            if c == "\\":
                i += 1
            elif c == '"':
                in_str = False
        elif in_char:
            if c == "\\":
                i += 1
            elif c == "'":
                in_char = False
        else:
            if c == "/" and nxt == "/":
                in_line = True
                i += 1
            elif c == "/" and nxt == "*":
                in_blk = True
                i += 1
            elif c == '"':
                in_str = True
            elif c == "'":
                in_char = True
            elif c == "{":
                depth += 1
            elif c == "}":
                depth -= 1
                if depth == 0:
                    return i
        i += 1
    return -1


def body_is_empty(body):
    """块体去掉注释后是否为空。"""
    s = re.sub(r"/\*.*?\*/", "", body, flags=re.DOTALL)
    s = re.sub(r"//[^\n]*", "", s)
    return not s.strip()


def main():
    rows = []
    for dirpath, _dirs, files in os.walk(ROOT):
        for fn in sorted(files):
            if not fn.endswith(".cs"):
                continue
            path = os.path.join(dirpath, fn)
            with io.open(path, "r", encoding="utf-8", errors="replace") as fh:
                text = fh.read()

            clauses = []
            for m in catch_pat.finditer(text):
                brace = text.index("{", m.start())
                end = find_block_end(text, brace)
                if end < 0:
                    continue
                clauses.append({
                    "start": m.start(),
                    "end": end,
                    "param": (m.group(1) or "").strip(),
                    "empty": body_is_empty(text[brace + 1:end]),
                })
            clauses.sort(key=lambda c: c["start"])

            chains = []
            for cl in clauses:
                if chains and not text[chains[-1][-1]["end"] + 1:cl["start"]].strip():
                    chains[-1].append(cl)
                else:
                    chains.append([cl])

            for chain in chains:
                if not all(c["empty"] for c in chain):
                    continue
                line_no = text.count("\n", 0, chain[0]["start"]) + 1
                params = " / ".join(c["param"] or "(no filter)" for c in chain)
                before = text[:chain[0]["start"]].split("\n")[-(CTX_BEFORE + 1):]
                rel = os.path.relpath(path, ROOT).replace("\\", "/")
                rows.append((rel, line_no, len(chain), params, before))

    rows.sort(key=lambda r: (r[0], r[1]))
    with io.open(OUT, "w", encoding="utf-8", newline="\n") as out:
        out.write("silent_chain_count=%d\n" % len(rows))
        out.write("# 全空 catch 链（每条：位置 / 子句数 / 各子句 filter / 被保护代码上文）\n")
        for rel, ln, n, params, ctx in rows:
            out.write("\n===== %s:%d  (clauses=%d | %s) =====\n" % (rel, ln, n, params))
            for c in ctx:
                out.write("    " + c + "\n")

    print("silent_chain_count=%d" % len(rows))
    print("written=%s" % OUT)


if __name__ == "__main__":
    main()
