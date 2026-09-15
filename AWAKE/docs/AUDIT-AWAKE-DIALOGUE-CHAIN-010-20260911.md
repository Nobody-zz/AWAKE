# AWAKE 人物对话链审查

- 审查日期：2026-09-11
- 审查对象：`AWAKE-DIALOGUE-CHAIN-010`
- 工作区：`D:\AWAKE-Dev\AWAKE`
- 游戏版本：Bannerlord `v1.3.15.110062`
- 审查方式：BannerlordSage MCP 只读源码取证 + 本地静态门禁
- 是否修改运行时代码：否
- 当前结论：**静态链路成立；实机闭环未证实；不宣布完成**

> ⚠ **时效注（2026-09-15 由全局主控线补，正文一字不改）**：本文 §三 的三个 P1 **部分已过期**——
> **P1-1（初始化未检查绑定返回值）与 P1-2（缺正向回合完成日志）已修**（证据：`NpcDialogueService.cs:474-475`、`:1666`）；
> **仅 P1-3（`NpcDialogueVM.cs:724` 仍用 `CancellationToken.None`）仍未修**。
> ⇒ **引用本文的 P1 清单前请先看 `docs/PLAN-OFFLINE-CLOSURE-20260915.md` §三.A 的复核表**，别照抄。

## 一、已确认的对话链

```text
入口
  -> NpcDialogueLauncher.TryOpenDialogue
  -> NpcDialogueService
  -> 玩家绑定 / 存储就绪 / 提示词登记
  -> BuildPromptInputAsync
  -> AiTaskGateway.SubmitAsync
  -> Host.Ai.SubmitAsync
  -> AiTaskEvent.Completed
  -> 输出校验
  -> UI 显示回复
  -> 转录落盘
  -> 关系命令异步结算（如输出包含命令）
```

关键证据：

- `NpcDialogueLauncher.cs:64` 创建 `NpcDialogueService`。
- `NpcDialogueService.cs:184` 从发送入口进入 `EnsureReadyAsync`。
- `NpcDialogueService.cs:247` 通过 `AiTaskGateway.SubmitAsync` 提交 AI 请求。
- `AiTaskGateway.cs:206` 通过 `Host.Ai.SubmitAsync` 进入框架边界。
- `NpcDialogueService.cs:1221` 处理完成事件并执行输出校验。
- `NpcDialogueService.cs:1257` 写入对话历史并触发转录。
- `AwakeTranscriptService.cs:98` 成功转录后写出 `transcript_turn_appended`。
- `NpcDialogueService.cs:1360` 附近执行关系命令结算。

## 二、已通过的静态检查

| 检查 | 结果 | 证据边界 |
|---|---|---|
| AWAKE caller contract | `7/7 PASS` | 只证明 Host 边界、Gateway 调用和入口连接，不证明游戏运行 |
| MCM contract | `47 PASS / 0 FAIL` | 只证明配置入口和 Provider 配置链，不证明 Provider 健康 |
| BannerlordSage 游戏索引 | 正常 | 15 DLL、3784 XML、0 解析失败；本地反编译源码为权威 |
| AWAKE 源码索引 | 正常 | 136 个 C# 文件、319 个类型、1430 个成员 |

## 三、缺口与风险

### P0：实机闭环仍未验证

当前源码能证明“应该会走到提交和完成处理”，但不能证明真实 Provider、Route、游戏进程和 UI 已经完成一个回合。必须由用户运行匹配 BuildId 的候选并提供日志，至少确认：

- `npc_dialogue_ready`
- `ai_task_submit_accepted`
- 完成事件对应的 UI 回复
- `transcript_turn_appended`
- 无 `host.*.unavailable`、`storage_not_ready` 或 Provider 失败

当前没有找到计划中所写的 `docs/evidence/AWAKE-DIALOGUE-CHAIN-010-DEPLOY-20260911.json`，因此该文件不能作为现成实机或部署证据引用。

### P1：初始化阶段没有检查玩家绑定返回值

`NpcDialogueService.cs:411` 调用了 `EnsureCurrentHeroBoundAsync`，但没有检查返回值，随后仍可能继续初始化并写入 `npc_dialogue_ready`。发送阶段 `EnsureReadyAsync` 会再次检查，因此不一定直接造成错误回复，但可能造成 UI 提前显示“已就绪”，实际发送时才失败。

建议在新的修复批次中让初始化显式区分：绑定成功、绑定失败、取消和会话已过期；不要修改冻结的 010 候选。

### P1：正向“回合完成”日志仍不完整

`NpcDialogueService.cs:1221` 的完成处理会校验输出、更新历史、触发转录和 UI 事件，但没有独立的 `npc_dialogue_turn_completed` 正向日志。现在能看到提交成功和转录成功，却不能仅凭日志明确判断“输出校验通过并已向 UI 完成派发”。

建议新批次补充带 `hero`、`generation`、`correlation` 的完成日志，并保持失败日志不变。

### P1：UI 发送使用 `CancellationToken.None`

`NpcDialogueVM.cs:165` 和通讯录发送路径把发送任务交给 `SendAsyncSafe` 时没有传入 UI 生命周期 CancellationToken。当前通过 `CancelActiveAsync` 和 Gateway 取消机制部分补救，但在绑定、存储、提示词构建等提交前阶段仍可能继续运行。

建议新批次补齐 UI 会话 CancellationToken，并验证关闭面板、切换联系人和重复点击三种情况。该项属于生命周期修复，不应混入 010 冻结候选。

### P1：计划与现场证据存在落盘不一致

010 计划写明已生成部署证据和构建验证记录，但当前目录中未发现计划引用的部署 JSON，且 `AWAKE\BUILD_VERIFICATION.txt` 位于源码根目录而不是计划中部分引用的 `_build_out` 路径。后续不能直接沿用计划文字，必须以实际文件和匹配 BuildId 重新核对。

## 四、当前能否继续开发

可以继续常规模组开发，但 010 不能被标记为“实机完成”。当前最高可信状态是：

- 代码调用链：静态成立；
- 离线门禁：已通过；
- Provider/真实 AI 回合：未验证；
- 游戏内 UI 回复：未验证；
- 存档/长时持久化：不属于本次对话链静态审查的已证范围。

## 五、下一步建议

1. 不修改 010 冻结候选，先由用户按当前候选 BuildId 做一次 E4 实机对话。
2. 若实机失败，依据第一条缺失日志定位，不凭 UI 表象猜测。
3. 若实机成功，再单独开一个小修复批次处理初始化绑定日志、回合完成日志和 UI 取消语义。
4. 010 的部署 JSON 缺失问题另行补齐证据索引，不把补文档伪装成运行时修复。

## 结论

**AUDIT RESULT: STATIC_CHAIN_CONNECTED / GAMEPLAY_UNVERIFIED / REPAIR_BATCH_REQUIRED**

