# AWAKE UI Lab · 工具链入口

这个目录下有三条能力线，`awake_ui.py` 是它们的统一入口——**一条命令，一份报告**。

## 一个入口

```bash
python awake_ui.py check                       # 全套：几何 + 审计 + 截图 + 流程 + 原版取证
python awake_ui.py check --prefab NpcDialogue.xml
python awake_ui.py check --no-shot             # 不截图（更快）
python awake_ui.py check --no-flow             # 不跑 dotnet（最快，只做几何）
python awake_ui.py check --no-native           # 不做原版取证
python awake_ui.py check --no-shot --diff      # 顺带和上次快照比
python awake_ui.py diff                        # 与上一次快照比：这次改了什么
python awake_ui.py check --json                # 报告打到 stdout（给 Agent 吃）
python awake_ui.py check --strict              # 有 warn/error 时退出码 1
```

产出（都在 `out/`，已 gitignore）：

| 文件 | 内容 |
|---|---|
| `out/ui-report.v1.json` | 统一报告：几何 / 问题 / 截图 / 流程 / 原版取证 / 汇总 |
| `out/index.html` | 可悬停看属性的线框页 |
| `out/shot/*.png` | 截图，信息自足，可直接贴进对话 |
| `out/flow.json` | C# 本地 Lab 的原始结果（E2 + fixture 全量） |
| `out/snapshots/*.json` | 每次 check 存一份，`diff` 靠它 |

## 三条线（都已在 `lines` 字段里标 wired）

| 线 | 载体 | 接线状态 |
|---|---|---|
| ① 几何 | `preview/preview_prefab_geometry.py` | **wired** |
| ② 流程 | `tests/Awake.UiLab.Tests`（dotnet，E2 + fixture） | **wired**（`--flow`，默认开） |
| ③ 取证 | 原版官方模块的 XML（本地解析） | **wired**（`--native-dir`，默认自动探测） |

### ② 流程怎么接的

`awake_ui.py` 调 `dotnet run --project tests/Awake.UiLab.Tests -- --flow-json out/flow.json`，
一次跑完 **16 条 E2 生命周期用例** + **17 个状态 fixture**，结果写进报告顶层 `flow` 字段。
dotnet 不在、超时、构建失败都**不致命**——只把 `lines.flow` 标成 `error`，几何照常产出。

### ③ 取证取得什么

对每个面板抽三样事实：**绑定**（`{Name}` / `@Name`）、**命令**（`Command.*`）、**节点 Id**，
再和原版官方模块（Native / SandBox / SandBoxCore / StoryMode / Multiplayer，约 430 个 Prefab）
对照：

- `exact` —— 原版有没有同名 Prefab；
- `nearest` —— 按绑定/命令找最像的面板（**IDF 加权**，避开 `ExecuteClose` 这类满地都是的通用命令）；
  `shared` 是原始共享项数，`delta` 是双向差集（原版还有哪些命令我们没实现）；
- `brushes` —— 引用的 Brush 在原版 `Brushes/*.xml` 里能否命中（实测 AWAKE 28 个引用全部命中）。

## diff 比较什么

`diff` 不是只比问题总数。它在三个层次上比：

1. **面板外框**（`panel`）——尺寸变了没有；
2. **问题明细**（键 = 规则 + 控件 + 属性 + 值）——哪些修好了、哪些新增；
3. **几何矩形**（`digest.layout`）——逐个控件的 `tag@depth:x,y,w,h` 签名，
   能定位到「哪个矩形从 1388 挪到了 1387」。

每条问题都带**出处**：`file` + `line`（读回原文件那一行核过，267/267 命中）
+ `path`（`Widget[0]/ButtonWidget[1]` 这样的节点路径）。所以 diff 的键也从
「控件类型 + 属性 + 值」升级为**带位置的键**——同一文件里两处写了同样的越格值
（12 改成 13 两边都越格、总数不变）不会再互相掩盖。

## 几何引擎现在能吃什么

原版 Prefab 的写法比 AWAKE 复杂得多，引擎已支持（`--prefab-dir` 可直接指向原版目录）：

| 写法 | 处理 |
|---|---|
| 外部 Prefab 引用（`<SPConversationAggresivePartyItem/>`） | 递归展开被引 Prefab，实例属性/参数优先 |
| `<Parameters>` + `*Item.Width` | 参数记号；实例上的 `Parameter.Item.Width="50"` 覆盖默认值 |
| `<Constants>` + `!Item.Height`（含 `MultiplyResult`） | 常量表，支持常量互相引用、常量依赖参数 |
| `LayoutImp="GridLayout"` / `GridWidget` | 按 列数 × 格宽 / 行数 × 格高 推导，不继承父级尺寸 |
| `DimensionSyncWidget` | 解析 `WidgetToCopy*From` 相对路径，复制目标控件尺寸 |
| `MinWidth/MinHeight/MaxWidth/MaxHeight` | 尺寸下限/上限约束 |
| 逻辑控件（`*Targeter` / `HintWidget` / `DimensionSyncWidget`） | 不占布局空间，也不参与审计 |

**实测效果**：原版 `SPConversation` 曾经推导成 `2375×35044`（远超画布），
现在收敛到 `1920×1080`，超出画布的控件 **0 个**。

审计只评判**写死的字面值**——`!常量` / `*参数` 表达的值归原版常量体系管，不按栅格规则扣分。

## 边界

**只读**：不修改任何 Prefab、不碰游戏目录、不启动游戏。

输出的是**推导几何**，不是渲染结果——颜色 / 字体 / 贴图不还原，
文本高度是估算值。最终验收仍以游戏内真机为准。详见 `preview/README.md`。

**已知未覆盖**：`BrushWidget` 这类由 Sprite 决定尺寸的控件，若没写 `SuggestedWidth`，
引擎仍推不出宽度（原版靠 `UseSpriteDimensions` + 图集元数据）。这类会出现在审计的
`fixed_without_size` / `zero_size_widget` 里。

## 真实文本度量（可选增强）

文本尺寸默认走**启发式估算**（CJK 1.0em / 西文 0.55em，行高 `fs*1.45`）。当游戏装在
`D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord` 下时，几何引擎会在首次
`check` 时**自动**多装配一层真实度量（`preview/ui_text_metrics.py`）：

```
字体数据  <game>/GUI/GauntletUI/Fonts/<font>/<font>.fnt  （逐字 xadvance + lineHeight）
字号/字体  <modules>/<Mod>/GUI/Brushes/*.xml 的 Brush 定义
语言映射  <modules>/Native/GUI/Fonts/NativeLanguages.xml（简体中文下 Galahad/Fira→simkai）
文本内容  src/*.cs 的 AwakeLocalization.Resolve("id", "回退字面量") + 语言文件
```

**覆盖范围内**（fs 16-28，CJK 文本）行高从 `fs*1.45` 收敛到真实 `lineHeight`（**5-6px 差**），
对 CoverChildren 文本控件（AwakeMessenger/NpcDialogue 的 8 个 `@属性` 控件）生效。
**覆盖范围外**：`Speaker`/`Text`/`StreamingText`/`InputText` 这类运行时才知道值的动态绑定
仍退启发式。

显式关掉：`AWAKE_UILAB_NO_METRICS=1 python awake_ui.py check`

**已知缺口**：与 audit 当前的 51 条问题（`grid_off_5`/`magic_large_literal`）零交集——B 真正
发挥作用在**长文本撑高场景**，而审计评的是写死的字面值栅格。

## 真色上色（C 线，同样不编造）

线框图/截图对控件上**真色**，三级来源按序取：

| 来源 | 判定 | 作用 | 实测覆盖 |
|---|---|---|---|
| `Color` + `AlphaFactor`（控件属性） | `attr` | 直接染 **fill**（如黑底 0.82、侧栏白 0.08） | ~13% |
| Brush 的 Default 态 `FontColor` | `brush` | 染文本控件**边框**（SVG 里文本只有矩形，边框即字色） | ~48% |
| Sprite 类 Brush | — | **无真色可信，不上色**，info 标注「sprite贴图(无真色)」 | 标注 ~7% |

Brush 数据来自 B 线的 BrushIndex（已扩展：解析 Default 态 `FontColor`、Sprite 标记，
沿 `Extends` 继承链补齐）。颜色永远不猜——没有真值的控件保持原分类色。
悬停任意控件，`data-info` 里会写 `色=#E4C59B(brush)` / `色=#000000(attr)` / `色=sprite贴图(无真色)`。

## 原版贴图（质感线）

Sprite 类控件直接铺**游戏原版贴图**（不是近似色，是 TPAC 里的真图集裁片）：

```
sprite 索引  <Modules>/<Mod>/GUI/*SpriteData.xml   （sprite 名 -> 图集+矩形，4455 条）
图集本体     gauntlet_ui.tpac（TPAC v2 / DXT1·DXT5·BC7）
解码裁剪     外部工具 schema-indexer --extract-sheet（kerema14/GauntletUI-LSP 的 TpacTool
             fork；克隆位置 out/GauntletUI-LSP，gitignore 内；重建：git clone --recursive
             后 dotnet build schema-indexer）
单 sprite    out/atlas/sprites/<名>.png，SVG <image> 拉伸铺贴
```

程序化 sprite（`BlankWhite` 等运行时生成的纯白块）用等价替身 PNG。
贴图不可用时自动退回分类色；`AWAKE_UILAB_NO_SPRITES=1` 显式关闭。
实测覆盖：6 面板 370 控件中 **122（33%）铺上原版贴图**（面板石纹底、关闭按钮、
滚动条、按钮框全为真素材）；列表区仍是分类色（其底色在真机由 canvas 透出）。

**九宫格（ExtendLeft/Top/…）**：Brush 的 Default 层带 Extend 参数时，先按目标尺寸
用 PIL **预合成**九片（四角原样、四边单向拉伸、中心双向拉伸），再当普通
`<image>` 铺——缓存 `sprites/__nine_<名>_<宽>x<高>.png`。按钮（`ButtonBrush1`
→ `General\Button\main_button_*`，Extend 22）边框不再变形。目标比边框还小
（w < L+R）时退回整图拉伸。

**渲染分层（两段式）**：先画全部分类色假底，再统一画贴图，最后标签。为什么：
XML 里后出现的兄弟容器（如消息列表）的分类色底是可视化假底（真机里透明），
按文档序画会把先铺好的真贴图盖掉——按钮曾是"平的"就是这个原因。
