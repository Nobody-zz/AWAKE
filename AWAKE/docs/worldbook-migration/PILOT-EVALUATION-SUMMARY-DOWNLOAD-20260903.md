# 30 个试点文件评价

## 评价范围

- 权威来源：`C:\Users\26811\Downloads\卡拉迪亚编年史\卡拉迪亚编年史`
- 批次：`pilot.awake.worldbook.download-20260903.geo-rules-30`
- 候选包：`C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\worldbook-migration\pilot-authoring-v1-candidate-download-20260903`
- 评价性质：只读评审；不代表人工批准、canon、编译或发布。

## 总体结论

**内容试点评级：橙色。**

这 30 个文件适合作为迁移模型试点，但不适合直接编译或发布。主要原因不是 JSON 或 Authoring v1 基础结构，而是：

1. 30 个文件共有 248 个旧变体，223 个带条件；旧 `When` 同时承载视角、身份、权限、技能和状态信息。
2. 22 个文件需要先拆分或重组，不能把一个旧文件直接视为一个完整 assertion。
3. 6 个文件含 `TextMappings`，涉及 kingdom/settlement/region/troop/leader 等绑定，不能凭显示名自动建立 entity registry。
4. 9 个文件的启发式主领域不是 geography，需要人工决定是否改归 politics、economy、culture 或 war。
5. 3 个文件含文件内重复变体。
6. `rule_吕卡隆` 含占位符信号，`rule_攻城塔` 含疑似截断信号。
7. 所有候选共用的 source registry 仍是 `unknown`，Studio 正确阻止进入正典；这属于包级门控，不重复计入每个文件的内容评级。

## 数量统计

| 指标 | 数量 |
|---|---:|
| 试点文件 | 30 |
| 旧变体总数 | 248 |
| 非空旧变体 | 248 |
| 生成的候选表达 | 248 |
| RAG 摘要 | 44 |
| 关键词 | 57 |
| 带条件的变体 | 223 |
| TextMappings | 11 |
| 变体重复文件 | 3 |
| RAG 超过 100 字文件 | 0 |
| 需要拆分的文件 | 22 |
| 需要领域复核的文件 | 9 |
| Authoring v1 文档 schema 错误 | 0 |
| Studio 来源门控错误 | 308 |

## 文件评级

文件评级不把共同的 source registry 门控混入内容评级。

### 橙色：先清理再继续

| 文件 | 主要问题 | 建议 |
|---|---|---|
| `knowledge/rules/rule_吕卡隆__吕卡隆.json` | 12 个变体、11 个带条件、3 个 TextMappings、占位符信号、主领域偏 politics | 先拆分政治事实与地理/经济关联，核对占位符和 leader/status 映射 |
| `knowledge/rules/rule_塞堤斯河__塞堤斯河.json` | 8 个变体，存在重复变体 | 先去重，再区分河流地理、文化视角和技能/身份访问条件 |
| `knowledge/rules/rule_攻城塔__攻城塔.json` | 疑似截断/占位，3 个变体 | 回到源文件修复或确认正文完整性后再迁移 |
| `knowledge/rules/rule_贝恩兰岛__贝恩兰岛.json` | 4 个变体，存在重复变体 | 确认重复是否为合法视角变体；不要直接保留两份相同表达 |
| `knowledge/rules/rule_车尔特格山__车尔特格山.json` | 2 个变体，正文重复 | 合并重复正文，并确认两个条件是否真的产生不同访问层 |

### 黄色：可进入人工编辑，但必须先确认边界

其余 25 个文件没有新的硬性内容阻断，但仍有领域、条件、拆分或 universe/era 未决项。22 个拆分候选为：

```text
rule_卡恰尔半岛
rule_厄吕特律斯山
rule_吕卡里亚
rule_坎尼人的王国
rule_塔奈西斯湖
rule_奥尼石山
rule_巴旦尼亚地理
rule_帝国风土人情
rule_库赛特地理
rule_德里亚特
rule_悲伤的肖农
rule_拉革塔
rule_斯特吉亚风土人情
rule_泰瓦尔湖
rule_潘德拉克战役
rule_珀拉斯海
rule_纳哈撒沙漠
rule_诺德皇家侍卫
rule_阿塞莱地理
rule_阿塞莱风土人情
rule_巴旦尼亚水之女神
rule_阿塞莱地理
```

上表中 `rule_阿塞莱地理` 在原候选清单中只应计一次；最终逐文件权威清单以 JSON 评价报告为准。

6 个相对适合先做人工 assertion 复核的文件是：

```text
rule_拉科尼斯湖
rule_攻城锤
rule_沙拉斯湾
rule_贝恩兰岛
rule_车尔特格山
rule_黎明山脉
```

注意：`贝恩兰岛` 和 `车尔特格山` 虽然结构较小，但因存在重复变体，仍属于橙色清理项。

## 领域归类问题

以下 9 个文件的启发式主领域不是 geography：

| 文件 | 当前主领域候选 | 需要复核的方向 |
|---|---|---|
| `rule_吕卡隆` | politics | 政治身份、聚落归属、领袖动态可能需要拆成多个 assertion |
| `rule_坎尼人的王国` | politics | 历史王国、海域地理、贸易经济和文化遗产分层 |
| `rule_奥尼石山` | economy | 资源分布归 geography，资源交易归 economy |
| `rule_巴旦尼亚水之女神` | culture | 宗教文化与地点/水域地理分开 |
| `rule_帝国风土人情` | politics | 政治制度与文化习俗分开 |
| `rule_悲伤的肖农` | economy | 先确认人物、地点还是经济主题 |
| `rule_拉革塔` | politics | 人物身份、政治关系与地理位置分开 |
| `rule_潘德拉克战役` | politics | 战争历史应优先评估为 war，政治后果另拆 |
| `rule_阿塞莱风土人情` | economy | 文化、经济和政治制度不要合并为同一 assertion |

## 迁移候选质量

### 做得好的部分

- 30 个候选文档均能解析。
- 30 个候选文档均使用 `awake.worldbook.authoring.v1`。
- 没有 `WB-SCHEMA-*` 错误。
- 248 个非空旧变体全部被保留为 248 个 source-backed expression candidate。
- 没有自动写入 grants 或 denies。
- 没有自动创建 entity/profile/referral registry。
- 没有自动标记 canon 或 approved。
- 原始内容通过 source bundle 保存，来源 hash 可回核。

### 不能接受为发布状态的部分

- expression 层全部是 `unknown`，需要人工决定 rumor/summary/detail/secret。
- grants/denies 全部为空，当前只能表示 unknown，不表示公开可读。
- `content_tier: base` 只是候选字段，仍为 `pending_author_review`，不能视为已批准的 base。
- source registry 的 `content_tier`、`license_status`、`use_status` 均为 unknown。
- universe 与 era 未确定。
- 旧 `When` 尚未转换为 Studio permission rule。
- entity/profile/ID ledger 未绑定。
- Studio 总体验证为 blocked，不能 compile/export。

## 结论与优先级

### P1

1. 修复或确认 `rule_攻城塔` 的源正文。
2. 处理 `rule_吕卡隆` 的占位符和 TextMappings。
3. 清理 `rule_塞堤斯河`、`rule_贝恩兰岛`、`rule_车尔特格山` 的重复变体。
4. 处理 `rule_巴旦尼亚水之女神` 与其他文件的重复旧 ID 决策。

### P2

1. 先用 22 个拆分候选验证“事实 assertion → 多表达 expression → 权限 grants/denies”的拆分模型。
2. 对 6 个 TextMappings 文件建立人工 entity 映射表。
3. 对 9 个跨领域文件重新确认主 domain/subdomain。

### P3

1. 决定 universe/era。
2. 审核关键词与 RAG 的检索职责。
3. 选择 expression layer。
4. 再决定 content tier 和 source registry 许可状态。

**最终评价：**这 30 个文件作为“迁移试点”是合格的，作为“可发布世界书”是不合格的；当前最有价值的产出是拆分模型、来源链和阻断证据，而不是立即扩大迁移数量。
