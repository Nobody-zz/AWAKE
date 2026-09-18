# 画位控件 · 数据源契约（UI 编辑层 → 主干）

> 日期：2026-09-14 · 出方：UI 编辑层 · 收方：主干（运行时）
> 配套文件：`AWAKE/GUI/Prefabs/AwakePortraitSlot.xml`（控件本体）
> 设计出处：`AWAKE/docs/ui-design/npc-dialogue-layout-07.html`（四态与按钮落点）

## 1. 这是什么

NPC 全身像的「一格」。空着时显空位，生成完显图，中间有生成中与失败两种过渡态。
**一个按钮靠位置表达状态** —— 空位时居画位中央，生成后下移到底部。

控件已经写完（纯 XML）。它不需要任何新 sprite、新 Brush —— 只用已有的
`Awake.Button.Secondary`、`Popup.Button.Text`、`Popup.Description.Text`，以及已有的自定义控件
`AwakePortraitImageWidget`（`src/AwakePortraitImageWidget.cs`，属性 `PortraitKey`）。

**它现在跑不起来，只差数据源。** 下面这组属性在 VM 上还不存在，绑上去会变成一块黑底。

## 2. 控件绑定的属性（VM 需要提供）

| 属性 | 类型 | 用途 |
|---|---|---|
| `ShowEmptySlot` | bool | 显空位层（画框 ＋ 顶标签） |
| `ShowPortrait` | bool | 显画位图层 ＋ 左上角标 |
| `ShowGenerating` | bool | 显生成中遮罩（压住下面所有层） |
| `ShowError` | bool | 显错误行 |
| `ShowCenteredButton` | bool | 显**居中按钮**（空位 或 首次失败时） |
| `ShowRegenerateButton` | bool | 显**底部按钮**（已生成 或 有图刷新失败时）。**生成中必须为 false** |
| `PortraitKey` | string | 画位图键；置空＝无图（`AwakePortraitImageWidget` 已有此属性） |
| `SlotLabelText` | string | 空位中央偏上的小标签，如「全身像」 |
| `PortraitBadgeText` | string | 左上角标，如「AI 全身像 · 09-14」 |
| `GeneratingText` | string | 遮罩上的文案，如「生成中…」 |
| `PortraitErrorText` | string | 错误行文案，如「生成失败 · 请重试」 |
| `GenerateButtonText` | string | 居中按钮文案：「生成全身像」/「重新生成」 |
| `RegenerateButtonText` | string | 底部按钮文案：「重新生成」 |
| `ExecuteGeneratePortrait()` | 方法 | 按钮点击命令（`Command.Click` 用，需 public 无参） |

## 3. 四个状态该怎么设（真值表）

| 状态 | ShowEmptySlot | ShowGenerating | ShowPortrait | ShowError | ShowCenteredButton | ShowRegenerateButton |
|---|:---:|:---:|:---:|:---:|:---:|:---:|
| ① 空位（从未生成） | ✔ | | | | ✔ | |
| ② 生成中 | | ✔ | 有旧图则 ✔ | | | **false（硬要求）** |
| ③ 已生成 | | | ✔ | | | ✔ |
| ④ 首次失败 | ✔ | | | ✔ | ✔ | |
| ⑤ 有图、刷新失败 | | | ✔ | ✔ | | ✔ |

- ⚠️ **`ShowRegenerateButton` 不是 `ShowPortrait` 的别名**——② 与 ⑤ 的全部差别都在这一个 bool 上。
  07 稿规格表写死「② 生成中 ⇒ 按钮**隐藏**（不可重复触发）」；而 ② 有旧图时 `ShowPortrait` 仍为真，
  ⇒ 底部按钮若直接绑 `ShowPortrait`，**生成中会露出一颗按钮、玩家能重复触发**。
  （**09-15 修正**：本契约旧版第 3 节正是这么写的，`AwakePortraitSlot.xml` 已同步改绑。）
- 居中按钮的文案随状态变：① 为「生成全身像」、④ 为「重新生成」——由 VM 写在 `GenerateButtonText` 里，
  控件不判断状态。
- ⑤ 保留旧图不丢：`ShowPortrait` 仍为 ✔，只多一行错误。

## 4. 两条实现上的提醒

1. **按钮是两个 Widget，不是一颗会动的按钮。** Gauntlet 没有位置过渡，只能"居中一颗、底部一颗、
   按状态各显一个"。视觉结果与 07 稿一致（切状态时按钮从中央消失、在底部出现），但没有平滑滑动。
2. **"转圈"在纯 XML 里做不了。** 07 稿的生成中状态有个旋转指示，现在退化成一行「生成中…」。
   要真转圈得靠 C# 驱动一个旋转的 Widget（或用序列帧 sprite）—— 需要的话另开一项。

## 5. 怎么加载

参考现成的 `src/AwakePortraitProbeOverlay.cs`：建 `GauntletLayer` → `LoadMovie("AwakePortraitSlot", vm)`。
控件自带一个 960×720 的黑底窗口，居中放画位 —— 那是为了能**单独打开看**；正式用的时候，
把 `<Window>` 里那个 212×360 的画位整段复制进目标面板即可。

## 6. 未验证 / 待办

- ⚠️ **真机未验**：控件还没在游戏里加载过（需要上面第 2 节的属性先到位）。本层只做了静态可解析性验证。
- ⚠️ **"极淡人形轮廓"没做**：07 稿的空位里有一个极淡的全身人形，纯 XML 画不出矢量，需要一张
  sprite。现版本的替代是"四边 1px 淡金线框 ＋ 顶标签"。
  **已向美术提需求**：`UI-ART-ASSET-INTERFACE-20260913.md` §4.4 第 12 条（212×360，P0）。
  图到手后在这里加一个 `Sprite=` 引用即可，控件结构不用动。
- **另三项同批需求**：画位细金线框（§4.4 第 13 条）、生成按钮 140×34 三态（第 14 条）、
  生成中静态指示（第 15 条）。都属"有更好、没有也能跑"。
- **画位尺寸** 212×360 与 `AwakePortraitProbe.xml` 一致；若对话面板三列布局（稿 04–07）落地，
  尺寸不变。

---

## 7. 几何归一（09-15，为过 Lab 的 5 栅格审计）

原本照 07 稿原值写，结果 Lab 报 **13 条 `grid_off_5`** —— 而老 6 个面板都是 **0 条**。逐一处理：

| 原值（照 07 稿） | 现值 | 为什么 |
| --- | --- | --- |
| 按钮 `top 163` ↔ `top 308` | 居中＝`VerticalAlignment="Center"`；下移＝`VerticalAlignment="Bottom"` ＋ `MarginBottom=20` | 163／308 都不落 5 栅格。**改用对齐式**：居中就是真居中（163 本来是 `(360-34)/2` 的近似值），下移＝底距 20 |
| 按钮高 `34` | **35** | 落 5 栅格，且与既有 `Awake.Button.Tab` 的 35 一致 |
| 空位细线框 `inset 7`（框 198×346） | **`inset 5`**（框 202×350） | 7 不落 5 栅格；差 4px 不影响观感 |
| 标签 `MarginTop 14` | **15** | 同上 |
| 标签／角标／错误行 `SuggestedHeight 18` | **20** | 同上 |
| 标签／角标 `FontSize 11`、错误行 `12` | **13** | **6 档阶梯最小档就是 13**，根本没有 11／12 两档（这个错是本层自己犯的，**不在"6 处"账面里**） |
| 画位 `212×360` | **不动** | 美术 §4.4 第 12 条与运行时纹理都钉在 212，改它要同时改三边 ⇒ **已接受偏差** |

**结果：`AwakePortraitSlot.xml` 问题数 30 → 1**（剩的 1 条是 `magic_large_literal`，info 级，与其余面板同类）。
6 个面板保持 `error 0`，改动后仍是原基线那 7 条 info。

⚠️ **主干侧同病**：`AwakePortraitProbe.xml` 仍有 **13 warn `grid_off_5`** ＋ 17 info —— 全是同一批 07 稿原值。
**探针面板归主干，这 13 条本层不代改。**
