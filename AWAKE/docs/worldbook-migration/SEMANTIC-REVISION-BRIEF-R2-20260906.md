# 五候选语义修订执行简报

> 日期：2026-09-06  
> 状态：`needs_review`  
> 范围：仅针对 r2 的五个临时候选；本文件是修订任务包，不是新的 rewrite candidate，也不是批准记录。

## 1. 不变边界

- 权威来源仍为 `C:\Users\26811\Downloads\卡拉迪亚编年史\卡拉迪亚编年史`。
- 父 snapshot：`source.awake.worldbook.download-20260903`。
- 当前 v1.1 child snapshot hash：`e91ffbeb709e967bff5373a191d5587c179afa09fa4ca8d8b271d8e1ed1a22be`。
- 不原地覆盖 r2 候选。
- 本轮不生成新的 rewrite candidate；只记录下一 revision 的必要修复。
- 不将任何修订结果标记为 `approved`、`canon`、`compiled`、`published` 或 `runtime-ready`。

## 2. 统一修订门

每个 assertion 必须同时具备：

1. 一个可读的 proposition；
2. 一个或多个明确的 `source_origin`；
3. 一个精确的 source locator；
4. 一个独立的 `claim_id`；
5. 一个明确的 `target_span`；
6. `epistemic_kind`、`perspective`、`time_scope`、`polarity`；
7. `decision=unresolved` 或人工明确的 `keep/revise/drop`；
8. 若为推导，必须标记为 interpretation，不得伪装成 source fact。

以下任一情况都必须阻止候选进入批准：

- 正文命题多于 claims；
- 一个 span 混合多个主体、视角或认识论层级；
- 历史状态被写成当前状态；
- 传闻被写成事实；
- 旧 When 被转换成 grants/denies；
- 旧 TextMapping 或 settlement marker 被转换成正式实体 ID；
- source registry 仍有未知字段。

## 3. 五个候选的最小修订动作

### 拉科尼斯湖

- 为“沿岸生计”建立独立 claim，至少拆出渔民、船工、码头搬运者。
- 为“帝国麦子—斯特吉亚毛皮转运”建立独立 relation claim。
- 为“浅滩铁质氧化层的自然解释”建立独立 interpretation claim。
- 将“帝国领主”回溯到源文主体；如果无法确认，改为“帝国叙述/帝国防务叙述”。
- 传说、考察解释、冬季通航必须保持三个不同 target span。

### 沙拉斯湾

- 为加隆托海峡建立独立 claim，不与沙拉斯湾总体功能合并。
- “帝国摇篮”只作为 imperial perspective。
- “先祖登陆”只作为未经考古和移民史确证的 rumor。
- 船员关于雾、罗盘、石刻的内容继续保持 rumor，不增加超自然解释。

### 卡恰尔半岛

- 增加独立 unresolved claim：当前归属与具体战争顺序不能仅凭旧世界书确认。
- 将巴旦尼亚、诺德、斯特吉亚三方叙述逐项绑定，不能只用一个 merged span。
- “适合建哨站”“控制/牵制海路”“骑兵难展开”分别标记为 strategic interpretation、mixed cultural interpretation、Khuzaite perspective。
- 历史先后控制只能写成历史状态，不能覆盖当前状态。

### 黎明山脉

- 将“地理边界”“地形屏障”“社会禁忌”“信仰边界”拆成独立命题。
- 保留源文的地貌粒度：只写“山麓覆盖针叶林与高山草甸”，不补写未经来源支持的山腰/山麓分层。
- 合儿必特部的通婚、共俗、守护者身份单独绑定 source span。
- 不从“守护者”推出当前实际控制。

### 德里亚特

- 将“其他御寒衣物”收窄为源文明确的“防水斗篷”和“卡琉斯堡守军冬季皮袄”。
- 将“运出货物成本较高”改为来源支持的“进入村庄需要费功夫”，或明确标记为 interpretation。
- 保留海豹油点灯为独立 fact。
- registry/entity 不进入正文，只保留为 unresolved metadata。

## 4. 修订后的验收顺序

1. 先修 worksheet claim，而不是先改 target text。
2. 为新增 claim 建立 origin 和 locator。
3. 再重写正文表达。
4. 生成新的 revision 目录，不覆盖 r2。
5. 运行 candidate closure validation。
6. 运行 v1.1 migration contract validator。
7. 检查正文命题数、claim 数、origin 数、target span 数的三向闭合。
8. 重新生成人工 review decision，仍为 `needs_review`。

## 5. 完成定义

本修订任务只有在以下条件都满足时才算“修订完成”：

- 五个候选均有新的 revision；
- 每个正文命题都有独立 claim 或明确 drop/unresolved 决策；
- 每个 claim 都有 source origin；
- 每个正文命题都有正确 target span；
- 视角、认识论类型、时间范围和极性没有扩大；
- 结构验证和语义审阅分别记录；
- 仍没有批准、canon、编译、发布或运行时注入。

