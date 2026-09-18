# 参考研究：AliceMM's AI World（第三方模组）· 生图功能与布局 UI 逻辑

> 来源：本机 `D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AliceMM-AI-World`
> 版本 `v1.1.0`（SubModule 标注），`bin/Win64_Shipping_Client/AliceMM-AIDiplomacy.dll` 848 KB。
> 时间：2026-09-15。

## 0. 证据分级（务必先读）

| 结论类别 | 证据 | 可信度 |
|---|---|---|
| 它的目录结构 / Prefab 内容 / 文案 / 提示词原文 | 直接读文件 | **实读** |
| 它的接口地址、请求字段、配置项名 | DLL 字符串表（`_extract_dll_strings.py` 抽取，有阳性对照） | **实读**（字符串是字面量，不给上下文） |
| 「引擎行为如何」 | 只在**另行对照官方原版**之后才成立 | 见 §3，那条是**一手** |
| 它「能不能跑 / 跑起来什么样」 | 未运行过（`ModuleData/logs/` 与 `saves/` 都没生成） | **未验** |

⚠️ 本文件里凡是**只凭它**得出的结论，一律标「疑似」。它是**社区样本**，按项目口径不得单独支撑结论。

---

## 1. 生图功能

### 1.1 四个后端地址（DLL 字符串原文）

```
http://127.0.0.1:7860        + /sdapi/v1/txt2img                              → 本机 Stable Diffusion WebUI(A1111)
https://ark.cn-beijing.volces.com/api/v3/images/generations                    → 火山方舟（豆包系）
http://127.0.0.1:4315/v1/image/generate                                        → Player2（本机 AI 网关）
https://api.openai.com/v1                                                      → OpenAI 兼容
```

配套字面量：`Bearer`（鉴权头）、`b64_json`（响应形状）、`No URL or b64_json in response`（它的失败文案）。
另有文本侧：`https://api.deepseek.com/v1`、`https://openrouter.ai/api/v1`、`http://localhost:11434/v1`（Ollama）。

**对照 AWAKE**：我们的 `AwakeImageEndpointResolver` 形状 ID 就叫 `player2` 与 `openai_compatible` —— 与它的 `127.0.0.1:4315/v1/image/generate` 是同一个生态位。**说明 AWAKE 的接口设计方向与这个生态一致，不需要改。**
差异：我们**没有 A1111 `/sdapi/v1/txt2img` 这一形状**；它的这三家里 A1111 是唯一「本机、免费、可离线」的选项。

### 1.2 两段式管线（**它最重要的设计**）

```
游戏数据(Hero/Culture/Traits/Role)
        ↓  文本模型
   「角色卡」6 分类: appearance / body / personality / background / voice / nsfw
        ↓  取 appearance 一段
   「立绘提示词」→ 图像模型
        ↓
   落盘 → 注入到 UI 的某个 Frame 里
```

角色卡生成提示词原文（DLL）：

```
You are the character card generator for Mount & Blade II: Bannerlord. Given a hero's
game data and known facts, generate a character card with 6 categories (appearance,
body, personality, background, voice, nsfw), each containing a single free-form
"description" field. NSFW description must always be empty. Output strict JSON.
```

⇒ **立绘不是直接从游戏数据拼出来的，中间隔了一层"AI 写的角色卡"。** 好处：服装/脸/体型的描述由模型从数据里推出来，提示词质量高；代价：出一张图要**两次模型调用**（文本 + 图像），且角色卡要先缓存。

### 1.3 立绘提示词模板（DLL 原文，两条）

```
A high-quality medieval fantasy character portrait. {appearance}. Name: {name},
{culture} culture, {gender}, {traits}. {role}. Portrait composition, 105:93 aspect
ratio, digital painting style, dramatic lighting, detailed clothing,
no modern elements, no text, no watermark.
```
```
A high-quality medieval fantasy character portrait of {name}, {culture} culture, {gender}. {appearance}
```

可提炼的六段结构（**这才是值得抄的部分**）：

1. **质量前缀** `A high-quality medieval fantasy character portrait`
2. **外貌** `{appearance}` ← 来自角色卡
3. **游戏事实** `Name / culture / gender / traits / role`
4. **构图** `Portrait composition, 105:93 aspect ratio`
5. **画法** `digital painting style, dramatic lighting, detailed clothing`
6. **负向尾巴** `no modern elements, no text, no watermark`

⚠️ **它的构图是「胸像 + 105:93」**（105:93 = 游戏原生头像比，`GenImagePlaceholder` 的注释也写了 "aligned to avatar ratio 105:93"）。
**AWAKE 的画位是 212×360 的全身像**（≈ 0.589，比 105:93 的 1.129 瘦长得多）。
⇒ **不能照抄它的构图词。** 我们的第 4 段应该是 `full body, standing, head to toe, 53:90 aspect ratio` 这一类，且第 6 段的负向尾巴要加"不裁切/不要半身"。

### 1.4 提示词**可由玩家编辑**，并按 NPC 持久化

- `PortraitGenPopup.xml` 里有一个 **170 高的 `MultilineEditableTextWidget`**（`RealText="@PromptEditText" MaxLength="1000" EditorFontSize="17"`）。
- 它有 `ShowPromptEditor` 方法（DLL），空状态文案：`No portrait yet. Edit the prompt and click Generate.`
- 每个 NPC 存 `PortraitPath`（`get_/set_PortraitPath`、`_portraitPath`）。

⇒ **这是甲方「提示词我还没做」那个问题的一种现成答案：把提示词做成玩家可编辑的文本框，预填模板，改完再生成。** 不是必须写死在代码里。

### 1.5 后端各自的尺寸

```
ImageGenPlayer2Width / ImageGenPlayer2Height
ImageGenSDWidth      / ImageGenSDHeight
```
两套分开配。合理解释（**疑似**）：SD 系要求尺寸是 8/64 的倍数，而网关/云端的尺寸是固定枚举。⇒ **一个尺寸值不可能对所有后端都对。**

### 1.6 上屏方式

- 大量 `Id="..."`（全模组 69 处）＋ `RefreshNpcPortraits` / `LoadPortrait` / `SetEmotion` 等方法名。
- 文案：`Portrait added. Frame children={0}`、`LoadPortrait: UIContext null`、`LoadPortrait crash:`、`Portrait error:`
- ⇒ **疑似走「运行时按 Id 找到控件、塞子控件进去」**（典型是 UIExtenderEx 那套）。

**对照 AWAKE**：我们用自定义 `TextureProvider` ＋ `TextureWidget`（`AwakePortraitTextureProvider` / `AwakePortraitImageWidget`），只把「缓存键」递给控件，纹理由 provider 自己加载。**我们的做法更干净**：不改动别人的控件树、不依赖 UI 注入框架、换图只换一个字符串。

### 1.7 情绪变体走静态图，不走生图

`GUI/Images/`（模组自带，约 20 MB）：

```
alice_{情绪}_{编号}.png   情绪 ∈ [neutral][happy][amused][worried][sad][impressed][curious]
                        编号 _0 ~ _19，注释写明「随机抽取」
dealer_portrait.png / diplomacy_cover.png / major_event_cover.png / minor_event_cover.png
```

⇒ **它的"立绘系统"其实是两套并行**：随包附带的静态情绪图（对话中换表情用），和玩家手动触发的 AI 生图（生成后 Apply）。**生图不参与日常对话演出。**

---

## 2. 布局 UI 逻辑

普查 20 个 Prefab / 658 个控件（脚本 `_survey_prefabs.py`）。

### 2.1 面板尺寸

| 面板 | 最大固定面板 |
|---|---|
| `NPCChatWindow.xml`（对话） | **1440×980** |
| `PortraitGenPopup.xml`（立绘生成） | **540×900** |
| 其余（外交/事件/情报） | 多为 1100×680 一档 |

按钮尺寸：`115×36`、`115×48`、`160×30`、`160×48`；头像框 `92×82` / `88×78`。

⚠️ **115 / 125 / 82 / 78 / 92 / 34 / 36 都不是 5 的倍数** ⇒ **它没有栅格纪律**。AWAKE 的 5 栅格比它严。

### 2.2 尺寸策略分布

| 组合 | 次数 |
|---|---|
| `StretchToParent × StretchToParent` | 188 |
| `Fixed × Fixed` | 135 |
| `StretchToParent × Fixed` | 116 |
| `StretchToParent × CoverChildren` | 115 |
| `Fixed × StretchToParent` | 17 |
| `CoverChildren × Fixed` | 12 |
| `CoverChildren × CoverChildren` | 12 |
| **`CoverChildren × StretchToParent`** | **9** |

⇒ 与官方口径一致：`CoverChildren` 父 + `StretchToParent` 子**极少见**（9/658 ≈ 1.4%）。
⇒ 反向印证我们在 `NpcDialogue` 里改用 `DimensionSyncWidget` 的决定（见 `UI-PAGE-NPC-DIALOGUE.md` §17.2）。
⇒ 它**完全没用** `DimensionSyncWidget`（0 处）——它的对齐问题是用别的方式绕的。

### 2.3 字号

`13 14 15 16 17 18 19 20 21 22 23 24 26 28 30 48 52`（＋`@BodyFontSize` 变量）

⇒ **它每个字号都有用例，没有档位概念。** AWAKE 的「6 档（13/15/17/20/24/28）」是**比生态常态更严的自律**，不是跟别人对齐。
⇒ 对我们有用的只有一条：它把覆盖页大标题放到 **48/52**，正文 17–22 —— 与我们的 28（标题）/17（正文）量级一致，说明我们的档位没选偏。

### 2.4 资产与写法

- **自有 sprite：0 张。** 全部用官方资产：`BlankWhiteSquare_9`(57) / `frame_9`(32) / `BlankWhite`(21) / `General\CharacterCreation\character_creation_background_gradient`(12) / `StdAssets\Popup\divider`(11) / `StdAssets\Popup\canvas`(11)。
  ⇒ AWAKE 自建 `GUI/SpriteParts/` + `AwakeBrushes.xml` 的产线，**生态里没人这么干**——这是我们的重投入，也是差异化。但代价是 8 个标准件还欠着。
- 惯用写法：`Widget ... IsEnabled="false"`（让纯装饰层不吃输入）、`Popup.CloseButton` / `Popup.Done.Button`（官方 Brush）。
- 全模组：`Command.Click` 87 · `Id` 69 · `ClipContents` 24 · `AutoHideScrollBars` 20 · `ExtendLeft` 32 · `ItemTemplate` 14 · 多行输入 3 · `ImageIdentifierWidget` 4 · `Scale` **0**。
  ⇒ 它不用 `Scale`（我们也不用），缩放一律靠固定尺寸/策略。

---

## 3. ⚠️ 由这次研究砸出来的 AWAKE 真问题：颜色位序

### 3.1 现象

它的 `Color` 8 位值里两种约定混用：

```
#000000FF × 30   （alpha 在末位）
#AA000000 × 16   （alpha 在首位）
```

### 3.2 一手判定（官方原版，不是猜）

| 统计 | 值 |
|---|---|
| 官方五模块 8 位色中 **以 `FF` 结尾**（alpha 末位） | **3460** |
| 以 `#FF` 开头（alpha 首位） | 713（含 `#FF0000FF` 红、`#FF00FFFF` 品红等**末位也是 FF** 的合法色） |
| 半透明黑 **`#000000XX`**（alpha 末位） | **961** |
| 半透明黑 **`#XX000000`**（alpha 首位） | 47 —— **全部是 `#00000000`，即两种写法都匹配的假阳性 ⇒ 真值 0** |

官方原文样例（`#000000CC` / `#00000040` / `#00000090` / `#000000AA` / `#00000066` / `#00000099` / `#000000DD`）——**若约定是 ARGB，官方会写成 `#CC000000`；它一处都没这么写。**

### 3.3 结论

> **8 位色 = `#RRGGBBAA`，alpha 在末位。零例外。**
> ⇒ AWAKE 的 Lab `color_format` 规则方向正确。
> ⇒ `AliceMM-AI-World` 那 16 处 `#AA000000` 按此约定是「全透明的红」——**疑似它那些全屏遮罩在真机上根本没显示**（未运行，标疑似）。

### 3.4 打到 AWAKE 自己头上

我们的 Prefab 大量写成 `#FFxxxxxx`（**ARGB 习惯**）。按约定解读，后果分两档：

| 写法 | 实际效果 | 性质 |
|---|---|---|
| `#FF000000` + `AlphaFactor` | alpha=0 ⇒ **完全不可见** | **真 bug**（暗底整块消失） |
| `#FFE0C8B0`（文字色） | 变成 (255,224,200) α=176 ⇒ 偏亮 + 半透明 | 错，但看得见 |
| `#FFD700FF` / `#FFFFFFFF` | 末位已是 FF ⇒ **本来就写对了** | 无误 |

**根因**：Lab 的 `color_format` 只查**位宽是否 8 位**，不查**位序**，所以一条都不报。

**已办的处置**：见 §4。

---

## 4. §3 引出的事：AWAKE 自己的颜色位序（处置）

### 4.1 自查结果

全库 8 个 Prefab 里 8 位色共 **58 处**（`GUI/Brushes/AwakeBrushes.xml` 172 行里**一处颜色都没有**，
只有 sprite→brush 映射 ⇒ 颜色问题 100% 在 Prefab 里）：

| 档 | 处数 | 实际后果 |
|---|---|---|
| **A · 真的看不见** | **5** | 首位 `FF` 被当红通道、末字节 = `00/05/06` ⇒ 有效不透明度 ≈ 0~2% |
| B · 错但看得见 | 40 | 偏色 ＋ 半透明，比设计稿淡 2~5 倍 |
| C · 本来就对 | 13 | 末字节已是 `FF`（`#000000FF`×6 / `#FFFFFFFF`×5 / `#FFD700FF`×2） |

**A 档 5 处（都是"要糊一片色"的东西 —— 底、遮罩）**：

| 文件（行号=控件起始行） | 现值 | 设计意图 | 现在渲染成 |
|---|---|---|---|
| `NpcDialogue.xml:72` 左列底 | `#FF000000` α0.16 | 16% 黑 | **0%（全透明）** |
| `NpcDialogue.xml:120` 中列底 | `#FF000000` α0.16 | 16% 黑 | **0%** |
| `NpcDialogue.xml:141` 右列底 | `#FF000000` α0.26 | 26% 黑 | **0%** |
| `AwakePortraitSlot.xml:92` 角标底 | `#FF0B0806` α0.66 | 66% 黑 | **1.6%** |
| `AwakePortraitSlot.xml:106` 生成中遮罩 | `#FF080605` α0.80 | 80% 黑 | **1.6%** |

⇒ **三列的分栏底色一直是隐形的**，中列"生成中"的压暗遮罩也一直是隐形的
（"生成中"三个字看得见，但它下面那层暗底没有）。

**B 档的代表值（同一套算法的换算）**：

| 现值 | 设计位置 | 现在实际 |
|---|---|---|
| `#FF8C6B38` α0.32 | 画位四边细金线 | 7%（设计 32%） |
| `#FFC8A468` α0.12 | 名册行选中底 | 4.9%（设计 12%） |
| `#FFD9A953` α1.0 | 最新一条的 2px 金条 | 33%（设计 100%） |
| `#FFE0C8B0` | 正文/提示字 | 69%（设计不透明） |
| `#FF9C7839` | 小标题字 | 22% |
| `#FF5A5142` | 计数、状态字 | 26% |
| `#FFE2AF54` | 角色名字 | 33% |

### 4.2 修法（机械，一句话）

> **本库一律 `#RRGGBBFF` ＋ `AlphaFactor` 管透明度。**
> 所以 `#FFxxxxxx` → `#xxxxxxFF`；末字节已经是 `FF` 的**原样不动**。

⚠️ **不能无条件"把首位 FF 挪到末位"**：`#FFD700FF` 本来就是对的（`D700FF` 里 `FF` 是金色本身的
红通道），硬挪会得到 `#D700FFFF` 这种错值。判据必须是"**末字节不是 FF 才动手**"。

⚠️ 这条规则**只在 AWAKE 自己的库里成立**。官方 `#FF0000CC` 是合法的"80% 红"
（见 §3.2 那 160 处），拿这条去查官方会大面积误报——所以它进了 Lab 也没法当通用规则用。

逐处清单由 `tools/awake-ui-lab/_probe_color_order_20260915.py` 生成（读 Lab 报告，只列不改）。

### 4.3 影响面与归线

| 归线 | 文件 | 处数 |
|---|---|---|
| UI 编辑层 | `NpcDialogue.xml` | 21（A 3 ＋ B 18） |
| UI 编辑层 | `AwakePortraitSlot.xml` | 10（A 2 ＋ B 8） |
| UI 编辑层 | `AwakeMessenger.xml` | 10 |
| **主干** | **`AwakePortraitProbe.xml`** | **4** |
| —（本来就对，无需动） | `DeveloperCheck.xml` / `WeeklyReportBrowser.xml` / `WorldEventInbox.xml` | 0 |

⇒ 41 处属本线，**4 处属主干 ⇒ 不代改，报上去**。

### 4.4 Lab 已补的检查（本轮已完成）

`preview/preview_prefab_geometry.py` 新增规则 **`color_channel_order`**（warn）。

- **判据**：8 位色末字节 ≠ `FF` ⇒ 报。**不是**"首位是 FF 就报"。
- **为什么不能用"首位是 FF"当判据**：官方 160 处「FF 开头且末字节非 FF」里 **157 处**是
  「R 通道恰好=FF 的纯色」（`#FF0000CC`、`#FFFFFF55` 这种），两种读法都通 ⇒ 拿它当判据会大面积假阳性。
  真孤例只有 3 处（`#FF1111AA`×2、`#FFD98755`×1，读法两可）。
- **顺手补的覆盖缺口**：原代码把 `Color` 检查放在 `is_logic()` 跳过**之后** ⇒
  挂在 `DimensionSyncWidget` 这类"逻辑控件"上的颜色整条漏掉。我们那条最新消息的 2px 金条
  正是这种写法（`NpcDialogue.xml:180`）⇒ 已把颜色检查**提到跳过之前**。
- **变异检验**（`_mutate_color_order_20260915.py`，喂一份故意写坏的小 Prefab）：
  8 个用例 **8/8 符合预期** —— 该报的报（含"逻辑控件上挂色"这条刚补的缺口），
  本来就对的 3 个（`#FFD700FF` / `#8C6B38FF` / `#E0C8B0FF`）**一个都没误报**。
  （第一版断言脚本拿 `FontColor` 当关键词，被另一条命中带成假阳性 ⇒ 关键词改成完整色值字面量。
  **判据自己写错，比规则写错更难发现**。）

**Lab 数字变化**：全库问题 **37 → 92**（新增 55 条）。**这不是新坏掉，是新规则把一直存在的老问题照出来了。**
`NpcDialogue.xml` 从「0 问题」变成 30，同理。E2 用例 16/16、fixture 17/17 仍全过。

⚠️ 两个读数口径（否则会白找）：
1. **Lab 报的行号是控件的起始行**，不是色值所在行——控件写成多行时色值在下面 1~2 行。
2. 被 `<AwakePortraitSlot>` 这种**子 Prefab 展开**进来的问题，会同时算在宿主面板和自己的文件上
   ⇒ 55 条要去重成 45 处真实（其中 1 处挂在逻辑控件上）。

### 4.5 未办（及理由）

1. **45 处颜色一处没改。** 理由：① 它一次性改变**所有面板**的观感（等于换一版亮度/透明度基线），
   该作为**一次单一目的**的改动做，不该夹在"研究一个 mod"里；② 4 处属主干，需其认领；
   ③ 真机视觉现在**验不了**（显存不够、不能边做边开游戏），改完只能给"按设计稿应该对了"，
   给不了"看过了"。**⇒ 已报上去等一句话：改 / 不改。**
2. `AwakePortraitProbe.xml` 那 4 处不动。
3. 未提交（与本轮其余产物一起等提交时机）：本文件、Lab 两个一次性脚本、Lab 规则改动。

