"""_probe_color_order_20260915.py —— 把 Lab 的 color_channel_order 命中摊成一张可执行的修正清单。

用途：docs/UI-REF-ALICEMM-20260915.md §4 的附证。读 out/ui-report.v1.json，
只为 `color_channel_order` 这一条规则生成「文件 / 行号 / 现值 / 应为」四列。

修法（本库口径）：8 位色末字节必须 FF，不透明度交给 AlphaFactor 管。
所以 `#FFxxxxxx` ——> `#xxxxxxFF`；末字节已 FF 的原样不动。

⚠️ 两个口径要先知道，否则会白找：
  1. **行号是控件的起始行**，不是色值所在行 —— 控件写成多行时，色值在它下面 1~2 行。
  2. 被 `<AwakePortraitSlot>` 这种**子 Prefab 展开**进来的问题，会**同时**出现在宿主面板
     （NpcDialogue.xml）和自己的文件里 ⇒ 同一处会被数两遍。看数字时记得去重：
     55 条 = 45 处真实（其中 1 处挂在逻辑控件上）+ 10 处 slot 重复计入。

⚠️ 只是清单，**不改文件**。

跑法： python _probe_color_order_20260915.py
"""
import json
import os
import re
import sys
from collections import OrderedDict

sys.stdout.reconfigure(encoding="utf-8")

HERE = os.path.dirname(os.path.abspath(__file__))
REPORT = os.path.join(HERE, "out", "ui-report.v1.json")
VAL = re.compile(r"=#([0-9A-Fa-f]{8})\b")


def suggested(hex8):
    """`#FFxxxxxx` -> `#xxxxxxFF`。"""
    body = hex8.lstrip("#")
    if body[:2].upper() == "FF":
        return "#" + body[2:] + "FF"
    return None


def main():
    if not os.path.exists(REPORT):
        print("找不到 %s —— 先在 tools/awake-ui-lab 跑一次 awake_ui.py check" % REPORT)
        return 1

    with open(REPORT, encoding="utf-8") as fh:
        data = json.load(fh)

    by_file = OrderedDict()
    for prefab in data.get("prefabs", []):
        for issue in prefab.get("issues", []):
            if issue.get("rule") != "color_channel_order":
                continue
            m = VAL.search(issue.get("message", ""))
            cur = "#" + m.group(1) if m else "?"
            by_file.setdefault(prefab.get("name") or prefab.get("file") or "?", []).append(
                (issue.get("line"), issue.get("attr"), cur, suggested(cur), issue.get("widget"))
            )

    grand = 0
    n_files = 0
    for fname, rows in by_file.items():
        rows.sort(key=lambda r: (r[0] or 0))
        n_files += 1
        grand += len(rows)
        print("## %s  （%d 处）" % (fname, len(rows)))
        for line, attr, cur, sug, widget in rows:
            print("   L%-5s %-16s %s -> %s   [%s]" % (line, attr, cur, sug or "请人工判", widget))
        print()

    print("合计 %d 个文件 / %d 处（Lab 规则 color_channel_order，全库问题 %s）"
          % (n_files, grand, data.get("summary", {}).get("issue_total")))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
