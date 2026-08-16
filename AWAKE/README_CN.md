# AWAKE: Awakened World AI · v0.2.0（过渡态）

AWAKE 是《骑马与砍杀2：霸主》的通用 AI 世界运行时：NPC 智能、跨会话记忆、世界知识、事件、命令治理与效果结算由运行时负责，不绑定任何特定世界观。

`SlaneshsEmbraceContent` 是内容包基础（世界书、事件、信件、NPC 主动基础）；女神人格与情色机制各自作为独立内容包支线，不在运行时内。

## 当前状态

- 运行时已内容无关：不再引用女神、祭坛、身体/发情、俘虏、信件代码。
- 代码名已全部改为 AWAKE：ModId `AWAKE`、DLL `Awake.dll`、namespace `Awake`、路由 `AWAKE.route.*`、存储 `awake.*`、日志 `Awake.log`。
- 运行时源码与本地化文件名也已统一为 `Awake*` / `awake_*`；`SlaneshsEmbraceContent` 内容包保留自己的 Slaanesh 身份。
- 内容包基础与女神/情色支线冻结在 `SlaneshsEmbraceContent/frozen`。
- 构建：0 警告 0 错误；`Awake.SdkSmoke` `PASS ALL`；本地化校验通过。
- NPC 深谈入口改为 AWAKE 命令台：按命令台快捷键（默认 `Y`，可在 MCM 修改）呼出独立面板，内附“深谈（醒世）”与“开发者检查”，选择目标后复用 `NpcDialogueLauncher` 打开覆盖层或回退原版对话；不再向城镇菜单插入选项。
- 运行时用户可见措辞已改为 AWAKE/醒世，不再把斯拉涅斯内容写进运行时权限提示、菜单与对话标题。

## 目录结构

```text
_houkai_merge/
  AWAKE/                    # 运行时主工程
    src/                    # 运行时源码
    docs/                   # 路线图、切割、API 契约、分类表
    dist/                   # 发布产物
    ModuleData/             # 运行时本地化
    GUI/                    # 运行时 UI
    tools/                  # 校验脚本
  AWAKE.Tests/              # 运行时 SdkSmoke
  SlaneshsEmbraceContent/   # 内容包工程（基础 + frozen 支线）
  MarcusAIFramework_Reference/  # SDK/参考
  archive/                  # 历史计划、备份、旧项目归档
```

## 版本路线

- `0.1.x`：运行时核心基线（NPC 深谈、存储、知识、配置、双路径验收）。
- `0.2.x`：内容包基础（世界书、事件、信件、NPC 主动基础）。
- `0.3.x`：世界模拟。
- `0.4.x`：关系与记忆深度。
- `0.5.x`：内容系统与工具。
- `0.6.x`：体验完善。
- `0.7.x`：生态与跨模组。
- `0.8.x`：性能与可观测。
- `0.9.x`：稳定与发布。

完整计划见 `docs/Awake-Roadmap-0.1-0.9-20260815.md`。

## 下一步

先完成 v0.1.x 运行时闭环：NPC 深谈入口、公开 API 实现、存储管道验证；再接入 v0.2.x 内容包基础。
