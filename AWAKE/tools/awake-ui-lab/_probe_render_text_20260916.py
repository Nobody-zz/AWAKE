"""_probe_render_text_20260916.py —— 真渲染模式下「文字从哪来、解成了什么」的探针。

为什么要它：`display_text()` 对 `Text="@属性名"` 的处理是**按属性名查本地化**，
同名绑定在不同面板里可能指向不同字符串 ⇒ 渲染图上的文字有串号风险。
渲染图看起来越像真的，串号越危险（没人会怀疑它）。所以把每条文本的
「原始属性 → 解出的值」摊开看一遍。

只读，不改任何 Prefab。
用法：
  python _probe_render_text_20260916.py NpcDialogue.xml
"""
import io
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "preview"))

import preview_prefab_geometry as geo  # noqa: E402


def main():
    names = sys.argv[1:] or ["NpcDialogue.xml"]
    prefabs = os.path.normpath(os.path.join(HERE, "..", "..", "GUI", "Prefabs"))
    out = os.path.join(HERE, "out")
    geo.init_metrics(prefabs)
    geo.init_atlas()
    collect = {}
    geo.build(prefabs, out, 3, names, render=False, collect=collect)

    for fn in names:
        if fn not in collect:
            print("!! 没有几何：%s" % fn)
            continue
        print("\n=== %s ===" % fn)
        for e in collect[fn]["entries"]:
            if not geo.is_text(e.node.tag):
                continue
            a = e.node.attrs
            raw = a.get("Text") or a.get("RealText") or a.get("Brush.Text") or ""
            shown = geo.display_text(e.node)
            kind = "绑定" if raw.startswith("@") else ("字面量" if raw else "空")
            mark = "  " if geo.is_known_text(shown) else "✗ "
            print("%s%-28s %-6s %-34r -> %r"
                  % (mark, e.node.tag.split(".")[-1], kind, raw,
                     shown if geo.is_known_text(shown) else None))


if __name__ == "__main__":
    main()
