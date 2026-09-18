# AWAKE UI 画风基准（2026-09-13）

- 批次：`AWAKE-UI-ART-STYLE-BASELINE-20260913`
- 归属：图标 & 插画会话（乙方）产出，交 UI 管理会话（甲方）复核
- 证据性质：**全部实测**——原版贴图已从游戏里真抽出来看过、色值是从像素里量的、管线口径出自 TaleWorlds 官方 mod 文档 + 全库实地扫描
- 配套素材：`tools/awake-ui-lab/out/style-study/`（拼版图 4 张 + 对话系逐图），生成脚本 `tools/awake-ui-lab/_style_study_20260913.py`

---

## 0. 一句话结论

骑砍原版 UI 的语言是 **「暗色石板 + 鎏金细线 + 纯白剪影图标 + 凿刻几何」**，不是金色华丽的奇幻面板。
AWAKE 的气质是 **「同一块碑，不同身份读到不同层次」**（中立内核 + 分层观感）——
⇒ 视觉母题定 **碑刻 / 铭牌 / 信笺 / 名册**，走**克制、中性、无魔法感**的路线。

---

## 1. 骑砍原版 UI 视觉语言（实测拆解）

### 1.1 形状语汇（这些是原版的"词汇表"）

| 元素 | 原版做法 | 实测样例 |
| --- | --- | --- |
| 标题 | **碑额**：梯形/倒角的石板，深色顶带 + 赭石面色，两侧斜切 | `TitleHeader` 203×51 |
| 按钮 | **横牌**：中央鎏金/橄榄铜底 + 两端金属雕花收头 + 深色石边 + 细金描线 | `General\Button\main_button_regular` 271×64 |
| 方向 | **凿石箭头**：三角体，明暗两个切面（像打出来的石头） | `StdAssets\arrow_large_pointing_left` 64×70 |
| 关闭 | **蜡印圆章**：深酒红圆盘 + 金圈 + 压印的 X | `StdAssets\close_button` 44×44 |
| 翻页 | **绳缠横杠**：赭色横条上缠着交叉的绳纹 | `StdAssets\page_button_center` 203×53 |
| 面板底 | **暖黑石面**：极暗、微暖、带石纹/裂纹，几乎没有渐变 | `StdAssets\Popup\canvas`、`stone_texture_overlay` 512×512 |
| 进度/分隔 | **金属条**：金色渐变横条，两端渐隐或收尖 | `GradientDivider_9`、`relation_bar_fill` |
| 装饰笔触 | **手绘金 Flourish**：一笔刷出来的金色回旋 | `Conversation\conversation_hover_indicator` 39×20 |
| 对话选项条 | **D 形圆角片**（左平右圆），黑/白两版供染色 | `dialog_option_canvas_9` 48×44 |

### 1.2 配色（从像素里量出来的，不是目测）

| 用途 | 色值 | 来源（实测） |
| --- | --- | --- |
| **标题金** | `#E2AF54` | `Conversation.HeaderText` 的 `FontColor` |
| 文字悬停金 | `#FBC86A` | `ConversationItem.Text` Hovered |
| 金色图标/指示 | `#D8A850` | `click_to_cont_triangle`（100% 单色） |
| 金色进度条 | `#C09858` | `relation_bar_fill` |
| 按钮底（常规） | `#484030` → hover `#705838` | `main_button_regular` 主色占比 14.2% / 14.2% |
| 按钮底（确认） | `#485020`（橄榄绿） | `main_button_done` 19.3% |
| 按钮底（取消） | `#403028`（暖锈褐） | `button_cancel` 24.1% |
| 印章底 | `#180808` → hover `#381008` | `close_button` 12.2% / 9.0% |
| 面板底（暖黑） | `#181010` ~ `#201818` | `Popup\canvas` 37.4% / 13.1% |
| 滚动条底 | `#181818` ~ `#202020` | `scroller_bed` 30.0% / 11.2% |
| 石板灰 | `#707070` ~ `#909090` | `stone_texture_overlay` / `stone_texture_continuous` |
| 图标白 | `#F8F8F8` | 全部功能图标 100% 单色 |

**读法**：原版的"金"是**细线级**的（描边、图标、进度条），面积很小；**面**一律是暗的（暖黑/暗橄榄/锈褐）。
把大面积做成金色 = 立刻跑出骑砍的语感。

### 1.3 图标语法（做图标必须遵守的三条）

1. **单色剪影**：功能图标是纯 `#F8F8F8` 的实心形，**引擎靠染色/透明度改色**。
   ⇒ 交付的图标**不要自带颜色**（要白版），不要渐变，不要多色分区。
2. **厚重几何**：笔画粗、转折硬、没有描边线、没有内部细节线。`persuasion_success`（对勾）、`persuasion_pass`（箭头）、`block_icon`（禁止符）都是"一块料切出来"的观感。
3. **硬边黑投影**：部分图标自带 1–2px 的**纯黑偏移副本**（不是模糊阴影），用来在亮底上也看得清。
   ⇒ 需要跨明暗底使用的图标才加；加了就要在清单里注明。

### 1.4 九宫格（九宫格边距的正确写法）

- **边距写在 Brush 层，不写在 sprite 上**：`<BrushLayer Sprite="..." ExtendLeft/Top/Right/Bottom="n">`（单位＝sprite 像素）。
- 全库 10 个 `*SpriteData.xml` **没有任何一处** Extend 属性（唯一 grep 命中是 sprite 名 `TacticsExtendedSkirmish`，与九宫格无关）；而 Native 的 `Brushes/*.xml` 里 `ExtendLeft` 有 **109 处**在用，例如 `conversation_frame_9` = 18/18/18/18、`conversation_frame_canvas_9` = 16/16/16/16。
- 出图要求：**四角不要放会被拉伸的细节**；`Extend` 值 ≥ 装饰所占像素。

### 1.5 字体（⚠️ 中文化后会发生"字体塌陷"）

- 原版衬线标题字体 = **Galahad**（拉丁文）。
- 但 `Native/GUI/Fonts/NativeLanguages.xml` 里 **简体中文把所有字体都映射到 `simkai`（楷体）**：
  连 `Galahad` / `FiraSans*` 也 Map 到 `simkai`。
- ⇒ **中文环境下 AWAKE 没有"衬线 vs 无衬线"这一维可用**，一切都渲染成楷体。
  **层级只能靠：字号 / 颜色 / 装饰线 / 留白 / 字母间距。**
- ⇒ **sprite 里绝不能烤文字**（中文字形会 fallback、多语言会错）。

---

## 2. AWAKE 的设定与气质 → 视觉母题

| 设定事实（来自本仓文档） | 对画风的含义 |
| --- | --- |
| 世界书内核 = **中立内核 + 分层观感**：同一对象 × 六身份，村民听到什么、商人知道什么、贵族看到什么（`WORLDBOOK-ENCYCLOPEDIA-CHARTER`） | 母题＝**碑刻/铭牌**：内容刻在石上，不同的人读到不同层次。适合"层层叠压的石面 + 局部打磨发亮" |
| 内容是**编年史**质感、禁现当代科学视角（硬规矩第 1 条） | 禁：齿轮/分子/芯片/数据流；宜：抄本、印鉴、羊皮、拓片 |
| 产品诉求 = **"像和一个人持续相处"，不是翻数据库**（`AWAKE-ContactPanel-Concept`） | 面板要**安静、不喧哗**：低对比底 + 少量金色锚点，把注意力留给字 |
| 信使面板＝写信/来信 | 副母题 **信笺**：折痕、封蜡、火漆、绳结 |
| 联系人/名录 | 副母题 **名册**：竖排条目、细分隔线 |
| 场景选人已用 **候选金 `(1,0.84,0.2)` / 目标品红 `(1,0.22,0.72)`**（`PLAN-SceneVisualSelection`） | **功能高饱和色只用在"必须一眼分辨"的 3D 场景标记**；界面内仍守暗石板+金。二者不混 |

### 明确不要的东西（四条禁忌）

1. **不要拟物**：木纹、皮革、金属镜面反光。原版是"石与金属的平面化处理"，不是写实材质。
2. **不要科技感**：玻璃、霓虹、磨砂、发光边框。
3. **不要奇幻魔法**：发光符文、法阵、宝石镶嵌、彩窗。
4. **不要大面积金**：金只做线与点，不做面。

---

## 3. 画风需求（可执行规则）

### 3.1 色板（建议 AWAKE 专用 token，全部从原版实测值派生）

| Token | 值 | 用途 |
| --- | --- | --- |
| `bg.panel` | `#181010` | 面板底（暖黑，最常见） |
| `bg.panel.deep` | `#0F0B07` | 最深底（浮层后面） |
| `bg.inset` | `#0B0806` | 内凹区（输入框、列表槽） |
| `line.hairline` | `#8C6B38` @ 60% | 细金描线（1–2px，只描边） |
| `gold.title` | `#E2AF54` | 标题文字/标题装饰 |
| `gold.hover` | `#FBC86A` | 悬停文字 |
| `gold.icon` | `#D8A850` | 金色图标、指示器 |
| `gold.dim` | `#9C7839` | 次级金线、禁用态 |
| `fill.brass` | `#484030` → hover `#705838` | 常规按钮底 |
| `fill.confirm` | `#485020` | 确认类按钮底 |
| `fill.danger` | `#403028` + 印 `#5A1E14` | 取消/危险 |
| `text.primary` | `#F2EDE4` | 正文（原版纯白略偏暖，减刺眼） |
| `text.secondary` | `#C8B088` | 次要信息 |
| `text.muted` | `#8A7C66` | 弱提示 |
| `icon.white` | `#F8F8F8` | 全部功能图标（引擎染色） |

### 3.2 形状与笔画

- **圆角**：面板取 6–10px，条目取 4–6px。原版面板是"圆而不圆润"的方石感，别做胶囊。
- **描边**：细金线 **1–2px**，只描轮廓；内层可再加一条 1px 更暗的线做"石面厚度"。
- **厚度感**用**明暗两切面**表达（照 `arrow_*` 的做法），不用阴影模糊。
- **装饰只在端点/四角**，中段留白——中段会被九宫格拉伸。

### 3.3 图标网格与尺寸

- 基准网格 **24×24**，笔画宽 **2–3px**（24 网格下），端点方头不收圆。
- 出图给 **48×48 / 72×72**（2× / 3×）两档，PNG 透明底，纯白。
- 尺寸一律取 **5 的倍数**（原版习惯），落在 AWAKE 的 4/12/24 栅格上也对齐。

### 3.4 九宫格边距要求

- 交付时**逐个 sprite 给出 `ExtendLeft/Top/Right/Bottom` 建议值**（写在交付清单里，由甲方落到 BrushLayer）。
- 四角装饰的像素范围必须 ≤ 对应 Extend 值，中段必须是**可平铺/可拉伸的连续纹理**。

### 3.5 面板尺寸（沿用甲方 §4.1 清单，规格对齐后）

| ID | 尺寸 | 九宫格边距 | 备注 |
| --- | --- | --- | --- |
| P-MAIN-1280 | 1280×760 | 32/32/32/32 | 信使主面板 |
| P-MAIN-960 | 960×720 | 32/32/32/32 | 对话主面板 |
| P-SIDE-LEFT / RIGHT | 300×700 / 260×700 | 24/24/24/24 | 侧栏（竖条带） |
| P-INPUT | 800×60 | 16/16/16/16 | 输入区（内凹感） |
| D-DIVIDER-GOLD | 800×2 可拉 | 0（整条拉伸） | 分隔 |
| D-HEADER-BAND | 1280×60 | 24/0/24/0 | 顶栏（上下不拉伸） |
| B-BTN-110×60 / 105×35 / 100×35 | 同尺寸 | 16/16/16/16 | 主/次/次 |
| B-BTN-CLOSE | 40×40 | 12/12/12/12 | 蜡印章 |
| BB-NPC / BB-PLAYER | 600×72（纵向可拉） | 20/20/14/14 | 气泡，**不得烤文字** |
| BB-CONTACT | 280×85 | 14/14/14/14 | 联系人条目 |

---

## 4. ⚠️ 对甲方规格书 `UI-ART-SPEC-20260913.md` 的四处技术更正

我按官方文档 + 全库扫描核了一遍，甲方那份有三处口径需要改，另有一处阻塞：

### 4.1 贴图源文件的位置（§5 需改）

- 甲方写：`AWAKE/GUI/Textures/ui_awake_main.png`（一张 2048×2048 大图集）。
- **实际管线**（TaleWorlds 官方文档《Generating and Loading UI Sprite Sheets》）：源文件是 **一图一 sprite**，放在
  `Modules\<模块名>\GUI\SpriteParts\ui_<分类名>\<sprite名>.png`
  子目录会成为 sprite 名的一部分（官方例：`SpriteParts/<cat>/StdAssets/FactionIcons/ButtonIcons/<culture>.png` → sprite 名 `StdAssets\FactionIcons\ButtonIcons\<culture>`）。
- 本地旁证：`SimpleBank/GUI/SpriteParts/Bank/SPGeneral/SPTraits/`（对应 sprite 名 `SPGeneral\SPTraits\bank_0`）、`AnimusForge/GUI/GUI/SpriteParts/ui_account/discord.png`（对应 sprite `discord`）。
- **大图集是"生成产物"，不是手放的源文件。**

### 4.2 `SpriteData.xml` 是生成物（§5 需改）

- 官方文档：跑 `TaleWorlds.TwoDimension.SpriteSheetGenerator.exe` 会生成 `Assets/`、`AssetSources/` 两个目录，并在 `GUI/` 下生成 **`<模块名>SpriteData.xml`**。
- 全库命名旁证：`SimpleBankSpriteData.xml`、`DramalordSpriteData.xml`、`ROT-ContentSpriteData.xml` —— **文件名 = 模块名 + SpriteData.xml，直接放 `GUI/` 根下**（不是 `GUI/SpriteData/` 子目录）。
- ⇒ 手写 SpriteData 风险高（坐标得自己排），**应由生成器产出**。

### 4.3 九宫格不在 SpritePart 上（§7 表"已确认"需改）

- 甲方写：「9-slice 拉伸 已确认：`<SpritePart>` 的 Extend 字段」。
- **实测：全库 10 个 `*SpriteData.xml` 里 0 处 Extend 属性**；九宫格边距只出现在 `Brushes/*.xml` 的 `<BrushLayer ExtendLeft/Top/Right/Bottom>`（Native 109 处在用）。
- ⇒ 出图时要提供边距值，但**落地写在 Brush 里**。

### 4.4 还差一个硬需求：`Config.xml` + `AlwaysLoad`

- 官方文档：贴图分类**默认不加载**。要么写 C# 在打开界面时 `category.Load(...)`，要么在
  `Modules\<模块名>\GUI\SpriteParts\Config.xml` 里声明：
  ```xml
  <Config>
    <SpriteCategory Name="ui_awake_main"><AlwaysLoad/></SpriteCategory>
  </Config>
  ```
  声明后重跑生成器，`<模块名>SpriteData.xml` 里会出现 `<AlwaysLoad />`。
- 本任务约定**不碰 `src/*.cs`** ⇒ `AlwaysLoad` 是必需项，甲方规格书没写这条。

---

## 5. ⛔ 管线阻塞：本机没有 Modding Kit

- 官方流程要求 `SpriteSheetGenerator.exe`，位置 `安装目录\bin\Win64_Shipping_wEditor`。
- **实测本机 `bin/` 下只有 `Win64_Shipping_Client`，没有 `wEditor`** ⇒ 生成器与"资源浏览器导入"两步都做不了。
- 后果：**自绘贴图线现在跑不通**（除非找到不需要该工具的路子）。

### 5.1 ~~一步实验~~ 已否决：AnimusForge 不能当样本（09-13 19:5x 复核）

原本打算借 `AnimusForge` 验证"松散 PNG 能否免打包加载"。**复核后否决**，理由（均为实测）：

- AF 的 sprite 索引 `GUI/SplitShadowsOnlyAISpriteData.xml` **只声明 `ui_account` / `ui_achievement` / `ui_subscribe` 三个分类**，而这三个分类**没有图集**；
- 唯一有图集的分类 `af_vassalage_notifications`（`GUI/SpriteSheets/af_vassalage_notifications/af_vassalage_notifications_1.png`）**不在索引里**；
- 索引文件名不合官方惯例（官方为 `<模块名>SpriteData.xml`，它叫 `SplitShadowsOnlyAI…`）⇒ 疑似从别处拷来的残留；
- 结构上还多了一层 `GUI/GUI/`（48 个重复副本），而游戏只扫模块根下**一层** `GUI/`。

⇒ **AF 自身的 sprite 加载本就不可控，观察它得不出引擎行为。该实验作废，别去跑。**

全库盘点（10 个带 UI 资源的模组）：同时具备 `SpriteParts` + `SpriteSheets` + `SpriteData` 的**只有 AF 一个** ⇒ **本机没有干净样本**。

⇒ 要定"松散 PNG vs 必须打包"，只能**直接读引擎的 sprite 加载代码**（`TaleWorlds.TwoDimension` / `TaleWorlds.GauntletUI`），或装 Modding Kit 实测。**这条仍卡着 M0 的真机验收。**

---

## 6. 交付规格（乙方产出物）

### 6.1 目录与命名（等甲方确认管线后落盘）

```
AWAKE/GUI/
  SpriteParts/
    Config.xml                      # 分类 AlwaysLoad 声明
    ui_awake_main/                  # 面板底、按钮、分隔、条目
      panel_main_1280.png
      panel_main_960.png
      btn_primary_110x60.png
      ...
    ui_awake_icons/                 # 纯功能图标（白剪影）
      icon_send.png
      icon_close.png
      ...
    ui_awake_bubble/                # 气泡
  <AWAKE>SpriteData.xml             # 生成器产出（不由我手写）
  Brushes/AwakeBrushes.xml          # BrushLayer + Extend 边距 + 状态切换
```

- 分类名以 `ui_` 开头（官方硬要求）。
- sprite 名与文件名一致，**不带 `Awake\` 前缀**（分类已隔离命名空间）；若要与原版做区分，用文件名前缀 `awake_`。

### 6.2 交付清单（每个 sprite 必须带这 6 项）

| 字段 | 例 |
| --- | --- |
| 文件名 | `panel_main_1280.png` |
| sprite 名 | `panel_main_1280` |
| 像素尺寸 | 1280×760 |
| 九宫格边距 | 32/32/32/32 |
| 状态 | Default（另附 Hover 版或给 Factor） |
| 用途 | 信使主面板底 |

### 6.3 首批（M0）建议顺序

1. `panel_main_960`（对话主面板）+ `panel_main_1280`（信使主面板）——换掉原版 `Popup\canvas`，观感提升最大；
2. `panel_input`；
3. `btn_primary_110x60` + `btn_secondary_100x35` + `btn_tab_105x35`（各 Default + Hover）；
4. `divider_gold`；
5. `icon_close`（蜡印章）。

---

## 7. 待验证清单（不许当结论用）

| 项 | 状态 | 验证方法 |
| --- | --- | --- |
| 松散 PNG 是否免 Kit 直接加载 | **待验证（决定整条线）** | ~~§5.1 一步实验（已否决）~~ ⇒ 改读引擎 sprite 加载代码，或装 Kit 实测 |
| `SpriteParts` 路径 | **已知＝`GUI/SpriteParts`** | 官方文档 ＋ `SimpleBank/GUI/SpriteParts/` 一致；AF 那层 `GUI/GUI/` 是残留副本，不采信 |
| sprite 名是否需含 `Awake\` 前缀避免撞名 | **待验证** | 分类已隔离，理论上不需要；撞名时引擎行为未知 |
| 楷体是否只有单一字重 | **待验证** | 查 `simkai` 字体资源；若只有一字重，"层级"只能靠字号/颜色 |
| `Config.xml` 与生成器的先后顺序 | 已知需"先写 Config 再生成" | 官方文档 |

---

## 8. 复现命令

```bash
cd D:/AWAKE-Dev/AWAKE/tools/awake-ui-lab

# 画风拼版（4 张）+ 对话系逐图  ->  out/style-study/
python _style_study_20260913.py

# 配色实测 + 九宫格口径  ->  stdout
python _style_colors_20260913.py

# 全库 sprite 管线实地扫描  ->  stdout
python _spritepipe_20260913.py

# 抽原版对话界面贴图集（4096×256 BC7）
G="D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord"
./out/GauntletUI-LSP/schema-indexer/bin/Debug/net8.0/schema-indexer.exe \
  --game-bin-path "$G/bin/Win64_Shipping_Client" --resource-path "$G/Modules" \
  --extract-sheet ui_conversation_1 --output "out/atlas/ui_conversation_1.png"

# 中文字体塌陷证据
grep -A 10 '简体中文' "$G/Modules/Native/GUI/Fonts/NativeLanguages.xml"

# 九宫格只在 Brush 层
grep -rc "ExtendLeft" "$G/Modules/Native/GUI/Brushes/"*.xml | grep -v ':0'
grep -l "Extend" "$G/Modules"/*/GUI/*SpriteData.xml   # 只命中 sprite 名，不是属性

# Modding Kit 是否就位
ls -d "$G/bin/Win64_Shipping_wEditor"
```

官方依据：<https://moddocs.bannerlord.com/zh_cn/asset-management/generating_and_loading_ui_sprite_sheets/>
