#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
金标样本 persona-load-v2-golden.json 的 expectedDsl 再生工具（2026-09-19 立）

用途
    当 PersonaDslGenerator 的输出**有意**改变时（如新增约束令牌、调整段序），
    AWAKE.Tests 的 shared-persona-golden-fixture 会红——那是它在正常工作，
    不是坏了。此时按下面的流程把金标同步过去。

为什么需要工具而不是手改
    expectedDsl 是压缩在一行里的 JSON 转义字符串（含 \\n 与 \\"），手抄必然出错。
    本工具直接消费烟测自己的输出，杜绝转写风险。

流程（三步）
    1) 跑烟测，让它把真实 DSL 吐到标准输出（失败时才打印，前缀 FIXTURE_ACTUAL_DSL）：
           cd AWAKE.Tests/bin/Debug/net472
           ./Awake.SdkSmoke.exe > run.txt 2>&1
       ⚠️ .NET 控制台按系统码页（简体中文机上多为 GBK）写重定向文件，故读取时须兼容 GBK。

    2) 从 run.txt 抽出那一行：
           python _persona_golden_fixture_regen_20260919.py extract run.txt actual.json

    3) 写回金标（原文件自动备份到 <fixture>.before）：
           python _persona_golden_fixture_regen_20260919.py apply actual.json

    4) 重跑烟测确认转绿。

硬约束（脚本会自行断言，不满足即拒绝写入）
    - 金标只允许改动 expectedDsl 一行；用「按行替换」而非整篇重排，其余字节原样保留。
    - 保持无 BOM、LF 行尾、结尾有换行——本仓库对这三个形态敏感。
    - 写入后回读校验 expectedDsl == 真实 DSL，且顶层键集合未变。

注意
    金标是「变化探测」而不是「独立验证」：它冻结输出，好让任何未预期的漂移立刻显形。
    因此**只应在输出有意改变时再生**；若说不清为什么变，先查清楚再动它。
"""

import io
import json
import os
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_FIXTURE = os.path.join(
    os.path.dirname(HERE), "AWAKE", "docs", "fixtures", "persona-load-v2-golden.json")
MARKER = "FIXTURE_ACTUAL_DSL"
ENCODINGS = ("utf-8", "gbk", "cp936", "utf-8-sig")


def read_text(path):
    with io.open(path, "rb") as handle:
        raw = handle.read()
    for encoding in ENCODINGS:
        try:
            return raw.decode(encoding)
        except UnicodeDecodeError:
            continue
    raise SystemExit("cannot decode %s with any of %s" % (path, ENCODINGS))


def extract(run_log, out_path):
    """从烟测输出里抽出真实 DSL（一行 JSON 字符串），落成 out_path。"""
    marker_line = None
    for line in read_text(run_log).splitlines():
        if line.startswith(MARKER):
            marker_line = line[len(MARKER):].strip()
    if marker_line is None:
        raise SystemExit(
            "no %s line in %s -- 说明该用例当前是绿的，或烟测没跑到它。" % (MARKER, run_log))
    dsl = json.loads(marker_line)
    with io.open(out_path, "wb") as handle:
        handle.write(json.dumps(dsl, ensure_ascii=False).encode("utf-8"))
    print("extracted dsl chars=%d lines=%d -> %s" % (len(dsl), len(dsl.split("\n")), out_path))


def apply(actual_path, fixture_path):
    dsl = json.loads(read_text(actual_path))

    with io.open(fixture_path, "rb") as handle:
        before = handle.read()
    text = before.decode("utf-8")
    assert b"\r" not in before, "fixture has CR, refusing to touch its line endings"
    assert not before.startswith(b"\xef\xbb\xbf"), "fixture has BOM, refusing"
    assert text.endswith("\n"), "fixture lacks trailing newline, refusing"

    lines = text.split("\n")
    hits = [i for i, line in enumerate(lines) if line.startswith('  "expectedDsl": ')]
    if len(hits) != 1:
        raise SystemExit("expected exactly one expectedDsl line, found %d" % len(hits))

    before_keys = sorted(json.loads(text).keys())
    backup = fixture_path + ".before"
    shutil.copyfile(fixture_path, backup)
    lines[hits[0]] = '  "expectedDsl": ' + json.dumps(dsl, ensure_ascii=False)
    with io.open(fixture_path, "wb") as handle:
        handle.write("\n".join(lines).encode("utf-8"))

    with io.open(fixture_path, "rb") as handle:
        after = handle.read()
    parsed = json.loads(after.decode("utf-8"))
    assert parsed["expectedDsl"] == dsl, "verification failed: written value differs"
    assert sorted(parsed.keys()) == before_keys, "top-level keys changed"
    assert b"\r" not in after and not after.startswith(b"\xef\xbb\xbf") and after.endswith(b"\n")
    print("ok: bytes %d -> %d, backup at %s" % (len(before), len(after), backup))
    print("    keys unchanged, no BOM, LF, trailing newline kept")


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return 2
    command = argv[1]
    if command == "extract" and len(argv) == 4:
        extract(argv[2], argv[3])
        return 0
    if command == "apply":
        actual = argv[2] if len(argv) > 2 else "actual.json"
        fixture = argv[3] if len(argv) > 3 else DEFAULT_FIXTURE
        apply(actual, fixture)
        return 0
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv))
