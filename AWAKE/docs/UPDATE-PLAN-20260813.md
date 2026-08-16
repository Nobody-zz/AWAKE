# 斯拉涅斯之拥 · 更新计划

> 状态：实施中（B1 云外发门 + 开发者模式已同步 dist；B2 MCM 中英双语 + 档位预设已完成并同步 dist，等待游戏内测试；H3/H4 已同步游戏目录，等待游戏内回归）
> 日期：2026-08-13（v2）
> 基线：v0.0.2
> 上游文档：`FEATURE-BRAINSTORM-20260813.md`（v6，已审查）、`FEATURE-FEASIBILITY-RANKING-20260813.md`（已审查）、`Awake-Development-Plan.md`、`Awake-Framework-Usage-Map.md`
> 执行约束：每批实施前必须走 grill-me → PLAN → APPROVED；版号只按可玩性验收提版，不按功能数量提版。

## 0. 进度总览

| 批次 | 内容 | 状态 | 证据 |
| --- | --- | --- | --- |
| M1-M2 | 女神对话最小闭环、知识检索、神谕效果桥 | 已完成 | SdkSmoke 26/26 |
| M3 | 世界底座第一批纵切：AiTaskGateway、Context Provider、Storage、R2 命令、Durable 事件、会话边界 | 已完成 | SdkSmoke 33/33 |
| M4 | 神谕愿力环：divine_candidate、提案条、favor_state 账本 | 已完成 | SdkSmoke 34/34 |
| H4 / M5 | 命令风险分级：CommandRiskPolicy、WorldCommandBridge 风险门 | 已完成 | SdkSmoke 35/35 |
| H3 | 权限目录与统一权限门：PermissionCatalog + PermissionGate | 已完成 | SdkSmoke 36/36；DLL 已同步 `_build_out` / `dist` / 游戏目录 |
| H6 | 云外发门 | 已完成（B1） | SdkSmoke 37/37；DLL 已同步 dist |
| F5 | 开发者模式 | 已完成（B1） | SdkSmoke 37/37；DLL 已同步 dist |
| F6 | MCM 中英双语 | 已完成（B2） | SdkSmoke 38/38；DLL 已同步 dist |
| G7 | 档位预设 | 已完成（B2） | SdkSmoke 38/38；DLL 已同步 dist |
| C12 | 历史归档 | 待实施 | 无 |
| G8 | 存档迁移 | 待实施 | 无 |

当前构建证据：主工程 `0 warnings / 0 errors`；SdkSmoke `PASS ALL` 38 条；maf-lint exit 0（114 条启发式警告人工复核，无阻断）；`SlaaneshsEmbrace.dll` SHA-256 `0728122FDB78C556B8336E3657EF6E3547434CE4DCE7378493BF8AB3D64FCFBB`。