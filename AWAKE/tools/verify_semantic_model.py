# -*- coding: utf-8 -*-
"""校验游戏目录里的本地语义模型是不是登记在册的那一份。

背景：这套模型件原本是从本机某个第三方模组的安装目录拷来的 —— 没有版本、没有校验依据。
2026-09-17 换成有公开出处的一份（见同目录 semantic-model-provenance.json）。

用法（不传参就找默认游戏目录）：
    python tools/verify_semantic_model.py
    python tools/verify_semantic_model.py "<模型目录>"

退出码：0 = 全部符合登记；1 = 有件缺失/大小或哈希不符（逐件列出）。
"""
import hashlib
import json
import os
import sys

sys.stdout.reconfigure(encoding="utf-8")

HERE = os.path.dirname(os.path.abspath(__file__))
REGISTRY = os.path.join(HERE, "semantic-model-provenance.json")

DEFAULT_GAME = r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord"
DEFAULT_TAIL = os.path.join("Modules", "AWAKE", "bin", "Win64_Shipping_Client",
                            "Runtime", "models", "bge-small-zh-v1.5")


def sha256(path, chunk=1 << 20):
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        while True:
            b = fh.read(chunk)
            if not b:
                break
            h.update(b)
    return h.hexdigest()


def main():
    if len(sys.argv) > 1:
        target = sys.argv[1]
    else:
        target = os.path.join(DEFAULT_GAME, DEFAULT_TAIL)

    with open(REGISTRY, "r", encoding="utf-8") as fh:
        registry = json.load(fh)

    source = registry["source"]
    print("登记来源 = %s @ %s" % (source["repo"], source["commit"]))
    print("被查目录 = %s" % target)
    print()

    if not os.path.isdir(target):
        print("** 目录不存在 **")
        return 1

    bad = 0
    print("%-24s %-6s %12s %-18s %s" % ("文件", "要求", "实际大小", "实际 sha256", "判定"))
    print("-" * 92)
    for entry in registry["files"]:
        name = entry["name"]
        path = os.path.join(target, name)
        required = entry["required"]
        if not os.path.exists(path):
            mark = "缺失" if required else "缺失(非必需)"
            if required:
                bad += 1
            print("%-24s %-6s %12s %-18s %s" % (name, "必需" if required else "留存", "-", "-", mark))
            continue
        size = os.path.getsize(path)
        digest = sha256(path)
        size_ok = size == entry["size"]
        hash_ok = digest == entry["sha256"]
        if not hash_ok and required:
            bad += 1
        print("%-24s %-6s %12d %-18s %s" % (
            name, "必需" if required else "留存", size, digest[:16] + "...",
            "符合" if (size_ok and hash_ok) else ("大小不符" if not size_ok else "**哈希不符**")))

    print()
    for entry in registry.get("obsolete", []):
        path = os.path.join(target, entry["name"])
        if os.path.exists(path):
            print("注意：目录里还留着已登记为冗余的 %s（%d B）—— %s"
                  % (entry["name"], os.path.getsize(path), entry["note"]))

    print()
    if bad:
        print("RESULT FAIL 有 %d 件必需文件不符合登记。" % bad)
        return 1
    print("RESULT PASS 必需文件全部符合登记。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
