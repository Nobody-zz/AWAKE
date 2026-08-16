# AWAKE 任务队列

> 日期：2026-08-16
> 规则：每轮开始必须先读 `awake-task-continuity/SKILL.md` 与本文件；延续候选必须列出并等待用户决策，不自动选择下一项。
> 当前状态：Marcus MCM 便捷配置代码完成；等待游戏内验收，未提版。

## 当前检查点

- 当前任务：无（等待用户决策）。
- 最近完成：Messenger 会话历史持久化。
- 下一步：从“待用户决策”选择，或进游戏完成“待游戏内验收”。
- 阻塞：游戏内验收需用户运行游戏。

## 已完成（代码/文档）

### 运行时基础

- 场景 T/Y 三维距离选人
- 无内容事件引擎骨架
- `awake.relationship.delta.v1` 关系命令
- NPC 提示词关系命令接线
- 事件弹窗“参与话题”第三入口
- 事件类型清单、枚举与校验
- 事件选项接入关系命令结算
- 存储管道离线 SdkSmoke
- NPC 记忆逻辑 SdkSmoke
- AI 架构清单

### 世界书

- 世界书接口与格式规范
- 世界书 30 轮自审查
- 世界书加载 / 索引 / 查询服务
- 世界书运行时接入 NPC 对话
- 世界书附加目录数据加载
- TextMappings 全量 AF Kind 覆盖（22 种）
- 一次性迁移脚本 `tools/migrate_af_worldbook.ps1`
- 迁移规范文档
- `variantSelection` 显式解析与校验
- 卡拉迪亚编年史嵌入 `ModuleData/Worldbook`（759 文件，约 3 MB）

### AI / 框架 / 配置

- 路由 ID 命名空间修复（`AWAKE.route.*`）
- Companion 路由恢复
- `AwakeFeedback` 统一操作反馈
- MCM 菜单重排、预设与操作按钮
- 世界事件持久化与周报自动生成
- Marcus MCM 便捷配置（状态、一键打开 AI 设置/诊断、自动同步引导）

### AF 结构学习

- AF 结构落地方案
- AF Batch 1：主动聊天状态机 + 60 秒 Esc 解锁
- AF Batch 2-5：命令台/收件箱/周报、记忆日结、UI 审计、回复规范化、性能探针
- AF 五批次 grillme 复查
- UI 借鉴映射文档
- Messenger 会话历史持久化
- `awake-task-continuity` 任务连续性 skill

## 待游戏内验收（blocked）

- 世界书：`worldbook_runtime_initialized`、NPC 命中、TextMappings、persona
- AF Batch 1：主动弹窗 → 深谈、拒绝冷却、60 秒 Esc
- AF Batch 2-5：收件箱/周报、日结日志、回复清洗、性能日志
- 世界事件/周报：重启后收件箱仍有历史、周报自动生成
- MCM：分组、预设、按钮
- 存储管道真机：Companion 读写、读档持久化
- NPC 记忆真机：读档回读、真实对话记忆

## 待用户决策（延续候选）

1. Messenger 统一会话：通讯录与 NPC 覆盖层统一入口调度，不删除任一层。
2. Messenger 写信/来信：远方联系人、回复延迟、未读、来信通知。
3. 事件收件箱 UI 升级：文本弹窗改为 Gauntlet 列表。
4. 周报浏览器 UI：独立查看器，不再只是文本弹窗。
5. 开发者检查面板：完整诊断 UI，而不是文本报告。
6. 对话覆盖层等待动画与状态提示增强。
7. Messenger 未读计数与大地图通知。
8. Marcus 一键配置：代码已完成（MCM 状态、AI 设置/诊断入口、自动同步引导），待游戏内验收；写入 Provider profile 不建议 AWAKE 直写。

## 待修复（自查发现）

1. P1：Messenger 历史缓存未随战役重置。
2. P1：持久化回读在存储未就绪时永久跳过。
3. P2：WorldEvent / Messenger fire-and-forget 写任务统一安全包装。
4. P2：周报触发日持久化，避免读档重复生成。
5. P2：MCM AI 自检补充路由/模型/Provider 状态。

## 半成品（有入口但未闭环）

- `EventDialogueQueue`：引擎已生产，但无内容规则。
- 命令层：只有关系命令，世界效果未接。
- Preprocess / Postprocess 路由：已注册但无调用方。
- 无名 NPC：有身份回退，无永久记忆与命令边界。
- 遭遇面谈 / 场景 T/Y / 通讯录：统一会话未完成；通讯录历史已持久化。
- 开发者检查：仍是文本报告。

## 排队中（新想法/建议，不打断当前任务）

- Messenger 群聊、头像、媒体、TTS。
- 记忆分级、承诺账本、秘闻传播。
- 世界事件 / 政令。
- 内容包接入后的事件内容批次。

## 已暂停 / 不迁移

- 内容注入与背景知识接入。
- 知识检索语料与内容包 RAG 接入。
- 内容包公开 API 落地。
- 女神人格、情色机制。
- 旧 AF / 爱与恨兼容。

## 远程同步

- 远端 `main` 已确认以本地最新为权威。
- 最近推送成功：`3b377a8..e41677b main -> main`。
