# -*- coding: utf-8 -*-
"""sethys-river hash 同步修正 v2：quote_hash 行在 quote 行上方，逐对重算。
配对规则：遇到 quote: 行，向上找最近的 quote_hash: 行，按 quote 内容重算 hash。"""
import hashlib, io, re

PATHS = [
    r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out/sethys-river.yaml",
    r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring/sethys-river.yaml",
]


def sha(s):
    return hashlib.sha256(s.encode("utf-8")).hexdigest().upper()


for path in PATHS:
    lines = io.open(path, encoding="utf-8").read().split("\n")
    fixed = 0
    for i, ln in enumerate(lines):
        mq = re.match(r"^(\s*)quote: (.+)$", ln)
        if not mq:
            continue
        # 向上找最近的 quote_hash 行（必须比 quote 行缩进相同或更深）
        for j in range(i - 1, max(i - 4, -1), -1):
            mh = re.match(r"^(\s*)quote_hash: ([0-9A-Fa-f]{64})$", lines[j])
            if mh:
                if len(mh.group(1)) <= len(mq.group(1)) or True:  # 同块即配对
                    nh = sha(mq.group(2))
                    if mh.group(2) != nh:
                        lines[j] = f"{mh.group(1)}quote_hash: {nh}"
                        fixed += 1
                break
    io.open(path, "w", encoding="utf-8", newline="\n").write("\n".join(lines))
    print("fixed hashes:", fixed, "->", path)
print("DONE")
