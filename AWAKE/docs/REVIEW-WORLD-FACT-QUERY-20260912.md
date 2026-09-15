# 世界事实查询层：实现后静态审查

- 范围：`WorldFactQuery`、五个具名选择策略、旧记录兼容适配、知识投影接缝、近期动态预览调用路径。
- 证据：`src/WorldFactQuery.cs`、`src/WorldEventContracts.cs`、`src/WorldKnowledgeProjectionService.cs`、`src/WeeklyReportService.cs`、`src/AwakeTerminalBehavior.cs`、Runtime smoke。
- 边界：未启动游戏、未同步；未把 Persona、人物记忆写入或对话接入本批次。

## 结论

当前查询切片通过静态核对，未发现新的 P0/P1：

1. `RecentDynamics`、`WeeklyDynamics`、`WorldKnowledge`、`CharacterMemoryCandidate`、`EventTriggerCandidate` 均通过统一的只读 `WorldFactQuery` 入口执行。
2. 近期/七日查询固定窗口并返回稳定排序；人物候选必须命中实体 ID；事件触发必须满足订阅的 kind；每条输入均有稳定的入选或排除理由。
3. Journal `corrupt`/`unavailable` 不会降级为空；只有 Journal `missing` 才可使用显式 `legacy_import` 兼容旁路。
4. 旧记录的事件 ID 与身份受众在兼容知识投影中保留；旧记录没有实体，因此被人物候选策略排除。
5. 近期动态预览已改走查询层；知识投影已消费查询结果。查询层没有写存储调用。

## 未宣称完成的边界

正式“本周动态”仍由独立周报 v2 批次治理；当前旧 v1 生成器路径仍保留旧事件账本读取。该路径未在本切片伪装成已迁移，待周报 v2 批次以 `QueryWeeklyDynamicsAsync` 消费 `SourceFactIds` 后再完成正式报告闭环。

## 验证

- `WorldbookRuntimeSmoke`：通过，包含查询策略矩阵、旧记录兼容、人物候选排除和 Journal 损坏隔离。
- `Awake.SdkSmoke.exe --world-fact-journal`：通过。
- `AWAKE/tools/build.ps1 -Configuration Release`：通过，BuildId `awake-20260912-world-fact-query-003`。
- 完整 `Awake.SdkSmoke.exe` 仍在既有 Persona golden fixture 检查处失败；该失败与本查询切片无关，未据此宣称全套通过。
