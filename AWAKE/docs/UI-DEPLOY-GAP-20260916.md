# UI 面板"做好了吗"· 部署侧现状与缺口（2026-09-16）

> 起因：甲方 09-16 11:40 问「对话、送信的 UI 面板做好了吗」。
> 结论：**纸面上做完了，游戏里都没跑起来。** 本文件只记**取证**与**缺口归属**，不重复面板设计文档。
> 取证时间 2026-09-16 11:4x。

## 1. 取证（全部实读，非推断）

### 1.1 两端入口是通的

| 面板 | 入口 | 加载的 Prefab |
|---|---|---|
| 对话 | `src/NpcDialogueOverlay.cs:120` | `_layer.LoadMovie("NpcDialogue", …)` |
| 送信 | `src/AwakeMessengerOverlay.cs:92` | `_layer.LoadMovie("AwakeMessenger", …)` |

### 1.2 游戏目录里躺的是旧版

| 文件 | 仓库（`AWAKE/GUI/Prefabs/`） | 游戏目录（`Modules/AWAKE/GUI/Prefabs/`） |
|---|---|---|
| `NpcDialogue.xml` | **23767 B · 09-15 09:01**（三列版） | **10492 B · 09-13 21:58**（三列之前那版） |
| `AwakePortraitSlot.xml` | 11497 B · 09-15 09:01 | **不存在** |
| `AwakeMessenger.xml` | 24079 B · 09-13 21:58 | 24079 B · 09-13 21:58（一致） |
| `AwakePortraitProbe.xml` | 6879 B · 09-14 11:15 | 6879 B · 09-14 11:15（一致） |

⇒ 游戏目录最后一次界面部署是 **09-14 11:25**；**09-15 的三列改写从未出去过**。

### 1.3 我们自制的 Brush 文件根本不在游戏树里

- `Modules/AWAKE/GUI/Brushes/` **目录不存在**；全 `Modules/` 下 `find -name AwakeBrushes.xml` = **0 命中**。
- 而**已部署**的那份 `NpcDialogue.xml` 引用了 7 个 `Awake.*` Brush
  （`Awake.Panel.Main960` / `Awake.Panel.Input800` / `Awake.Button.Close` / `Awake.Button.Primary` /
  `Awake.Button.Tab` / `Awake.Ornament.HeaderBand` / `Awake.Ornament.DividerGold`）。
- 全游戏目录 grep `Awake.Panel.Main960`：**只命中那个 Prefab 自己**（即没有任何 Brush 定义）。

### 1.4 自制贴图游戏读不到（与 09-14 真机日志对得上）

- `Modules/AWAKE/` 下**没有 `AssetPackages/`**、模块内 **0 个 `.tpac`**。
- `GUI/AWAKESpriteData.xml` 三个 category 全带 `<AlwaysLoad />`（`ui_awake_button` / `ui_awake_frame` / `ui_awake_ornament`）。
- 09-14 23:14 真机日志：`Cannot find texture: ui_awake_button_1 / ui_awake_frame_1 / ui_awake_ornament_1`。
  ⇒ `<AlwaysLoad>` 让引擎在启动时就去找这三个图集，**找不到**（图集 PNG 只在 `GUI/SpriteParts/` 里散着，
  没经过 Modding Kit 的 Import 打成 `.tpac`）。
- `AWAKESpriteData.xml` 里**没有 `ui_awake_icon` category**（28 张 `icon_*.png` 无登记）⇒ 与「最后一公里」断点 4 同一条。

## 2. 缺口归属（谁修哪块）

| # | 缺口 | 归线 | 说明 |
|---|---|---|---|
| 1 | `tools/sync_module.ps1` 的 `$managedGuiFiles` **只列了 7 个 Prefab**，缺 `GUI\Brushes\AwakeBrushes.xml`、`GUI\AWAKESpriteData.xml`、`GUI\SpriteParts\`（目录）、`GUI\Prefabs\AwakePortraitSlot.xml` | **主干 · 运行时**（`tools/`） | 不补，GUI 侧资产**永远推不出去**（游戏目录里那几件是 09-13 手工放的） |
| 2 | Import 出 `.tpac`（Modding Kit 控制台 `resource.show_resource_browser` → Scan → Import） | **美术资产线** | 全链唯一不能脚本化的一步；不做，所有自制贴图在游戏里都读不到 |
| 3 | `AWAKESpriteData.xml` 缺 `ui_awake_icon` category ＋ 28 张图标没登记；`ui_awake_frame` 图集已满（4088/4096）⇒ 212×360 画位框装不下，须新开 `ui_awake_slot` | **美术资产线** | 「最后一公里」断点 4 |
| 4 | 颜色通道位序：全库 45 处（本线 41 ＋ 主干 `AwakePortraitProbe.xml` 4）⇒ **三列底色现在全是透明的** | **UI 编辑层** ＋ 主干 4 处 | 见 `UI-REF-ALICEMM-20260915.md` §4 |
| 5 | 对话面板生图按钮的提示词口子留空（`BuildPortraitPrompt()` 返回空串）⇒ 点了不出图，只提示"提示词还没定下来" | **等甲方** | 甲方 09-15「先别动提示词，我还没做」 |
| 6 | `AwakeMessenger.xml`（送信）没有页级设计稿与验收文档（对话面板有 `UI-PAGE-NPC-DIALOGUE.md`） | **UI 编辑层** | 它比对话面板旧（09-13 一次成稿，没走 07 稿那套落稿流程） |
| 7 | 名册行/对话行仍穿官方 `ButtonBrush1` 的皮 | **UI 编辑层** | 缺标准件 `list_row` / `contact_item`，等风格统一案批准 |

## 3. 给主干的交接提示词（可直接粘贴）

> AWAKE 项目 · `AWAKE/tools/sync_module.ps1` 的 `$managedGuiFiles`（约第 26–34 行）只列了 7 个
> `GUI\Prefabs\*.xml`，遗漏了 4 项，导致 **GUI 侧资产永远无法通过部署脚本进游戏目录**：
> ① `GUI\Brushes\AwakeBrushes.xml`（本模组全部自有 Brush 定义；游戏目录里**没有** `GUI\Brushes\` 目录，
> 已部署的 `NpcDialogue.xml` 引用的 7 个 `Awake.*` Brush 在游戏树里搜不到任何定义）；
> ② `GUI\AWAKESpriteData.xml`（sprite 登记表，三个 category 带 `<AlwaysLoad />`）；
> ③ `GUI\SpriteParts\**`（自制贴图源 PNG，含 `Config.xml`）；
> ④ `GUI\Prefabs\AwakePortraitSlot.xml`（09-15 新增的画位控件，被 `NpcDialogue.xml` 引用，漏了会在游戏里加载失败）。
> 现在游戏目录里这几件是 **09-13 手工放的**，已落后。
> 请补进受管清单（注意 ③ 是**目录**，参照既有 `$managedWorldbookDirectories` 的写法），
> 并顺带把 `tools/tests/sync_module.Tests.ps1` 里断言受管清单的 fixture 一并更新。
> 归属：`tools/` 属主干线；本次未代改。
> 依据：本文件 `docs/UI-DEPLOY-GAP-20260916.md` §1.3 / §1.4。

## 4. 现在**不该**做的事

- **先别急着手工把三列版 `NpcDialogue.xml` 拷进游戏目录。** 缺口 1、2 没解决之前，
  它引用的 Brush（`Awake.Panel.Main960` 等）在游戏里一律解析不到，贴图也读不到
  ⇒ 推上去只会得到一块**没有底、没有框、没有字色**的东西，比现在那版更难看出问题。
  **顺序应是：先补清单 ＋ 出 `.tpac`，再推面板，再开游戏看。**

---

## 5. 追加（2026-09-19）：§2 的缺口 2 被证伪了一半，另一半更硬了

本条**不改写上面任何一句**，只追加后来的取证结论。全文见
`docs/UI-SPRITE-ATLAS-LOOKUP-20260919.md`。

**背景**：另一条线（Leverage）判定「图集放错目录」，把图集从 `AssetSources/` 摆到
`GUI/SpriteSheets/<分类名>/`，并据此认为「Import 出 `.tpac` 不是必需的」。
**复检结论：命名规则对，落点结论错。**

**追加的三条硬事实**：

1. **摆完没有生效**。目录 mtime 17:14，17:21 启动、现在还在跑的那一局
   （`rgl_log_33016`）报的还是 `Cannot find texture: ui_leverage_1`。
   09-19 那 5 次启动全部同句。文件本身合法（128×128，与声明对上）。
2. **原版根本不从 `GUI/SpriteSheets/` 读**。`Modules/Native/GUI/` **没有这个目录**；
   而 `Native/AssetPackages/gauntlet_ui.tpac`（323 MB）里搜得到
   `ui_conversation_1` / `ui_barter_1` / `ui_bannericons_1` / `ui_bannerbuilder_1`。
   **名字在包里。**
3. **全机 108 个模块，29 个有 `AssetPackages/`，只有 2 个有 `GUI/SpriteSheets/`，
   这 2 个没有任何成功记录。** 唯一资产管线齐全的样本 `SimpleBank` 走的是
   `AssetPackages/pack0.tpac`（内含 `Bank_1` ＋ `$BASE/…/AssetSources/GauntletUI/Bank_1.png` 源路径）。
   ⇒ **`AssetSources/` 是源、不是运行时落点；AWAKE 现在放的就是那里，这一点是对的。**

**修正 §2 的表述**（原句「Import 出 `.tpac`…全链唯一不能脚本化的一步」）：

- 「必须要有 tpac」这个方向，**今天的证据是加强的**，不是推翻。
- 但「**只能**手点 Import」这句**收窄**：`SimpleBank` 那张 `_tex.tpac` 只有 **479 字节**，
  内容就是「名字 ＋ 源路径 ＋ 格式」。若这种壳能被运行时接受，这一步就能脚本化。
  **未验，别当结论。**

**给 AWAKE 侧的动作**：`GUI/SpriteSheets/` 现在**是空的，别往里放东西**；
三张图集（`ui_awake_button_1` / `frame_1` / `ornament_1`）已在 `AssetSources/GauntletUI/`
且尺寸与声明逐一对上，**保持原位**。
