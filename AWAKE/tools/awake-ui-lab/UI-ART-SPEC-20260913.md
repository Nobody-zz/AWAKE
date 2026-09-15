# AWAKE UI 美术资产规格书（2026-09-13）

> 写给"负责创作新美术资产"那个会话看的需求文档。
> 工作流：你按这份规格生产 PNG/9-slice/SpriteData XML，产出的资产交还我（UI 会话 e9d223be）做集成、wire 到 Prefab、构建与真机验收。

## 0. 一句话目标

为 AWAKE 的两个对话窗口（信使 Messenger 1280×760、对话 NpcDialogue 960×720）提供一套**自绘的暗色石板 + 暗金衬线**的视觉皮肤，让真机观感对得起"骑砍质感"四个字。
当前的尴尬现状：UI 骨架已重设计（提交 `4b3da30`），但皮肤仍沿用原版 `Popup.*` 系 Brush 和 `StdAssets\Popup\canvas` 灰扑扑底图——观感上等于没改。用户原话：「观感上实在没有什么大的变动。我觉得很丑」。

## 1. 工作树现状（接手必读）

- 提交链：HEAD = `4b3da30` ui(messenger/dialogue): 整体重设计骨架（已提交，用户授权）；
  **用户明确要求本会话后续改动不提交**，先留在工作树等真机满意再一起收口。
- 工作树里**未提交**的临时改动（XML 已落地但未提交，集成美术时以这些为基线）：
  - `GUI/Prefabs/AwakeMessenger.xml` — 主底图从 `StdAssets\Popup\canvas` 换为 `stone_texture_overlay`（暗褐染色 `#1A140EFF`、α 0.92）；按钮/Tab/联系人条目切换到 `ConversationItemBrush`/`ConversationItemBrush.Simple`；顶栏标题 brush 改为 `Conversation.HeaderText`（Galahad 金色）；气泡切到 `dialog_option_canvas_9` 与 `dialog_option_canvas_white_9`；左右栏底改为 `dialog_option_canvas_9`。
  - `GUI/Prefabs/NpcDialogue.xml` — 主底图换 `npc_dialogue_panel_9`；其余同上系。
  - 这些 sprite **原生是真实存在的**（Native 的 Conversation 皮肤），真机里能正确显示；但**本地预览器（awake-ui-lab）的 atlas 没有它们**——所以本地 HTML/PNG 预览画不出新皮肤，这是 lab 的覆盖缺口，不是 sprite 问题。
  - 工作树里**还有一笔真 bug**：之前我笔误写了非法色值 `#FF1A140EFF`（10 位），已当场修成 `#1A140EFF`（合规 `#RRGGBBAA`）。请以当前 `git diff` 为准。
- VM/Models 已就绪：NpcDialogueChatRowVM 加了 `IsFromPlayer`/`IsFromNpc`，聊天气泡分侧已绑定；MessengerVM 加了 `IsChatTabSelected`/`IsNegotiationTabSelected`/`IsInputEmpty`/`InputHintText`/`HeaderText`；新本地化键 `awake.ui.messenger_header`/`awake.ui.input_hint`/`awake.ui.transcript_tab` 已在英文与 CNs 两个 XML 里就位。集成时直接用现成 VM 属性即可，**不需要再动 VM**。

## 2. 接入路径（已取证，需复核）

**主线方案：SpriteData XML + 自有 atlas PNG。**

证据：游戏 `Modules/Native/GUI/NativeSpriteData.xml` 单一文件用如下结构定义全部 6354 个 sprite：

```xml
<SpriteData>
  <SpriteCategories>
    <SpriteCategory>
      <Name>ui_<sheet-name></Name>
      <SpriteSheetCount>1</SpriteSheetCount>
      <SpriteSheetSize ID="1" Width="2048" Height="2048" />
    </SpriteCategory>
  </SpriteCategories>
  <SpriteParts>
    <SpritePart Name="My\MySprite"
                Width="512" Height="128"
                SheetX="0" SheetY="0"   <!-- 待补：图集坐标系 -->
                Category="ui_<sheet-name>"
                ExtendLeft="16" ExtendRight="16" ExtendTop="16" ExtendBottom="16" />
  </SpriteParts>
</SpriteData>
```

Bannerlord 引擎会扫描模块根目录下的 `ModuleName/GUI/*.xml` 加载 SpriteData（具体是 `<Module>/GUI/SpriteData/*.xml` 还是 `<Module>/GUI/XxxSpriteData.xml`，**需要在 `Modules/CustomBattle`/`Bannerlord.Diplomacy`/`PlayerSettlement` 等模块实地复现加载路径**，本规格书暂以 Native 单一文件结构为基线、标"待验证"）。

**fallback 方案（如果主线接入成本过高）：**
- A) 全部用 Native 已有的 sprite 重组（`stone_texture_overlay` 染色、`dialog_option_canvas_9`、`main_button_regular` 加 `ButtonBrush2`、`GradientDivider_9` 替代、character_creation_background_gradient、name_input_area 等）；
- B) 配合 `BlankWhiteSquare_9` + `Color="#RRGGBBAA"` + `AlphaFactor` 调出色块（无真实纹理但能改气质）；
- C) 上述 A+B + 用户许可下，加 `Stone_texture_*` 系列石板纹理叠加层。

**硬红线**（来自 `docs/UI-ASSET-CATALOG-20260913.md §2 Marcus 参考模组对话覆盖层`）：
- 不得复用 `MarcusAINpcDialogueOverlay.xml` 的 sprite / brush；
- 不得从 Native/官方模块抽取 sprite 后原样塞进 AWAKE（避免"打包复制 Vanilla 资产"嫌疑）—— Native sprite **可以直接引用其原名**（引擎允许），但不能整段复制其资源文件到 AWAKE 模块下当作"自绘"；
- 改/调 Native sprite 形态（如改色、做衍生）也属于衍生品，**先报备再决定**。

## 3. 设计方向

**基线：** 暗色石板（#0F0B07 / #1A140E 范围）+ 暗金描边（#C8A468 主金、#9C7839 副金、#E2AF54 亮金）+ 暖白文字（#E8D8C0 主、#C8B088 次）。
**字体：** 标题用 `Conversation.HeaderText` Brush（自带 Galahad + 描边光晕）；正文用 `ConversationItem.Text` 或自定义衬线；**Galahad 对中文无效**（自动 fallback），这是引擎限制不是 bug，不要试图在 sprite 里塞中文。
**风格：** 仿中世纪石板 + 金线雕花，不要拟物（不要木纹/皮革/金属反光），不要科技感（不要玻璃/霓虹/磨砂）。参考对照：Native `npc_dialogue_panel_9`（已在用）和 `conversation_frame_9` 的金线质感方向。
**状态：** 按钮/Tab/输入框需至少 Default/Hover/Disabled 三态；Pressed 走 α/ColorFactor 派生即可。

## 4. 资产清单（按优先级）

> 单位 px，所有 Sprite 均为 9-slice（带 `_9` 后缀，或 `<SpritePart>` 显式带 ExtendLeft/Right/Top/Bottom）。
> 所有坐标/尺寸**取 5 的倍数**，延续现有 4pt/12/24 栅格体系（参考 `awake-ui-lab/out/_check_prefab_bindings.py` 的栅格规则）。

### 4.1 面板底（最高优先）

| ID | Sprite 名 | 推荐尺寸 | Extend (L/R/T/B) | 用途 |
|---|---|---|---|---|
| P-MAIN-1280 | `Awake\panel_main_1280_9` | 1280×760 | 32/32/32/32 | 信使主面板底（替换 `stone_texture_overlay`） |
| P-MAIN-960 | `Awake\panel_main_960_9` | 960×720 | 32/32/32/32 | 对话主面板底（替换 `npc_dialogue_panel_9`） |
| P-SIDE-LEFT | `Awake\panel_side_left_9` | 300×700 | 24/24/24/24 | 信使左栏（300 宽，竖向条带） |
| P-SIDE-RIGHT | `Awake\panel_side_right_9` | 260×700 | 24/24/24/24 | 信使右卡（260 宽，竖向条带） |
| P-INPUT | `Awake\panel_input_9` | 800×60 | 16/16/16/16 | 输入区底（替换 `name_input_area`） |

### 4.2 装饰条/分隔

| ID | Sprite 名 | 尺寸 | 用途 |
|---|---|---|---|
| D-DIVIDER-GOLD | `Awake\divider_gold_9` | 800×2 (可拉伸) | 顶栏下方分隔（替换 `GradientDivider_9`） |
| D-HEADER-BAND | `Awake\header_band_9` | 1280×60 | 顶栏背景（替换 `character_creation_background_gradient`） |

### 4.3 按钮/Tab

| ID | Sprite 名 | 尺寸 | 状态 | 用途 |
|---|---|---|---|---|
| B-BTN-105x35 | `Awake\btn_default_105x35_9` | 105×35 | D/H/P | Tab 按钮（闲聊/交涉/往来） |
| B-BTN-100x35 | `Awake\btn_secondary_100x35_9` | 100×35 | D/H/P | 次按钮（写信） |
| B-BTN-110x60 | `Awake\btn_primary_110x60_9` | 110×60 | D/H/P/Ds | 主按钮（发送） |
| B-BTN-40x40 | `Awake\btn_close_40x40_9` | 40×40 | D/H/P | 关闭 ×（替换 `Popup.CloseButton`） |

状态说明：D=Default、H=Hovered（叠暖金边）、P=Pressed（α 0.9）、Ds=Disabled（α 0.4）。状态通过不同 SpritePart 引用实现，或在 Brush XML 的 `<Style>` 块里用 `<BrushLayer HueFactor/SaturationFactor/ValueFactor/AlphaFactor>` 派生。**所有按钮都需要 Brush XML 包装**（不是直接 `Sprite=`）才能用 IsEnabled/IsSelected 状态切换——参考 Native `Brush="ButtonBrush2"` 的两层（Default + HoverLayer）结构。

### 4.4 聊天气泡（次优先）

| ID | Sprite 名 | 尺寸 | Extend | 用途 |
|---|---|---|---|---|
| BB-NPC | `Awake\bubble_npc_9` | 600×N (Cover) | 20/20/14/14 | NPC 气泡底（左对齐，暖灰染色） |
| BB-PLAYER | `Awake\bubble_player_9` | 600×N (Cover) | 20/20/14/14 | 玩家气泡底（右对齐，暗金染色） |
| BB-CONTACT | `Awake\contact_item_9` | 280×85 | 14/14/14/14 | 联系人条目底（替换 `ConversationItemBrush.Simple`） |

注意气泡宽 600 是固定值（`<Widget WidthSizePolicy="Fixed" SuggestedWidth="600">`），但 CoverChildren 高度自适应——sprite 9-slice 必须能纵向无限延伸不被拉伸变形。**气泡正文用 RichTextWidget，不要在 sprite 里画文字**——这是动态内容，不能烤进 PNG。

### 4.5 可选（不强求）

- `Awake\portrait_frame_9` — 角色卡头像框（替换 `frame_9` 金边）；
- `Awake\ornament_corner_tl_9` 等四角装饰；
- `Awake\title_namestone_9` — 顶栏居中的"刻名石板"装饰（参考 `SPKingdom\Decision\namestone`，那是 Native 的，**只作风格参考不可复用**）。

## 5. 命名与放置

```
AWAKE/
  GUI/
    SpriteData/
      AwakeSpriteData.xml    # 新增：sprite 定义文件
    Brushes/
      AwakeBrushes.xml       # 新增：brush 定义文件（含状态切换）
    Textures/
      ui_awake_main.png      # 新增：atlas 大图集（推荐 2048×2048 RGBA）
      ui_awake_extra.png     # 可选：第二张大图集
```

- 资产 PNG 用 **RGBA**，背景透明，**不带** 已合成 alpha 的"成品图"（引擎会自己合成）。
- Sprite 名统一前缀 `Awake\`，避免和 Native 命名空间冲突。
- 文件夹与文件名进 git（当前这两个目录可能不存在，需创建；项目 .gitignore 检查见下）。
- 检查 `.gitignore` 里有没有忽略 `AWAKE/GUI/Textures/`——若被忽略，需修改 .gitignore 或把资产放在其它已跟踪目录。

## 6. 验证流程

美术会话产出后，我会做：

1. **静态校验**：`awake-ui-lab` 的 `awake_ui.py check` 跑 Brush 命中（要求 100% 命中 Native+Awake 库）；
2. **构建**：`dotnet build -c Release -p:BannerlordApi=1.3.15 --nologo -v:q` 必须 0/0；
3. **本地预览**：awake-ui-lab 的 HTML 预览需要 **更新 sprite atlas 索引**——`_sprite_pilot.py` 当前只覆盖 33 个 sprite（未含 Native 对话系），需扩到含 Awake 系。这是我端的二次开发，需要新索引流程（不在你的产出范围，但你的产出决定它的内容范围）；
4. **真机视觉验收**（用户 E4）：游戏内观察，**最终视觉以此为准**——HTML/PNG 预览仅作几何参考。

## 7. 风险与开放问题

| 项 | 状态 | 说明 |
|---|---|---|
| 自定义 SpriteData 是否自动加载 | **待验证** | Native 走单文件 `GUI/NativeSpriteData.xml`；mod 应放在 `AWAKE/GUI/SpriteData/AwakeSpriteData.xml` 还是 `AWAKE/GUI/AwakeSpriteData.xml`，需打开 `CustomBattle`/`PlayerSettlement`/`TournamentOfChampions` 等模块实地验证。若失败，回退 §2 fallback A |
| 中文 font fallback | 已确认 | Galahad 不含中文 glyph，会 fallback 到模块默认字体（中文环境下通常 OK） |
| Brush State 切换 | 已确认 | Brush XML 内 `<Style>` 多态支持 |
| 9-slice 拉伸 | 已确认 | `<SpritePart>` 的 Extend 字段 |
| 用户是否要 accept 这种改造 | **需用户在真机确认** | 美术产出的资产替换进 Prefab 后，用户得在游戏里看过才算合格 |

## 8. 交付清单（你的产出）

最小可行集（M0，先交这个）：
- [ ] P-MAIN-1280 + P-MAIN-960（共用一张也可，分别拉伸）
- [ ] P-INPUT
- [ ] B-BTN-110x60 + B-BTN-105x35 + B-BTN-100x35（含 Default+Hover，至少这两态）
- [ ] D-DIVIDER-GOLD
- [ ] `AwakeSpriteData.xml`（包含上述 sprite 定义）
- [ ] `AwakeBrushes.xml`（ButtonBrush2 风格的两层 Brush，含状态切换）

后续迭代（M1）：气泡、角色卡框、四角装饰。

## 9. 协同约束

- 不需要碰 `src/*.cs`（VM 全部已就绪）；
- 不需要碰 `ModuleData/Languages/`（本地化键已加好）；
- 不需要碰 `docs/control-plane/`；
- `AWAKE/.gitignore` 若忽略 GUI/Textures/，要由你或我后续补登记；
- 产出后告诉我 sprite 名清单 + PNG 文件路径，我做集成（改 Prefab 把 `Sprite="Awake\..."` 写进去、把 `Brush="Awake\..."` 写进去）；
- 集成后先在本地 HTML 预览看效果，**真机视觉验收由用户拍板**。