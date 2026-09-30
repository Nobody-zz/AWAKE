# AWAKE: Awakened World AI · 醒世

《骑马与砍杀2：霸主》的**通用 AI 世界运行时** —— NPC 智能、跨会话记忆、世界知识、事件、命令治理与效果结算，全部由运行时负责，**不绑定任何特定世界观**。

> **本模组不含成人向内容**，也不附任何内容包。世界观内容（世界书、事件、信件）以独立内容包形式存在，与本运行时解耦。

---

## 一、这是什么

AWAKE 让卡拉迪亚的 NPC「记得住事、说得上话、知道该知道的事」。它不改变战斗与经营玩法，而是给这个世界加一层**知识与对话的运行时**：

| 能力 | 做什么 |
|---|---|
| **世界知识** | 一个可检索的知识库。NPC 被问到时，按「谁知道 × 何时知道 × 信不信」决定他说什么 |
| **跨会话记忆** | 存盘 → 退出 → 读档，NPC 记住的事还在 |
| **NPC 主动行为** | NPC 会主动找你、送信、提起旧事 |
| **命令治理** | NPC 的「行动」走统一的命令通道，可审计、可回滚 |
| **效果结算** | 命令产生的世界变化有明确的效果规则，不是自由发挥 |

---

## 二、当前状态（截至 2026-09-30）

> 判据一律以**游戏内实测**为准；离线全绿不算过版。

### 已经跑通的

- ✅ **进过游戏**：2026-09-14 23:14–23:19，`Awake.dll` 加载、注册成功、会话跑满整场、干净退出（`pending_writes=0 dropped=0`，`rgl_log` 3553 行 0 异常）。
- ✅ **运行时肖像第一次上屏**：`portrait_texture_ready` 212×360 ×3。
- ✅ **出图链路通**：本地端点 → 200，6.6 s，512×512 JPEG（落盘魔数 `ffd8ffe0` 是真图）。
- ✅ **NPC 对话面板游戏内开过**：`hero:lord_1_18`，`Negotiation` ↔ `Chat` 正常切换关闭。
- ✅ **世界书已投送**：790 档（`geography 416 / economy 151 / politics 132 / war 71 / culture 20`），仓库侧与游戏目录**逐字节一致**，registry 三 hash 与包内逐条相符。
- ✅ **离线测试全绿**：`Awake.SdkSmoke` `total=64 passed=64 failed=0`。

### 已知缺陷（都在游戏内真实发生过）

| 缺陷 | 影响 |
|---|---|
| `awake.world_fact.root_corrupt` ×4 | 周报 / WeeklyDynamics 整链不可用 |
| `native_readiness` 空引用 | 09-10 起反复出现，同场里也多次 `Ready` ⇒ 是**次序/竞态**，不是恒定坏 |
| 会话头两分钟 `awake_host_resolution runtime_not_ready` | 开场两分钟的功能走不了 |
| `npc_dialogue_open_failed` ×2 | 主动对话候选建了、也被接受了，**就是张不开嘴** —— v0.3「活起来」卡在这 |
| 状态落盘进 `AwakeState/unbound/`（推断，未证实） | 存储绑在认出存档 id 之前，绑了个空 —— v0.2「记得住」卡在这 |

---

## 三、环境要求

| 项 | 值 |
|---|---|
| 游戏 | 《骑马与砍杀2：霸主》，**基础游戏 v1.4.8**（2026-09 起） |
| 目标框架 | `net472` |
| 必需依赖 | `Bannerlord.Harmony`、`Bannerlord.ButterLib`、`Bannerlord.UIExtenderEx`、`Bannerlord.MBOptionScreen` |
| 官方模块 | `Native`、`SandBoxCore`、`Sandbox` |
| **可选 DLC** | **`NavalDLC`（战帆）—— 非必需**。见 §五 |

---

## 四、构建与验证

```powershell
cd AWAKE
# 默认 BannerlordApi=1.3.15；现行游戏是 1.4.8，按你的游戏版本传
powershell -File tools\build.ps1 -BannerlordApi 1.4.8
```

`build.ps1` 会校验 `BannerlordApi` 与 `GamePath` 下 Native 的版本**严格相等**，不符即报错（这能防住「编错目标却以为没问题」）。

```powershell
# 离线烟测
cd AWAKE.Tests
bin\Debug\net472\Awake.SdkSmoke.exe

# 本地化校验
cd AWAKE
powershell -NoProfile -ExecutionPolicy Bypass -File tools\validate_localization.ps1
```

> ⚠️ **离线全绿 ≠ 游戏内生效**。所有版本判据都在游戏里，不在本地预览里。

---

## 五、关于 NavalDLC（战帆）

**AWAKE 不硬依赖 DLC —— 不带 DLC 也能玩。** DLC 在官方侧是 `OfficialOptional`。

但 DLC 默认加载（`DefaultModule=true`），所以**必须处理它**。当前状态**分三层，不是"完全没接"**：

| 层 | 状态 |
|---|---|
| **工具/参考层** | ✅ **已建**（08-24）：`docs/mappings/war-sails-reference/`（528 条中英对照）＋ 实体注册表已带 DLC 状态（`hero_official_dlc_not_installed: 53`） |
| **内容层（世界书）** | 🟡 部分：4 份正典档写 DLC 引入的 **Nord（诺德）** 势力（`clan-clan_nord_1/2/3`、`military-nord`），但**无 DLC 条件** |
| **运行时层** | ❌ 未接：`src/*.cs` 中 `NavalDLC` 零命中；世界书 schema 无 DLC 字段 |

完整评估（三层修正、DLC 内容规模、待办 N0–N2、需裁定问题）见 **`docs/DLC-COMPAT-NAVAL-20260930.md`**。

---

## 六、仓库结构

```text
D:\AWAKE-Dev/
  AWAKE/                        # 运行时模组（本仓库主体）
    src/                        # 运行时源码（163 个 .cs）
    ModuleData/                 # 本地化 + 世界书包（Worldbook/packages/calradia/）
    GUI/                        # 运行时 UI（Prefab / Brush / SpriteParts）
    framework/                  # 构建依赖（ProjectReference，勿删）
    tools/                      # 构建、同步、校验、世界书 Studio
      worldbook-studio/         # 世界书编译工具链（authoring → 编译 → 投送）
    docs/                       # 路线图、契约、审计、评估
  AWAKE.Tests/                  # 离线烟测 Awake.SdkSmoke
  MarcusAIFramework_Reference/  # 框架 SDK 参考
```

---

## 七、不在本仓库的东西

- **内容包**：世界观内容以独立包形式分发，本仓库只承载运行时。
- **旧版世界书**：早期曾以已被取代的 `awake.worldbook.v1` 布局捆绑过，**现行运行时拒绝该形态**（只认 `awake.worldbook.v2` 或 `awake.worldbook.registry.v1`）。那棵树已从 HEAD 移除。

---

## 八、版本路线

**判据是「玩家能多做什么」，不是「写了多少代码」。**

| 版本 | 玩家能多做什么 |
|---|---|
| **v0.1 点亮** | 装上、进得去、看得见 AWAKE 在跑 |
| **v0.2 记得住** | 存盘 → 退出 → 读档，状态还在且一致 |
| **v0.3 活起来** | NPC 会主动找你、送信、提起旧事 |
| **v0.4 有脸** | 界面、图标、**肖像**都长出来了 |
| **v0.5 撑得住** | 连续游玩不崩；与常见模组同装不冲突 |
| **v1.0** | 可以公开上创意工坊 |

详见 **`docs/AWAKE-ROADMAP.md`**。

> 旧的 `0.1.x`–`0.9.x` 按内容模块排的版本表**已废止**（与本项目实际的五线并行不符）。

---

## 九、发布合规（v1.0 前必须补齐）

- ⚠️ **当前缺 `LICENSE` 与 `NOTICE` 署名文件** —— 这是上架前的硬门槛。
- 本体（工坊主包）**不含成人向内容**。
- 需准备：存档兼容说明、依赖与加载顺序、MCM 配置指引、工坊页面文案与截图。

---

## 十、仓库与同步

- 仓库：`https://github.com/Nobody-zz/AWAKE.git`
- 权威工作区：`D:\AWAKE-Dev`（**公开镜像只作下游**，同步方向只允许 本工作区 → 镜像）

```powershell
cd D:\AWAKE-Dev
# ⚠️ 本工作树有多个并行 agent 共用同一个 index
# 不要 `git add .`，只提交你实际改动的路径
git commit -m "Update AWAKE runtime" -- AWAKE/src AWAKE/framework AWAKE/tools
git push origin main
```
