# AWAKE Prefab 本地几何预览 + 布局审计（只读 CLI）

> 本地把 `AWAKE/GUI/Prefabs/*.xml` 的**布局几何**算出来、画成线框，并按规则做**布局审计**——
> 用于检查位置 / 大小 / 间距 / 对齐 / 层级。
> 不启动游戏、不碰游戏目录、不修改任何 Prefab。
> **这是一个纯本地 CLI：任何能跑命令的 Agent（WorkBuddy / 其他）都可以直接调用它，不需要额外集成。**

## 为什么这件事可行

Gauntlet 的布局是**确定性规则**，不是引擎里长出来的模糊结果：

| 规则 | 来源字段 |
|---|---|
| 一个控件多大 | `WidthSizePolicy` / `HeightSizePolicy`（`Fixed` / `StretchToParent` / `CoverChildren`）+ `SuggestedWidth/Height` |
| 它落在哪 | `HorizontalAlignment` / `VerticalAlignment` + `MarginLeft/Right/Top/Bottom` |
| 一排东西怎么排 | `ListPanel` + `StackLayout.LayoutMethod` + `Spacing` |

这些字段在 Prefab 里 100% 显式写死。给定"参考画布 1920×1080"，每个控件的绝对矩形是可以**算出来**的——所以本地能看见。

## 诚实边界（先读这一段）

**本工具输出的是「推导几何」，不是渲染结果。**

不做、也不该做的事：

- 颜色、字体、字形、贴图、图标、动效 —— 一律不还原；
- 文本高度用字符宽度**估算**（CJK 按 1em、西文按 0.55em），与 Gauntlet 真实排版有偏差；
- 不模拟九宫格贴图的实际绘制与拉伸；
- 不判定"好不好看"。

因此：**本地用来确认「结构和数字对不对」，最终「跑起来对不对」仍然只认游戏内真机（E4）。**
本地绿 ≠ 真机绿。

## 用法

| 你想干什么 | 命令 |
|---|---|
| 生成 HTML 线框预览 | `python tools/awake-ui-lab/preview/preview_prefab_geometry.py` |
| 列出所有 Prefab | `python tools/awake-ui-lab/preview/preview_prefab_geometry.py --list` |
| 布局审计（人看的文本） | `python tools/awake-ui-lab/preview/preview_prefab_geometry.py --audit` |
| 布局审计（Agent 吃的 JSON） | `python tools/awake-ui-lab/preview/preview_prefab_geometry.py --audit --json` |
| 几何 + 审计全量 JSON | `python tools/awake-ui-lab/preview/preview_prefab_geometry.py --json` |
| 只看某个面板 | `... --prefab NpcDialogue.xml` |
| 有问题就返回非 0 退出码 | `... --audit --strict` |
| **截图**：单个面板 → PNG | `... --shot out/NpcDialogue.png --prefab NpcDialogue.xml` |
| **截图**：每个面板各一张 | `... --shot out/shot/` |
| 截图但不标文字（纯线框） | `... --shot out/x.png --prefab X.xml --shot-label none` |

参数：

| 参数 | 说明 |
|---|---|
| `--prefab-dir` | Prefab 目录，默认 `AWAKE/GUI/Prefabs` |
| `--out` | HTML 输出目录，默认本目录下 `out/` |
| `--rows` | `ListPanel` 的夹具行数，默认 3 |
| `--prefab` | 只处理指定文件名，可重复 |
| `--list` | 只列出可检查的 Prefab |
| `--json` | 结果以 JSON 打到 stdout（不写 HTML） |
| `--audit` | 只做审计，不写 HTML |
| `--strict` | 存在 warn / error 时退出码 1 |
| `--shot` | 截图输出：`*.png` 路径 = 只截一个面板；目录 = 每个面板一张 |
| `--shot-label` | 截图标签：`none` 纯线框 / `compact` 只标尺寸（默认）/ `full` 标类型名 |
| `--shot-scale` | 截图缩放倍数，默认 1.0 |
| `--chrome` | Chrome / Edge 可执行文件路径，默认自动探测（也可用环境变量 `AWAKE_UILAB_BROWSER`） |
| `--native-dir` | 原版 Modules 目录（或某个 Prefabs 目录），用于展开原版 Prefab 引用；默认自动探测 |
| `--no-native-refs` | 不展开外部 Prefab 引用（只看本工程内的几何） |

## 支持的原版 Prefab 写法

原版 Prefab 比 AWAKE 的写法复杂，下面这些都已支持（`--prefab-dir` 可直接指向原版目录）：

| 写法 | 处理 |
|---|---|
| 外部 Prefab 引用 `<SPConversationAggresivePartyItem/>` | 递归展开被引 Prefab；实例属性与参数优先 |
| `<Parameters>` + `*Item.Width` | 参数记号；实例上的 `Parameter.Item.Width="50"` 覆盖被引 Prefab 的默认值 |
| `<Constants>` + `!Item.Height`（含 `MultiplyResult`） | 常量表；支持常量互相引用、常量依赖参数（`Item.Height = *Item.Width × 0.71`） |
| `GridWidget` / `LayoutImp="GridLayout"` | 按 **列数 × 格宽** / **行数 × 格高** 推导，不让格子继承父级尺寸 |
| `DimensionSyncWidget` | 解析 `WidgetToCopy*From` 相对路径（`..\..\..\AnswerListContainer`），复制目标控件尺寸 |
| `MinWidth` / `MinHeight` / `MaxWidth` / `MaxHeight` | 尺寸下限 / 上限约束 |
| 逻辑控件（`*Targeter` / `HintWidget` / `DimensionSyncWidget`） | 不占布局空间，也不参与审计 |

**实测**：原版 `SPConversation` 早先推导成 `2375×35044`（远超画布），现在收敛到 `1920×1080`，
超出画布的控件 **0 个**。

## 审计规则

基准来自对骑砍**原版 430 个 Prefab / 11026 个控件**的只读扫描，结论在
`docs/UI-LAYOUT-BASELINE-FROM-NATIVE-20260913.md`：
**原版间距与尺寸以「5 的倍数」为栅格**（5/10/15/20/25/30/50/60…），小值 1/2/3 只用于贴边微调。

| 规则 | 级别 | 含义 |
|---|---|---|
| `grid_off_5` | warn | 间距类（margin / spacing）不在 5 系栅格上，附建议值 |
| `grid_off_5_size` | info | 尺寸类（`SuggestedWidth/Height`）不在 5 系栅格上，仅供参考（可能受字体/内容约束） |
| `magic_large_literal` | info | ≥100 的写死字面数值；原版用 `<Constants>` + `!名字` 表达关系，改一处即全局生效（多为按钮宽度之和这类咬合链） |
| `fixed_without_size` | error | 声明 `Fixed` 却未给尺寸，推导尺寸会塌成 0 |
| `zero_size_widget` | warn | 推导宽高为 0 且控件可见 |

**审计按控件去重**：`ListPanel` 的 `ItemTemplate` 夹具会复制多行，同一处问题只报一次。

**审计只评判「写死的字面值」**：值为 `!常量` 或 `*参数` 表达式的属性会跳过栅格检查——
那是原版常量体系管的范围（例：`SuggestedHeight="!Item.Height"` 算出来是 49.7，不是作者写死了一个越格值）。
审计看的是**原始写法**，不是内联参数后的数字。

## 输出

- **默认模式**：`out/index.html`（浏览器打开）+ `out/report.json`
- **`--json` / `--audit --json`**：结构化结果打到 **stdout**，不写文件
- **`--shot`**：`<名字>.png` 截图 + 同名 `.html`（截图专用的干净页，无导航、无悬停条）

`out/index.html` 里每个面板一张**面板特写**线框 + 一张全画布位置图；
**鼠标悬停任意方块**，底部信息条显示它的真实布局属性（控件类型、推导尺寸、坐标、SizePolicy、Margin、绑定名）。

`--json` 结构要点：

```json
{
  "tool": "awake-ui-lab", "tool_version": "1", "command": "audit", "read_only": true,
  "canvas": { "width": 1920, "height": 1080 },
  "baseline": { "name": "native-prefab-grid-20260913", "grid_step": 5 },
  "prefabs": [
    {
      "prefab": "NpcDialogue.xml", "ok": false, "widget_count": 32,
      "panel_box": { "w": 960, "h": 720 }, "issue_count": 44,
      "issues": [
        { "rule": "grid_off_5", "severity": "warn", "widget": "ButtonWidget",
          "attr": "MarginRight", "value": 12, "suggest": 10, "message": "..." }
      ],
      "geometry": [ { "tag": "Widget", "depth": 1, "x": 480, "y": 180, "w": 960, "h": 720, "text": "" } ]
    }
  ],
  "summary": { "issue_total": 267, "by_rule": {}, "by_severity": {}, "by_value": [] }
}
```

`--audit` 时不含 `geometry` 字段，输出更小。

## 截图：给人和 Agent 看同一张图

截图用**本机 Chrome / Edge 的无头模式**把线框页截成 PNG——**不需要安装任何东西**（不需要 Playwright / Puppeteer / Node 包）。

```
python tools/awake-ui-lab/preview/preview_prefab_geometry.py --shot out/shot/
  # 全部面板各一张

python tools/awake-ui-lab/preview/preview_prefab_geometry.py \
  --shot out/NpcDialogue.png --prefab NpcDialogue.xml
  # 单个面板
```

为什么要有这个——**开发 UI 时的反馈环**：

| 环节 | 谁做 |
|---|---|
| 改 `GUI/Prefabs/*.xml` | 你 |
| 算几何 → 出图 → 审计 | 本工具（约 0.2 秒，离线） |
| 看图给意见 / 提改动 | Agent（读 PNG + 读 `--json`） |
| 拍板 / 下一条指令 | 你 |

**这张图是「我们看的是同一个东西」的凭证。** 每个方块标着推导尺寸，顶部写明面板尺寸，底部写明问题数——一张图信息自足，可以直接贴进对话。

`--shot-label` 三档：

| 档 | 效果 | 用途 |
|---|---|---|
| `none` | 只有色块轮廓 | 看整体排布、对齐、留白 |
| `compact`（默认） | 大控件标尺寸 | 日常看结构 |
| `full` | 标控件类型名 + 尺寸 | 定位到具体控件类型 |

标签带白色描边，父子同角处叠着的字也能读。

**注意**：截图截的是**推导线框**，不是渲染结果。颜色 / 字体 / 贴图 / 图标都不在里面——这不是缺陷，是刻意的（见上面「诚实边界」）。

## 图例

| 颜色 | 含义 |
|---|---|
| 中性描边 | 容器 Widget |
| 蓝 | 文本（`TextWidget` / `RichTextWidget`） |
| 灰底深框 | 按钮 / 输入区 |
| 青（虚线） | 滚动区 `ScrollablePanel` / 列表 `ListPanel` |
| 青（实线细框） | 列表行（`ItemTemplate` 展开） |
| 淡灰填充 | 全屏遮罩层 |

## 它能查什么

- 控件有没有落在**意料之外的位置**（例如整块跑到父容器外面）；
- 撑满父级的控件拿到的是**什么尺寸**（`StretchToParent` 的实际解）；
- 一排按钮的**实际间隙与累加位置**（咬合链是否对齐）；
- 列表行高、滚动区可用高度、滚动条位置；
- 每个 `@绑定名` 出现在哪一块、多大；
- **哪些间距 / 尺寸不在原版 5 系栅格上**（审计规则）。

## 它不能替代什么

- **E4 真机验收**：Gauntlet 的真实排版、层序、焦点、输入限制；
- 审美判断：审计只能保证"不破规范"，保证不了"布局设计得好"。

## 已知限制

- 外部标准件（`Standard.VerticalScrollbar` 等）的尺寸用内置兜底值（滚动条宽 20），未从游戏目录读取；
- **由 Sprite 决定尺寸的控件**（`BrushWidget` / `ImageWidget` 配 `UseSpriteDimensions`）若没写
  `SuggestedWidth/Height`，宽度推不出来——原版靠图集元数据，本地拿不到，会落进
  `fixed_without_size` / `zero_size_widget`；
- `CoverChildren` 的高度在文本控件上是估算值；
- 遮罩层的视觉压暗不做，只画轮廓；
- 审计目前只覆盖 5 条规则，尚无：对齐轴检查、同层重叠检查、跨 Prefab 重复结构检查。

若某处几何与游戏内实际不符，**以游戏内为准**，并顺手把这条差异记到本文件。
