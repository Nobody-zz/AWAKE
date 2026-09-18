# -*- coding: utf-8 -*-
"""把 git 未跟踪清单分桶，生成可被 git add --pathspec-from-file 消费的路径清单。

只读 git 状态，不 add / 不 commit。
输出：tools/_bucket_<name>.txt（NUL 分隔 UTF-8，配 --pathspec-file-nul 用）
"""
import subprocess
import pathlib

ROOT = pathlib.Path(r"D:\AWAKE-Dev")
OUT = ROOT / "AWAKE" / "tools"


def untracked():
    p = subprocess.run(
        ["git", "-c", "core.quotepath=false", "status", "--porcelain", "-uall", "-z"],
        cwd=ROOT, capture_output=True,
    )
    raw = p.stdout.decode("utf-8", errors="surrogateescape")
    items = []
    for rec in raw.split("\0"):
        if not rec:
            continue
        status, path = rec[:2], rec[3:]
        if status.strip() == "??":
            items.append(path)
    return items


MIRROR = "AWAKE/docs/worldbook-migration/projection/authoring-out/"

DOCS_EXCLUDE = {
    "AWAKE/docs/AUDIT-COMPILE-VERIFY-latest.json",
    "AWAKE/docs/OBSERVE-RUNTIME-DEFINITIONS-latest.txt",
    "AWAKE/docs/_af_vers.txt",
}

TOOLS_EXCLUDE_PREFIX = (
    "AWAKE/tools/_reranker_survey/dl/",   # 2.2 GB 下载模型
    "AWAKE/tools/out/",                    # 试件产物
)
TOOLS_EXCLUDE_SUBSTR = ("/native/",)       # onnxruntime 原生 dll

EXCLUDE_PREFIX = (
    "AWAKE/artifacts/",                       # 可再生的打包/部署备份
    "AWAKE/_backup_dist_runtime_20260917_1125/",
    "AWAKE/AssetSources/",                    # 原始暂存（待甲方判）
    "_wtest_ps/",                             # PowerShell 写入测试件
)
EXCLUDE_EXACT = {
    "AWAKE/_orig_tax_tmp.json",
    "AWAKE/_wb_validate.txt",
    "AWAKE/_wb_validate_v2.txt",
    "_commit_msg_20260918.txt",
    "_commitmsg-20260915-audit.txt",
    "_commitmsg-20260915-rag.txt",
    "_verify_out.txt",
    "_village_inventory_20260913.json",
    "前置：（那份结论，本文复核它）。",
    "起因：甲方「按你说的增长点，你打算怎么做？」",
}


def main():
    items = untracked()
    mirror, docs, gui, tools, skipped = [], [], [], [], []

    for p in items:
        if p in EXCLUDE_EXACT or p.startswith(EXCLUDE_PREFIX):
            skipped.append(p)
            continue
        if p.startswith(MIRROR):
            mirror.append(p)
        elif p.startswith("AWAKE/docs/"):
            if p in DOCS_EXCLUDE:
                skipped.append(p)
            else:
                docs.append(p)
        elif p.startswith("AWAKE/GUI/"):
            gui.append(p)
        elif p.startswith("AWAKE/tools/"):
            if p.startswith(TOOLS_EXCLUDE_PREFIX) or any(s in p for s in TOOLS_EXCLUDE_SUBSTR):
                skipped.append(p)
            else:
                tools.append(p)
        else:
            skipped.append(p)  # 仓库根散件等

    buckets = {
        "3-mirror": mirror,
        "4-docs": docs,
        "5-gui": gui,
        "6-tools": tools,
    }
    for name, files in buckets.items():
        fp = OUT / f"_bucket_{name}.txt"
        fp.write_bytes("\0".join(files).encode("utf-8"))
        print(f"{name:10s} {len(files):4d}  -> {fp.name}")

    fp = OUT / "_bucket_excluded.txt"
    fp.write_text("\n".join(sorted(skipped)), encoding="utf-8")
    print(f"{'excluded':10s} {len(skipped):4d}  -> {fp.name}")
    print(f"{'TOTAL':10s} {len(items):4d}")


if __name__ == "__main__":
    main()
