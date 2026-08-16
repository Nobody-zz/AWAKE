# AWAKE 进行中任务队列

> 日期：2026-08-16
> 规则：每轮开始必须先读 `awake-task-continuity/SKILL.md` 与本文件；延续候选必须列出并等待用户决策，不自动选择下一项。
> 本轮：Messenger 持久化完成；等待用户选择下一项或进游戏验收，未提版。

## 当前主线

- 主线：AWAKE v0.1.x 运行时核心基线。
- 最近完成：
  - 场景 T/Y 三维距离选人
  - 无内容事件引擎骨架
  - `awake.relationship.delta.v1` 关系命令
  - NPC 提示词关系命令接线
  - 事件弹窗“参与话题”第三入口
  - 事件类型清单文档
  - 事件类型枚举与校验
  - 事件选项接入关系命令结算
  - 存储管道离线 SdkSmoke
  - NPC 记忆逻辑 SdkSmoke
  - AI 架构清单文档
  - 世界书接口与格式规范草案
  - 最终对抗审查与阶段总结
  - 世界书 30 轮自审查
  - 世界书加载/索引/查询服务骨架
  - 世界书运行时接入 NPC 对话
  - 世界书附加目录数据加载
  - 世界书 TextMappings 全量 AF Kind 覆盖（22 种）
  - 世界书一次性迁移脚本 `tools/migrate_af_worldbook.ps1`
  - 世界书迁移规范 `AWAKE-Worldbook-Migration-Spec-20260816.md`
  - 世界书 `variantSelection` 显式解析与校验
  - 卡拉迪亚编年史嵌入 `ModuleData/Worldbook`（759 文件，约 3 MB）
  - 路由 ID 命名空间修复（`AWAKE.route.*`）
  - Companion 路由恢复：`profiles.awake.deepseek.routes.json` 已同步四条 AWAKE 路由
  - 命令台默认快捷键改为 `U`
  - AF 结构落地方案文档 `AF-Structures-Landing-Plan-20260816.md`
  - AF Batch 1 代码落地：`NpcProactiveService` 主动聊天状态机 + 覆盖层 60 秒 Esc 解锁
  - AF Batch 2-5 代码落地：命令台收件箱/周报、记忆日结、UI 审计、回复规范化与性能探针
  - AF 五批次 grillme 复查文档 `GRILLME-AF-BATCHES-20260816.md`
  - AF 反馈层：`AwakeFeedback` 统一游戏内操作反馈
  - MCM 菜单重排、预设与操作按钮
  - 世界事件持久化与周报自动生成
  - Messenger 会话历史持久化
  - UI 借鉴映射文档 `AWAKE-UI-Borrow-Map-20260816.md`
  - `awake-task-continuity` 任务连续性 skill

## 待用户决策（延续候选）

1. Messenger 统一会话：通讯录与 NPC 覆盖层并存；统一入口调度，不删除任一层。
2. Messenger 写信/来信：远方联系人显示“后续开放”；写信、回复延迟、未读、来信通知。
3. 存储管道真机验证：离线 SdkSmoke 已覆盖；剩余 Companion 真机、读档持久化。
4. NPC 记忆游戏内验证：逻辑 SdkSmoke 已覆盖；剩余读档回读、真实对话记忆。
5. 世界事件/周报游戏内验收：持久化与自动生成已接，等待进游戏验证收件箱与周报。
6. 开发者检查面板：当前只有文本报告，无完整诊断面板。
7. MCM 游戏内验收：分组、预设、按钮已接，等待进游戏验证。
8. AF 结构落地：五个 batch 代码已完成，等待游戏内逐批验收。

## 半成品（有入口但未闭环）

- `EventDialogueQueue`：引擎已生产，但没有内容规则。
- 命令层：只有关系命令，世界效果未接。
- `WorldEventLedger` / `NarrativeReportBuilder`：持久化与周报生成已接；收件箱 UI 仍为文本弹窗。
- 开发者检查：仍是文本报告，无完整诊断面板。
- Preprocess/Postprocess 路由：已注册但无调用方。
- 无名 NPC：有身份回退，无永久记忆与命令边界。
- 遭遇面谈 / 场景 T/Y / 通讯录：入口存在，统一会话未完成；通讯录历史已持久化。
- 世界书游戏内验收：世界书已嵌入，等待用户进游戏验证 `worldbook_runtime_initialized`、NPC 对话命中、TextMappings、persona 注入。
- AF Batch 1 游戏内验收：等待用户验证主动弹窗 → 深谈、拒绝冷却、AI 等待超 60 秒 Esc 取消。
- AF Batch 2-5 游戏内验收：命令台收件箱/周报、日结日志、回复清洗与性能日志。

## 远程同步说明

- 远端 `main` 原为更早版本的无共同祖先根提交 `8ae927d Add files via upload`；用户确认后，以本地最新 `main` 为权威，强推覆盖远端。
- 本地待推送提交：`6742aa9`、`341ed27`、`7be56f2`、`079cc1b`；推送因 GitHub 连接失败待重试。

## 当前检查点

- 已完成：Messenger 历史持久化。
- 下一步：由用户从“待用户决策”选择，或进游戏完成验收。
- 阻塞：GitHub 推送网络中断；游戏内验收需用户运行游戏。

## 排队中（新想法/建议，不打断当前任务）

- Messenger 群聊、头像、媒体、TTS。
- 记忆分级、承诺账本、秘闻传播。
- 世界事件 / 政令（周报接线见待决策第 7 项）。
- 内容包接入后的事件内容批次。

## 已暂停/不迁移

- 内容注入与背景知识接入：用户明确先搁置。
- 知识检索语料与内容包 RAG 接入：用户明确先搁置。
- 内容包公开 API 落地：依赖内容注入，先搁置。
- 女神人格、情色机制：内容包支线，不在运行时开发。
- 旧 AF/爱与恨兼容：不保留反射与桥接。
