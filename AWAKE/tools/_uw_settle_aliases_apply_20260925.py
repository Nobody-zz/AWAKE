# -*- coding: utf-8 -*-
"""落盘：给 321 个稀薄聚落档补 aliases（村 273 / 镇 48）。
- 只插在 `content_tier: base` 之后、`entity_ids:` 之前（与城堡档/已有档一致）。
- 已有 aliases 的档：**合并去重**，不覆盖。
- 改前先备份整个 authoring 目录到 _archive-20260925-aliases/。
- 幂等：重复跑不重复插。
用法： python _uw_settle_aliases_apply_20260925.py [--apply]
      不带 --apply = 干跑（只打印将要改的档数与前 5 例）
"""
import io, json, os, re, sys, shutil, collections

ROOT = r"D:\AWAKE-Dev\AWAKE"
FULL = os.path.join(ROOT, "tools", "worldbook-studio", "workspace", "full-geo1")
AUTH = os.path.join(FULL, "authoring")
PKG = os.path.join(FULL, "compiled", "geo1-v29-uw-wide")
ARCH = os.path.join(FULL, "_archive-20260925-aliases")

sys.path.insert(0, os.path.join(ROOT, "tools"))
import importlib.util
spec = importlib.util.spec_from_file_location(
    "m", os.path.join(ROOT, "tools", "_uw_settle_aliases_probe_20260925.py"))
M = importlib.util.module_from_spec(spec)
spec.loader.exec_module(M)


def zh(t):
    return t.get("zh-CN") or "" if isinstance(t, dict) else (t or "")


def yaml_aliases_block(zh_al, en_al):
    """生成 aliases 块（2 空格缩进，列表项 2+2）。"""
    lines = ["aliases:", "  zh-CN:"]
    for a in zh_al:
        lines.append("  - %s" % a)
    if en_al:
        lines.append("  en:")
        for a in en_al:
            lines.append("  - %s" % a)
    return "\n".join(lines) + "\n"


def existing_aliases(txt):
    """若已有 aliases 块，返回其文本（含）与结束位置。"""
    m = re.search(r"^aliases:\n(?:[ \t].*\n)*", txt, re.M)
    return m


def main():
    apply = "--apply" in sys.argv
    ents = M.load_entries()
    targets = []
    for x in ents:
        head, zh_al, en_al = M.gen_aliases(x)
        if head is None:
            continue
        k = x.get("keywords") or []
        if len(k) > 3:
            continue
        slug = str(x.get("id")).split("entry:")[-1].split(".", 1)[-1]
        targets.append((slug, head, zh_al, en_al))

    print("待改档 =", len(targets))

    if not apply:
        for slug, head, za, ea in targets[:5]:
            print("---", slug)
            print(yaml_aliases_block(za, ea))
        return

    # 备份
    if os.path.isdir(ARCH):
        shutil.rmtree(ARCH)
    shutil.copytree(AUTH, ARCH)
    print("已备份 ->", ARCH)

    changed = 0
    for slug, head, zh_al, en_al in targets:
        p = os.path.join(AUTH, slug + ".yaml")
        if not os.path.exists(p):
            print("!! 缺档:", slug); continue
        txt = io.open(p, encoding="utf-8").read()
        m = existing_aliases(txt)
        if m:
            # 合并：解析已有 zh-CN / en 列表
            old = m.group(0)
            old_zh = re.findall(r"^  - (.*)$", old, re.M)
            merged_zh = []
            for a in zh_al + old_zh:
                if a and a not in merged_zh:
                    merged_zh.append(a)
            new = yaml_aliases_block(merged_zh, en_al)
            txt2 = txt[:m.start()] + new + txt[m.end():]
        else:
            # 插在 content_tier 行之后
            cm = re.search(r"^content_tier:.*\n", txt, re.M)
            if not cm:
                print("!! 无 content_tier:", slug); continue
            ins = cm.end()
            txt2 = txt[:ins] + yaml_aliases_block(zh_al, en_al) + txt[ins:]
        if txt2 == txt:
            continue
        # revision +1
        rm = re.search(r"^revision:\s*(\d+)\s*$", txt2, re.M)
        if rm:
            txt2 = txt2[:rm.start(1)] + str(int(rm.group(1)) + 1) + txt2[rm.end(1):]
        io.open(p, "w", encoding="utf-8", newline="\n").write(txt2)
        changed += 1
    print("已改档 =", changed)


if __name__ == "__main__":
    main()
