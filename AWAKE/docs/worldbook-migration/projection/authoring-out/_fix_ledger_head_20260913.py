# -*- coding: utf-8 -*-
"""台账坏字节修复 v2：注记节放在 引言之后、总览表之前，总览表整表保完整。
源 = HEAD 干净全文 + 工作副本中的批次进度注记节（字节级干净，仅位置放对）。
"""
import subprocess, io

P = r"d:/AWAKE-Dev/AWAKE/docs/worldbook-migration/WORLDBOOK-CHRONICLE-LEDGER-20260912.md"
head = subprocess.run(
    ["git", "-C", r"d:/AWAKE-Dev", "show",
     "HEAD:AWAKE/docs/worldbook-migration/WORLDBOOK-CHRONICLE-LEDGER-20260912.md"],
    capture_output=True).stdout.decode("utf-8")
work = open(P, "rb").read().decode("utf-8", errors="replace")

# B = 批次进度注记节（从标题行到"素材已被上述两档与 §六 消化。"行止）
ib = work.index("## 批次进度注记")
B = work[ib:work.index("\n", work.index("素材已被上述两档与 §六 消化。"))].rstrip()
assert B.startswith("## 批次进度注记") and B.endswith("消化。")

# head 拆两段：引言（标题+3行引文）与 总览表起的其余正文
it = head.index("## 一、总览")
intro = head[:it].rstrip()          # 标题 + 性质/数据源/机器件引言
rest = head[it:]                    # ## 一、总览 … 文件尾（整表完整）
assert rest.startswith("## 一、总览") and "五域草判 | war 165" in rest and "| 官方交叉 |" in rest

final = intro + "\n\n---\n\n" + B + "\n\n" + rest

raw = final.encode("utf-8")
raw.decode("utf-8")  # 非法字节即抛
for needle in ["# 编年史第三步", "## 批次进度注记", "## 一、总览",
               "| 五域草判 | war 165 · politics 64 · culture 56 · geography 28 · economy 24 |",
               "| 官方交叉 |", "## 二、Ⅰ锚定 107 条"]:
    assert needle in final, needle
assert final.count("## 批次进度注记") == 1
# 总览表五行连续完整（表头到已知源缺陷行之间不得插入别的内容）
seg = final[final.index("## 一、总览"):final.index("## 二、")]
for row in ["| 条数 |", "| 类型草判 |", "| 五域草判 |", "| 官方交叉 |", "| 已知源缺陷 |"]:
    assert row in seg, row
assert seg.index("| 条数 |") < seg.index("| 已知源缺陷 |")

io.open(P, "w", encoding="utf-8", newline="\n").write(final)
print("REPAIRED v2, chars:", len(final))
