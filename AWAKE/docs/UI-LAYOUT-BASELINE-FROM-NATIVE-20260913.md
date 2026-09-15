# AWAKE UI 布局基准（从骑砍原版提取）

> 日期：2026-09-13
> 动机：自研 UI 不该拍脑袋定尺寸数字。本文从骑砍原版 430 个 Prefab / 11026 个布局控件里，把原版的**尺寸体系**抽出来，作为 AWAKE 的布局基准。
> 数据来源：`docs/NATIVE-PREFAB-METRICS-20260913.md`、`docs/AWAKE-PREFAB-LAYOUT-VS-NATIVE-20260913.md`
> 复现命令：见文末第 5 节。全部只读游戏目录，未修改任何原版或 AWAKE 文件。

---

## 1. 原版基准（可直接作为规范）

### 1.1 基准分辨率 1920×1080
原版背景层用 1920×1080 全屏铺（`Standard.Background`：粒子 1920×1080、烟雾条 1920×700）。所有面板尺寸都是在这个画布下、居中或角落锚定的绝对像素值。

### 1.2 间距栅格 = 5 的倍数（10 最常用）

原版间距（MarginLeft/Right/Top/Bottom）频次榜：

| 排名 | 水平间距 | 次数 | 垂直间距 | 次数 |
|---|---|---|---|---|
| 1 | 5 | 526 | 5 | 457 |
| 2 | 10 | 437 | 10 | 429 |
| 3 | 15 | 227 | 15 | 295 |
| 4 | 20 | 206 | 20 | 204 |
| 5 | 25 | 119 | 25 | 108 |
| 6 | 30 | 90 | 30 | 90 |
| 7 | 50 | 81 | 50 | 83 |
| 8 | 60 | 100 | 60 | 33 |

**倍数占比（不含 0）：5 的倍数 55.2% / 54.3%，10 的倍数 36.6% / 35.1%，4 的倍数只有 31.3% / 31.1%。**
小值 2/3 用于贴边微调，不属于主栅格。

> **结论：原版主栅格是 `5 / 10 / 15 / 20 / 25 / 30 / 50 / 60`，大间隔用 10 的倍数。不是 4 的倍数、不是 8 的倍数。**

### 1.3 尺寸同样落在 5 的倍数

常用 `SuggestedWidth`：20, 50, 45, 100, 40, 60, 30, 35 …
常用 `SuggestedHeight`：40, 20, 50, 30, 35, 60, 45, 25, 55, 10 …
（8 / 4 / 22 / 58 这类是图标或字符框的贴图尺寸，不是布局主值。）

### 1.4 数字用「命名常量 + 表达式」，不手写裸值

原版 `<Constants>` 是一套**可计算的变量系统**，支持四种取值方式：

| 写法 | 含义 | 实例 |
|---|---|---|
| `Value="50"` | 字面值 | `LeftPanel.MarginRight = 50` |
| `BrushValueType="Width/Height"` | 从 Brush 取尺寸（Brush 又从 Sprite 取） | `Standard.PopupCloseButton.Width` |
| `Additive="20" Value="!Other.Width"` | 加法 | `SidePanel.ScrollablePanel.Width = BarterToggle.Width + 20` |
| `MultiplyResult="-1"` / `"0.30"` | 取反 / 缩放 | `SidePanel.NegativeWidth = -SidePanel.Width`；`Banner.Width.Scaled = Banner.Width × 0.30` |

使用处写 `MarginRight="!LeftPanel.MarginRight"`。**同一数字只有一处定义。**

对比 AWAKE 现状：`AwakeMessenger.xml` 里 `EditableTextWidget MarginRight="344"`，实际是 `226(写信箱) + 106(按钮宽) + 12(间隙)` 心算出来的；改按钮宽度必须反向重算。原版不会这么干。

### 1.5 有标准件库 `Standard.*`

原版 `Native/GUI/Prefabs/Standard/` 提供 16 个可复用标准件，最相关的：

| 标准件 | 关键尺寸/间距 |
|---|---|
| `Standard.Window` | 标题头 615×114，内容区 MarginTop 60 / 左右 10 / 底 10 |
| `Standard.Button` | 227×40 |
| `Standard.TopPanel` | 外高 196，顶栏 887×150 |
| `Standard.ScrollablePanel` | 滚动面板 MarginLeft 25 / Top 20 / Bottom 10；滚动条宽 20 |
| `Standard.PopupCloseButton` | 尺寸从 Brush 取，居中顶部 |
| `Standard.TripleDialogCloseButtons` | Cancel/Done `MarginLeft/Right = 35` 对称 |

原版 `<Prefab>` 里可以用 `<Standard.Window>` / `<Standard.ScrollablePanel>` 之类的展开语法直接复用，不必重写结构。

### 1.6 面板尺寸量级（原版实例）

| 面板 | 宽×高 | 宽高比 |
|---|---|---|
| 全屏界面（Loading / ProfileSelection） | 1920×1080 | 1.78 |
| 记分板 `MultiplayerScoreboard` | 1662×533 | 3.12 |
| 场景通知 `SceneNotification` | 1500×840 | 1.79 |
| MP 档案 `Lobby.Profile` | 1350×800 | 1.69 |
| 角色创建·文化 `CharacterCreationCultureStage` | 1300×700 | 1.86 |
| 部队选择 `GameMenuTroopSelection` | 1128×965 | 1.17 |
| 物品栏 `Inventory` | 1080×1080 | 1.00 |
| 继承人/婚姻弹窗 | 1050×900 | 1.17 |
| 兵种选择弹窗 | 930×900 | 1.03 |
| 交易 `BarterScreen` | 912×555 | 1.64 |
| 棋盘 `BoardGame` | 820×700 | 1.17 |
| 新造武器弹窗 | 750×750 | 1.00 |

**量级结论：弹窗类多在 750–1500 宽、500–900 高；全屏界面 1080–1920。** 常用宽高比是约 1.6–1.9（宽扁）或约 1.0–1.2（近方）。

---

## 2. AWAKE 现状（量化对照）

### 2.1 栅格体系相反

| 体系 | AWAKE（6 个 Prefab） | 原版 |
|---|---|---|
| 5 的倍数占比 | **18.3%** | 55.2% / 54.3% |
| 10 的倍数占比 | 15.1% | 36.6% / 35.1% |
| 4 的倍数占比 | **65.1%** | 31.3% / 31.1% |

AWAKE 最常用的间距值是 **12（62 次）、24（29 次）**——这是 Web / 8-4 栅格思维，不是骑砍的体系。

### 2.2 各 Prefab 尺寸

| 文件 | 控件数 | 顶层固定尺寸 | 宽高比 |
|---|---|---|---|
| `AwakeMessenger.xml` | 65 | 1280×760 | 1.68 |
| `NpcDialogue.xml` | 26 | 960×720 | 1.33 |
| `DeveloperCheck.xml` | 22 | 1100×680 | 1.62 |
| `WeeklyReportBrowser.xml` | 10 | 1100×680 | 1.62 |
| `WorldEventInbox.xml` | 14 | 1100×680 | 1.62 |
| `SceneDialogueStatus.xml` | 2 | 720×54 | 13.3（状态条，正常） |

### 2.3 三个实打实的差距

1. **栅格不同**：主间距 12/24 系，与原版 5/10 系不同频。这是"看着不像原版"的数学根源，不是审美问题。
2. **同一功能两套尺寸**：`NpcDialogue`（960×720，1.33）与 `AwakeMessenger`（1280×760，1.68）都是"对话类"面板，尺寸与比例都不同，且聊天区几乎逐行重复、margin 各自手写一遍。
3. **裸值与咬合链**：无命名常量，数字之间靠人脑记账（例：`344 = 226+106+12`；`NpcDialogue` 按钮间隙 2 / 输入框到按钮 8；`AwakeMessenger` 按钮间隙 12/4/12；卡片行距 8/4/6/6/4）。

---

## 3. 建议（按性价比排序）

### 3.1 把栅格从 4 系换到 5 系（最见效）
不要求一次性全改，但新写的面板、以及顺手改到的旧值，都向 5 的倍数靠。建议映射：

| 现用 | 建议 | 现用 | 建议 |
|---|---|---|---|
| 12 | 10 | 106 | 105 |
| 24 | 25 | 112 | 110 |
| 6 | 5 | 118 | 120 |
| 8 | 10 | 204 | 200 |
| 44 | 45 | 226 | 225 |
| 64 | 65 | 344 | 345 |
| 66 | 65 | 142 | 140 |

（180 / 220 / 70 / 30 / 40 / 60 已是 5 的倍数，不动。）

### 3.2 关键数字常量化
把重复出现、或由别的元素算出来的距离提成 `<Constants>`，用 `!名字` 引用。优先处理三处：
- 面板内边距（`Panel.Padding`）
- 按钮组间隙（`Buttons.Gap`，现在是 12/4/12 和 2/8 两种）
- 输入框右边距（`Input.RightOffset = 按钮总宽 + 间隙`）

### 3.3 复用 `Standard.*` 标准件
至少把**滚动区**换成 `Standard.ScrollablePanel` 的尺寸惯例（`MarginLeft 25 / Top 20 / Bottom 10`、滚动条宽 20），替代现在两处手写的 `ScrollablePanel + ClipRect + ListPanel + Scrollbar` 结构。

### 3.4 同一功能统一一种尺寸
`NpcDialogue` 与 `AwakeMessenger` 二选一作为"对话面板标准尺寸"，另一个改为引用同一组常量。建议取原版常用比例 1.6–1.8（现 `AwakeMessenger` 1280×760 = 1.68 已经落在原版区间内，`NpcDialogue` 960×720 = 1.33 偏方）。

---

## 4. 明确不做

- **不照抄原版对话形态**：原版 `SPConversation` 是全屏底部锚定（对话右下、选项左下、无输入框、无关闭按钮）。AWAKE 的是"居中弹窗式 AI 聊天窗"，原版没有对应物。**形态自己定，只把栅格和规范跟原版对齐。**
- **不追像素渲染**：本地只做数字层审计与结构线框，像素级只认真机 E4。
- **不改游戏目录**：本文所有扫描均为只读。原版 Prefab 与 Sprite 数据只作参考，不进 AWAKE 仓库。

---

## 5. 数据与复现

```bash
# 原版全量扫描（430 文件 / 11026 控件）
python docs/scan_native_prefab_metrics.py
#   → docs/NATIVE-PREFAB-METRICS-20260913.md
#   → docs/native-prefab-metrics-20260913.json

# AWAKE 自有 Prefab 对照诊断
python docs/scan_awake_prefab_vs_native.py
#   → docs/AWAKE-PREFAB-LAYOUT-VS-NATIVE-20260913.md
```

游戏目录（只读）：`D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\{Native,SandBox,SandBoxCore,StoryMode,Multiplayer}\GUI\Prefabs`

### 原版关键文件位置（备查）

| 内容 | 路径（相对游戏根） |
|---|---|
| 标准件库 | `Modules/Native/GUI/Prefabs/Standard/` |
| 原版对话界面 | `Modules/Native/GUI/Prefabs/Conversation/SPConversation.xml` |
| Sprite 尺寸表 | `Modules/Native/GUI/NativeSpriteData.xml` |
| Brush 定义 | `Modules/Native/GUI/Brushes/*.xml` |
| 标准尺寸常量（含表达式范例） | `Modules/SandBox/GUI/Prefabs/Barter/BarterScreen.xml` |
