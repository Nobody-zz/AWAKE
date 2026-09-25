# 真机走查单 · 最小功能回环（2026-09-25）

> 用途：**开一局游戏，按本单顺序做一遍**，把"对话／写信／周报／事件／存读档"五件事一次看完。
> 出具：主干线（阿砚）。本单所有入口与日志关键字都是**从代码里抄出来的**，不是凭印象写的（出处见每节末尾）。
>
> ⚠ **本单是"看什么"，不是"改什么"。** 走查完把读数贴回来，再决定动哪一件。
> ⚠ **不进游戏的验收全绿不算过版**（项目纪律）。所以这一步没法省，也没有替代品。

---

## 〇、开机前先做三件事（不做可能白跑）

1. **先把旧日志挪走。** AWAKE 的日志满 2 MB 就轮转，**而且只留一代**（`src/AwakeLog.cs` 的轮转逻辑）
   ⇒ 长局会把开头冲掉，而我们要看的恰恰是开头。
   做法：把 `Modules\AWAKE\Logs\Awake.log` 复制一份到桌面（或直接删掉让它重新长）。
2. **记住日志时间比北京时间晚 8 小时**（写的是 UTC）。
3. 每次开游戏都是新进程 ⇒ **先看文件修改时间**，确认你看的是最新那份。

路径（本机）：

```
游戏目录   D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord
日志       …\Modules\AWAKE\Logs\Awake.log
状态落盘   …\Modules\AWAKE\PlayerExports\AwakeState\
```

---

## 一、按顺序做（一局串完，约十几分钟）

| 步 | 在游戏里做什么 | 看什么 | 看到什么算通 |
|---|---|---|---|
| **1** | 进游戏、等它加载完 | 日志里有 `register_ok` | 有 ⇒ AWAKE 起来了 |
| **2** | 同上（顺带） | 日志里 `worldbook_runtime_initialized … package=awake:worldbook.calradia` | **`package=` 后面必须是两段**（`awake:worldbook.calradia`）。**看到三段（`awake:worldbook:calradia`）＝读的是旧包**；`entries=` 应等于你投的那份包的条数，**不是**死记 448／482 |
| **3** | 打开 MCM（Mod Options） | 6 组 36 项能打开 | 能打开、项数对 ⇒ v0.1 最后一格补上（09-14 漏看） |
| **4** | **按 U** 开终端 → 点「**附近对话（地图）**」→ 选一个英雄 → 说一句话 | 日志依次出现 `npc_dialogue_ready` → `npc_dialogue_turn_completed`（带 hero/generation/correlation）→ `transcript_turn_appended` | 三条串齐 ⇒ **对话这条腿通了**。09-14 手动开面板那次只验到"面板能开"，没验到"一轮说完" |
| **5** | **按 U** → 点「**通讯录（醒世）**」→ 找一个**远方**联系人 → 写信、发出 | 日志出现 `letter_send_succeeded` | 有 ⇒ **写信这条腿第一次被证实**。⚠ 入口只对"远方"开（近处当面谈），这是有意设计，不是 bug |
| **6** | **按 U** → 点「**事件收件箱**」 | 界面里有条目 | 有条目 ⇒ 事件腿通（09-14 落盘里确实有记录，但那批全是失败事件） |
| **7** | **按 U** → 点「**本周动态**」 | 日志 `awake_weekly_report_refresh status=…`，界面能显示 | `status=` 不是 `unavailable`，界面有内容 ⇒ **周报这条腿通了**（坏根 09-15 修过三笔，一直没在游戏里看过） |
| **8** | **存档 → 退出 → 重进 → 读档** | 看 `PlayerExports\AwakeState\` 下是不是**多了一个像乱码的目录**（那就是存档 id） | **出现带存档 id 的目录 ⇒ 通**；**又落进 `unbound` ⇒ 没通**（v0.2 卡这） |
| **9** | 读档后重做第 4、5 步各一次 | 刚才说过的话、发过的信还在不在 | 还在 ⇒ **"记得住"成立**，最小回环才真的闭上了 |

> 想省事的话，第 7 步也可以在 MCM 里直接点「打开世界周报」，第 6 步点「打开事件收件箱」——不用等它自然触发。

---

## 二、三个已知的坑：看到别慌，是老毛病

### 坑 1 · NPC 主动找你说话，张不开嘴（最关键的一条）

日志里会长这样：

```
npc_dialogue_open_failed … CharacterObject_1741:target_unavailable
```

**这次走查请务必把冒号前面那一串抄下来。** 09-14 那次落盘的三条，冒号前全是 `CharacterObject_1741`、`CharacterObject_1586` 这种——
**那是"兵种模板"的编号，不是人名。** 而代码里找人要求编号长成 `hero:某人` 这样带前缀
（`NpcDialogueLauncher.FindTargetById` 只认 `hero:` 开头的）。

⇒ **拿兵种编号去找英雄，当然永远找不到**，于是每次都记一句"目标不可用"就放弃了。
**这很可能就是"张不开嘴"的真病根**，而且它是**不用开游戏就能改的**——跟我原先以为的"只能真机复现"不一样。

**所以走查时请确认一件事**：这次冒号前那串，还是不是 `CharacterObject_` 开头？
- 是 ⇒ 病根坐实，回来我直接改（不用再开一次机）。
- 不是（比如变成了 `hero:xxx`）⇒ 那是另一回事，得重新查。

### 坑 2 · 写信：09-14 那次其实一封都没写成

不用翻日志，落盘文件里就写着：`PlayerExports\AwakeState\unbound\awake.letters.json` 里是
`"letters":[]`（**空的**），只有三条"推进"的痕迹。
⇒ 系统跑过，**但玩家没发出过一封信**。这就是"写信从未被触发过"的实证，不是推断。

### 坑 3 · 存档绑了个空（v0.2 卡这）

同一个 `AwakeState\` 目录下现在有两份：

| 目录 | 时间 | 说明 |
|---|---|---|
| `1IgZ8yHJynfn` | 09-12 00:02 | **带存档 id** ⇒ 那次绑对了 |
| `unbound` | 09-14 23:15–23:17 | **绑空了** ⇒ 那次没绑上，东西全落这儿 |

⇒ 说明**能绑对，后来又绑错了**（不是从来没对过）。走查第 8 步就是看这次落进哪个。

---

## 三、跑完之后，把这六样贴回来

1. 日志里有没有 `register_ok`（有／没有）
2. `worldbook_runtime_initialized` 那一行**原文**（看 `package=` 是两段还是三段、`entries=` 是多少）
3. MCM 打开了吗、是 6 组 36 项吗
4. 第 4 步三个日志串（`npc_dialogue_ready`／`npc_dialogue_turn_completed`／`transcript_turn_appended`）齐不齐
5. 第 5 步有没有 `letter_send_succeeded`；第 7 步 `awake_weekly_report_refresh status=` 是什么
6. **第 8 步：`AwakeState\` 下这次落的是哪个目录名**（存档 id 还是 `unbound`）
   另外：**坑 1 里冒号前那串抄下来了吗**

有报错就把报错原文一起贴（尤其是 `npc_dialogue_open_failed`、`awake_weekly_report_error`、`awake_world_inbox_error` 这几条）。

---

## 附 · 本单内容的出处（便于复核，不是让你读的）

| 内容 | 出处 |
|---|---|
| 终端按键 U（MCM 可改） | `src/AwakeConfig.cs:191` `TerminalKey = "U"` |
| 终端菜单五项 | `src/AwakeTerminalBehavior.cs:838-868`（附近对话／通讯录／事件收件箱／本周动态／开发者检查） |
| MCM 里的测试动作 | `src/AwakeDeveloperTestActions.cs`（打开收件箱／打开周报／重置主动状态／附近对话） |
| 对话三个日志串 | `src/` 内各 1–2 处：`npc_dialogue_ready`、`npc_dialogue_turn_completed`、`transcript_turn_appended` |
| 写信成功串 | `letter_send_succeeded`（`src/AwakeLetterService.cs`） |
| 周报串 | `awake_weekly_report_refresh status=…`（`src/AwakeEventBehavior.cs:189`）；面板 `weekly_report_panel_opened` |
| 目标不可用判定 | `src/SubModule.cs:177-181` ＋ `src/NpcDialogueLauncher.cs:362-380`（只认 `hero:` 前缀） |
| 落盘目录与"绑空" | `src/AwakeFileStorageService.cs:70,103`（空 id ⇒ 目录名 `unbound`） |
| 日志轮转只留一代 | `src/AwakeLog.cs` 的 `TryRotate` |
| 上一份同类清单 | `docs/CLOSURE-BASIC-GAMEPLAY-20260920.md`（§三 逐环节实测、§四 B 档） |

---

*—— 阿砚（主干线）· 2026-09-25*
