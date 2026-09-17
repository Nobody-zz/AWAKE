# -*- coding: utf-8 -*-
"""上线件的第三方组件与许可声明：**体检** ＋ **渲染**。

为什么要有这个脚本：声明文件最大的失败模式不是写错，是**漏件**——
以后往 Runtime/ 里添一个 dll，没人会记得回去改声明。
所以这里的默认动作是「把发布目录整个扫一遍，逐件归类，有归不进去的就报红」，
渲染只是它的一个副产物。

用法
  python tools/third_party_notices.py                     # 体检（找默认游戏目录）
  python tools/third_party_notices.py "<Runtime 目录>"     # 体检指定目录
  python tools/third_party_notices.py --render            # 体检并写出 THIRD-PARTY-NOTICES.txt
  python tools/third_party_notices.py --check             # 体检 ＋ 确认盘上那份声明没落后（CI 用）

退出码：0 = 全归类且（--check 时）声明是最新的；1 = 有未归类件 / 声明过期 / 用法错。
"""
import fnmatch
import hashlib
import json
import os
import sys

sys.stdout.reconfigure(encoding="utf-8")

HERE = os.path.dirname(os.path.abspath(__file__))
REGISTRY = os.path.join(HERE, "third-party-notices.json")
LICENSE_DIR = os.path.join(HERE, "third-party-licenses")
AWakeRoot = os.path.dirname(HERE)
NOTICE_OUT = os.path.join(AWakeRoot, "THIRD-PARTY-NOTICES.txt")

DEFAULT_GAME = r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord"
DEFAULT_TAIL = os.path.join("Modules", "AWAKE", "bin", "Win64_Shipping_Client", "Runtime")

# 生成文件里不展开许可全文的（正文太长，指向 URL 即可）
PLACEHOLDER_NOTE = (
    "许可正文中的 `<year> <copyright holders>` / `<year> <owner>` 为占位符，"
    "对应上表各组件登记的著作权人与其版权年份；各件元数据未声明年份的，不代填。"
)


def load_registry():
    with open(REGISTRY, "r", encoding="utf-8") as fh:
        return json.load(fh)


def scan(root):
    out = []
    for dirpath, _dirnames, filenames in os.walk(root):
        for name in filenames:
            full = os.path.join(dirpath, name)
            rel = os.path.relpath(full, root).replace("\\", "/")
            out.append((rel, full))
    return sorted(out)


def classify(rel, components):
    for comp in components:
        for pat in comp["files"]:
            if fnmatch.fnmatch(rel, pat):
                return comp
    return None


def verify(registry, root, verbose=True):
    components = registry["components"]
    files = scan(root)
    unclassified = []
    counts = {c["id"]: 0 for c in components}
    for rel, _full in files:
        comp = classify(rel, components)
        if comp is None:
            unclassified.append(rel)
        else:
            counts[comp["id"]] += 1

    if verbose:
        print("发布目录 = %s" % root)
        print("文件总数 = %d" % len(files))
        print()
        print("%-34s %-9s %-14s %6s" % ("组件", "版本", "许可", "命中件数"))
        print("-" * 78)
        for comp in components:
            print("%-34s %-9s %-14s %6d%s" % (
                comp["name"][:32], str(comp["version"])[:9], comp["license"],
                counts[comp["id"]],
                "" if comp.get("render", True) is not False else "   (不渲染)"))
        print()
        if unclassified:
            print("** 有 %d 件归不进去（声明会漏）**：" % len(unclassified))
            for rel in unclassified:
                print("    %s" % rel)
        stale = [c["id"] for c in components if counts[c["id"]] == 0]
        if stale:
            print("注意：登记了但一件都没命中的条目（可能已淘汰）：%s" % ", ".join(stale))
        if not unclassified:
            print("全部文件均已归类。")
    return files, unclassified, counts


def render(registry):
    components = [c for c in registry["components"] if c.get("render", True) is not False]
    lines = []
    w = lines.append
    w("AWAKE — 第三方组件与许可声明")
    w("=" * 78)
    w("")
    w("本文件随模组一起分发，列出 `bin/Win64_Shipping_Client/Runtime/` 下用到的全部第三方组件")
    w("及其许可。生成于 %s；由 `tools/third_party_notices.py --render` 自动产出，" % registry["updated"])
    w("请改 `tools/third-party-notices.json` 而不是直接改本文件。")
    w("")
    w("说明：Runtime/ 是自包含发布目录，里面除本项目自有代码（MarcusAwake*）外，")
    w("还有 .NET 运行时、ONNX Runtime、分词与存储相关库，以及本地嵌入模型。")
    w("")
    w("-" * 78)
    w("一、清单")
    w("-" * 78)
    w("")
    for comp in components:
        w("%s" % comp["name"])
        w("    版本    : %s" % comp["version"])
        w("    著作权人: %s" % comp["author"])
        w("    许可    : %s" % comp["license"])
        if comp.get("url"):
            w("    出处    : %s" % comp["url"])
        if comp.get("license_note"):
            w("    许可说明: %s" % comp["license_note"])
        w("    取证    : %s" % comp["evidence"])
        w("")
    w("-" * 78)
    w("二、许可全文")
    w("-" * 78)
    w("")
    w(PLACEHOLDER_NOTE)
    w("")

    # 同一份正文只印一次，列出它覆盖哪些组件
    by_text = {}
    for comp in components:
        name = comp.get("license_text")
        if not name:
            continue
        by_text.setdefault(name, []).append(comp)

    order = ["MIT.txt", "BSD-3-Clause.txt", "Apache-2.0.txt"]
    for name in order + [n for n in by_text if n not in order]:
        if name not in by_text:
            continue
        path = os.path.join(LICENSE_DIR, name)
        with open(path, "rb") as fh:
            body = fh.read()
        digest = hashlib.sha256(body).hexdigest()
        text = body.decode("utf-8").replace("\r\n", "\n").rstrip("\n")
        w("=" * 78)
        w("### %s" % name)
        w("=" * 78)
        w("")
        w("适用组件：")
        for comp in by_text[name]:
            w("  - %s（%s）—— %s" % (comp["name"], comp["version"], comp["author"]))
        w("")
        w("正文来源：SPDX License List 官方文本 `%s`" % name)
        w("          sha256 %s" % digest)
        w("")
        w(text)
        w("")
        w("")

    return "\n".join(lines).rstrip("\n") + "\n"


def main():
    argv = [a for a in sys.argv[1:]]
    do_render = "--render" in argv
    do_check = "--check" in argv
    paths = [a for a in argv if not a.startswith("--")]
    if len(paths) > 1:
        print("用法：third_party_notices.py [--render|--check] [<Runtime 目录>]")
        return 1

    root = paths[0] if paths else os.path.join(DEFAULT_GAME, DEFAULT_TAIL)
    if not os.path.isdir(root):
        print("** 目录不存在：%s **" % root)
        return 1

    registry = load_registry()
    _files, unclassified, _counts = verify(registry, root)
    print()

    want = render(registry)

    if do_render:
        with open(NOTICE_OUT, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(want)
        print("已写出 %s（%d 字节）" % (NOTICE_OUT, len(want.encode("utf-8"))))

    if do_check:
        if not os.path.exists(NOTICE_OUT):
            print("** 声明文件不存在：%s **" % NOTICE_OUT)
            return 1
        have = open(NOTICE_OUT, "rb").read().decode("utf-8")
        if have != want:
            print("** 盘上那份声明与登记不一致（改过 registry 忘了重出，或手工改过文件）**")
            return 1
        print("声明文件与登记一致。")

    return 1 if unclassified else 0


if __name__ == "__main__":
    raise SystemExit(main())
