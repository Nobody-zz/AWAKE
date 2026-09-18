# 石与纸：骑砍的材质语言（一手取证）

- 日期：2026-09-14
- 起因：Max「我不觉得骑砍的那种质感是会和中世纪抄本背离的，你把他们两个理解成相斥的风格是为什么呢？我觉得是你的骑砍的世界观不够了解，不懂骑砍的那种氛围」
- 取证源（**一手，官方模块**）：`Modules/Native/GUI/NativeSpriteData.xml`（1.4 MB，官方索引）、`Modules/SandBox/GUI/SandBoxSpriteData.xml`（600 KB）

---

> ## ⛔ 本文结论已作废（2026-09-14 00:20，同源一手数据推翻）
>
> Max 追问「**和石头和纸张有什么关系吗...?**」——**没关系。** 本文的 §1.1、§1.2、§2 是把**文件名**当设计结论。
>
> **全量计数（同一份 `NativeSpriteData.xml`，6353 个 `<Name>`）**：`icon` **1263（19.9%）**／`flag` 84／`texture` **19（0.3%）**／`paper` **1**／`parchment` **0**。
> 真界面底材质只有 3 个（`paper_texture_tile`／`stone_texture_continuous`／`stone_texture_overlay`）；而原版面板底叫 **`flat_panel_texture`**——「平的」。
> ⇒ **骑砍 UI 是图标／纹章系统，不是材质系统。** 本文拿 19 个里的 2 个，编成了"两族材质语言"。
>
> **§1.2「卷册语法」是词形误读**：`scroll` 48 命中里 `scrollbar` 占 **21**，其余大半是 `scroller` 滚动滑块与 `mousescrollup|down`（鼠标滚轮输入键）。真卷轴语义只有 **3** 个。`StdAssets` 实际骨架＝`button_*`／`arrow_*`／`banner_*`／`ItemIcons`(33)／`Popup`(33)／`FactionIcons`(13)。
>
> **§4 修正前提里的「壳＝石／件＝纸」不再作为设计依据**。唯一仍站得住的发现见文末 §5。
>
> **教训**：索引只能回答「有什么」，永远回答不了「看着什么味」。这是第二次拿文件名当证据（前一次：`Extend`→`<NineRegionSprite>`）。

---

## 0. 这份文档纠正什么

我上轮把「骑砍质感」和「中世纪抄本」判成了**相斥的风格**，据此向 Max 抛了一个"口径二选一"的问题。**那个判断是错的**，而且错的方式很外行：我把两者都当成了**表面（明度／材质）**，然后拿"底暗 vs 底亮"去比。

**骑砍原版的 UI 材质语言本来就是纸和石并用。** 下面全部是索引里的实名取证。

---

## 1. 取证

### 1.1 原版自己的贴图，只有两族：**纸** 和 **石**

索引里名字带 `texture` 的 sprite，全量如下：

| sprite | 是什么 |
| --- | --- |
| **`paper_texture_tile`** | **可平铺的纸纹** |
| **`stone_texture_overlay`** / **`stone_texture_continuous`** | **石纹（叠加／连续两版）** |
| `flat_panel_texture` / `popup_canvas_texture` / `gradient_texture` | 面板／弹窗／渐变底 |
| `General\InitialMenu\main_menu_texture` / `MPGeneral\MPScoreboard\result_texture` | 主菜单／记分板专用 |
| **`ui_textures`** | 一个**独立分类**（且是官方 4 个 `AlwaysLoad` 分类之一） |

⇒ **纸和石在原版里是并列的两种材质，不是对立的两套风格。** 我上轮只做了一族（石），还把石做成了现代近黑。

### 1.2 原版通用 UI 套件（`StdAssets`）的骨架，就是「卷册／页」

从索引里捞出的实名（均为官方）：

| sprite | 说明 |
| --- | --- |
| `StdAssets\scroll_header` / `scroll_hide` | **卷册的起与收** |
| `StdAssets\page_button_left` / `page_button_center`（各带 `_selected`） | **页码按钮**（翻页） |
| `StdAssets\subpage_divider` | **子页分隔** |
| `StdAssets\game_menu_hinges` | **装帧铰链**——书脊上的金属件 |
| `StdAssets\diamond_seperator` | **菱形分隔**——抄本边饰里最常见的 lozenge 母题 |
| `StdAssets\history_button`（`_selected`） | **历史按钮** |
| `StdAssets\panel_dent` / `slick_frame` / `frame_small` / `standart_popup` | 面板、凹痕、框 |
| `StdAssets\tabbar_long` / `tabbar_standart` / `tabbar_popup` / `tabbar_long_namebox` | 页签组 |

⇒ 原版的通用件是**「卷册 + 页 + 子页 + 装帧金属件 + 菱形分隔」**。
**这不是"抄本的邻居"，这就是抄本的语法。**

### 1.3 游戏内「百科」，就是我们产品的同构物

`Encyclopedia\` 分类实名（节选）：

`banner`／`hero`／`hero_silhouette`／`settlements`／`kingdom`／`clan`／`units`／`concept`／`canvas`／`navbar`／`icon_search`／`search_bar`／`search_bar_expansion`／`list_divider`／`list_open_divider`／`list_closed_divider`／`list_filters_divider`／`subpage_slick_frame`／`subpage_ball`／`star_outline`／`star_without_glow`／`troop_tree_side`／`troop_tree_straight`

⇒ 原版百科 = **左侧分类列表 + 搜索 + 右侧子页内容 + 分隔线 + 星标**。
AWAKE 世界书（同一对象 × 六身份）在结构上**就是它的重写**，不是外来物种。

### 1.4 原版有自己的功能图标

`StdAssets\ItemIcons\` 实名：`Axe`／`Sword`／`Bow`／`Crossbow`／`Spear`／`Javelin`／`Mace`／`Shield`／`Banner`／`Ammo`／`Stone`／`Mount`／`horse`／`war_horse`／`ThrowingAxe`／`ThrowingKnife`／`pickup_hand`／`None`——**每个都带 `_selected` 变体**。

⇒ **纯白剪影 + 选中变体** 是原版自己就有的口径，不是我的发明。

### 1.5 官方本来就给这些界面做过皮肤

`ui_conversation`／`ui_barter`／`ui_encyclopedia`／`ui_order_of_battle`／`ui_bannerbuilder`／`ui_bannericons`／`ui_facegen`／`ui_options`／`ui_credits`／`ui_textures`／`ui_fullbackgrounds`／`ui_fullscreens`／`ui_loading`／`ui_group1`／`ui_launcher` …

⇒ **对话、交易、百科、战阵** 官方全做过独立分类。我们要做的事，原版有先例。

---

## 2. 结论：不是相斥，是同一件事的两面

**石是屋子，纸是信。**

你收到一封信：信是羊皮（亮、暖、有纤维、有折痕、盖着火漆），屋子是石砌的（暗、冷、粗凿）。**羊皮之所以显得亮，正因为屋子是暗的。**

⇒ 明度差不是"风格冲突"，那是**光**。
⇒ 骑砍原版同时备着 `paper_texture_tile` 和 `stone_texture_overlay`，正是因为它清楚：**这两样东西在同一个世界里。**

**我上轮的错误**：把"光"读成了"风格"，然后拿两套表面去比明度。

---

## 3. 我上轮那张图，重判

它的问题不只是"不够中世纪"——**它连骑砍也不像**：

| 我做的 | 原版 |
| --- | --- |
| 底＝现代近黑 `#14100C` | 石＝暖灰、有低频颗粒（`stone_texture_overlay`） |
| **全版没有纸** | 纸石**并用**（`paper_texture_tile`） |
| 版式＝瑞士平面（mono 大字距／套准角标／比例尺） | 版式＝**卷册**（`scroll_header`／`page_button`／`subpage_divider`／`diamond_seperator`） |
| 齿轮／放大镜／圆环 i | 原版图标母题是**世界里真有的东西**（剑、盾、旗、鞍） |
| 只有一族材质，还是错的那一族 | 两族并用 |

**「禁齿轮」不是随便写的——游戏里没有齿轮。** 这条禁忌的实质是：**母题必须是卡拉迪亚真实存在之物。**

---

## 4. 修正后的前提（待 Max 拍板）

1. **壳＝石**：用**原版口径**的石（暖灰、低频、可平铺），不是现代近黑。
2. **内容件＝纸**：羊皮亮底 ＋ 铁胆墨 ＋ 朱砂，接原版 `paper_texture_tile` 那一族。
3. **版式＝卷册语法**：页／子页／菱形分隔／装帧件——原版现成，直接接，不要另发明一套。
4. **图标＝纯白剪影 + 选中变体**（合原版 `ItemIcons` 口径），但**母题换成卡拉迪亚真有之物**：印、钥、卷、旗、缰、灯、秤、鞍、盔、弓。
5. **光**：暗石底上的羊皮是**被照亮的一块**，不是"浅色贴纸"。边界的处理要按光来，不按描边来。

---

## 5. 唯一站得住的发现（作废后保留）

**原版已经有「以什么身份看它」的标记语法**——这是**属性标记**学，不是装饰：

| 族 | 实名 | 说的是什么 |
| --- | --- | --- |
| `SPGeneral\GeneralFlagIcons\` | `civillian`／`female_only`／`male_only`／`stealth`／`unique` | **看的人是谁／这东西对谁可见** |
| `SPGeneral\WeaponFlagIcons\` | `billhook`／`bonus_against_shield`／… | 武器属性 |
| `SPGeneral\MountFlagIcons\` | `slaughterable`／`speed_mount`／`weight_carrying_mount` | 坐骑属性 |
| `SPGeneral\GoodsFlagIcons\` | `consumable`／… | 货物属性 |

⇒ **「同一对象 × 六身份」在内核上的对应物，原版给的是 `FlagIcon`，不是抄本边饰。** 世界书面板若要表达"村民听到什么／商人知道什么／贵族看到什么"，该接的是**这套标记语汇**。

另：顶层命名空间占比 —— `Order`（军事指令）**262**、`MPPlayerBadges`（玩家徽章）**306**、`FaceGen` 438、`General` 1188、`SPGeneral` 1016。⇒ 原版 UI 最大的语义族是**指令／徽章／人像**，不是材质。

**待办**：判断"像不像骑砍"必须**打开游戏看屏幕**（官方 UI 全在 `.tpac` 内，磁盘上 0 张 UI PNG，无法旁窥）。**在此之前不出图。**
