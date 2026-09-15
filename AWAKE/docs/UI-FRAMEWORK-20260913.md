# AWAKE UI 框架（2026-09-13）

- 批次：`AWAKE-UI-FRAMEWORK-20260913`
- 归属：**UI 编辑层**（甲方）维护。Preface/F 面板装配、Brush 层、绑定契约、UI Lab 都归本层。
- 三份文档的分工：
  - `UI-ART-SPEC-20260913.md`（甲方早期需求书）= 要什么 **（部分口径已被乙方更正，见 `UI-ART-ASSET-INTERFACE` §八）**
  - `UI-ART-STYLE-BASELINE-20260913.md`（乙方）= 画成什么样（**实测**）
  - `UI-ART-ASSET-TAXONOMY-20260913.md`（乙方）= 有哪些 sprite、怎么归堆、先做哪个
  - **本文 = 界面怎么搭、怎么接线**（框架层：token / 布局 / 组件 / 命名 / 绑定）
- 证据等级：**凡标「实测」的都是本机取证的**；凡未标的一律是设计决定，可改。
  ⚠️ 全篇所有视觉结论都是**静态预览**得出，**真机 E4 一律未验**（见 §8）。

---

## 1. 三层结构（谁维护什么）

```
token（色 / 字号 / 栅格）        ← 本文 §2 定义；色板值取自乙方实测，不重造
  └ 组件（面板底 / 头带 / Tab / 按钮 / 气泡 …）  ← 每个组件 = 1 条 Awake.* Brush + 若干状态图
      └ 面板装配（6 个 Prefab）                ← Prefab 只写 Awake.* 名字，不写图名
```

**关键约束：Prefab 不出现 sprite 名。** 面板只引用 `Awake.*` Brush，换图/改状态只动
`GUI/Brushes/AwakeBrushes.xml`，**不需要动 Prefab**。这是这一层的价值所在。

| 层 | 文件 | 归属 |
| --- | --- | --- |
| Prefab | `GUI/Prefabs/*.xml` | UI 编辑层 |
| Brush | `GUI/Brushes/AwakeBrushes.xml` | **UI 编辑层**（`GUI/Brushes/` 不属美术，也不属 SpriteParts） |
| 源贴图 | `GUI/SpriteParts/ui_awake_*/` | 资产制作层 |
| 索引 / 图集 | `GUI/*SpriteData.xml`、`SpriteSheets/` | 生成器产出（**待 Modding Kit**，见 §8） |
| 预览工具 | `tools/awake-ui-lab/` | UI 编辑层 |

---

## 2. Token

### 2.1 色板

**不自造**，直接用乙方实测值（`UI-ART-STYLE-BASELINE-20260913.md` §3.1）：
`bg.panel #181010`｜`bg.panel.deep #0F0B07`｜`bg.inset #0B0806`｜`line.hairline #8C6B38@60%`｜
`gold.title #E2AF54`｜`gold.hover #FBC86A`｜`gold.icon #D8A850`｜`gold.dim #9C7839`｜
`fill.brass #484030→hover #705838`｜`fill.confirm #485020`｜`fill.danger #403028 + 印 #5A1E14`｜
`text.primary #F2EDE4`｜`text.secondary #C8B088`｜`text.muted #8A7C66`｜`icon.white #F8F8F8`

**两条使用纪律**（来自实测）：
1. **金只做线与点，不做面。** 原版的「金」是细线级（描边/图标/进度条），面积很小；
   面一律是暗的。大面积上金 = 立刻跑出骑砍语感。
2. **`Color` 必须 `#RRGGBBAA` 8 位**（10 位会被引擎静默丢弃、渲染成全白/无色；本项目已犯两次）。
   本层已把它做成审计规则 `color_format`。
3. **末字节才是 alpha，所以末字节一律写 `FF`**，不透明度交给 `AlphaFactor` 管。
   写成 `#FF8C6B38`（ARGB 习惯）会被引擎读成「RGB(255,140,107) ＋ 22% 透明」——
   **首位 `FF` 不是"不透明"，它是红通道**；末字节 `00` 时整块**完全不可见**。
   审计规则 `color_channel_order`（09-15 加）；全库现存 **45 处**待改，
   判定与处置见 `UI-REF-ALICEMM-20260915.md` §3–§4。

### 2.2 字号阶梯（⚠️ 中文字体塌陷，这条比原版更要紧）

**实测前提**：`NativeLanguages.xml` 里简体中文把所有字体映射到 `simkai`（楷体）——
**AWAKE 没有「衬线 vs 无衬线」这一维可用**，一切都渲染成楷体。
⇒ 层级**只能靠**：字号 / 颜色 / 装饰线 / 留白 / 字距。**sprite 里绝不能烤文字。**

**现状问题（实测）**：6 个 Prefab 当前用了 **10 档字号**（13/14/15/16/17/18/19/20/24/28）——阶梯失控。
**框架收敛为 6 档**：

| 档 | 用途 | 替代 |
| --- | --- | --- |
| 28 | 屏标题（弹窗类面板的唯一标题） | — |
| 24 | 顶栏主标题（信使 / 对话的 HeaderText） | — |
| 20 | 顶栏副标题（当前对象）/ 分区小标题 | — |
| 17 | 正文强调（气泡正文、联系人名） | **18 → 17、19 → 17** |
| 15 | 正文（通用、按钮文字） | **14 → 15、16 → 15** |
| 13 | 次要（身份 / 状态 / 时间） | — |

> 落地：6 处替换（14×1、16×3、18×1、19×1）。**本轮未改**，列为下一步（见 §7）。
> 理由：字号改动会牵动 `SuggestedHeight`，要与布局一起过一遍，不夹在皮肤层里做。

### 2.3 栅格

- 画布基准 **1920×1080**；间距与尺寸走 **5 的倍数**（5/10/15/20/25/30/50/60…），
  **1/2/3 仅用于贴边微调**。← 审计口径（`grid_step=5`、`allow_small=[1,2,3]`）
- `MarginLeft/Top/Right/Bottom` 里 **≥100 的字面值要进 `<Constants>`**（改一处全局生效）。
  `MAGIC_LITERAL_THRESHOLD=100`，当前只剩三个小面板的 `105/320` 未常量化（info 级）。
- 面板实数（实测，不许改）：**1280×760 / 960×720 / 1100×680 ×3 / 720×55**。
- ⚠️ 旧记忆里「AWAKE 用 12/24 系间距」这条**未在 Prefab 实证到**（grep `="12"|="24"` 零命中）
  —— 5 系栅格才是实际口径。已于本轮更正 `TOPIC-UI.md`。

### 2.4 形状与状态派生

- 圆角：面板 6–10px、条目 4–6px（方石感，**不做胶囊**）；描边细金线 1–2px，只描轮廓。
- 厚度感用**明暗两切面**，不用模糊阴影/发光。
- **状态一律逐状态出图**（已定口径，非 Brush 因子提亮）：`_hover` / `_pressed` / `_selected` / `_disabled`。
  理由：因子提亮会把材质颗粒一起提亮，读起来像蒙雾。
  **四个状态的九宫格边距必须一致**，否则按下时内衬会跳。

---

## 3. 布局系统

### 3.1 窗口壳（三种，按面板实数）

| 壳 | 尺寸 | 用于 | 结构 |
| --- | --- | --- | --- |
| L | 1280×760 | 信使 | 三栏：左 300 名单 / 中 弹性 / 右 260 角色卡 |
| M | 960×720 | 对话 | 两栏：中 弹性（无侧栏） |
| S | 1100×680 | 三个弹窗 | 单栏（头带 + 内容） |
| 条 | 720×55 | 场景状态条 | 单行，无头带 |

### 3.2 三段式（所有带顶栏的面板统一）

```
┌─ 头带 60（Awake.Ornament.HeaderBand，上下不拉伸）
├─ 分隔金线 5（Awake.Ornament.DividerGold，整条拉伸）      ← 本轮已落地 5 处
├─ 内容区（弹性，MarginTop 70 起）
└─ 输入区 60（Awake.Panel.Input800，仅两个对话面板，贴底）
```

**头带带高 60 是硬值**：`header_band` 的九宫格是 `24/0/24/0`（上下不拉伸），高度一变上下沿刻线会被拉成斜纹。

### 3.3 弹性规则（普适 UI 的做法）

- 中栏用 `StretchToParent` + 左右 `MarginLeft/MarginRight` **咬合**，不用固定宽；
- 侧栏固定宽（300 / 260）贴边；
- 内容区上下堆叠自撑（`CoverChildren`），容器给 `ScrollablePanel` + `ClipRect`；
- **气泡/条目高度自适应**（`CoverChildren`）⇒ 其九宫格纵向必须能无限延伸不变形，
  **装饰只能放左右边和四角**。

---

## 4. 组件清单与状态矩阵（M0 已落地部分）

**本轮已接线**（`AwakeBrushes.xml`，11 条 Brush / 21 个 sprite，全部实测可解析）：

| 组件 | Brush | sprite（状态） | 九宫格 L/T/R/B | 落点 |
| --- | --- | --- | --- | --- |
| 主面板底 L | `Awake.Panel.Main1280` | `panel_main_1280` | 32/32/32/32 | 信使 |
| 主面板底 M | `Awake.Panel.Main960` | `panel_main_960` | 32/32/32/32 | 对话 |
| 弹窗底 S | `Awake.Panel.Dialog1100` | `panel_dialog_1100` | 32/32/32/32 | 三个弹窗共用 |
| 输入区 | `Awake.Panel.Input800` | `panel_input_800` | 16/16/16/16 | 信使 / 对话 |
| 状态条 | `Awake.Panel.Status720` | `panel_status_720` | 16/16/16/16 | 场景状态条 |
| 头带 | `Awake.Ornament.HeaderBand` | `header_band` | 24/0/24/0 | 5 个面板 |
| 分隔金线 | `Awake.Ornament.DividerGold` | `divider_gold` | 0/0/0/0 | 5 个面板 |
| 关闭键 | `Awake.Button.Close` | `btn_close_40`(+hover/pressed) | 12/12/12/12 | 全部面板 |
| Tab | `Awake.Button.Tab` | `btn_tab_105`(+hover/pressed/selected) | 14/14/10/10 | 信使 3 / 对话 2 |
| 主按钮 | `Awake.Button.Primary` | `btn_primary_110`(+hover/pressed/disabled) | 16/16/16/16 | 发送、开发工具 ×4 |
| 次按钮 | `Awake.Button.Secondary` | `btn_secondary_100`(+hover/pressed) | 16/16/16/16 | 写信 |

**尚未接线（等 M1，见 §7 需求）**：

| 组件 | 现状（原版） | 目标 sprite | 影响 |
| --- | --- | --- | --- |
| 联系人条目 | `ButtonBrush1`（橄榄绿实心条，**观感最刺眼**） | `contact_item` 280×85 | 信使左栏 |
| 聊天气泡 ×2 | `BlankWhite` α0.10/0.16（暗底上**几乎看不见**） | `bubble_npc` / `bubble_player` 600×72 | 两个对话面板 |
| 侧栏条带 ×2 | `BlankWhite` α0.08 | `panel_side_300` / `panel_card_260` | 信使左右栏 |
| 列表行 / 内凹槽 | `BlankWhite` α0.08 | `list_row` / `inset_slot` | 三个弹窗 |
| 头像框 | `frame_9`（原版） | `portrait_frame` 200×200 | 信使右卡 |
| 小标记键 | `ButtonBrush1`（65×25） | `btn_chip_65` | 信使「置顶」 |
| 细金线 | — | `hairline` 800×3 | 条目分隔 |

---

## 5. 命名与 Brush 契约

- **Brush 名**：`Awake.<组>.<名>`，组＝`Panel` / `Ornament` / `Button` / `Item` / `Text`。
  前缀 `Awake.` 防撞原版（brush 名是全局命名空间）。
- **sprite 名**＝文件名（分类目录不进名）；源图 `GUI/SpriteParts/ui_awake_<类>/<名>.png`。
- **九宫格只在 `BrushLayer` 的 `ExtendLeft/Top/Right/Bottom`**，四个全写才是九宫格。
  （实测：全库 10 个 `*SpriteData.xml` 里 0 处 Extend；`Native/GUI/Brushes/` 里 109 处在用。）
- **状态名只许这五个**：`Default` / `Hovered` / `Pressed` / `Disabled` / `Selected`
  （原版 Brushes 词频实测；另有 `SelectedDisabled`，本项目暂不用）。
- **`<Style>` 是部分覆盖**（实测 `BrushFactory.LoadStyleInto` → `style.GetLayer(name)` 增量赋值）
  ⇒ Extend 只在 `<Layers>` 写一次，状态里只覆盖 `Sprite`。
- Brush 文件**自动加载**，不需要在 `SubModule.xml` 声明
  （实测 `BrushFactory.LoadBrushes` → `ResourceDepot.GetFiles("Brushes",".xml")`；
  全库 0 个模组的 SubModule.xml 声明过 Brush/SpriteData）。

---

## 6. 绑定契约（Gauntlet 硬约束）

1. **只支持正向属性路径** ⇒ 空状态必须由 VM 提供**互补的正向 bool**（`IsContactsEmpty => !HasContacts`）。
2. **VM「算好 + 发通知」≠ UI「已消费」**——属性加了必须确认 Prefab 里有节点读它；
   本层吃过这个亏（4 个属性算好了、XML 里 0 消费）。
3. 命令走 `Command.<名>`（`Event.` 前缀**静默失效**，实测）。
4. 文字绑定用 `@属性`（`RealText="@InputText"` 为双向）；`{属性}` 为 DataSource 绑定。
5. 状态可见性只用正向 bool，不在 XML 里写取反/表达式（原版 0 命中，不支持）。

---

## 7. 采用矩阵与待办

| 面板 | 底 | 头带 | 分隔 | 关闭 | Tab | 输入 | 按钮 | 条目/气泡 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| AwakeMessenger | ✅1280 | ✅ | ✅ | ✅ | ✅3 | ✅ | ✅主+次 | ⏳M1 |
| NpcDialogue | ✅960 | ✅ | ✅ | ✅ | ✅2 | ✅ | ✅主 | ⏳M1 |
| WorldEventInbox | ✅1100 | ✅ | ✅ | ✅ | — | — | — | ⏳M1 |
| WeeklyReportBrowser | ✅1100 | ✅ | ✅ | ✅ | — | — | — | ⏳M1 |
| DeveloperCheck | ✅1100 | ✅ | ✅ | ✅ | — | — | ✅主×4 | ⏳M1 |
| SceneDialogueStatus | ✅720 | — | — | — | — | — | — | — |

**下一步（按价值排序）**：
1. ~~向美术线提 M1 正式需求~~ ✅ **已提（09-13）** —— `UI-ART-ASSET-INTERFACE` §4 已填 11 行（联系人条目 / 气泡 ×2 / 侧栏 ×2 / 列表行 / 内凹槽 / 细金线 / 置顶底板 / 头像框 / 图标 7 个），
   并回了乙方 §7 的两问（§4.2）：**C5 图标＝只做 7 个有落点的**（关闭键不出图标 —— 实测 `btn_close_40` 已烤 ×；返回/箭头/提示/编年史暂缓，因控件位不存在）、**按钮状态＝逐状态出图 + 四态九宫格边距必须一致**。
   **等乙方交付 M1 后**，本层接线顺序：条目 → 气泡 → 侧栏 → 列表行/凹槽 → 细金线 → 置顶 → 头像框 → 图标。
2. 字号阶梯收敛 6 档（6 处替换），与 `SuggestedHeight` 一起过
3. 侧栏/条目的 α 与内凹关系：现在侧栏是 `BlankWhite` α0.08 的平铺洗色，
   等 `panel_side_300` 到位后改为真底图
4. 三小面板的 `105/320` 进 `<Constants>`

---

## 8. 未验清单（不许当结论用）

| 项 | 状态 |
| --- | --- |
| **真机渲染**（贴图是否真的被引擎加载、观感是否如预览） | ⛔ **未验**。本地预览 ≠ 真机 |
| **管线阻塞**：Modding Kit 未装 ⇒ `SpriteSheetGenerator` 做不了 ⇒ `*SpriteData.xml`/`SpriteSheets/` 与 `_tex.tpac` 都产不出 | ⛔ 仍在。**不卡出图、不卡本地预览**（Lab 已能直读 `SpriteParts`），**只卡真机验收** |
| 松散 PNG 能否免 Kit 直接被引擎加载 | ⛔ 未定论。本轮核查：可用反编译源码（10 个程序集）里 `SpriteParts`/`SpriteSheets` **0 命中** ⇒ 那是**离线生成器**的输入；运行时走 `SpriteData`/`Brushes` 两个资源目录（实测 `UIContext.cs:289-293`）。但 `TaleWorlds.TwoDimension`（`SpriteData` 的实现所在）**未反编译** ⇒ **仍标待验证，不可当结论** |
| 焦点 / 手柄导航 / 层序 | 未验（见技能 `bannerlord-gauntlet-ui-input-focus`） |
| 状态条透明度 | 原版是 `#C8141414` 的 8% 洗色；换成 `panel_status_720` 后**是否过重**，要真机看 |

---

## 9. 本轮变更记录（2026-09-13）

**新增**
- `GUI/Brushes/AwakeBrushes.xml` —— 11 条 Brush / 21 个 sprite（M0 全量），九宫格取自
  `tools/awake-art-lab/out/m0-manifest.json`。
- 本文。

**改（6 个 Prefab 纯皮肤层）**：30 处 `Sprite=`/`Brush=` 换成 `Awake.*`；插分隔金线 5 条。
未动布局数值、未动 Command/绑定、未动文字 brush。

**UI Lab 能力补（本层维护）**
- sprite 解析新增**直读 `GUI/SpriteParts/**/<名>.png`** ⇒ 免图集、免索引、**免 Modding Kit** 就能
  预览自绘贴图（实测 21/21 命中）。
- Brush 加载把**本模组自有 Brushes 排在最前**（实测 11/11 解析，九宫格全对）。
- 报告口径修正：Brush 统计拆成 **自有命中 / 原版命中 / 未命中** ⇒ 此前 `Awake.*` 一律被算
  「未命中」，看着像故障；现在未命中 0 才是真的。
- 审计新增规则 **`color_format`**（`Color` 必须 8 位；口径来自原版 729 处全 8 位）。

**验证（静态）**：6 面板 `error 0 · warn 0 · info 7`；Brush 未命中 0；控件 212。
**未验**：真机 E4（§8）。
