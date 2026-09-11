# 可复制性验证：第二个聚落（加伦 / Galend）——2026-09-10

> 目的：验证「官方文本 → 来源登记 → 真实本地生成 → 期望对照 → 建档」这条流程不是为帕拉汶德特调。

## 1. 选例

- 聚落：**加伦（Galend）**，`town_V5`，瓦兰迪亚，归属 `clan_vlandia_1`，坐标 (107.5, 411.689)。
- 官方中文文本（`SandBox/.../CNs/std_settlements_xml-zho-CN.xml`，键 `79R6gi4l`）：
  > 加伦遥望着浪蚀的比斯坎海岸。无畏的比斯坎渔民在出航深入西海时，温顺的绵羊在迷雾缭绕的山峦上吃草时，都能瞥见加伦。如今的瓦兰迪亚国王一脉就出自“铁壁”奥斯里克入侵时就定居在这片地区的家族。
- 夹具：`tests/fixtures/official-reference/galend-cluster/`（摘录 txt + `expected-facts.v1.json`）。

## 2. 复用方式（无新代码路径）

- `scripts/real-worker-pravend-smoke.ps1` 已参数化：`-CaseName`（用例名，用于候选/事实 id 前缀）、`-SourceFile`（官方摘录）、`-EvidencePath`（证据输出）。
- `scripts/pravend-expected-facts-check.ps1` 已参数化：`-ClusterPath` + `-EvidencePath`，期望事实从 `expected.source_extract` 读取来源文件。
- 地点锚点自动来自 `docs/mappings/persona-entity`（本次命中 `entity.settlement.town_v5`）。

## 3. 结果

| 检查 | 结果 |
|---|---|
| 生成 | 2 个候选；`status=needs_review`；`era=current`；建档成功 |
| 期望事实覆盖 | 3/3（比斯坎海岸 / 渔民+绵羊 / 瓦兰迪亚国王+奥斯里克） |
| 禁止项 | 年份、正式实体 ID、战争、以及**帕拉汶德/弗雷吉昂跨例污染** 全部未出现 |
| 状态与证据 | 全部 pending、引文逐字可定位、事实非照抄、无占位表达 |
| 地点锚点 | `entity.settlement.town_v5` 出现且能在映射中解析 |
| **合计** | **15/15 PASS** |

回归：帕拉汶德默认用例仍 **19/19 PASS**。

## 4. 结论与经验

- 流程可复制：换来源、换聚落、换期望事实即可复跑，无需新增代码路径。
- 观察：第二个候选（历史沿革）没有锚点——因为该段官方文本没有再次提到“加伦”，锚点匹配基于名称/别名出现，属预期行为；若希望“同簇共享锚点”，可在作者侧手动补锚点（编辑器已支持 `entity_ids`）。
- 待办（沿用 backlog）：R5 runtime 侧消费 `entity.settlement.*`；R6 把“一个主题拆成若干文档”的做法固化为编辑器引导。
