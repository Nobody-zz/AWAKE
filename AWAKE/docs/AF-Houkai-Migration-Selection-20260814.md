# 斯拉涅斯之拥 · AF 内容迁移勾选清单

> 日期：2026-08-14
> 上游清单：`AF-Houkai-Content-Inventory-20260814.md`
> 用途：迁移前按文件勾选。本文只做选择与排期，不写实现代码。
> 勾选规则：`[x]` 表示选定，`[ ]` 表示未定。
> 状态更新：2026-08-14，齁改AF撤项，`AnimusForgeHoukai` 转为只读整合来源；本清单升级为整合执行清单，完整阶段计划见 `AF-STANDALONE-MERGE-PLAN-20260814.md`。

## 1. 四个拍板项：建议

| 拍板项 | 建议 | 理由 |
| --- | --- | --- |
| 388 条事件是否全量搬 | 全量入库，分批接入 | 正文/选项/Aftermath 是纯文案资产，入库成本低；机制接入按 P0 批次逐步打开，避免一口吃成胖子 |
| 世界书四档是否合并 | 保留四档分档，第一波只接 clean | 四档是文风承诺，合并会丢掉差异；先以 clean（炫压抑）做默认知识，dark/extreme/bloody 作为后续档位切换语料 |
| 炼金/禁忌是否进第一波 | 只入库文案，不进玩法 | 炼金工作台与禁忌仪式需要独立闭环 UI 和账本，不应与身体/发情/神谕争第一波机制资源 |
| 俘虏三块是否合并成“权力关系” | 合并 | 俘虏/玩家被俘/营地三块触发语境重叠，统一 captor/captive 状态机可避免三套重复逻辑，也更利于男女视角分支 |

## 2. 直接复用（✅）

> 勾选表示第一波就复制内容；复制后仍需在斯拉涅斯之拥侧建立对应内容库。

- [x] 女神人格：`goddess_slanesh.txt`
- [x] 效果解析器人格：`goddess_analyst.txt`
- [x] 六条世界铁律与七文化知识
- [x] `BodyBalance.json`：9 分区、7 档位、开发公式
- [x] `HeatStagePrompts.json`：7 天周期 5 阶段文案
- [x] `CultureAliases.json`
- [x] 炼金配方文案与档位定价表（仅文案）

### 事件正文/选项/Aftermath（388 条）

> 建议全部 `[x]` 入库；运行时启用按第 4 节批次。

- [x] `alchemy.json` 炼金（7）
- [x] `captivity.json` 俘虏（36）
- [x] `divine.json` 神谕（6）
- [x] `divine_taboo.json` 禁忌（8）
- [x] `heat_stage.json` 发情阶段（4）
- [x] `indulgence_month.json` 放纵月（2）
- [x] `sexual_power.json` 性即权力（3）
- [x] `global_heat.json` 插件式定义（12）
- [x] CulturalEvents 七文化（91）
- [x] EroticEvents global + camp + 七文化（157）
- [x] SystemEvents 七类（62）

## 3. 要重建（🔧）

> 不复制 AF 实现，只复用机制设计与数值口径。

- [x] 事件引擎数据驱动化：当前 Slaanesh 事件是代码硬编码，改为 JSON 内容库 + 运行时启用开关
- [x] 身体/发情状态机：接 `body.develop`、`estrus.tick` 命令
- [x] 世界效果结算：金币/声望/影响力/士气/技能/关系改走马库斯命令 + 日限
- [x] 女神命令解析：GoddessAnalyst 改为马库斯 allowlist 命令
- [x] UI 重建：女神面板、身体面板、事件弹窗按 Slaanesh Gauntlet 结构重做
- [ ] 四档世界书切换：第一波只做 clean 数据，切换 UI 后置
- [ ] 权力关系完整系统：先入内容，完整 captor/captive 状态机后置

## 4. 第一波建议范围

1. 事件内容库全量入库，运行时只启用发情/身体/神谕/放纵月批次。
2. 知识库：clean 档 rules + personality 抽入 `slaanesh_knowledge.json` 或新 corpus。
3. 机制：事件引擎数据驱动骨架 + body/estrus 命令对接 + 女神 persona 提示词。
4. 不做：炼金/禁忌工作台、四档切换、权力关系完整版。

## 5. 不迁移（❌）

- [ ] AF/爱与恨反射、Harmony、状态桥、插件事件注册
- [ ] Standalone + Adaptive 双模式判定
- [ ] AF 记忆/周报桥（改为马库斯存储与 Timeline）

## 6. 待定项

- [ ] 388 条全量入库是否按本清单执行
- [ ] 世界书第一波是否只接 clean
- [ ] 炼金/禁忌是否确认后置
- [ ] 俘虏三块是否确认合并为“权力关系”
