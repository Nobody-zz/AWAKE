# AWAKE UI 美术资产 · 对接契约

- 批次：`AWAKE-UI-ART-HANDOFF-20260913`
- 建立时间：2026-09-13 19:30
- **甲方（收资产）**：UI 管理会话 — 负责 UI Lab / Prefab / 运行时接线 / 面板布局
- **乙方（产资产）**：图标 & 插画 会话 — 负责图标、插画、贴图、可编辑设计稿
- 口径：本文件只写**通道、规格、需求表**；不复述双方各自的正文工作

---

## 一、通道（先看这条：本机两个对话之间没有直连）

已核实：本机 WorkBuddy **没有跨会话消息能力**（无 messaging 工具；Agent Mail 未开通）。所以走**仓库内文件**当信箱：

| 通道 | 位置 | 谁读 |
| --- | --- | --- |
| **本契约（主信箱）** | `AWAKE/docs/UI-ART-ASSET-INTERFACE-20260913.md` | 双方 |
| 共享工作日志（通知） | `.workbuddy/memory/2026-09-13.md` | 本工作区所有会话 |

**用法（各写各的节，不覆盖对方）**：

- 甲方：在**§4 需求登记表**追加行 → 乙方按行交。
- 乙方：在**§6 交付记录**追加行 + 落盘路径 → 甲方取用。

---

## 二、乙方能交付的形态（挑一个即可）

| 形态 | 产物 | 适合 |
| --- | --- | --- |
| **A. 位图贴图** | PNG（透明底 RGBA），按 §3 装箱 | 图标、九宫格底板、纹样、小幅插画 |
| **B. 可编辑设计稿** | 画布稿（矢量/组件），可再改、可复导出 | 需反复调的图标组、组件族、留要改的余地 |
| **C. 大幅插画** | 生成图，位图 | 背景氛围图、开屏、事件插图 |
| **D. 规格书** | 尺寸 / 九宫格边距 / 命名 / 色值 | 甲方自己找人或自己画时 |

> 需要 A 还是 B：**看这东西之后还要不要改**。定稿不再动 → A；还要调比例/配色 → B。

---

## 三、技术交付规格（实测口径，不是猜的）

证据来源：本机原版模块与本机第三方模组目录（见文末复现命令）。

### 3.1 贴图源文件（⚠️ 本节 2026-09-13 19:5x 已更正，以官方文档为准）

```
AWAKE/GUI/SpriteParts/ui_<分类名>/<sprite名>.png     ← 手放的源文件，一图一 sprite
AWAKE/GUI/SpriteParts/Config.xml                     ← 分类 AlwaysLoad 声明
AWAKE/GUI/SpriteSheets/<分类>/<分类>_<SheetID>.png    ← 生成产物，不是手放的
```

- 官方依据：TaleWorlds 官方 mod 文档《Generating and Loading UI Sprite Sheets》——在 `GUI/SpriteParts/ui_{分类}/` 放单个 PNG，跑 `TaleWorlds.TwoDimension.SpriteSheetGenerator.exe` 生图集与索引。
- 本地旁证：`SimpleBank/GUI/SpriteParts/Bank/SPGeneral/SPTraits/`（对应 sprite 名 `SPGeneral\SPTraits\bank_0`）、`AnimusForge/GUI/GUI/SpriteParts/ui_account/discord.png`（对应 sprite `discord`）。
- 生成产物实测样例：`AnimusForge/GUI/SpriteSheets/af_vassalage_notifications/af_vassalage_notifications_1.png` → **PNG / 8bit RGBA / 1024×1024**。
- ⇒ **不要手排坐标**：方框坐标由生成器排，SpriteData 由它产出。

### 3.2 Sprite 索引

`AWAKE/GUI/AWAKESpriteData.xml`（文件名待甲方定，参考原版 `NativeSpriteData.xml` / `SandBoxSpriteData.xml`）：

```xml
<SpriteData>
  <SpriteCategories>
    <SpriteCategory>
      <Name>ui_awake_dialogue</Name>
      <SpriteSheetCount>1</SpriteSheetCount>
      <SpriteSheetSize ID="1" Width="1024" Height="1024" />
    </SpriteCategory>
  </SpriteCategories>
  <SpriteParts>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>Awake\Dialogue\option_frame_9</Name>
      <Width>256</Width><Height>128</Height>
      <SheetX>0</SheetX><SheetY>0</SheetY>
      <CategoryName>ui_awake_dialogue</CategoryName>
    </SpritePart>
  </SpriteParts>
  <Sprites>
    <GenericSprite>
      <Name>Awake\Dialogue\option_frame_9</Name>
      <SpritePartName>Awake\Dialogue\option_frame_9</SpritePartName>
    </GenericSprite>
  </Sprites>
</SpriteData>
```

- 命名对齐原版惯例：`<组>\<名>`（原版例 `BannerBuilder\align_center`）。
- 提议 category 前缀 `ui_awake_*`；**待甲方确认**（甲方掌握运行时加载口径）。

### 3.3 Brush 与九宫格

`AWAKE/GUI/Brushes/*.xml`（参考原版 `Native/GUI/Brushes/ConversationBrush.xml`、模组样例 `AnimusForge/GUI/Brushes/AFCourierLetterBrushes.xml`）：

```xml
<Brush Name="Awake.Dialogue.OptionItem">
  <Layers>
    <BrushLayer Name="Default" Sprite="Awake\Dialogue\option_frame_9"
                ExtendLeft="24" ExtendTop="20" ExtendRight="24" ExtendBottom="20" />
  </Layers>
  <Styles>
    <Style Name="Default"><BrushLayer Name="Default" AlphaFactor="0.9" /></Style>
    <Style Name="Hovered"><BrushLayer Name="Default" AlphaFactor="1.0" HueFactor="8" /></Style>
  </Styles>
</Brush>
```

- **九宫格边距＝`BrushLayer` 的 `ExtendLeft/Top/Right/Bottom`（单位＝sprite 像素）**；四个全写才是九宫格，缺一个退回整体拉伸。
- 渲染语义：四角原样、四边单向拉伸、中心双向拉伸 ⇒ **四角像素不要参与拉伸**，出图时把装饰放角里。

### 3.4 现状（乙方看到的起点）

- `AWAKE/GUI/` 目前**只有 `Prefabs/`**：没有任何自有贴图、没有 `Brushes/`、没有 `SpriteSheets/`。
- 现有 6 个面板全部引用**原版 brush**（`ConversationItemBrush`、`Popup.CloseButton`、`Conversation.HeaderText` 等）⇒ **AWAKE 目前零自有美术**。
- ⇒ 第一批自有资产落地时，`GUI/SpriteParts/`（源贴图）、`GUI/Brushes/`、`*SpriteData.xml`（生成物）、`Config.xml` 四个入口要**一起建**，否则贴图放进去也不会被引用。

---

## 八、追加（09-13 19:5x）：画风基准已出 + 对甲方规格书的三处更正

乙方已完成骑砍原版 UI 画风的实测拆解，成果：

- **画风基准书**：`AWAKE/docs/UI-ART-STYLE-BASELINE-20260913.md`（形状语汇 / 实测配色 / 图标语法 / 九宫格 / 字体塌陷 / AWAKE 视觉母题 / 色板 token / 交付清单）
- **实测素材**：`AWAKE/tools/awake-ui-lab/out/style-study/`（原版组件拼版 3 张 + 对话系 28 图逐张），生成脚本在同目录上一级的 `_style_study_20260913.py`

对 `UI-ART-SPEC-20260913.md` 的三处技术更正（详见基准书 §4，均带证据）：

1. 贴图源文件位置：应为 `GUI/SpriteParts/ui_<分类>/<文件名>.png`（一图一 sprite），**不是** `GUI/Textures/ui_awake_main.png` 大图集；图集是生成产物。
2. `SpriteData.xml` 由生成器产出（文件名＝`<模块名>SpriteData.xml`，放 `GUI/` 根下），**不建议手写**。
3. 九宫格边距**不写在 `SpritePart`**：全库 10 个 `*SpriteData.xml` 0 处 Extend；边距只写在 `BrushLayer`。
4. 另需补一个硬需求：`GUI/SpriteParts/Config.xml` 里声明 `<SpriteCategory Name="ui_awake_*"><AlwaysLoad/></SpriteCategory>`，否则分类不会加载（不碰 src 的前提下这是必需项）。

**另有一个阻塞，以及一个已否决的实验**：本机未装 Modding Kit（`bin/` 下无 `Win64_Shipping_wEditor`），生成器与资源导入都做不了。原打算借 `AnimusForge` 当"松散 PNG 能否免打包加载"的样本，**19:5x 复核后否决**：AF 的索引 `SplitShadowsOnlyAISpriteData.xml` 只声明 `ui_account`/`ui_achievement`/`ui_subscribe` 三个分类（**这三个没有图集**），唯一有图集的 `af_vassalage_notifications` **不在索引里**；索引名也不合 `<模块名>SpriteData.xml` 惯例 ⇒ 疑似别处拷来的残留，**它自己的 sprite 加载本就不可控，观察它得不出引擎行为**。⇒ **该实验作废，不必去跑。** 全库盘点：同时具备 `SpriteParts`+`SpriteSheets`+`SpriteData` 的模组只有 AF 一个 ⇒ **本机无干净样本**；要定"松散 PNG vs 打包"须直接读引擎 sprite 加载代码。这个判断仍卡着 M0 的真机验收。

---

## 九、追加（09-13 20:0x）：乙方已出资产分类表（范围比需求书大）

**`AWAKE/docs/UI-ART-ASSET-TAXONOMY-20260913.md`** —— 按功能角色分 6 类、约 39 个 sprite 的资产地图。

要点四条：

1. **需求书只覆盖 2 个面板，实测有 6 个面板在用原版素材**：信使（1280×760）、对话（960×720）、世界事件收件箱（1100×680）、周报浏览（1100×680）、场景状态条（720×55）、开发者检查（1100×680）。只做前两个，其余四个仍是原版灰底，观感会割裂。
2. **6 个落盘分类**：`ui_awake_frame`（面板底/框）｜`ui_awake_ornament`（栏头/分隔/角饰）｜`ui_awake_button`（按钮）｜`ui_awake_item`（条目/气泡/槽）｜`ui_awake_icon`（白剪影图标）｜`ui_awake_art`（插画，可选）。
3. **M0＝10 个 sprite**，性价比最高的两项是：`panel_dialog_1100`（**一处顶三个面板**）与 `btn_close_40`（**五个面板共用关闭键**）。其余为两块主面板底 + 输入区 + 三个按钮 + 顶栏带 + 分隔线。
4. **七个待确认项**（分类表 §7）。卡住进度的两条：
   - **C5 图标要不要引入**——实测 6 个面板的按钮**全是纯文字**，没有任何 `Sprite=` 图标引用，画了没处放；
   - **管线阻塞**（本机无 Modding Kit + 松散 PNG 能否加载未定论）——决定 M0 能否真机验收。

> 甲方若要动的，只需在 §4 需求登记表里补"图标是否有落点"和"按钮状态用出图还是 Brush 因子"这两条，其余按分类表推进即可。

---

## 四、需求登记表（甲方填；乙方不动）

> 每条最少填 5 项。缺尺寸或缺「放哪」的需求，乙方只能猜，会返工。
>
> **填表日期：2026-09-13（甲方，UI 编辑层）**。本批＝分类表 §5 的 **M1**，另加 M0 未覆盖但已在 Prefab 里实测到的两处落点。
> 尺寸一律沿用分类表原值（不再重复论证）；本节只补 **落点 / 参考质感 / 状态 / 优先级**这三样分类表没定的。
> 参考质感的共同底座：**与 `panel_main_1280` / `btn_tab_105` 同族** —— 暗石面 + 1px 细金描边 + 四角 2px 切角，金只做线不做面。

| # | 用途（放哪个面板/控件） | 目标像素尺寸 | 是否九宫格（边距） | 参考质感 / 风格 | 形态 A/B/C | 优先级 | 状态 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | **联系人条目**（信使左栏，固定高 85，宽撑满 300 栏） | 280×85 | 是 `14/14/14/14` | 与 `btn_tab_105` 同族：暗石面 + 细金描边 + 四角切角。**当前是 `ButtonBrush1` 橄榄绿实心条，是六个面板里最大的一处观感违和**。三行文字（名 17 / 身份 13 / 状态 13）由控件渲染，**图内不烤文字**；文字区靠左 10，右侧留空 | A | **P0** | 待排期 |
| 2 | **NPC 气泡**（两个对话面板中栏，左对齐，`CoverChildren` 高自适应） | 600×72 | 是 `20/20/14/14` | 比面板底亮一档的暗石面，**左侧 2px 纵向金线起手**。纵向必须能无限延伸 ⇒ **装饰只放左右边与四角**，上下沿不许有横向元素 | A | **P0** | 待排期 |
| 3 | **玩家气泡**（右对齐，高自适应） | 600×72 | 是 `20/20/14/14` | 同 2，但**金线在右侧**起手；面比 NPC 侧暖半档（现着色 `#FFC8A468` α0.16）以区分 | A | **P0** | 待排期 |
| 4 | **信使左栏条带**（宽 300，上下自撑） | 300×700 | 是 `24/24/24/24` | 比主面板底再暗一档（`bg.panel.deep`），**不描边**，只与主面板之间留 1px 金缝。现状态＝`BlankWhite` α0.08 白洗色 ⇒ 改真底图 | A | **P0** | 待排期 |
| 5 | **信使右栏卡条带**（宽 260，上下自撑，承载头像框） | 260×700 | 是 `24/24/24/24` | 同 4（同一张语汇，仅宽度不同） | A | **P0** | 待排期 |
| 6 | **列表行底**（世界事件收件箱 / 周报浏览 / 开发工具 三面板） | 1000×60 | 是 `12/12/12/12` | 暗石面，比容器背景亮半档；**底边一条 1px 暗线**（行与行靠 `hairline` 或自身底线分）。现状态＝`BlankWhite` α0.08 | A | P1 | 待排期 |
| 7 | **内凹槽**（三弹窗的列表容器底，列表坐进凹槽） | 400×200 | 是 `16/16/16/16` | **内凹读法**：上沿 1px 金线 + 顶部内阴影 1px、通体比背景暗一档（`bg.inset`）。与 6 是**「凹槽里放行」**的关系 | A | P1 | 待排期 |
| 8 | **细金线**（条目之间 / 列表行之间的分隔） | 800×3 | 否（整条拉伸） | 居中 1px 金线 + 上下各 1px 羽化透明；与 `divider_gold` 同色系但更细。与 O-02 分工：O-02 是**块分隔**，本条是**行分隔** | A | P1 | 待排期 |
| 9 | **小标记键底板**（信使「置顶」键，65×25） | 65×25 | 是 `12/12/8/8` | 缩小的 Tab 同族；**只做底板，图内不烤文字、不烤图形**（图标由 UI 侧叠）。三态：`_hover` / `_pressed` | A | P1 | 见 §4.2 |
| 10 | **头像框**（信使右卡，显示 95×90） | 200×200 | 是 `24/24/24/24` | 原版 `frame_9` 是金色**实心**框（`#FFD700FF` α0.55）；自绘要**细金线框 + 内空**（金只做线），四角可带小刻饰 | A | P2 | 待排期 |
| 11 | **功能图标**（纯白剪影，引擎染色；**只做 §4.2 里「有落点」的 7 个**） | 交互档 48×48（另交 72×72 档） | 否 | 24 网格 / 笔画 2–3px / 方端点 / 纯白 `#F8F8F8` / 无渐变、无内部细节线；**不烤文字** | A | P1 | 见 §4.2 |

> 与分类表的差异说明：**+ 第 10 项（头像框 F-08）从 P2 提到本批** —— 它在信使右栏卡的正中，是可见面板的一部分，不是收尾装饰。

### 4.1 有意保留原版（**不请求**，乙方别做）

| 项 | 原因 |
| --- | --- |
| `BlankWhiteSquare_9`（全屏遮罩，`#000000FF` α0.72–0.82，6 个面板都有一层） | ⏸ **不急着换，但不封死**（本条 09-13 夜改了三次，最终口径见下）。它本身是纯黑洗色、不带画风信息 —— 按 `UI-LAYOUT-RENDER-LESSONS` §2.0 判据①（遮掉文字后只剩"一片深色"）**属于"空洞"那一侧** ⇒ **它确实是"该被质感替换的那一块"**。但**替换前提＝有一张"安静的大图"**（连续质感，非场景画）且**真机验过正文可读性**。**现在乙方不用做。** |
| 文字 brush（`Popup.Description.Text` ×37 / `Popup.Title.Text` ×11 / `Popup.Button.Text` ×8 / `Clan.TabControl.Text` ×5 / `Review.NameInput.Text` ×2） | 文字样式（字号 / 颜色 / 字距）属 **UI 编辑层**，不需要出图；本层下一步做**字号 6 档收敛**时一并处理 |
| `ButtonBrush1`（联系人条目 + 置顶两处） | 就是上表第 1、9 项要**替换**的对象，非保留项，此处仅登记当前占用，避免乙方误以为要新出 |

### 4.2 回乙方 §7 的两问（**甲方口径，已定**）

#### Q1 · C5 图标要不要引入 → **引入，但落点只认下面 4 条纪律**

1. **关闭键不出独立图标（N-01 取消）。**
   实测 `btn_close_40.png` 三态图里**已经把鎏金 × 烤进图**（已核图，非推测）⇒ 比在按钮上叠一个 `ImageWidget` 更省一层，且 × 的明暗能随状态自然变。**故不需要 `icon_close`。**
2. **图标 + 文字并列**（`ImageWidget` 与既有 `TextWidget` 同容器；图标显示宽 24 + 间距 5）：
   `icon_send`（发送键 110×35）、`icon_write`（写信键 100×35）、`icon_refresh` / `icon_settings` / `icon_diagnostics` / `icon_logs`（开发工具四键 110×35）。
   实测：110×35 放「24 图标 + 2 字 17px」放得下；100×35 同理。
3. **仅图标（无文字）**：`icon_pin` —— 置顶键 65×25 太窄，只叠图标，落点＝上表第 9 项底板正中。
4. **暂缓交付（当前无落点，别先画）**：`icon_back`（返回）、`icon_hint`（提示）、`icon_arrow_l` / `icon_arrow_r`（翻页箭头）、`icon_chronicle`（编年史）。
   **实测依据**：6 个面板**没有返回键、没有翻页箭头、没有独立提示图标位**，世界书界面尚未建。等 UI 侧先建控件位再要，现在画了只能压箱底。

⇒ **本批 C5 只要 7 个**：`icon_send` `icon_write` `icon_pin` `icon_refresh` `icon_settings` `icon_diagnostics` `icon_logs`。

#### Q2 · 按钮状态：出图 ×3 还是 Brush 因子派生 → **逐状态出图，无例外；并补一条硬约束**

- 维持**逐状态出图**（用户已拍板），**不用 Brush 因子提亮**。理由：因子提亮会把材质颗粒一起提亮，读起来像蒙了层雾。
- **补一条硬约束（重要，会直接影响观感）**：同一按钮的各状态，**九宫格边距必须逐字节一致**。
  实测口径：`<Style>` 是**部分覆盖**（`BrushFactory.LoadStyleInto` → 增量赋值），所以 UI 侧只在 `<Layers>` 写一次 Extend、状态里只覆盖 Sprite。
  ⇒ **请乙方出图时，四态共用同一画布、同一内容框（同一透明边界）**，不要各自紧裁 —— 否则按下时内衬会跳。
- 每个状态一条 `<BrushLayer>`，由 UI 侧在 `AwakeBrushes.xml` 里接，**不需要乙方管 Brush**。

### 4.3 新增方向（09-13 夜 · 经三次修正后）：**用图** —— 目前只登记方向，**不填需求行**

**这份方向怎么来的（留痕，免得后人重走）**：用户递来两份外部参考让本层"学习"。本层**连错两次**：
① 先照搬其中**场景模拟**类模组的主张（整屏场景画撑沉浸）—— 但 AWAKE 做的是**对话 / 写信**，是**文字界面**；
② 为守基线又**矫枉过正**，把图压成"只做小锚点、大图不做"—— 被用户当场纠正：
**「安静、不喧哗」不是「无趣、空洞」。**

⇒ 最终口径：**安静 ＝ 低对比、不抢戏、有秩序 ≠ 无内容。**
判据两条（`UI-LAYOUT-RENDER-LESSONS-20260913.md` §2.0）：

| 判据 | 怎么问 | 通过 |
| --- | --- | --- |
| **① 有物** | **遮掉文字**还看得出这是什么？ | 看得出"一封信 / 一块碑 / 一个人" |
| **② 不抢戏** | 正文读着费不费劲？ | 字始终最亮 |

**据此的用图方向**（形态 C，生成图；**注意不是"场景画"**）：

| 想做的 | 接在哪 | 调门 | 为什么现在不填需求行 |
| --- | --- | --- | --- |
| **信笺质感**（纸面纤维 / 折痕 / 封蜡 / 印鉴） | 信使面板 —— 可用大面积 | **安静的大图**（判据①的正面样本） | ⛔ 尺寸未定（§3.1：渲染的输入必须是已定死的格子尺寸） |
| **说话人头像 / 半身立绘** | 对话面板（回答"谁在说"） | 小图、低对比 | 同上；且要先定"随人变还是随场景变" |
| **整屏背景质感**（石面 / 墙面 / 极弱氛围，**不是场景画**） | 现全屏遮罩那层（`BlankWhiteSquare_9`） | 安静的大图 | 同上 **＋ 必须真机验过正文可读性** |

**已定**：调性＝**暖调烛光**（09-13 用户拍板）。
**未定**：尺寸、每地点几张、是否带人物、立绘随不随人。
⇒ **乙方先不必动** —— 等 UI 侧把格子尺寸定死，再按 §4 的六列格式补成正式需求行。

---

### 4.4 新增需求行（09-14 补）：**画位控件** —— §4.3 里「说话人立绘」那一条现在能填了

**为什么现在能填**：§4.3 当时卡在"尺寸未定"。09-14 画位尺寸**定死为 212×360**（`docs/ui-design/npc-dialogue-layout-06/07.html`），控件本体已落到 `GUI/Prefabs/AwakePortraitSlot.xml`，数据源契约在 `docs/UI-PORTRAIT-SLOT-CONTRACT-20260914.md`。
下面 4 条是**这套控件跑起来缺的图** —— 已核过现有资产，`ui_awake_button` 最大只到 110 宽、`ui_awake_frame` 没有画位框、`ui_awake_ornament` 没有人形。

| # | 用途（放哪个面板/控件） | 目标像素尺寸 | 是否九宫格（边距） | 参考质感 / 风格 | 形态 A/B/C | 优先级 | 状态 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 12 | **空位人形轮廓**（画位未生成时的那道淡人形，`AwakePortraitSlot` ①态） | 212×360 | 否（整图，不拉伸） | **极淡线稿人形**：单色描边、无面部细节、无衣物纹样、无阴影。像"还没上色的作画底稿"，**不是速写、不是实心剪影**。透明度由 UI 侧控（现用描边 α0.20 / 填充 α0.06）⇒ **请交不带 alpha 的实心稿，压淡交给 UI**。人物占画幅高约 70%，脚下留空 | A | **P0** | 待排期 |
| 13 | **画位细金线框**（212×360 空位的外框） | 212×360 | 是 `8/8/8/8` | 1px 金线 + 四角 2px 切角，与 `btn_tab_105` 同族；**线内全透明**（不要底）。取代现在用 4 条 `BlankWhite` 细线拼的临时方案 | A | P1 | 待排期 |
| 14 | **生成按钮**（画位中央／底部那颗 140×34） | 140×34 | 是 `16/16/16/16` | 与 `btn_secondary_100` 同族（描边款，非实心）。**三态逐状态出图**、共用同一透明边界（§4.2 Q2 硬约束）。现有最大 110 宽，九宫格拉到 140 会把中段纹理拉长 | A | P1 | 待排期 |
| 15 | **生成中指示**（画位 ② 态中央） | 32×32（另交 48×48 档） | 否 | **静态即可** —— Gauntlet 没有逐帧动画，转圈做不了，先用一张静态图示（细金线稿的圆环／沙漏皆可），引擎染色 | A | P2 | 待排期 |

**暂时不请求（写清理由，免得乙方误画）**：

| 项 | 为什么不请求 |
| --- | --- |
| **画位里的 AI 全身像本身** | 那是**玩家侧运行时生成**的（本地生图端点），不是美术资产。美术不画 |
| **生成中遮罩 / 左上角标底** | 现用 `BlankWhite` + 黑色 alpha 叠色已够。按 `UI-LAYOUT-RENDER-LESSONS` §2.0 判据②，这类"压住底图让字读得清"的层**越素越好**，出图反而加噪 |
| **画位上的刷新小圆键** | 07 稿已取消（按钮下移后本身就是刷新入口），连带 `icon_refresh` 也暂时没有落点 |
| **左列在场人物头像** | 用**游戏原生肖像**（`ImageIdentifierWidget`），不出图。同类逻辑见 §4.1 |

**下一批（现在别填）**：三列面板的另两件 —— 在场人物条目底（164×70）与对话面（488×523）—— 等 `NpcDialogue.xml` 从单栏改成三列之后再说。按 §3.1，**渲染的输入必须是已定死的格子尺寸**；面板还没落地就出图会返工。

> 与 §4.3 的关系：本条只兑现了 §4.3 方向表里的**第二行（说话人头像／半身立绘）**。第一行（信笺质感）与第三行（整屏背景质感）**尺寸仍未定**，继续留在 §4.3，不填需求行。

---

## 五、乙方已观察到的两条（供甲方确认或否决）

1. **本地预览画不出对话系皮肤 ≠ 缺图。** 甲方日志里的现象（换装后预览无变化）根因是 Lab 的图集只抽到 33 个 sprite，对话系 sprite 不在其中；**自绘资产可以绕开这个限制**，但需要先补 Lab 的取图路径（见第 2 条）。
2. **⚠️ Lab 取不到模组自绘贴图（建议甲方先补这一处）。** `tools/awake-ui-lab/preview/ui_sprite_atlas.py`：
   - 索引只扫**硬编码的 5 个原版模块**（`Native/SandBox/SandBoxCore/StoryMode/Multiplayer`），不含 `AWAKE`；
   - 图集只走 `AssetPackages/*.tpac` 解码路径，**不认模组惯例的 `GUI/SpriteSheets/<Category>/<Category>_<ID>.png`**。
   - ⇒ 即使 AWAKE 按 §3.1 出了自绘 PNG，**本地预览照样画不出来**。要本地可见，需要给该模块加一条：sprite 来源模块是 `AWAKE` 时，直接读 `Modules/AWAKE/GUI/SpriteSheets/<Category>/<Category>_<SheetID>.png` 并按 `SheetX/SheetY/Width/Height` 裁剪。
   - 这是**取图路径**的事，不是美术的事；乙方可以出图，但图能不能被预览到取决于这一处。

---

## 六、交付记录（乙方填）

| # | 对应需求 | 产物路径 | 形态 | 规格摘要 | 日期 |
| --- | --- | --- | --- | --- | --- |
| 1 | 预览能力交接（工具，非资产本体） | `AWAKE/tools/awake-ui-lab/out/handover/awake-ui-preview-handover-20260913.zip` | 独立可跑包（解压即用） | 31.3 MB / 109 项 | 2026-09-13 |
| 2 | §4.4 第 12 条 空位人形轮廓 | `AWAKE/tools/awake-art-lab/out/ui-controls-20260915/delivered/empty_slot_figure_212x360.png` | 单色线稿 PNG（RGBA，线像素 α=255，整图不拉伸） | 212×360 · 纯白 `#FFFFFF` · 线像素 3509 · 人高 236／中轴 x=106／脚底 y=284。⚠️ 与本行原需求的一处偏离：§4.4 写"人物占画幅高约 70%"（＝252px），本件取 236px（65.6%）——按 07 稿画位里那道空位人形轮廓的实际高度对齐。<br>**另有一条过程更正（重要）**：一度按 §4.4「无衣物纹样」把袍上竖褶清掉（丢 <40px 连通体，3509→2962），**甲方复核指出那样线条会断、人形变空**；确认「纹样」指衣服上的花纹，褶子是体积、不是纹样 ⇒ **已退回带竖褶的原稿**，去碎屑版作废（留档在乙方侧 `LocalAIPictureGeneration/experiments/ui17_final_white_out/_rejected_despeckled.png`，同目录 `options/pick_figure.png` 是四候选对照板）。 | 2026-09-15 |
| 3 | §4.4 第 13 条 画位细金线框 | `AWAKE/tools/awake-art-lab/out/ui-controls-20260915/delivered/empty_slot_frame_212x360.png` | 空心线框 PNG（RGBA，线内全透明）· 九宫格 `8/8/8/8` | 212×360 · 1px 金线 `#D8A850` · **线贴画布边** · **四角切角 8px**。⚠️ 与本行原需求的三处偏离：① §4.4 写"四角 2px 切角"，实测 2px 在 212 宽的框上看不出是切角，逐档比过后取 **8**；② §4.4 写"与 `btn_tab_105` 同族"——实测现有按钮族端头是 **13px 连续曲线**（`artkit.chamfer_pts`→`tip_curve_pts`），薄线框跟不了那个量级，且 **8 已是 `8/8/8/8` 能容纳的最大切角**（要跟族齐得把边距改成 16）；③ 07 稿里画位框是 `inset:7px`，本件贴边交付，要那个内缩由布局侧留 7px 边距。九宫格拉伸已逐像素验过：四角 8×8 原样、切角保持 1px、四角全透明。 | 2026-09-15 |
| 4 | §4.4 第 14 条 生成按钮三态（140×34） | `AWAKE/tools/awake-art-lab/out/ui-controls-20260915/delivered/btn_generate_140.png`（`_hover` / `_pressed` 同目录） | 逐状态单张 PNG（RGBA）· 九宫格 `16/16/16/16` | 140×34 · 三态各一张 · 调 `parts.btn_plate(…, cut=13.0, frame_c=(48,42,35), frame_lit=(82,73,61))`，只换 `field` 一个色。详见 §6.4。 | 2026-09-15 |
| 5 | §4.4 第 15 条 生成中指示（画位 ② 态中央） | `…/delivered/icon_busy_32.png`（另交 `icon_busy_48.png`） | 单张静态 PNG（RGBA）· 不拉伸 | 32×32（另 48×48）· 缺口环 · 金 `#D8A850` 已烤进图 · 缺口中心角 315°／张角 70° · 四角全透明。浓淡与色相仍可由引擎 `AlphaFactor` / `ColorFactor` 覆盖。 | 2026-09-15 |

> 本批产物在 `AWAKE/tools/awake-art-lab/out/ui-controls-20260915/delivered/`（**4 件资产 / 7 张 PNG ＋ 6 张证据图**）：
> `empty_slot_figure_212x360.png` · `empty_slot_frame_212x360.png` ·
> `btn_generate_140{,_hover,_pressed}.png` · `icon_busy_32.png` · `icon_busy_48.png`（资产）；
> `evidence_slot_state1.png`（① 态落地）· `evidence_slot_state1_with_button.png`（① 态＋按钮）·
> `evidence_state2_busy.png`（② 态＋环）· `evidence_button_states.png`（按钮三态 4x）·
> `evidence_frame_corners.png`（框四角）· `evidence_figure_options.png`（人形原稿／去碎屑版对照）（证据）。
> 本批四件资产**共用一个色（金 `#D8A850`）与一套端头曲线**，同页叠加不打架。
> **落盘单在 `delivered/MANIFEST.md`**（现文件名 → 建议 sprite 名 / Extend / Brush 片段 / 自检结果）。
> 自检脚本在乙方侧 `LocalAIPictureGeneration/tools/verify_delivered.py`，可复跑；过程稿（含作废版与备选方案）也在乙方侧 `experiments/`，未混进本仓库。
> 最终并入 `GUI/SpriteParts/` 并重跑 sprite 生成器属落地动作，等甲方点头后再做 —— 但**先看 §6.6 那两处**。

### 6.1 ⚠️ 更正 §5.2：预览自绘贴图的堵点已解

§5.2 说「Lab 不认模组自绘贴图 ⇒ 出了图也预览不到」。**这条现在不成立了。**

Lab 已加 **`custom/` 覆盖机制**：把 PNG 按 Prefab 里写的 sprite 名丢进
`tools/awake-ui-lab/out/atlas/custom/<sprite 名>.png`，预览时**优先于游戏原版**使用。
不需要改 Python、不需要重编索引、**也不需要**给 `ui_sprite_atlas.py` 加 `AWAKE` 模块扫描。

（`AWAKE` 模块扫描那条仍然值得补，但它是**真机集成**时的事；做资产预览不受它阻塞。）

### 6.2 交接包怎么用

```
1) 解压  awake-ui-preview-handover-20260913.zip  到任意目录
2) 先读  HANDOVER-UI-PREVIEW.md
3) 跑    python awake_ui.py check --prefab AwakeMessenger.xml --prefab NpcDialogue.xml
4) 出图  out/shot/<Prefab 名>.png
5) 预览自己的图： 把 PNG 丢进 out/atlas/custom/<sprite 名>.png，重跑第 3 步
```

包内**已带**游戏图集原图（6 张）与已裁切 sprite（59 件），所以能离线渲染原版贴图。

**两点必须知道：**

1. **包大在哪**：31.3 MB 里 **29.3 MB 是游戏图集原图**。嫌大可删 `out/atlas/*.png`，
   体积降到约 2 MB，代价是原版贴图退化成色块（自绘的 `custom/` 贴图**不受影响**）。
2. **自定义 `_9` sprite 拿不到九宫格参数**，预览会整体拉伸 ⇒ 交付时请**一并附上 `Extend*` 数值**。
   （原版 `_9` 的九宫格参数是能读到的，实测 `npc_dialogue_panel_9` → 左 97 / 右 25 / 上 93 / 下 92。）

> 注：该 zip 位于 `out/`（lab 的 `.gitignore` 排除），**不在 git 里**，请直接从磁盘取。

### 6.3 乙方对交接包做过的验收（不是"应该能用"）

解压到独立临时目录后实测：

| 项 | 结果 |
| --- | --- |
| 主线 `check` | 2 个 Prefab · 控件 147 · **问题 0** |
| Brush 命中 | 11/11 与 7/7，未命中 0 |
| 原版库取证 | 自动找到 430 Prefab · 2134 Brush（不需要手填游戏路径） |
| sprite 缓存解析（**完全离线**：无游戏目录、无索引器） | **59/59** |
| `custom/` 覆盖优先级 | 放入即顶掉原版，**生效** |
| 出图 | 1040×848 · 唯一色 3347 ⇒ 真实贴图层次（非色块降级） |

**已知边界**（判图前必读 `HANDOVER-UI-PREVIEW.md` §5）：不执行数据绑定、字体是近似、
`Color` 需 `#RRGGBBAA` 8 位、中文排版细节不可依赖预览。

### 6.4 §4.4 第 14 条：生成按钮三态（140×34）—— **已交付**（2026-09-15）

产物在 `out/ui-controls-20260915/delivered/`：`btn_generate_140.png` / `_hover` / `_pressed`
（证据图 `evidence_button_states.png` ＝三态 4x 并排；落位见 `evidence_slot_state1_with_button.png`）。

- **做法**：直接调美术线自己的 `parts.btn_plate(140, 34, cut=13.0, field=…, frame_c=(48,42,35), frame_lit=(82,73,61))`
  —— 与 `build_m0.secondary()` **同一函数、同一套常量**，只把尺寸从 100×35 换成 140×34；
  三态只换 `field` 一个色（`BTN_SEC_FIELD / HOVER / PRESS`）。**不是**把 110 宽的图拉长。
- **三态同形（§4.2 Q2 硬约束）已有数值判据**：hover / pressed 的 alpha 通道与 default **逐像素相同**
  ⇒ "共用同一透明边界"成立。
- **同族已有数值判据**：六张（现有三态 × 新三态）在 α>200 区域的平均色 ——
  常态 42.0 / 42.0、悬停 49.6 / 49.8、按下 34.8 / 35.0（L 值），差 ≤0.2。
- **九宫格 `16/16/16/16` 拉伸预演**：拉到 280×68 后，四角 16×16 与原图**逐像素相同**
  （端头的连续曲线、框带、内衬上沿高光／下沿压深都落在角区内）⇒ 不会因拉长走样。
- **一处留给甲方**：本条按 §4.4 走**次按钮族**（内衬 `#3C3126` 锈褐、框更暗），
  但 07 稿里那颗是**金框亮底**、并且是 ① 态唯一的行动点。要不要改用主按钮族内衬
  （`BTN_FIELD` 橄榄铜、亮一档），或加一道金 underline（`btn_plate` 的 `accent` 参数）——一句话即可，现有件已是可直接用的终稿。
- **没动的**：`artkit.py` / `parts.py` / `build_m0.py` 一律未改；产物未写进 `GUI/SpriteParts/`。

### 6.5 §4.4 第 15 条：生成中指示（32×32，另交 48×48）—— **已交付**（2026-09-15）

产物在 `out/ui-controls-20260915/delivered/`：`icon_busy_32.png`（主推）/ `icon_busy_48.png`。

- **形态**：金 `#D8A850` **缺口环**，缺口中心角 315°、张角 70°、圆头端帽 —— 缺口的位置就是"在转"的那一点，
  即便 Gauntlet 放不了逐帧动画，静态看也读得出"正在动作"。
- **颜色已烤进图**（与 #13 框同色 `#D8A850`），浓淡／色相仍可交给引擎 `AlphaFactor` / `ColorFactor` 覆盖。
- **做法**：`ring(size, r, lw)` 用 **8x 超采样 + LANCZOS 降采样**，线缘无锯齿、无灰边；四角全透明。
  32×32 档 `r=11.5, lw=2`；48×48 档 `r=18.5, lw=3`（**重画、不是放大**）。
- **落在哪**：画位 ② 态中央，合成预览 `evidence_state2_busy.png`（人形压到 α20%×0.30 ＋ 盖 `rgba(8,6,5,.8)` 遮罩 ＋ 环 ＋ 文案）。
- **没动的**：同 §6.4，产物未写进 `GUI/SpriteParts/`。

### 6.6 ⚠️ 落盘前必须先解决的两处（乙方 09-15 一手核对，非推测）

本批资产本身已定稿，但**照现状直接丢进 `GUI/SpriteParts/` 会被游戏忽略**，有两处要先处理：

1. **`ui_awake_frame` 的图集已满，装不下画位框。**
   现表 `4096×1024`，最大占用 `panel_status_720` @ `SheetX=3368` ＋ 宽 720 ⇒ 到 **4088**，只剩 8px。
   212×360 的画位框放不进。
   ⇒ 建议给画位类**新开一个 category**（如 `ui_awake_slot`），画位框与人形一起放，语义也更干净。
2. **`ui_awake_icon` 目录里有 28 张 `icon_*.png`，但 `GUI/AWAKESpriteData.xml` 里没有这个 category。**
   现有表只有 `ui_awake_button` / `ui_awake_frame` / `ui_awake_ornament` 三个 category，
   28 张图标**没有任何 `SpritePart` / `GenericSprite` 登记**，也没有 Brush 引用它们。
   ⇒ 本批的 `icon_busy_32` / `icon_busy_48` 若直接丢进该目录，**游戏读不到**；
   要么补登记，要么跟图标那条线一起解决（**图标资产的接线状态建议甲方复核**）。

> 好消息：按钮表 `ui_awake_button 2048×64` 已用至 `SheetX=1348` ＋ 40 ⇒ 到 1388；
> 新增 140 宽的按钮到 1536，**塞得下，不必扩表**。

---

## 七、证据与复现命令

```bash
GAME="D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord"

# 模组惯例：自绘贴图集＝PNG（1024×1024 RGBA）
ls -la "$GAME/Modules/AnimusForge/GUI/SpriteSheets/af_vassalage_notifications/"
file  "$GAME/Modules/AnimusForge/GUI/SpriteSheets/af_vassalage_notifications/af_vassalage_notifications_1.png"

# sprite 索引 schema（SpriteCategories / SpriteParts / Sprites）
head -40 "$GAME/Modules/Native/GUI/NativeSpriteData.xml"
grep -n "SpriteParts" -A 12 "$GAME/Modules/Native/GUI/NativeSpriteData.xml"

# brush 与九宫格
head -40 "$GAME/Modules/Native/GUI/Brushes/ConversationBrush.xml"
ls "$GAME/Modules/AnimusForge/GUI/Brushes/"

# AWAKE 现状：GUI 下只有 Prefabs
ls -la AWAKE/GUI/ AWAKE/GUI/Prefabs/

# Lab 取图路径限制
grep -n "SpriteData.xml" AWAKE/tools/awake-ui-lab/preview/ui_sprite_atlas.py
grep -n "modules=(" AWAKE/tools/awake-ui-lab/preview/ui_sprite_atlas.py
```

---

## 十、追加（09-13 20:3x）：Modding Kit 阻塞已定性 —— 需不需要、怎么装

**结论：不是「可选优化」，是「最后一公里」的硬需求；但只卡打包，不卡出图、不卡预览。**

### 10.1 游戏认的最终产物是什么（实测拆解 `SimpleBank` ＝本机唯一完整成功样本）

```
Modules/SimpleBank/
├── GUI/SpriteParts/Bank/SPGeneral/SPTraits/bank_0.png   ← 源图（可嵌套子目录）
├── GUI/SimpleBankSpriteData.xml                          ← 索引（名＝<模块名>SpriteData.xml ✓）
├── GUI/Brushes/BankBrushes.xml
├── AssetSources/GauntletUI/Bank_1.png      49 KB         ← 装箱后的图集（真像素在这）
└── Assets/GauntletUI/Bank_1_tex.tpac      479 B          ← 元数据壳
```

那个 **479 字节**的 tpac，`xxd` 里可直接读出一行：

```
$BASE/Modules/SimpleBank/AssetSources/GauntletUI/Bank_1.png
```

⇒ **tpac 是薄元数据层**（格式头 `TPAC`＋版本＋GUID＋资源名＋**源路径**＋纹理元数据 `DXT5`/`albedo`/`B8G8R8A8_UNORM`＋尺寸 `0x800×0x200`＝2048×512）。
⇒ **真像素在图集 PNG 里**，引擎按 tpac 里那行路径去 `AssetSources/` 取。
⇒ 对比：Native 的 `AssetPackages/gauntlet_ui.tpac` 是 **323 MB**（基础游戏像素真烤进包里）；模组侧走的是「壳＋松散 PNG」这条路。

### 10.2 绕不开（三条路都堵，各有出处）

| 路 | 结果 |
| --- | --- |
| `TpacTool`（szszss / lewisajax，开源） | **只能读/导出，不能写**。作者 FAQ 原文："打包？Jeez… could be later." |
| `BannerEdge`（hunharibo） | **是壳**。它的 "Pack Atlas" 按钮＝去调 Kit 里的 `SpriteSheetGenerator.exe`；README 把 "modding kit on Steam" 列为前置条件 |
| 手写那 479 字节 | **理论可行**（结构已读懂），但含版本/哈希字段，**拿项目赌不值得** |

⇒ **`_tex.tpac` 只能由 Kit 的资源浏览器生成。**

### 10.3 怎么装（精确到点击）

1. Steam → **库** → 顶部筛选下拉勾 **「工具 / Tools」**
2. 搜 **`Modding`** 或 **`Bannerlord`**（**app id 1393600**）——**别按图找，按名字找**（原因见下）
3. 同意 EULA → 安装

**⚠️ 为什么「找不到」——09-13 实测四块证据**

| 事实 | 证据 |
| --- | --- |
| **它不在 DLC 列表** ⇒「属性 → DLC」永远找不到 | `store.steampowered.com/dlc/261550` 只有 4 条：War Sails Modding Kit(4456490)、War Sails(2927200)、OST(2194520)、Digital Companion(2240110)——**没有 1393600** |
| **它没有商店页** ⇒ 商店搜索也搜不到 | 访问 `store/app/1393600` 直接跳回商店首页（Tool 类 app 不设商店页） |
| **它在库里没有封面图** ⇒ 极易漏看 | `appcache/librarycache/1393600/` 里**只有一张图标 jpg**，无 `header.jpg`／`library_600x900.jpg`／`library_hero.jpg`／`logo.png`（对照 Bannerlord 本体这五项俱全）；日志里对应 `No download URL available for app 1393600 and asset type Capsule/Hero/Logo/Header` |
| **它一直在库里** | `appinfo_log.txt` 自 2026-04 起多次记 `Apps changed: 1393600=…`；`librarycache/1393600/` 目录建于 2025-07-05 |

⇒ **唯一入口＝「库 → 工具筛选」**。进去后它是一块**没有封面的格子**，靠名字认；也可以直接试 `steam://install/1393600`。
4. **⚠️ 版本必须对齐**（不对齐会崩）：本机游戏＝**v1.3.15**（据 `Modules/Native/SubModule.xml`）；Kit 的 BETAS 里有 **`v1.3.15` 分支**（Build 22123340，2026-02-27）⇒ 右键 Kit → **属性 → BETAS → 选 `v1.3.15`**，**别用默认 public/beta**（现为 v1.5.2 系）
5. 体积：**下载 23.32 GB / 占盘 28.80 GB**（SteamDB）。本机 D 盘余 232 G，够。
6. 装完 `bin/Win64_Shipping_wEditor/` 出现，内含 `TaleWorlds.TwoDimension.SpriteSheetGenerator.exe`（**它不动 `Win64_Shipping_Client`，不影响现有构建**）

> ⚠️ 另有一个形态很像、但**不是它**的包：见 §10.6。

### 10.6 ⛔ 别装错：`War Sails Modding Kit` 不是主工具包

09-13 实测本机**已在下载** `War Sails Modding Kit`（**app 4456490**，16.7 GB staged / 14.7 GB 下载）。**那不是主工具包**，是它的附加包：

- **形态是 DLC**（挂在 Bannerlord 的「属性 → DLC」下），官方原文：*"contains the required files to access and use War Sails content **within** the Mount & Blade II: Bannerlord - Modding Kit"* ⇒ **它装在 Kit 之上，本身不带编辑器**；使用说明第一句就是「先启动 `Modding Kit`」。
- **依赖 `War Sails` 扩展**（app 2927200，$24.99）。本机 `librarycache` 里既无 2927200，`Modules/` 下也无 War Sails 模块。
- **配套 BL v1.4.8**（2026-08-10 随 WS v1.2.8 / BL v1.4.8 发布）；本机游戏锁在 **v1.3.15**，版本对不上。

⇒ **对本任务无用，建议暂停/卸载**（Steam → 下载 → 暂停；或 Bannerlord 属性 → DLC → 取消勾选）。要的是 **1393600**。

### 10.4 更正 §八 里「本机无干净样本」那条

`SimpleBank` 是**完整成功样本**（源图＋SpriteData＋AssetSources 图集＋Assets tpac 四件套齐全），推翻「本机无样本」。它同时给出两条之前不知道的口径：

- **源图可嵌套子目录**，sprite 引用名＝**带子目录的相对路径**（`SPGeneral\SPTraits\bank_0`），不是扁平名；
- `AlwaysLoad` 直接写在 SpriteData.xml 里（与 `Config.xml` 两条路等价）。

### 10.5 时机判断

- **不装也能继续**：出图（已有 21 张）、预览（Lab 已验证）全不依赖它。
- **装了就闭环**：出图 → 装箱 → 出 tpac → 真机验收，一条龙，不必再依赖 UI 会话。
- 建议时机＝**第一批美术要真进游戏时**；不必今天装。对 codex 主干线（资源浏览器 / 材质编辑器）同样有用。
