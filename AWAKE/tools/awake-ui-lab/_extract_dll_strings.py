# -*- coding: utf-8 -*-
"""从 .NET DLL / 二进制里抽字符串（本机没有 strings 命令）。

为什么要自己写：Git Bash 的 binutils 里**没有 strings**，直接调它会静默返回空 ⇒
"没命中"是假的。所以这个脚本第一件事就是**打阳性对照**（总条数），再谈匹配。

用法：
  python _extract_dll_strings.py <文件> [关键字正则] [--min 6] [--all]

默认 utf8+utf16le 两侧都抽；`--all` 时不过滤关键字、按长度倒序打前 200 条。
"""
import argparse
import re
import sys


def runs(data, encoding, min_len):
    """按编码抽出可打印串。utf-16le 时把字节按 2 字节一组过滤。"""
    out = []
    if encoding == "ascii":
        for m in re.finditer(rb"[\x20-\x7e]{%d,}" % min_len, data):
            out.append(m.group().decode("ascii", "replace"))
    else:
        # UTF-16LE：可打印 ASCII 后跟 \x00
        for m in re.finditer(rb"(?:[\x20-\x7e]\x00){%d,}" % min_len, data):
            out.append(m.group().decode("utf-16le", "replace"))
    return out


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser()
    ap.add_argument("path")
    ap.add_argument("keyword", nargs="?", default=None)
    ap.add_argument("--min", type=int, default=6, dest="min_len")
    ap.add_argument("--all", action="store_true")
    ap.add_argument("--limit", type=int, default=120)
    args = ap.parse_args()

    with open(args.path, "rb") as fh:
        data = fh.read()
    print("文件 %s  大小 %.1f KB" % (args.path, len(data) / 1024.0))

    ascii_strs = runs(data, "ascii", args.min_len)
    utf16_strs = runs(data, "utf16le", args.min_len)

    # ---- 阳性对照：这一步不做完，下面的“没命中”一个字都不能信
    print("阳性对照：ASCII 串 %d 条 · UTF-16LE 串 %d 条" % (len(ascii_strs), len(utf16_strs)))
    if not ascii_strs and not utf16_strs:
        print("!! 一条都没抽到 ⇒ 探针本身有问题，别下任何结论")
        return

    pool = ascii_strs + utf16_strs
    if args.keyword:
        rx = re.compile(args.keyword, re.I)
        hits = [s for s in pool if rx.search(s)]
        # 去重保序
        seen, uniq = set(), []
        for s in hits:
            if s in seen:
                continue
            seen.add(s)
            uniq.append(s)
        print("匹配 %r：%d 条（去重后 %d）" % (args.keyword, len(hits), len(uniq)))
        for s in uniq[: args.limit]:
            print("  " + s)
        return

    if args.all:
        for s in sorted(set(pool), key=len, reverse=True)[: args.limit]:
            print("  " + s)
    else:
        print("（没给关键字，用 --all 看长度最长的前 %d 条）" % args.limit)


if __name__ == "__main__":
    main()
