# 齁改AF撤项 · 斯拉涅斯之拥整合计划

> 日期：2026-08-14
> 决策：`AnimusForgeHoukai`（齁改独立版 v0.3.4.0）停止继续开发，改为内容与代码来源；斯拉涅斯之拥成为唯一继续线。
> 定位：斯拉涅斯之拥调整为完整的 AI 世界模组，承接齁改世界观与完整玩法；马库斯为唯一 AI 底层，女神只是入口之一。
> 原则：只带走可复用内容、纯逻辑与设计经验；不把 AF/爱与恨耦合层、反射、Harmony、状态桥带进斯拉涅斯之拥。

## 1. 整合目标

- 保住 388 条事件、四版世界书、女神语料、身体/发情数值等现有资产。
- 保住已被验证的算法：周期计算、身体开发公式、发情阶段、炼金配方、世界效果限幅。
- 保住玩家已经习惯的体验入口：身体面板、事件弹窗、地图周期 HUD、营地与俘虏玩法。
- 全部改为走 MarcusAIFramework：逻辑 Route、受控命令、存储、RAG、权限、跨模组能力。
- 不再维护 Standalone + Adaptive 双模式，不再读 AF/爱与恨状态。

## 2. 直接搬入的内容

| 资产 | 来源 | 去向 |
| --- | --- | --- |
| 事件正文/选项/Aftermath | HoukaiEvents 78 + CulturalEvents 91 + EroticEvents 157 + SystemEvents 62 | 斯拉涅斯之拥事件内容库 |
| 身体开发数值 | BodyBalance.json | 斯拉涅斯之拥 ModuleData |
| 发情阶段文案 | HeatStagePrompts.json | 发情状态与提示词 |
| 文化别名 | CultureAliases.json | 文化判定 |
| 女神人格 | goddess_slanesh.txt | 女神 Prompt |
| 效果解析人格 | goddess_analyst.txt | 女神输出解析 |
| 世界铁律与四版世界书 | PlayerExports | 知识语料，先接 clean |
| 炼金配方与档位定价 | AlchemyCore/ModuleData | 先入库，后接玩法 |
| 语言包文案 | 24 个 CNs XML | 转斯拉涅斯之拥本地化键 |
| UI 布局参考 | BodyPanel、EventPopup、MapCycleHud | 自建 Gauntlet 时参考 |

## 3. 纯逻辑移植

以下代码不依赖 AF，可直接按斯拉涅斯之拥命名空间与存储契约重写：

- `HoukaiCalculator.cs`：周期、阶段、强度、身体开发公式
- `HoukaiBalance.cs`：84 天年、21 天季、7 天周期常量
- `HoukaiModels.cs`：NPC/玩家/女神/俘虏/政治状态模型
- `HoukaiStorageCodec.cs`：JSON 编解码与旧档迁移思路
- `HoukaiStatDescriptions.cs`：玩家可见数值文案
- `HoukaiBodyPackDetector.cs`：身体包检测
- `HoukaiTimedCache.cs`、`HoukaiConflictGuard.cs`：通用工具
- `HoukaiVowService.cs`：承诺体系设计
- `HoukaiAlchemyCore.cs`：配方与档位计算
- `HoukaiDivineCodeTable.cs`：效果白名单表思路

## 4. 需要重建的机制

| AF 实现 | 斯拉涅斯之拥里的重建方式 |
| --- | --- |
| HoukaiEventEngine | 数据驱动事件库 + Marcus 事件/存储 + 冷却/权重/事件链 |
| HoukaiBodyPanel | 3D 身体面板，数据源走 `body_state` 与 `body.develop` |
| HoukaiMapHud | 大地图周期条，数据源走发情周期 |
| HoukaiEventPopup | 事件弹窗，数据源走事件引擎 |
| HoukaiWorldEffectBridge | 金币/声望/影响力/士气/技能/关系命令 + 日限 |
| HoukaiGoddessAnalyst | Marcus 受控命令 allowlist |
| HoukaiGoddessDialogueService/VM | Marcus Route + Prompt + 输出 Schema |
| HoukaiNpcInitiation | 并入现有 NPC 主动行为 |
| HoukaiCaptivity/Camp | 合并为“权力关系”系统 |
| HoukaiMemory | Marcus Storage + Prompts 压缩 |
| HoukaiNavigation/EntryBehavior | 按斯拉涅斯之拥现有菜单结构重接 |
| HoukaiConfig/ConfigBridge | 56 个设置项并入 MCM，删除 AF 专属项 |

## 5. 不迁移

- `HoukaiAfGoddessBridge.cs`
- `HoukaiAfMemoryBridge.cs`
- `HoukaiAfOpeningBridge.cs`
- `HoukaiAfResumeBridge.cs`
- `HoukaiAfSceneReplyProbe.cs`
- `HoukaiConversationBridge.cs` / `HoukaiConversationResultBridge.cs`
- `HoukaiMergeRuntime.cs` 的 Standalone + Adaptive 双模式
- `LoveHate/` 全部：反射、Harmony、插件事件注册、状态桥、配置同步

## 6. 与斯拉涅斯之拥现有能力对照

| 功能 | 斯拉涅斯之拥现状 | AF 独立版来源 | 本次动作 |
| --- | --- | --- | --- |
| 女神对话 | 已有 | GoddessDialogue + DivinePrompts | 补人格/知识/命令解析 |
| 身体开发 | 已有 9 区入口 | BodyBalance + BodyPanel | 补公式/7 档位/3D 面板 |
| 发情周期 | 已有 7 天 | HeatStagePrompts | 补 5 阶段文案与权重 |
| 事件引擎 | 代码硬编码少量测试事件 | 388 条 JSON | 数据驱动化 |
| NPC 主动 | 已有 | NpcInitiation | 补概率/开场/待续细节 |
| 俘虏/营地/被俘 | 已有基础 | Captivity + Camp | 合并权力关系系统 |
| 周报/状态 | 已有 | WorldEffect + Memory | 补世界效果与日限 |
| 炼金/禁忌 | 无 | Alchemy + DivineTaboo | 先入库，后接闭环 |
| 文化/情色/系统事件 | 无 | 91 + 157 + 62 条 | 内容库接入 |
| 四版世界书 | 只有 20 篇知识 | 四档 rules + personality | 先接 clean，后做四档切换 |

## 7. 阶段计划

### Phase 0 冻结与归档

- 把 `AnimusForgeHoukai` 标记为撤项/只读，保留完整备份。
- 更新工作区规则、README、版本线文档。
- 按已整理的清单完成逐文件勾选。

验证：备份完整、文档状态更新、无继续开发入口。

### Phase 1 内容资产入库

- 388 条事件 JSON 转成斯拉涅斯之拥内容库 schema。
- 女神人格、世界铁律、clean 世界书并入知识语料。
- BodyBalance / HeatStagePrompts / CultureAliases 转 ModuleData。
- 语言包转斯拉涅斯之拥本地化键。

验证：JSON 可解析、事件计数一致、关键词覆盖、双语文案 key 对齐。

### Phase 2 纯逻辑移植

- 移植 Calculator、Balance、Models、StorageCodec、StatDescriptions。
- 身体 9 分区公式与 7 档位、发情阶段文案接入。
- 炼金/禁忌只移植数值与配方，不接 UI。

验证：SdkSmoke 公式断言、边界与男女分支、与 AF 结果对照。

### Phase 3 机制整合

- 事件引擎数据驱动：条件、权重、冷却、选项、Aftermath、事件链。
- 世界效果命令：金币、声望、影响力、士气、技能、关系 + 日限。
- 俘虏/营地/被俘合并“权力关系”状态机。
- 女神效果解析接 Marcus allowlist。
- UI：身体面板、事件弹窗、地图 HUD 按需接入。

验证：SdkSmoke + 游戏内事件触发/结算/存读档。

### Phase 4 体验与发布

- 四档世界书切换。
- 炼金工作台/禁忌仪式完整闭环。
- 正式切到斯拉涅斯之拥 0.3.x 版本线。
- 发布包验证与存档迁移测试。

## 8. 执行约束

- 新机制、新内容批次动手前先走 grill-me，锁定 PLAN 再写代码。
- 不把 AF 依赖、反射、Harmony、状态桥带进斯拉涅斯之拥。
- 不复制 AF 事件引擎代码；只复用内容、公式、模型字段与设计。
- 版号不因合并数量快速升级，按可玩能力升级。
- 游戏内验证由用户跑，以游戏日志为准。

## 9. 待确认项

- [ ] Phase 1 是否立即开始（只动 ModuleData 与知识库，不动玩法）
- [ ] 四版世界书第一波是否只接 clean
- [ ] 炼金/禁忌是否后置到 Phase 4
- [ ] 俘虏三块是否确认合并为“权力关系”
- [ ] `AnimusForgeHoukai` 是留在工作区作只读归档，还是移出主工作区
