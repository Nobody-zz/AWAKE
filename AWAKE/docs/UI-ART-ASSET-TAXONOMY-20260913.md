# AWAKE UI 美术资产分类表（2026-09-13）

- 批次：`AWAKE-UI-ART-TAXONOMY-20260913`
- 归属：图标 & 插画会话（乙方）产出，交 UI 管理会话（甲方）复核
- **这份表解决什么**：把"要做哪些图"按**功能角色**分成 6 类，每类对齐一个落盘分类目录，并标出落点面板、规格、批次。
- 与另两份的关系：
  - `UI-ART-SPEC-20260913.md`（甲方需求书）= **要什么**
  - `UI-ART-STYLE-BASELINE-20260913.md`（乙方画风基准）= **画成什么样**
  - **本文 = 有哪些、怎么归堆、先做哪个**（把上面两份接起来的中间层）

---

## 0. 先纠一个范围问题（重要）

甲方需求书只覆盖了 **2 个面板**（信使 1280×760、对话 960×720）。
但实测 `AWAKE/GUI/Prefabs/` 下有 **6 个面板**，且**全部**在用原版素材：

| 面板 | 尺寸（实测） | 玩家可见 | 关闭键 | 自有皮肤现状 |
| --- | --- | --- | --- | --- |
| `AwakeMessenger.xml` | 1280×760 | ✅ | 40×40 | 全原版 brush |
| `NpcDialogue.xml` | 960×720 | ✅ | 40×40 | 全原版 brush |
| `WorldEventInbox.xml` | 1100×680 | ✅ | 40×40 | 全原版 brush |
| `WeeklyReportBrowser.xml` | 1100×680 | ✅ | 40×40 | 全原版 brush |
| `DeveloperCheck.xml` | 1100×680 | ⚙️ 开发工具 | 40×40 | 全原版 brush |
| `SceneDialogueStatus.xml` | 720×55 | ✅ | 无 | 全原版 brush |

⇒ **只做 2 个对话面板的话，另外 4 个面板还是原版灰底，观感会割裂。** 本分类表按 **6 个面板**铺开，但批次上把"玩家可见"排在前面（开发工具排最后）。

---

## 1. 分类总览（6 类）

分类依据＝**功能角色**（同一类的规格、笔画、状态需求一致，可一起画、一起验）。
落盘分类名对齐官方管线的 `ui_` 前缀要求（依据：TaleWorlds《Generating and Loading UI Sprite Sheets》）。

| # | 类 | 落盘分类目录 | 内容 | 数量 | 优先级 |
| --- | --- | --- | --- | --- | --- |
| C1 | **面板底 / 框** | `ui_awake_frame` | 主面板、弹窗、侧栏、输入区、状态条、头像框 | 8 | **P0** |
| C2 | **栏头 / 分隔 / 角饰** | `ui_awake_ornament` | 顶栏带、金分隔线、细金线、四角饰、名石 | 7 | P0（前 2 项）/ P2 |
| C3 | **按钮** | `ui_awake_button` | 5 种尺寸 × 状态（D/H/P/Sel/Ds） | 5 | **P0** |
| C4 | **条目 / 气泡 / 槽** | `ui_awake_item` | 联系人条目、聊天气泡 ×2、通用列表行、内凹槽 | 5 | P1 |
| C5 | **功能图标** | `ui_awake_icon` | 纯白剪影 24 网格 | ~14 | P2（**落点待确认**，见 §7） |
| C6 | **插画 / 氛围** | `ui_awake_art` | 背景氛围、开屏、事件插图 | 待定 | P2（可选，走生成图形态 C） |

**合计（不含 C6）：约 39 个 sprite。**
M0 交 **C1 前 5 项 + C2 前 2 项 + C3 全部（逐状态）**，共 **21 个 sprite**（✅ 2026-09-13 已出齐），
即可让 6 个面板全部换底、两个对话面板完成主要换肤。

---

## 2. 逐类清单

> 单位 px；尺寸一律取 **5 的倍数**（原版习惯 + AWAKE 的 4/12/24 栅格）。
> 「落点」＝ 当前引用它的原版 sprite（自有资产上线后替换它）。

### 2.1 C1 · 面板底 / 框 → `ui_awake_frame`

| ID | 文件名 | 尺寸 | 九宫格 L/T/R/B | 落点（替换） | 批次 |
| --- | --- | --- | --- | --- | --- |
| F-01 | `panel_main_1280.png` | 1280×760 | 32/32/32/32 | 信使主面板（`stone_texture_overlay`） | **M0** |
| F-02 | `panel_main_960.png` | 960×720 | 32/32/32/32 | 对话主面板（`npc_dialogue_panel_9`） | **M0** |
| F-03 | `panel_dialog_1100.png` | 1100×680 | 32/32/32/32 | **3 个弹窗共用**（`StdAssets\Popup\canvas`）※ | **M0** |
| F-04 | `panel_side_300.png` | 300×700 | 24/24/24/24 | 信使左栏条带 | M1 |
| F-05 | `panel_card_260.png` | 260×700 | 24/24/24/24 | 信使右卡条带 | M1 |
| F-06 | `panel_input_800.png` | 800×60 | 16/16/16/16 | 输入区（`name_input_area`） | **M0** |
| F-07 | `panel_status_720.png` | 720×55 | 16/16/16/16 | `SceneDialogueStatus` 状态条 | M1 |
| F-08 | `portrait_frame.png` | 200×200 | 24/24/24/24 | 角色卡头像框（`frame_9`） | P2 |

※ F-03 一处顶三处（`WorldEventInbox` / `WeeklyReportBrowser` / `DeveloperCheck` 实测同为 1100×680）——**性价比最高的一项**。

### 2.2 C2 · 栏头 / 分隔 / 角饰 → `ui_awake_ornament`

| ID | 文件名 | 尺寸 | 九宫格 L/T/R/B | 落点 | 批次 |
| --- | --- | --- | --- | --- | --- |
| O-01 | `header_band.png` | 1280×60 | 24/0/24/0（**上下不拉伸**） | 顶栏带（`character_creation_background_gradient`） | **M0** |
| O-02 | `divider_gold.png` | 800×5 | 0/0/0/0（整条拉伸） | 主分隔（`GradientDivider_9`） | **M0** |
| O-03 | `hairline.png` | 800×3 | 0/0/0/0 | 条目间细金线 | M1 |
| O-04 | `corner_tl.png` | 60×60 | — | 四角饰（左上） | P2 |
| O-05 | `corner_tr.png` | 60×60 | — | 四角饰（右上） | P2 |
| O-06 | `corner_bl.png` | 60×60 | — | 四角饰（左下） | P2 |
| O-07 | `title_namestone.png` | 320×56 | 40/0/40/0 | 顶栏居中刻名石板（风格参考原版 `namestone`，**不可复用**） | P2 |

> O-01 的高度写 60，但**上下不拉伸**是关键：顶栏带一旦上下拉伸就会把上下沿的刻线拉成斜纹。
> O-04~06 若做，建议合并成一张 `corner.png` 用旋转/换向复用，省两个 sprite（待甲方定）。

### 2.3 C3 · 按钮 → `ui_awake_button`（**全部需要 Brush 包装才能切状态**）

| ID | 文件名 | 尺寸 | 九宫格 | 状态 | 落点 | 批次 |
| --- | --- | --- | --- | --- | --- | --- |
| B-01 | `btn_close_40.png` | 40×40 | 12/12/12/12 | D/H/P | **5 个面板共用的关闭键**（`Popup.CloseButton`） | **M0** |
| B-02 | `btn_tab_105.png` | 105×35 | 14/14/10/10 | D/H/P/**Sel** | Tab（信使 3 个 + 对话 2 个） | **M0** |
| B-03 | `btn_primary_110.png` | 110×35 | 16/16/16/16 | D/H/P/**Ds** | 发送（信使 + 对话）、开发工具 110×35 ×4 | **M0** |
| B-04 | `btn_secondary_100.png` | 100×35 | 16/16/16/16 | D/H/P | 写信（信使） | **M0** |
| B-05 | `btn_chip_65.png` | 65×25 | 12/12/8/8 | D/H/P | 小标记键（信使"置顶"） | M1 |

**状态实现方式 ⇒ 已定（2026-09-13，用户拍板）：① 每种状态各出一张图。**

- 理由：用户口径＝「图不嫌少，但嫌劣质」——逐状态出图能把**状态差异画实**（如选中态的金规线、禁用态的脱色），
  而不是靠 Brush 因子把 Default 提亮（提亮会让材质颗粒一起变亮，读起来像蒙了层雾）。
- **命名**：`<按钮名>_<状态>.png`，状态后缀 `_hover` / `_pressed` / `_selected` / `_disabled`。
  例：`btn_close_40.png` / `btn_close_40_hover.png` / `btn_close_40_pressed.png`。
- **代价**：sprite 数 ×3~4（M0 从 10 涨到 21）。**集成时每个状态一条 BrushLayer**，见交接清单 `out/m0-manifest.json`。
- 九宫格边距**四个状态必须一致**，否则按下时内衬会跳。

### 2.4 C4 · 条目 / 气泡 / 槽 → `ui_awake_item`

| ID | 文件名 | 尺寸 | 九宫格 L/T/R/B | 落点 | 批次 |
| --- | --- | --- | --- | --- | --- |
| I-01 | `contact_item.png` | 280×85 | 14/14/14/14 | 联系人条目（`ConversationItemBrush.Simple`） | M1 |
| I-02 | `bubble_npc.png` | 600×72 | 20/20/14/14 | NPC 气泡（左对齐） | M1 |
| I-03 | `bubble_player.png` | 600×72 | 20/20/14/14 | 玩家气泡（右对齐） | M1 |
| I-04 | `list_row.png` | 1000×60 | 12/12/12/12 | 周报 / 收件箱的列表行底 | M1 |
| I-05 | `inset_slot.png` | 400×200 | 16/16/16/16 | 内凹槽（列表容器底） | M1 |

**两条硬约束**：
- 气泡的**高度自适应**（`CoverChildren`）⇒ 九宫格纵向必须能无限延伸不变形，装饰只能放左右边和四角。
- 气泡/条目里**绝对不烤文字**（动态内容 + 中文 fallback）——文字全部由控件渲染。

### 2.5 C5 · 功能图标 → `ui_awake_icon`（**纯白剪影，引擎染色**）

规格：基准网格 **24×24**、笔画 **2–3px**、方形端点；交付 **48×48 与 72×72** 两档；透明底、纯白 `#F8F8F8`、无渐变、无内部细节线。

| ID | 文件名 | 用途 | 落点 |
| --- | --- | --- | --- |
| N-01 | `icon_close.png` | 关闭 | 关闭键 B-01（**备注：也可直接画进 B-01 的图里**，二选一） |
| N-02 | `icon_send.png` | 发送 | 按钮 B-03 |
| N-03 | `icon_write.png` | 写信 | 按钮 B-04 |
| N-04 | `icon_reply.png` | 回复 | 信使 |
| N-05 | `icon_back.png` | 返回 | 历史模式 |
| N-06 | `icon_pin.png` | 置顶/标记 | 按钮 B-05 |
| N-07 | `icon_hint.png` | 提示/说明 | 输入区提示 |
| N-08 | `icon_arrow_l.png` | 左箭头 | 翻页/导航 |
| N-09 | `icon_arrow_r.png` | 右箭头 | 翻页/导航 |
| N-10 | `icon_refresh.png` | 刷新 | 开发工具 |
| N-11 | `icon_settings.png` | 设置 | 开发工具 |
| N-12 | `icon_diagnostics.png` | 诊断 | 开发工具 |
| N-13 | `icon_logs.png` | 日志 | 开发工具 |
| N-14 | `icon_chronicle.png` | 编年史/名册标记 | 世界书相关界面 |

⚠️ **这类现在没有落点**：实测 6 个面板的按钮**全是纯文字**（`Brush="ConversationItemBrush"` + 文字），没有任何 `Sprite=` 图标引用。
⇒ 图标属于**新引入**，需要甲方先决定"哪些按钮要改成图标按钮"，否则画了没处放。**详见 §7。**

### 2.6 C6 · 插画 / 氛围 → `ui_awake_art`（可选）

| ID | 文件名 | 用途 | 形态 |
| --- | --- | --- | --- |
| A-01 | `bg_ambient.png` | 面板背后的氛围底 | C（生成图） |
| A-02 | `splash.png` | 开屏/加载 | C |
| A-03 | `event_illustration.png` | 世界事件插图 | C |

> 这类**不走 sprite 九宫格**，直接当整图用；且**不建议现在做**——先等界面皮肤定稿，氛围图才知道配什么调子。

---

## 3. 覆盖矩阵：6 个面板 × 用到哪些类

| 面板 | C1 底 | C2 装饰 | C3 按钮 | C4 条目 | C5 图标 |
| --- | --- | --- | --- | --- | --- |
| AwakeMessenger 1280×760 | F-01 F-04 F-05 F-06 | O-01 O-02 | B-01 B-02 B-03 B-04 B-05 | I-01 I-02 I-03 | 待定 |
| NpcDialogue 960×720 | F-02 F-06 | O-01 O-02 | B-01 B-02 B-03 | I-02 I-03 | 待定 |
| WorldEventInbox 1100×680 | F-03 | O-02 | B-01 | I-04 I-05 | 待定 |
| WeeklyReportBrowser 1100×680 | F-03 | O-02 | B-01 | I-04 I-05 | 待定 |
| SceneDialogueStatus 720×55 | F-07 | — | — | — | — |
| DeveloperCheck 1100×680 | F-03 | O-02 | B-01 B-03 | I-04 I-05 | 待定 |

**读法**：F-03（弹窗底）和 B-01（关闭键）覆盖最广，是最该先做的两个。

---

## 4. 命名与落盘（对齐官方管线）

```
AWAKE/GUI/SpriteParts/
  Config.xml                      ← 6 个分类全部声明 <AlwaysLoad/>
  ui_awake_frame/                 ← C1
    panel_main_1280.png
    panel_main_960.png
    panel_dialog_1100.png
    ...
  ui_awake_ornament/              ← C2
  ui_awake_button/                ← C3
  ui_awake_item/                  ← C4
  ui_awake_icon/                  ← C5
  ui_awake_art/                   ← C6
```

规则（依据官方文档 + 全库实测）：

| 项 | 规则 |
| --- | --- |
| 分类名 | **必须以 `ui_` 开头**（官方硬要求） |
| 文件名 | 直接取 sprite 名；分类目录**不进** sprite 名（实测旁证：`SimpleBank/GUI/SpriteParts/Bank/...`、`AnimusForge/GUI/SpriteParts/ui_account/discord.png` → sprite 名就是 `discord`） |
| 撞名防护 | 文件名自带类前缀（`panel_` / `btn_` / `icon_` / `bubble_` …），跨分类不重名 |
| 源图 | 一图一 sprite，PNG / RGBA / 透明底 / **不做预乘 alpha** |
| 不改的原版 sprite | **可以直接引用原名**（引擎允许），但**不许把原版资源文件复制进 AWAKE**（红线，见需求书 §2） |
| 图集与索引 | 由生成器产出（`<模块名>SpriteData.xml` + `SpriteSheets/<分类>/<分类>_<ID>.png`），**乙方手写不了坐标** |

**九宫格边距落地位置**：写在 `AWAKE/GUI/Brushes/AwakeBrushes.xml` 的 `<BrushLayer ExtendLeft/Top/Right/Bottom>`，**不是**写在 sprite 上（全库实测：10 个 `*SpriteData.xml` 里 0 处 Extend）。

---

## 5. 批次

| 批次 | 内容 | sprite 数 | 交付后效果 |
| --- | --- | --- | --- |
| **M0** ✅已出 | F-01 F-02 F-03 F-06 **F-07** + O-01 O-02 + B-01×3 B-02×4 B-03×4 B-04×3 | **21** | 6 个面板全部换底；两个对话面板完成主要换肤 |
| **M1** | F-04 F-05 + O-03 + B-05×3 + I-01~I-05 | 12 | 侧栏、联系人、气泡到位 |
| **M2** | F-08 + O-04~O-07 + N-01~N-14（×2 档尺寸） | 21 | 图标与装饰收尾 |

> **M0 的 21 个已在 2026-09-13 出齐**，落盘 `AWAKE/GUI/SpriteParts/`，工具与清单见 `tools/awake-art-lab/`。
> 与本文原计划的差异：① 按钮按状态拆图（10→21）；② `panel_status_720`（F-07）按用户要求提前到 M0；
> ③ C5 图标按用户要求**保留**（原「没有落点」的问题改为：**需要 UI 侧把部分文字按钮改成图标按钮**，见 §7）。

**M0 内部顺序**（先做性价比最高的）：
1. `panel_dialog_1100`（一处顶三个面板）
2. `btn_close_40`（五个面板共用）
3. `panel_main_1280` + `panel_main_960`
4. `panel_input_800`
5. `btn_tab_105` + `btn_primary_110` + `btn_secondary_100`
6. `header_band` + `divider_gold`

---

## 6. 明确不做的

| 不做 | 原因 |
| --- | --- |
| 给原版 sprite 做"复制版"塞进 AWAKE | 红线：不得把原版资源文件复制进模块 |
| 复用 `MarcusAINpcDialogueOverlay.xml` 的任何 sprite/brush | 红线（`docs/UI-ASSET-CATALOG-20260913.md` §2） |
| 在 sprite 里画文字 | 中文 fallback + 多语言会错 |
| 渐变、发光、模糊阴影 | 画风四禁忌（拟物/科技/奇幻/大面积金） |
| 用金色做大面积 | 金只做线与点 |
| 图标自带颜色 | 引擎靠染色，交付必须是白版 |

---

## 7. 待确认（需要甲方或用户拍板）

| # | 问题 | 影响 | 结论 |
| --- | --- | --- | --- |
| 1 | **C5 图标要不要引入？** | 不定则 N-01~N-14 画了没处放 | ✅ **要**（09-13 用户定）。但**落点仍需 UI 侧指定**：现存按钮全是纯文字，得挑出哪几个改成图标按钮（建议先做 关闭/发送/写信/返回 四个） |
| 2 | **按钮状态：出图 ×3 还是 Brush 因子派生？** | 决定 sprite 数量与 Brush 结构 | ✅ **逐状态出图**（09-13 用户定）。命名 `_hover`/`_pressed`/`_selected`/`_disabled` |
| 3 | **弹窗底 F-03 三个面板共用**（1100×680 实测同尺寸）——确认可以共用？ | 共用一个省两个 sprite | ✅ 已按共用出图 |
| 4 | **四角饰 O-04~06 合并成一张复用**，还是分别出？ | 省 2 个 sprite | ⏳ 待定（M2） |
| 5 | `SceneDialogueStatus`（720×55）要做皮肤吗？ | 决定 F-07 做不做 | ✅ **要做**（09-13 用户定），已提前到 M0 |
| 6 | `DeveloperCheck`（开发工具）要不要皮肤？ | 决定它进 M1 还是不做 | ✅ 间接解决：它共用 F-03 弹窗底 + B-01 关闭键，**零额外成本**已覆盖 |
| 7 | **管线阻塞**：本机无 Modding Kit | 决定 M0 能否真机验收 | ⛔ 仍在。**不卡出图，只卡真机验收** |

---

## 8. 复现命令

```bash
cd D:/AWAKE-Dev/AWAKE

# 6 个面板清单与尺寸
ls -la GUI/Prefabs/

# 每个面板引用了哪些原版 sprite / brush
for f in GUI/Prefabs/*.xml; do echo "== $f"; grep -o 'Sprite="[^"]*"' "$f" | /usr/bin/sort -u; grep -o 'Brush="[^"]*"' "$f" | /usr/bin/sort -u; done

# 按钮尺寸与命令
grep -o '<ButtonWidget[^>]*>' GUI/Prefabs/*.xml

# 自有美术现状（应只有 Prefabs）
ls -R GUI/
```
