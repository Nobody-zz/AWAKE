# AWAKE 模组本体回归检查点

- task_id: `AWAKE-MOD-RETURN-20260911`
- batch_id: `AWAKE-G3-B-IMPLEMENT-20260911`
- status: `implemented_approved_e2`
- updated_at_utc: `2026-09-11T13:12:28.5656321Z`

## 当前事实

- 工作区权威源码为 `D:\AWAKE-Dev\AWAKE`。
- G3-A RuntimeBridge 已完成实现；当前实现不再由生产 NPC 对话路径调用 legacy `WorldbookService.BuildPersona`。
- 当前离线最高证据等级为 E2。
- 当前 Release 构建产物 `AWAKE\_build_out\1.3.15\Release\Awake.dll` SHA-256：`AC903ADFAC0E844B4E6D52DC88C223024755DC99ECC3AA4736CBE832BE9F509D`。
- G3-A lease 已释放；未启动游戏，未同步游戏目录。
- G3-B Persona Storage 已完成离线实现；revision 4.1 新批次 round 2 已 `APPROVED`，用户已签收，独立实现复审已 `APPROVED`。

## 已核验项目

- `dotnet build AWAKE.Tests\AWAKE.Tests.csproj -c Release --nologo`：通过，0 warning / 0 error。
- `AWAKE\tools\build.ps1`：通过，API `1.3.15`。
- `AWAKE.Tests\bin\Release\net472\Awake.SdkSmoke.exe`：`PASS ALL Awake.SdkSmoke`。
- G3-A 静态检查：G3-A blocking checks 全部通过；Persona Storage wiring/schema 检查属于 G3-B 延后项。

## G3-B 计划边界

- 包含：PersonaPersistenceEnvelope、Storage namespace、保存/读档、新档/换主角入口、身份/schema 校验、sequence/watermarks 恢复、Runtime projection 恢复和 fail-closed 降级。
- 不包含：外部内容包正文、PersonaWorkbench、云端 Provider 实测、游戏目录同步、游戏启动、版本号提升及关系/记忆/事件后续玩法。

## 已知限制

- `AWAKE-CURRENT.md` 含历史批次叙述，本检查点和 `control-plane/CURRENT.json` 是本次模组本体回归后的紧凑状态入口。
- G3-A 原始 scope 文档属于已冻结门控记录，不在本次状态恢复中改写；其历史路径字段不作为当前源码权威路径。
- 尚未获得当前候选的 E3/E4/E5 证据；本批次最高为 E2。

## next_action

当前目录可继续常规模组代码开发；如需验证实际游戏效果，再单独安排 E3/E4/E5 实测门禁。

## last_error

实现与离线验证无新的失败；租约已释放，未启动游戏，未同步游戏目录。当前候选 BuildId 为 `awake-20260911-g3b-persona-storage-001`。
