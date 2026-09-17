# -*- coding: utf-8 -*-
"""定点改 docs/DECISION-20260917-两条通道怎么合.md（含全角引号，Edit 工具锚不上，故用行号法）。

用途：把 §7.5 尾段那条"给词条补口语说法"的修法整段标记为撤回（甲方 09-17 二次裁定）。
做法：按"锚句"定位行号，整段替换；找不到就报错退出，不静默。
"""
import io
import sys

PATH = r"D:\AWAKE-Dev\AWAKE\docs\DECISION-20260917-两条通道怎么合.md"

NEW_BLOCK = """⇒ ~~**两条真短板的修法在内容侧，不在代码侧**：给目标条目补**口语说法**~~ —— **⚠️ 09-17 二次裁定：这整段修法撤回**，
甲方原话：「**不让进本来就不应该成提示词，好马被多个词条占据也是正常现象**」。理由见 §7.10：

- **拿自造的题去改语料，等于把答案背下来。** 题集自己写着"写问法时只读了 title/summary"，它不是玩家问法的样本，
  凭什么用它的措辞去改 448 条条目？
- `好马` 那一处是**靶子任意**：全库地理条目里 **50 条**正文提到马、多条在讲养马（俄尼拉＝"东部平原的养马业中心"／
  泽翁尼卡＝"盛产美酒谷物骏马"／巴里哈勒／阿萨利格／拜特·哈提夫…）⇒ 补上"好马"也只是让拉迈萨变成**第 8 个**，
  不解决任何问题。
- `不让进` 那一处是**拿题面词当索引词**：该词的出处是出题人自己的措辞，不是语料里会出现的说法。
"""

with io.open(PATH, "r", encoding="utf-8") as fh:
    lines = fh.read().split("\n")

anchor = "两条真短板的修法在内容侧"
hits = [i for i, ln in enumerate(lines) if anchor in ln]
if len(hits) != 1:
    sys.exit("锚句命中 %d 次，需要恰好 1 次：%r" % (len(hits), anchor))

start = hits[0]
# 原段共 3 行（第 3 行是"补 summary 进索引把字面 B 组从 0 抬到 7"那句）
end = start + 3
print("替换行区间：%d..%d" % (start + 1, end))
print("原第 1 行前 40 字：" + lines[start][:40])
print("原第 3 行前 40 字：" + lines[end - 1][:40])

lines[start:end] = NEW_BLOCK.split("\n")

with io.open(PATH, "w", encoding="utf-8", newline="\n") as fh:
    fh.write("\n".join(lines))
print("已写回，共 %d 行" % len(lines))
