# Gauntlet UI · 发现全集

> 由 `AWAKE/tools/gen_skill_refs.py` 从既有 skill 的发现提取生成。**不要手改**——改了会被下次重跑覆盖。
>
> **这是跨 agent 的共享知识**（见 `AWAKE/AGENTS.md`〈跨 agent 共享知识〉）。任何 harness 的 agent 都应读这里，不要各自维护私有副本。
>
> 共 **432** 条发现，其中 **150** 条标了 `needs_reverify`（测量于旧版本，引用前复核）。

## 头部规则

1. **`valid_for` 是测量版本，不是当前版本。** 本机游戏已从 v1.3.15 升到 **v1.4.8**；凡 `valid_for=v1.3.15` 的，引用前先复核。
2. **`未标版本` 的条目 = 提取时未记录版本**，请**按 v1.3.15 对待**（即同样需要复核）。
3. **否定式断言（`negative-claim`）风险最高**——最容易因升级变成假话。先看文末〈待重验队列〉。
4. **`出处` 只到「旧 skill 名 # 小节名」一级。** 旧 skill 在各 harness 的私有目录里（`~/.workbuddy/skills` 等），**不在本仓库**，故不保留行号——留着是假的可点性。
5. 本文件是**世界里的事实**，不是程序。怎么做任务看对应 skill 或 `AWAKE/AGENTS.md`。

## 按 kind 索引

| kind | 条数 | 待重验 |
|---|---|---|
| `engine-behavior` | 141 | 34 |
| `byte-layout` | 8 | 3 |
| `measured-number` | 95 | 14 |
| `silent-failure` | 54 | 28 |
| `api-quirk` | 44 | 9 |
| `negative-claim` | 56 | 55 |
| `version-fact` | 2 | 2 |
| `path-fact` | 32 | 5 |
| **合计** | **432** | **150** |

---

# 引擎行为（engine-behavior）

> 共 141 条。`valid_for` 是这批事实**被测量时**的版本——引用前按当前版本复核。

| id | 事实 | 测量版本 | 待重验 | 出处 |
|---|---|---|---|---|
| F002 | StackLayout.MeasureLinear 对栈里 StretchToParent 的孩子给的是 measureSpec − 定值孩子之和，且只在 IsVisible 为真的孩子之间分余量。 | 未标版本 |  | bannerlord-gauntlet-ui-prefab-layout §2.5 |
| F003 | 栈内余量等分比例由 WidthStretchRatio 决定，其默认值为 1；竖向对应 HeightStretchRatio。 | 未标版本 |  | bannerlord-gauntlet-ui-prefab-layout §2.5 |
| F004 | 引擎在 StretchToParent 下不读元素自己的 SuggestedWidth / SuggestedHeight，该值此时只作设计档记录。 | 未标版本 | 是 | bannerlord-gauntlet-ui-prefab-layout §2.5 |
| F005 | 离线工作台 AWAKE UI Lab 不实现栈里吸余量：preview_prefab_geometry.py 里 pw_ = 父宽 − 边距，不减去兄弟的宽。 | 未标版本（本机工作台） | 是 | bannerlord-gauntlet-ui-prefab-layout §2.5 |
| F007 | GridWidget 配 LayoutImp=GridLayout 与 ColumnCount、DefaultCellWidth 即可折行，静态 Children 够用，无需 DataSource。 | 未标版本 |  | bannerlord-gauntlet-ui-prefab-layout §2.6 |
| F008 | GridLayout 折行位置按 row = i / ColumnCount、column = i % ColumnCount 计算，GridDirection.RowFirst 是默认值。 | 未标版本 |  | bannerlord-gauntlet-ui-prefab-layout §2.6 |
| F009 | GridWidget 在 CoverChildren 下格宽等于 DefaultCellWidth（走 GridLayout.UpdateCellSizes 那一支），不是按列数等分父宽。 | 未标版本 |  | bannerlord-gauntlet-ui-prefab-layout §2.6 |
| F017 | 只有滚动容器上有 MarginRight=15 的滚动条让位，列头那一行没有这个让位。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §1 |
| F019 | 页区盒子不定高时，换到内容矮的一页底栏会往上飘。 | 未标版本 |  | bannerlord-gauntlet-ui-prefab-layout §1② |
| F026 | 页区是 VerticalBottomToTop 且内容贴底排，所以首块 y 不等于页区盒顶：盒顶 55、首块 65。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §6.3 |
| F055 | 绑定只有 @属性 与 DataSource="{属性}" 两种合法形状；非 DataSource 键上的花括号是非法写法，引擎静默作废。 | 未标版本 |  | bannerlord-gauntlet-ui-prefab-layout §9.2 |
| F058 | 滚动条嵌进 ScrollablePanel 里面会让 FindChild 返回 null，两个滚动条都是 null，OnMouseScroll 一个分支都不进，表现为滚轮没反应、拖条也不动。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §10 |
| F065 | 表体是页区里那个 StretchToParent 的吸收者，所以页区是硬账时，加任何一行都直接从表体里扣、可见行数跟着掉。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §6.4b |
| f008 | 生成器不强制 ui_ 前缀：社区 SimpleBank 分类叫 Bank、AnimusForge 用 af_* 也照收。 | 未标版本 |  | bannerlord-ui-sprite-assets §1 L67 |
| f009 | sprite 名不含分类目录名，分类目录之后的子目录才进名字，且用反斜杠分隔（ConceptArts\ConceptArt_0）。 | 未标版本 |  | bannerlord-ui-sprite-assets §1 |
| f010 | 官方 sprite 名子目录可多层嵌套：SPPerks\TacticsBait、Crafting\WeaponTypes\Dagger。 | 未标版本 |  | bannerlord-ui-sprite-assets §1 L70 |
| f011 | SpriteData.xml 顶层只有三段：<SpriteCategories> / <SpriteParts> / <Sprites>。 | 未标版本 |  | bannerlord-ui-sprite-assets §1 L71 / §10 L605 |
| f012 | sprite 分类默认不加载，需 C# category.Load(...) 或 Config.xml 里 <AlwaysLoad/> 才加载。 | 未标版本 |  | bannerlord-ui-sprite-assets §1 |
| f019 | 未解矛盾：AWAKE 仓库那份 Config.xml 三条杀手全中，产出却带着 AlwaysLoad x3。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §1 |
| f024 | 分类级能调的只有 AlwaysLoad / PackAllSpritesToUniqueTextures / SingleChannel / EdgeSize 四项。 | 未标版本 |  | bannerlord-ui-sprite-assets §1 |
| f028 | SpriteParts/<分类>/ 里有几张 PNG 就登记几张，没有清单可以勾选取舍。 | 未标版本 |  | bannerlord-ui-sprite-assets §1 L135 |
| f029 | 想把源图排除出图集，挪进子目录不算——子目录名会被并进 sprite 名。 | 未标版本 |  | bannerlord-ui-sprite-assets §1 |
| f032 | Brush 层九宫格写作 <BrushLayer Sprite="…" ExtendLeft/Top/Right/Bottom="n"/>，单位是 sprite 像素，四个全写才成立。 | 未标版本 |  | bannerlord-ui-sprite-assets §2 |
| f035 | sprite 层九宫格靠 PNG 旁同名 .xml sidecar（<NineRegionSprite>），生成器额外产出 <X>_9 的新 sprite 名。 | 未标版本 |  | bannerlord-ui-sprite-assets §2 |
| f037 | _9 后缀是约定不是规则，真正生效的是 sidecar 里 <Name> 写的名字；官方 4 个都是原名_9。 | 未标版本 |  | bannerlord-ui-sprite-assets §2 L192 |
| f039 | 九宫格出图要求：四角装饰像素范围 ≤ 对应边距值，四边中段可单向拉伸、中心可双向拉伸。 | 未标版本 |  | bannerlord-ui-sprite-assets §2 L195 |
| f042 | Extend 单位是 sprite 像素，四角按原尺寸画、只有中段被拉；预览时不能拿"出图倍率"乘 Extend。 | 未标版本 |  | bannerlord-ui-sprite-assets §2 |
| f046 | 游戏只扫模块根下的一层 GUI/，GUI/GUI/ 是残留副本、不生效。 | 未标版本 |  | bannerlord-ui-sprite-assets §4 L234 |
| f051 | 图集容量判据：max(SheetX + Width) + 8(间隙) + 新件宽 ≤ SpriteSheetSize 的 Width，高同理。 | 未标版本 |  | bannerlord-ui-sprite-assets §4 L237 |
| f054 | Native/GUI/Fonts/NativeLanguages.xml 里简体中文把所有字体映射到 simkai，层级只能靠字号/颜色/装饰线。 | 未标版本 |  | bannerlord-ui-sprite-assets §4 L239 |
| f055 | 功能图标是纯白剪影、引擎靠染色；交付必须白版、无渐变、无描边线。 | 未标版本 |  | bannerlord-ui-sprite-assets §4 L240 |
| f057 | 出图与预览都不需要 Modding Kit，只有产 _tex.tpac 与真机显示需要它。 | 未标版本 |  | bannerlord-ui-sprite-assets §7 L304 |
| f078 | 游戏当前分支取自 appmanifest_<261550>.acf 的 UserConfig/BetaKey，Modding Kit 的 BETAS 必须选同一分支，否则崩。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §7 L317 |
| f080 | 生成器不带参数运行会遍历所有已安装模块的 GUI/SpriteParts/，并改写别人的产物。 | 未标版本 |  | bannerlord-ui-sprite-assets §8 L354 |
| f082 | 生成器只写 SpriteData.xml 与图集 PNG，不动 tpac、不动二进制。 | 未标版本 |  | bannerlord-ui-sprite-assets §8 L395 |
| f083 | 源图 SpriteParts/ 不动、打包是确定性的；真正会被改坏的是 SpriteData.xml（重跑会洗掉没随分发的 AlwaysLoad，SimpleBank 即此）。 | 未标版本 |  | bannerlord-ui-sprite-assets §8 L397 |
| f084 | 沙箱里生成器的工作目录必须是 bin/Win64_Shipping_wEditor（从当前目录往上两级接 Modules/），在沙箱根跑会 DirectoryNotFoundException。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §8 |
| f092 | 槽位之外成片不透明像素是 <EdgeSize> 的边缘外扩（把源图四条边的像素向外复制，供双线性过滤不渗色）。 | 未标版本 |  | bannerlord-ui-sprite-assets §8 |
| f097 | 纹理命名规则是 <分类名>_<序号>：SimpleBankSpriteData.xml 声明 <Name>Bank</Name>，pack0.tpac 里条目就叫 Bank_1。 | 未标版本 |  | bannerlord-ui-sprite-assets §9.1 |
| f103 | AssetSources/ 是「源」不是运行时落点；包记录源路径，像素在包里（DXT5）。 | 未标版本 |  | bannerlord-ui-sprite-assets §9.1 L432 |
| f108 | TaleWorlds.TwoDimension.dll（Client/运行时，97 KB）第 4375 行拼接 SpriteSheets\Name\Name_i 调 LoadTexture。 | 未标版本 |  | bannerlord-ui-sprite-assets §9.9 |
| f109 | 同字面量也出现在生成器侧 SpriteSheetGenerator.Library.dll 第 149 行，前缀是 _resourcesPath + SpriteSheets\。 | 未标版本 |  | bannerlord-ui-sprite-assets §9.9 |
| f111 | LoadTexture 只取 name.Split('\\')[^1] 并剥掉扩展名，再 Texture.GetFromResource("ui_leverage_1")。 | 未标版本 |  | bannerlord-ui-sprite-assets §9.9 |
| f112 | 运行时只认纹理名、不认图集路径；名字必须已在 native 纹理库里，库由 AssetPackages/*.tpac 填充。 | 未标版本 |  | bannerlord-ui-sprite-assets §9.9 |
| f118 | AnimusForgeHoukai 在地图栏插图标：MapBar.Left.Button.Backgrounds#MapBar.Left.Icons!houkai_dashboard。 | 未标版本 |  | bannerlord-ui-sprite-assets §9.9 |
| f119 | 该帧的图不走图集，而是运行时用 TextureProvider + TextureWidget + GoddessEmblemImage + goddess_emblem.png 喂进去。 | 未标版本 |  | bannerlord-ui-sprite-assets §9.9 |
| f120 | 它自建注册流程：goddess_emblem_injected / _provider_set_failed / _texture_missing / _file_missing。 | 未标版本 |  | bannerlord-ui-sprite-assets §9.9 |
| f121 | AnimusForgeHoukai 只有 GUI/Images/goddess_emblem.png 一张散图，无 AssetPackages、无 Assets、无 tpac，照样显示。 | 未标版本 |  | bannerlord-ui-sprite-assets §9.9 |
| f123 | 「必须 Modding Kit」只对图集这一条路成立，换运行时纹理就绕过去了。 | 未标版本 |  | bannerlord-ui-sprite-assets §9.9 |
| f129 | AssetPackages/ 是模块级资产注册处；BannerFix 等 4 个模组放 Don't delete it..txt 占位。 | 未标版本 |  | bannerlord-ui-sprite-assets §9.5 L473 |
| f131 | RuntimeDataCache/*.rdc 是编辑器跑过一次留下的缓存，可作「该模块被编辑器打开过」的判据。 | 未标版本 |  | bannerlord-ui-sprite-assets §9.5 L475 |
| f133 | 5 个官方模块的 SubModule.xml 写 <ModuleType value ="Official">（= 前有空格），StoryMode 写 value="Official"。 | 未标版本 |  | bannerlord-ui-sprite-assets §10 |
| f149 | 无缝场做法是整数频率正弦叠加，天然在 n×n 上周期，无需羽化接缝。 | 未标版本 |  | bannerlord-ui-sprite-assets §12 L661 |
| brush-01 | Brush 自动加载：BrushFactory.LoadBrushes 经 ResourceDepot.GetFiles 扫每个模块 GUI/Brushes/*.xml 全量加载，无需声明。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §1 Brush 文件是自动加载的 |
| brush-04 | RefreshState 状态优先级：Disabled > Selected(true 时) > Pressed > Hovered > Selected(false 时) > Default。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §2「谁决定按钮落在哪个状态」 |
| brush-05 | ButtonWidget.DominantSelectedState 默认值为 true，故选中态压得住 Hovered 与 Pressed。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §2（第 70-71 行） |
| brush-06 | HandleClick 先跑完所有 ClickEventHandlers（VM 命令），之后才按 IsToggle 翻转 / 按 IsRadio 强制置真 IsSelected。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §2（第 74-81 行） |
| brush-07 | ButtonType 缺省为 Push，此时引擎完全不碰 IsSelected；Toggle 翻转、Radio 强制置真并调 OnChildSelected 让兄弟取消。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §2（第 84-89 行） |
| brush-08 | IsSelected 可由外部写入：原版 CircleActionSelectorWidget.cs:307 直接写它。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §2（第 86 行） |
| brush-09 | IsEnabled=false 不只是变灰：EventManager.CollectEnableWidgetsAt 见 !IsEnabled 直接 return，该控件不进鼠标命中表。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §2（第 91-93 行） |
| brush-10 | Brush 的 <Style> 是部分覆盖：LoadStyleInto 取 style.GetLayer(name) 后由 LoadBrushLayerInto 对已有层增量赋值。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §3 <Style> 是部分覆盖 |
| brush-11 | 同一 Brush 四个状态的九宫格边距必须逐字节一致，否则状态切换时内衬会跳。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §3（第 101-102 行） |
| brush-12 | ExtendLeft/Top/Right/Bottom 四个全写才是九宫格，缺一个退回整体拉伸；单位为 sprite 像素。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4（第 108-109 行） |
| brush-15 | 老式 brush 只能靠贴图注册表拿九宫格：Native/GUI/Brushes/Brush.xml:2-6 的 BrushLayer 上一个 Extend* 都没有，而官方拿它当按钮底板。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4（第 123-125 行） |
| brush-21 | Widget 基类没有任何旋转/变换成员，旋转只在 BrushLayer 上；只查 Widget 会误判为界面不能旋转。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4.5（第 132-135 行） |
| brush-27 | AlphaFactor 是叠乘，与 Color 的逐通道相乘语义不同。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §6（第 183 行） |
| brush-30 | 预览合成缓存按「文件名+尺寸」为键；换图后 mtime/size 未变则预览不变。 | 未标版本 | 是 | bannerlord-gauntlet-ui-brushes §7B（第 198-199 行） |
| brush-33 | ButtonBrush 用贴图 button_canvas_9，九宫格写在贴图注册表（BrushLayer 上无 Extend*）。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4.7 表（第 161 行） |
| brush-39 | brush 名在引擎里是全局命名空间，所有模块共享，改原版同名 brush 会污染别的面板。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §5（第 173-176 行） |
| vm-01 | 改 VM 字段界面不会动，必须在 setter 里调 OnPropertyChangedWithValue(值, nameof(Foo))。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §1（第 17-19 行） |
| vm-03 | DropdownWidget 换 DataSource 时会把 CurrentSelectedIndex 经 ListPanelValue 当场回写进 VM 的 BasisIndex。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §2 链条第 2 步 |
| vm-09 | 控件的 OnListItemAdded/Removed 只置 dirty 标记，真正刷新发生在 OnUpdate（下一帧），赋值完不会立刻一致。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §3（第 97-98 行） |
| vm-12 | EditableTextWidget 的 Text setter 会发通知，回发则把光标弹到行尾；原版 MultiSelectionQueryPopUpVM.SearchText 也这么做。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §5（第 113-116 行） |
| vm-13 | 自做页签用 VM 上三个人工只读属性（=> _page == 0 等）并在切页时三个一起通知，不需要原版 TabControl。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §6（第 120-123 行） |
| vm-14 | 点亮当前页应走原版 ButtonBrush2 自带的 Selected 档加 IsSelected，不要自造刷子。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §6（第 124 行） |
| vm-15 | 多页直接排在竖直流里时外面必须套定高页区盒子，否则换到矮的一页底栏会往上飘。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §6（第 125 行） |
| vm-16 | 验证日志判据需含行数/池子大小（rows=53/53、pool=53/53）等字段；同一时刻两条且第二条数量翻倍即为重入。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §7（第 129-133 行） |
| vm-21 | WidgetAttributeValueTypeBinding.CheckValueType 以 value.StartsWith("@") 判为 Binding 型。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §9（第 161 行） |
| vm-22 | WidgetAttributeValueTypeBindingPath.CheckValueType 要求首字符 { 且末字符 } 才算 BindingPath 型。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §9（第 162 行） |
| vm-23 | Data 层只有一处建立绑定（:1280）gauntletView.BindData(key2, new BindingPath(value5))，只有 @ 进 BindData。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §9（第 163-164 行） |
| vm-31 | Widget.FindChild（GauntletUI :22686）只有 .. 走 ParentWidget，其余只按 Id 在直接子控件里找，找不到 return null。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §10 链条（第 188-189 行） |
| vm-33 | UpdateScrollablePanel（:19239）把竖直逻辑挂在 VerticalScrollbar != null 内；两滚动条皆 null 时 OnMouseScroll（:19103）无路走。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §10 链条（第 191-194 行） |
| F005 | NavigationScopeTargeter 的 ScopeParent 是相对路径：..\ 指上一级容器，..\. 指自己这一级。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §2 |
| F010 | EditableTextWidget.HandleInput 在 Input.IsKeyReleased(Enter) 时触发 EventFired('TextEntered')。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §4 文本框 |
| F015 | new GauntletLayer(name, localOrder, isFocusable) 的第二参 localOrder 即该层的 InputRestrictions.Order。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §5.1 层序 |
| F016 | ScreenLayer.CompareTo 只比 InputRestrictions.Order，仅同序时才用 Id 兜底。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §5.1 |
| F020 | ScreenManager.TrySetFocus(layer) 抢焦点时不改动该层的 IsFocusLayer。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §5.2 焦点归还 |
| F021 | TryLoseFocus 从 SortedLayers 顶端向下找第一个 IsActive && IsFocusLayer 的层并把焦点交回，找不到则置 null。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §5.2 |
| F022 | 原版弹窗关闭顺序是 SetSuspendLayer(true) → IsFocusLayer=false → TryLoseFocus，故你的层需在此之前已是 IsFocusLayer。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §5.2 |
| F023 | InputContext.IsKeyPressed 先过 CanUse，Escape 这类普通键落在大 switch 里 ⇒ return IsKeysAllowed。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §5.3 按键按层认领 |
| F024 | IsKeysAllowed 由 ScreenLayer.ProcessEvents 从 _usedInputs 取，Key 位只在 FocusedLayer == 该层时置上。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §5.3 |
| F030 | OnShowInquiry 的订阅方是 TaleWorlds.MountAndBlade.GauntletUI.dll 的 GauntletQueryManager : GlobalLayer。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §5.4 |
| F030b | GauntletQueryManager 由 GauntletUISubModule.OnBeforeInitialModuleScreenSetAsRoot() 在主菜单前建一次，整个会话存活。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §5.4 |
| F032 | 弹窗按钮左右顺序固定：negativeText（算了）在左、affirmativeText（确认）在右。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §5.4 |
| F033 | 在按钮回调里直接调 ShowInquiry 是安全的：原版 KingdomManagementVM.ExecuteKingdomAction（退位）与 OnGrantFief 都这么做。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §5.4 |
| F034 | ShowInquiry 队列语义：_activeDataSource != null 时入队不显示，关掉上一个才轮到。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §5.4 |
| F037 | 弹窗根节点 StretchToParent ⇒ HitTest 走 EventManager.AnyWidgetsAt（任一点命中即真）⇒ 全屏为真，鼠标键被独占，框外不漏点。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §5.4 |
| F039 | 上述三属性由模组自定义 Widget 子类提供，如 AnimusForge 的 AnimusForgeNativeConversationEditableTextWidget 等。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §6、§7 本机先例 |
| F044 | Color 是逐通道相乘的染色而非覆盖；AlphaFactor 是叠在它之上的乘数。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §6 |
| F048 | 原版 IsVisible 取值全部是正向引用（@IsVisible / @HasFormation / @IsSelected / @CanAdvance / @IsActive）。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §9 |
| F049 | 计算属性（=> 表达式体）不会自动刷新，须在每个变更点调 OnPropertyChangedWithValue。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §9 |
| F053 | 官方 OnlineImageTextureProvider 走下载字节 → 存 .png → 转纹理，是运行时纹理的官方样板。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §0 一句话原则 |
| F055 | TextureWidget.OnRender 关键行是 simpleMaterial.Texture = Texture。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §1 链路 |
| F055b | TextureWidget 的 NinePatchParameters = SpriteNinePatchParameters.Empty ⇒ 运行时纹理不走九宫格。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §1 |
| F057 | Texture.CreateFromMemory(byte[]) 吃的是编码好的图片字节，不是裸像素。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §6 未验证 |
| F058 | 官方 AvatarThumbnailCache 用 ImageType 分流：0 走 CreateFromMemory(bytes)，1 走 CreateFromByteArray。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §6 |
| F062 | 造纹理再 GetPixelData 读回，若两方向用同一格式则往返恒等，验不出任何东西（阴性对照问题）。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §6 |
| F068 | RefreshProviderTypes() 扫 AppDomain.CurrentDomain.GetAssemblies()，无程序集过滤。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §4 注册机制 |
| F068b | RefreshProviderTypes 按 type.Name 简单名收录所有非抽象 TextureProvider 子类。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §4 |
| F069 | Module.LoadSubModules 是两段式：第一段遍历所有模块把 DLL 全 AssemblyLoader.LoadFrom，整个循环结束后第二段才逐个 OnSubModuleLoad()。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §4 |
| F070 | GauntletUISubModule.OnSubModuleLoad() → RefreshResources(true) → WidgetInfo.Refresh()。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §4 |
| F071 | 刷新发生时自定义程序集必已在 AppDomain ⇒ provider / widget 注册不需要任何额外动作。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §4 |
| F073 | provider 撞名时 _textureProvidertypes.Add(type.Name, type) 抛 ArgumentException。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §4 |
| F074 | widget 撞名走索引覆盖，谁覆盖谁由 assemblyOrder 里 DLL 先后决定（WidgetFactory.cs:63-69）。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §4 |
| F075 | RefreshProviderTypes 整个方法无 try/catch，且 _textureProvidertypes.Clear() 在扫描循环之前就已执行。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §4 |
| F077 | provider 撞名是塌方（后续程序集全不扫），widget 撞名只是索引覆盖。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §4 |
| F078 | WidgetFactory.CreateBuiltinWidget 用 GetConstructor(…, new Type[1]{typeof(UIContext)}, …) 取构造函数。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §3 Widget 与 Prefab |
| F080 | @ 绑定支持自定义 widget 的自定义属性，语法是 widget属性名=@数据源属性名（左边是 C# 属性名、右边才是 VM）。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §3 |
| F081 | 官方 prefab 有 @ 绑自定义属性的用例，如 BannerTableauWidget 的 BannerCodeText。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §3 |
| F082 | 官方另有 DataSource={子数据源} 语法，给子 widget 换数据源（列表项用）。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §3 |
| F084 | widget 标签名 = C# 类简单名（WidgetFactory._builtinTypes[type.Name]）。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §3 |
| F085 | SetTextureProviderProperty(name, value) 反射进 provider 的同名属性。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §3 |
| F087 | 所在程序集必须引用 TaleWorlds.GauntletUI，否则 widget 类型不被收录（BannerlordApi 满足）。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §3 |
| F088 | 模块的 GUI/Prefabs/*.xml 被 ResourceDepot 自动纳入，判据是 Directory.Exists(moduleFolder + /GUI/)。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §3 |
| F089 | UpdateTextureWidget() 仅在 TextureProvider == null 且 TextureProviderName 非空时才 CreateInstance。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §5.3 |
| F090 | SetTextureProviderProperty 内部会置 Texture = null ⇒ 必须先设属性再取纹理，顺序反了白跑一帧。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §5.3 |
| F091 | 落盘 → 建纹理 → 下一帧 OnRender 才取到，中间有帧延迟。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §5.3 |
| F092 | _isRenderRequestedPreviousFrame 在 OnRender 先置位、Tick 下一帧才跑到 ⇒ Tick 轮询天然滞后一帧。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §5.3 |
| F094 | TextureWidget.LoadingIconWidget（类型 Widget）挂上去就自动工作，不需 VM 维护加载中 bool。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §5.5 |
| F095 | 基类 OnTextureUpdated() 把 LoadingIconWidget 的 IsVisible 设为 !texture.IsValid，并在可见性变化时递归应用到所有子节点。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §5.5 |
| F096 | provider 实例与 widget 一一对应，由 OnDisconnectedFromRoot() → OnClearTextureProvider() 收尾。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §5.2 |
| F097 | TextureProvider 基类 Clear(bool) 只清 get-method 缓存。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §5.2 |
| F099 | 官方全部 provider 无一处调 Release()；唯一按路径读 png 的 OnlineImageTextureProvider 连 Clear 都不重写。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §5.2 |
| F102 | provider 无纹理时返回 null ⇒ 控件不画，这正是生成中的占位状态。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §2 Provider 写法 |
| F103 | 官方 image identifier 路线在 LateTick 阶段会 Debug.FailedAssert，异步回调落点同理。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §2 |
| F105 | grep -a -r -l --include='*.dll' 符号 目录 可反查符号被哪些程序集引用（MemberRef 把名字写进调用方字符串堆）。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §7 |
| F113 | TaleWorlds.Engine.Texture 与 TaleWorlds.TwoDimension.Texture 同名，后者 ctor 收 ITexture；同一文件 using 两者必歧义。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §5.1 |
| F114 | ilspy 反编译出的官方 OnlineImageTextureProvider 源码在该点上不可编译（裸用 Texture）⇒ 照抄语义别照抄文本。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §5.1 |
| F118 | 中文日期格式串形态为 {YEAR}年{SEASON}季{DAY}日 ⇒ 1084年夏季2日；三个候选里少一个季字会让那一列量宽一直偏低。 | v1.3.15 |  | bannerlord-ui-copy-parity-gate §1② |
| F120 | C# 的 ToString('N0') 跟系统区域设置走，有些区域用空格或点当千位分隔。 | net472 |  | bannerlord-ui-copy-parity-gate §1③ 对实现 |
| F123 | 文案由引擎运行时拼（GameTexts.FindText）时 DLL 里只多一个方法调用、不多字符串字面量，字节判据证不了那一格真打出了新样子。 | v1.3.15 |  | bannerlord-ui-copy-parity-gate §1⑥ |

## 证据

- **F002** 读引擎源码 StackLayout.cs 的 MeasureLinear 与 LayoutLinearHorizontalLocal 两个分支。
- **F003** 读引擎 StackLayout.cs 的 MeasureLinear 分支，等分按 StretchRatio 加权、缺省为 1。
- **F004** 读引擎布局分支：StretchToParent 时尺寸取自 measureSpec 分配，Suggested 值不参与测量。
- **F005** 读本地预览脚本 preview_prefab_geometry.py 中那条 pw_ 计算，StretchToParent 孩子一律铺满父级。
- **F007** 引擎 GridLayout 实现；坤坤编辑器 21 处在用，原版 SandBox 船位图与多人中场也有用例。
- **F008** 读引擎 GridLayout 的行列分配逻辑；RowFirst 为默认 GridDirection。
- **F009** 读引擎 GridLayout.UpdateCellSizes 的 CoverChildren 分支。
- **F017** 读预制件结构：让位写在滚动容器的 MarginRight 上，列头 ListPanel 不带该边距。
- **F019** 页区高度非定值时布局按内容高度收缩，底栏随之抬高；技能要求页区必须定高。
- **F026** 从渲染结果量页区盒顶与首块 y，两者相差 10，验证贴底排版行为。
- **F055** 归纳绑定闸要穷举的三种形状；非 DataSource 键带花括号时属性未被应用且无报错。
- **F058** 从症状链定位：滚动条层级写错后 FindChild 找不到，滚轮与拖条两条路径同时失效。
- **F065** 页区定高 655，表体以 StretchToParent 吃掉剩余高度；增加其它块会等比压缩表体。
- **f008** 社区模组目录形态实测；技能同时标注社区模组不可作权威。
- **f009** 官方 SpriteParts/ui_launcher/ConceptArts/ConceptArt_0.PNG 对应产出的 sprite 名。
- **f010** SandBox 的 SpriteParts 目录树与产出 sprite 名对照；另有 CharacterCreation\Culture\aserai。
- **f011** 对官方索引正则列顶层节点。
- **f012** 官方 Config.xml 与运行时加载行为对照。
- **f019** 技能自记：无法判定生成器读的是干净部署副本，还是"命中即丢弃"在该版本不成立。
- **f024** 穷举官方 Config.xml 出现过的子节点。
- **f028** 生成器按目录扫描登记，官方与项目实测一致。
- **f029** 同 f009 的官方命名证据：子目录不但藏不住，还会改名。
- **f032** 读官方 Brushes XML 的九宫格写法。
- **f035** 读官方 LauncherGUI sidecar 与其产出索引的 <Sprites> 段。
- **f037** 对比 sidecar 的 <Name> 与产出 sprite 名。
- **f039** 两套机制通用要求，来自拉伸实测与官方出图形态。
- **f042** 踩坑实测并钉死：把 UI 模拟图按 2x 渲染会误导 Extend 取值。
- **f046** 多层 GUI/GUI/ 目录下资源像是"没生效"的实测判据。
- **f051** 从产出索引的已用范围反推的装箱约束。
- **f054** 读官方 NativeLanguages.xml 的简中字体映射。
- **f055** 换肤后颜色不对的实测归因。
- **f057** 技能定性：没有 kit 闭不了环，但制作贴图本身不依赖它。
- **f078** 技能记录的版本对齐要求与崩溃后果。
- **f080** 2026-09-13 实测：一次无参数运行改了 AnimusForge 与 SimpleBank 的 SpriteData/图集。
- **f082** 误跑范围枚举结果。
- **f083** 误跑影响分析 + SimpleBank 实际形态。
- **f084** 2026-09-19 实测；异常类型原文 DirectoryNotFoundException。
- **f092** 对成片不透明区域的逐像素归因。
- **f097** 读 SimpleBank 索引声明与 pack0.tpac 内纹理条目名，序号从 1 起。
- **f103** pack0.tpac 内条目与 AssetSources 图片对照。
- **f108** 反编译运行时程序集 TaleWorlds.TwoDimension.dll 第 4375 行。
- **f109** 反编译生成器侧程序集第 149 行。
- **f111** 反编译 LoadTexture 实现。
- **f112** 由 f108-f111 的拼接与剥离行为推出的链路结论。
- **f118** 读该模组的 MapBar prefab 补帧写法；底板借 MapBar\bottom_left_button6_clan。
- **f119** 读该模组 prefab 与 provider 代码。
- **f120** 从该模组二进制/代码里读出的状态串；另有 MapBarIconRegistered/Applied/RetryTicks。
- **f121** 枚举该模组目录 + 真机显示实证。
- **f123** 由 Houkai 实证的绕行路与 tpac 路的对照。
- **f129** 枚举这些模组的 AssetPackages 目录内容；4 个模组是 BannerFix/PerfectFireArrows/RaiseYourBanner/WomeninCalradia。
- **f131** 观察编辑器运行后生成的缓存文件。
- **f133** 读官方 SubModule.xml 原文，注意空格差异。
- **f149** make_icon_system.py 的无缝场实现。
- **brush-01** 反编译 TaleWorlds.GauntletUI.dll：BrushFactory.LoadBrushes() → GetBrushesNames()；Base.xml 由原版提供。
- **brush-04** 反编译 TaleWorlds.GauntletUI.BaseTypes/ButtonWidget.cs:111 RefreshState()。
- **brush-05** 反编译 ButtonWidget.cs 的 DominantSelectedState 默认值（同文件 RefreshState 优先级链）。
- **brush-06** 反编译 ButtonWidget.cs:287 HandleClick()：foreach(ClickEventHandlers) h(this); bool isSelected = IsSelected; if(IsToggle)…else if(IsRadio)…
- **brush-07** 反编译 ButtonWidget.cs:287 HandleClick() 的 if(IsToggle)/else if(IsRadio) 两分支，缺省不进入。
- **brush-08** 反编译 TaleWorlds.GauntletUI 的 CircleActionSelectorWidget.cs:307。
- **brush-09** 反编译 TaleWorlds.GauntletUI/EventManager.cs:879 CollectEnableWidgetsAt。
- **brush-10** 反编译 BrushFactory.LoadStyleInto → style.GetLayer(name) → LoadBrushLayerInto。
- **brush-11** 由 brush-10 的增量赋值语义推出；对照原版 Native/GUI/Brushes/Main.xml 的 Popup.CloseButton。
- **brush-12** 反编译 BrushLayer 的 Extend* 消费点；渲染语义为四角原样、四边单向拉伸、中心双向拉伸。
- **brush-15** 读 Native/GUI/Brushes/Brush.xml:2-6 与使用方 Native/GUI/Prefabs/ButtonCancel.xml。
- **brush-21** 对 Widget 基类成员逐项核查，旋转相关成员数为 0；对应成员在 BrushLayer.cs:330。
- **brush-27** 反编译 Brush/Widget 的 Color 与 AlphaFactor 取值消费点，两者运算方式不同。
- **brush-30** 源技能以「换 Default 图重跑预览看是否变」的交叉验证得出，属实测推断。
- **brush-33** 对照 Native/GUI/Brushes/Brush.xml 与 Native/GUI/NativeSpriteData.xml 的 button_canvas_9 条目。
- **brush-39** 由 BrushFactory 全模块全量加载（brush-01）推得的同名覆盖语义。
- **vm-01** 反编译 TaleWorlds.Library.dll 的 ViewModel 通知机制与 BindingPath 订阅。
- **vm-03** 反编译 TaleWorlds.GauntletUI.BaseTypes.DropdownWidget.cs:486 的 RefreshSelectedItem。
- **vm-09** 反编译 GauntletUI 列表控件 OnListItemAdded/Removed 与 OnUpdate 的调用时序。
- **vm-12** 反编译 EditableTextWidget 的 Text setter，并对照原版 MultiSelectionQueryPopUpVM.SearchText 写法。
- **vm-13** 源技能给出的接法与原版 TabControl 语义对比（原版按子控件序号切显示）。
- **vm-14** 原版 ButtonBrush2 的 Selected 状态档存在，配合 ButtonWidget 的 IsSelected 语义。
- **vm-15** 源技能对换页后底栏位置的实测（缺定高时底栏跟随内容高度上移）。
- **vm-16** 源技能给出的日志字段清单与重入的成对翻倍特征（与 vm-04 同一实测）。
- **vm-21** 反编译 TaleWorlds.GauntletUI.Data 层 WidgetAttributeValueTypeBinding.CheckValueType。
- **vm-22** 反编译 Data 层 WidgetAttributeValueTypeBindingPath.CheckValueType。
- **vm-23** 反编译 Data 层 :1280 的唯一 BindData 调用点及其 if (valueType is WidgetAttributeValueTypeBinding) 条件。
- **vm-31** 反编译 GauntletUI :22686 Widget.FindChild(BindingPath) 的分支实现。
- **vm-33** 反编译 GauntletUI :19239 UpdateScrollablePanel 与 :19103 OnMouseScroll 的分支条件。
- **F005** 读 TaleWorlds.GauntletUI.GamepadNavigation 源码，并用原版 prefab GameMenuTroopSelection.xml 的 ScopeParent 用法对照。
- **F010** 读 TaleWorlds.GauntletUI.BaseTypes/EditableTextWidget.cs 的 HandleInput；Enter 与 NumpadEnter 都在判据里。
- **F015** 读 TaleWorlds.ScreenSystem/ScreenLayer.cs 构造函数：InputRestrictions = new InputRestrictions(localOrder)。
- **F016** 读 TaleWorlds.ScreenSystem/ScreenLayer.cs 的 CompareTo 实现。
- **F020** 读 TaleWorlds.ScreenSystem/ScreenManager.cs 的 TrySetFocus 实现。
- **F021** SKILL §5.2 贴出 ScreenManager.TryLoseFocus 完整方法体。
- **F022** 读原版 inquiry 关闭路径代码顺序（SKILL §5.2 记录）。
- **F023** 读 TaleWorlds.InputSystem 的 InputContext.CanUse / IsKeyPressed 实现（ilspycmd 直查）。
- **F024** 读 TaleWorlds.ScreenSystem/ScreenLayer.cs 的 ProcessEvents 与 _usedInputs 置位条件。
- **F030** 反编译该程序集找到订阅点（SKILL §5.4 记录）。
- **F030b** 反编译该程序集的创建时机（SKILL §5.4 记录）。
- **F032** 读 SingleQueryPopup.xml：ControlButtons 是 HorizontalLeftToRight，ButtonCancelContainer 在 ButtonOkContainer 之前。
- **F033** 读原版 KingdomManagementVM 的 ExecuteKingdomAction / OnGrantFief 实现。
- **F034** 反编译 GauntletQueryManager 的队列判断（SKILL §5.4 记录）。
- **F037** 读 SingleQueryPopup.xml 根节点属性 + TaleWorlds.Engine.GauntletUI 的 GauntletLayer.HitTest 实现。
- **F039** 在 Modules/AnimusForge/GUI/Prefabs/AnimusForgeNativeConversationOverlay.xml 与其自定义控件源码中定位。
- **F044** 读 Gauntlet 渲染侧的 Color/AlphaFactor 应用逻辑（SKILL §6 记录）。
- **F048** 对原版 Prefab 的 IsVisible 取值全量取样（SKILL §9 记录）。
- **F049** AWAKE 实测：属性值变了但 UI 不刷新，补通知后恢复。
- **F053** 反编译 Modules/Native/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.GauntletUI.dll（1.3.15 出货客户端）。
- **F055** 反编译 TaleWorlds.GauntletUI.dll 的 TextureWidget.OnRender 实现。
- **F055b** 反编译 TextureWidget.OnRender，九宫格参数被写死为空。
- **F057** 一手用例 TaleWorlds.MountAndBlade.View.dll 的 AvatarThumbnailCache.cs:36 走 CreateFromMemory（调试名 byte_array）。
- **F058** 读 TaleWorlds.MountAndBlade.View.dll 的 AvatarThumbnailCache.cs:36 与 :41；调试名分别为 byte_array 与 raw_data。
- **F062** SKILL §6 明列该自证法无效，须导出成图后外部看图验色。
- **F068** 反编译 TaleWorlds.GauntletUI.dll 的 TextureProviderFactory.RefreshProviderTypes。
- **F068b** 反编译 TextureProviderFactory.RefreshProviderTypes 的字典写入键。
- **F069** 反编译 TaleWorlds.ModuleManager 的 Module.LoadSubModules（SKILL §4 记录）。
- **F070** 反编译 Native 的 GauntletUISubModule.OnSubModuleLoad 调用链（SKILL §4 记录）。
- **F071** 由 §4 的 LoadSubModules 两段式与 RefreshResources 时机推出（SKILL 结论）。
- **F073** 反编译 TextureProviderFactory.RefreshProviderTypes 的字典写入（1.3.15 一手核对）。
- **F074** 反编译 WidgetFactory.Initialize 的 _builtinTypes[type.Name] = type 写入。
- **F075** 反编译该方法体，1.3.15 一手核对。
- **F077** 对比两条注册路径的异常与写入语义（SKILL §4 结论）。
- **F078** 反编译 TaleWorlds.GauntletUI.PrefabSystem.dll 的 WidgetFactory.CreateBuiltinWidget。
- **F080** 官方 prefab 一手用例 + SKILL §3 说明。
- **F081** 读原版 prefab；同批用例还有 ImageIdentifierWidget 的 ImageId 与 TextureProviderName、CharacterCreationStageSelectionBarWidget 的 CurrentStageIndex。
- **F082** 官方 prefab 用例（SKILL §3 记录）。
- **F084** 反编译 WidgetFactory 的 _builtinTypes 用法（SKILL §3 记录）。
- **F085** 反编译 TextureWidget.SetTextureProviderProperty 的反射写入；故 widget 属性名须与 provider 属性名一致。
- **F087** SKILL §3 记录；由 widget 类型扫描机制推出。
- **F088** 反编译 ResourceDepot 的目录判定（SKILL §3 记录）。
- **F089** 反编译 TextureWidget.UpdateTextureWidget 的创建条件，即 provider 懒创建（SKILL §5.3 记录）。
- **F090** 反编译 SetTextureProviderProperty 与 UpdateTextureWidget 的先后关系（SKILL §5.3 记录）。
- **F091** SKILL §5.3 记录：UI 状态机必须吃得住这个延迟。
- **F092** 反编译 TextureWidget.OnRender 与 Tick 的置位/读取顺序（SKILL §5.3 记录）。
- **F094** 反编译 TextureWidget 与基类 OnTextureUpdated 的联动（SKILL §5.5 记录）。
- **F095** 反编译 TextureWidget.OnTextureUpdated 的赋值与递归（SKILL §5.5 记录）。
- **F096** 反编译 TextureWidget 的断开链路，末步是 provider.Clear(clearNextFrame: true)（SKILL §5.2 记录）。
- **F097** 反编译 TextureProvider.Clear(bool) 方法体（SKILL §5.2 记录）。
- **F099** 对官方 13 个 provider 反编译结果全量检索 Release/Clear 调用。
- **F102** SKILL §2 记录：OnGetTextureForRender 返回 null 即不绘制。
- **F103** SKILL §2 记录官方路线在 LateTick 触发断言。
- **F105** SKILL §7 记录的 .NET 元数据行为与实测用法。
- **F113** SKILL §5.1 记录，两个同名类型分属不同程序集。
- **F114** SKILL §5.1 记录反编译产物无法编译。
- **F118** 从官方本地化 XML 核出真形状（SKILL §1② 记录）。
- **F120** SKILL §1③ 记为记号判据要防的失效（.NET 格式化行为）。
- **F123** SKILL §1⑥ 星号段落记录的判据边界。

---

# 字节布局（byte-layout）

> 共 8 条。`valid_for` 是这批事实**被测量时**的版本——引用前按当前版本复核。

| id | 事实 | 测量版本 | 待重验 | 出处 |
|---|---|---|---|---|
| f102 | SimpleBank pack0.tpac 条目：Bank_1 → $BASE/…/AssetSources/GauntletUI/Bank_1.png → DXT5 has_alpha。 | 未标版本 |  | bannerlord-ui-sprite-assets §9.1 |
| f122 | goddess_emblem 只存在于 UTF-16 串里，用 ASCII 搜不到。 | 未标版本 |  | bannerlord-ui-sprite-assets §9.9 L555 |
| f125 | tpac 字段：@0x1C=316+name_len+path_len；name 后 u32=114+path_len；@0x123=21；@0x168=21+log2(WxH)。 | 未标版本 |  | bannerlord-ui-sprite-assets §9.8 L496 |
| brush-14 | 贴图注册表另有 NineRegionSprite：NativeSpriteData.xml:40392 的 button_canvas_9 为 L25/R24/T43/B44。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4（第 111-121 行） |
| vm-17 | 改完的验证顺序为编译 → 部署 → 内容级校验（反编译部署件/cmp）→ 才进游戏；DLL 内字符串是 UTF-16LE。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §7（第 135-136 行） |
| F004 | GamepadNavigationTypes 枚举值为 None=0、Up=1、Down=2、Vertical=3、Left=4、Right=8、Horizontal=0xC。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §2 手柄导航与焦点 |
| F013 | InputUsageMask 位值：Invalid=0、MouseButtons=1、MouseWheels=2、Keyboardkeys=4。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §5 层输入遮罩 |
| F122 | 部署 DLL 的 .NET 串堆是 UTF-16LE，strings -e l 读不出来，得按字节找。 | v1.3.15 | 是 | bannerlord-ui-copy-parity-gate §1⑥ |

## 证据

- **f102** 实读 pack0.tpac 内容。
- **f122** 二进制串搜索实测。
- **f125** 对真实 tpac 样本做十六进制字段比对。
- **brush-14** 读 Native/GUI/NativeSpriteData.xml:40392 的 <NineRegionSprite> 块（Name/SpritePartName/LeftWidth/RightWidth/TopHeight/BottomHeight）。
- **vm-17** 源技能给出的验证顺序与 '某句中文'.encode('utf-16-le') in open(dll,'rb').read() 判据。
- **F004** 读 TaleWorlds.GauntletUI 的 GamepadNavigation 命名空间源码，1.3.15 出货客户端。
- **F013** 读 TaleWorlds.Library/InputUsageMask.cs 的 [Flags] 枚举：另有 Mouse=3、All=7、BlockEverythingWithoutHitTest=8。
- **F122** SKILL §1⑥ 第 2 条记录实测；属否定式断言故列入复核。

---

# 实测数值（measured-number）

> 共 95 条。`valid_for` 是这批事实**被测量时**的版本——引用前按当前版本复核。

| id | 事实 | 测量版本 | 待重验 | 出处 |
|---|---|---|---|---|
| F006 | 在该工作台预览里，弹性格与兄弟重叠会把内容包围盒撑到实测 2306 宽，渲染类探针当场报面板尺寸对不上。 | 未标版本（本机工作台） |  | bannerlord-gauntlet-ui-prefab-layout §2.5 |
| F014 | 本项目行情面板横向账：面板 SuggestedWidth 940 − 滚动条让位 15 = 列宽总额上限 925。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §1 |
| F015 | 本项目行情面板竖直账：25 + 30 + 10 + 455 + 10 + 40 + 30 = 600。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §1 |
| F016 | 本项目行情面板表体高 340 ÷ 行高 30 ⇒ 11 行（330），余 10 是滚动区里看不见的余量。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §1 |
| F020 | 本项目城名列设计宽 360 px，占整行宽约 38%。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §2 |
| F021 | 官方简中最长的聚落名是 6 字 × 20 px = 120 px，即城名列约 300 px 永远是空白。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §2 |
| F022 | 把城名列从 360 收到 205 后，省出的宽足够再加两列新数据。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §2 |
| F023 | 本项目行情表与台账表的列宽合计都等于同一个上限 925。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §3 |
| F024 | 渲染实测行位：页签行 y=25、页区首块 y=65、筛选行 y=105、表体首行 y=180、底栏 y=730，表体每行高 30。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §6.3 |
| F025 | 底栏那颗关闭按钮实测尺寸 160×40，其 y 量在 730。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §6.3 |
| F027 | 面板原点有两套互不相等的坐标：画布像素 (40,66) 与 HTML 里 1320×800 底板的 (260,74)/(300,140)，工作台按内容包围盒算、面板不居中。 | 未标版本（本机工作台） |  | bannerlord-gauntlet-ui-prefab-layout §6.3 |
| F028 | 页签行里尾格是读数不是按钮；按整行建按钮表会多出一格 690×30。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §6.3 |
| F030 | 底栏那颗按钮的字号比页签大一号，实测 20 vs 17。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §6.3 |
| F031 | 本项目面板的页区高是硬账 655。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §6.3 / §6.4b |
| F032 | 行情页 8 块的竖直合计是 1280，因为两档视图各排了一遍、同时只亮一档。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §6.3 |
| F033 | 记账页 6 块的竖直合计是 640，比页区 655 少的 15 是页底故意留的余量，注释里写着。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §6.3 |
| F034 | 筛选行是一个不带 IsVisible 的 ListPanel，里面比价档 6 格、走向档 5 格，合计 11 格。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §6.4 |
| F035 | 比价档格宽 130+130+205+165+170+420，合计 1260，等于面板内宽。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §6.4 |
| F036 | 走向档格宽 130+205+10+165+70×4+10×5+10+410，合计 1260，与比价档各自闭合。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §6.4 |
| F037 | offsets() 把同一父下所有孩子一路累加，走向档那几格被算到 x=1300…2030，而面板内宽只有 1260。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §6.4 |
| F038 | 09-20 漏挂一颗按钮的 IsVisible，导致那一档横向宽 1580（应为 1260），界面表象只是右边那格读数被切掉一截。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §5.3 |
| F040 | 09-20 自写「数标签配平深度」的循环把一棵 175 行的子树量成 139 行（开工行 1042），根因是跨行自闭合标签判断错。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §5.2 |
| F047 | 预览与游戏在右对齐上天生差 1~4 px：预览一侧按墨迹右缘摆、一侧按字宽右缘画，游戏里两处都走引擎 TextHorizontalAlignment=Right。 | 未标版本 |  | bannerlord-gauntlet-ui-prefab-layout §7 |
| F051 | 12 位数字实测只有 120 px，装得进 205 px 的格子；要确定放不下需用 40 位数字（约 400 px）。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §7 |
| F059 | 同一面板各页骨架常常恰好同高：30 筛选/工具 + 25 列头 + 540 表体 + 30 下单条。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §6.1 |
| F060 | 同一面板两页的表体列数不同（七列 vs 八列），而列宽合计可能都恰好是 1245，那是巧合不是同一出处。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §6.1 |
| F064 | GridWidget 保持节距实例：按钮 SuggestedWidth=150 ＋ MarginLeft=10 ⇒ DefaultCellWidth=160，首颗仍在网格边距+10处。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §2.6 |
| f003 | 官方 NativeSpriteData.xml 为 1.40 MB，SandBoxSpriteData.xml 为 628 KB。 | 未标版本 |  | bannerlord-ui-sprite-assets §1 L54 |
| f004 | 官方 Native/AssetPackages/gauntlet_ui.tpac 为 323~324 MB（技能两处分别记 324/323 MB），SandBox 为 137 MB。 | 未标版本 |  | bannerlord-ui-sprite-assets §1 L56 / §9.1 L418 |
| f007 | 官方 sprite 分类数：Native 25 个、SandBox 15 个、LauncherGUI 2 个，全部以 ui_ 开头、零例外。 | 未标版本 |  | bannerlord-ui-sprite-assets §1 L66 |
| f013 | Native 25 个分类里只有 4 个用了 AlwaysLoad：ui_fonts / ui_fullbackgrounds / ui_group1 / ui_textures。 | 未标版本 |  | bannerlord-ui-sprite-assets §1 L79 |
| f020 | 官方 LauncherGUI Config.xml 仅 174 B、无声明无注释，含 PackAllSpritesToUniqueTextures/SingleChannel/EdgeSize。 | 未标版本 |  | bannerlord-ui-sprite-assets §1 |
| f023 | AWAKE 的 Config.xml 声明图集 2048x2048，真实产出是 2048x64 / 4096x1024 / 4096x128。 | 未标版本 |  | bannerlord-ui-sprite-assets §1 L113 |
| f025 | 生成器排法：左/上留 4 px、件间距 8 px，SheetY 从 4 起。 | 未标版本 |  | bannerlord-ui-sprite-assets §1 L120 / §4 L237 |
| f030 | AWAKE 仓库 GUI/ 有 63 个文件、部署 Modules/AWAKE/GUI/ 有 30 个，内容不同 23 个、只在仓库 33 个。 | 未标版本 |  | bannerlord-ui-sprite-assets §1 |
| f031 | Brush 层 Extend* 官方命中数：Native 426、SandBox 565、SandBoxCore 12、LauncherGUI 8。 | 未标版本 |  | bannerlord-ui-sprite-assets §2 L156 |
| f034 | 官方 NineRegionSprite 数：Native 索引 168、SandBox 索引 22、LauncherGUI 索引 4，合计 194。 | 未标版本 |  | bannerlord-ui-sprite-assets §2 / L23 |
| f036 | 官方 sidecar 实物只有 4 个，全在 Native/LauncherGUI/SpriteParts/ui_launcher/。 | 未标版本 |  | bannerlord-ui-sprite-assets §2 L191 |
| f038 | Native 的 BlankWhiteSquare_9 来自 BlankWhiteSquare.png 的 sidecar，四边各 1 px。 | 未标版本 |  | bannerlord-ui-sprite-assets §2 L193 |
| f040 | 212x360 空心线框内缩 7 + 切角 2，在 Extend=8 下 1 px 斜切角被拉成 2 px 台阶；Extend=16 才干净。 | 未标版本 |  | bannerlord-ui-sprite-assets §2 |
| f041 | 同条件下内缩 0 + 切角 8（切角正好卡满 8 px 角）拉伸后逐像素无损。 | 未标版本 |  | bannerlord-ui-sprite-assets §2 L199 |
| f044 | 拉伸验收：140x34 用 e=16 拉到 280x68，四角 16x16 逐像素等于原图。 | 未标版本 |  | bannerlord-ui-sprite-assets §2 L206 |
| f049 | AWAKE 的 ui_awake_icon/ 有 28 张 PNG，但索引 SpriteCategories 只有 button/frame/ornament 三个。 | 未标版本 |  | bannerlord-ui-sprite-assets §4 L236 |
| f052 | AWAKE ui_awake_button 2048x64 已用至 1388；ui_awake_frame 4096x1024 已用至 4088/4096 已满，212x360 的件装不下。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §4 L237 |
| f058 | 原版 Prefab 的尺寸口径是 5 的倍数，1/2/3 只用于贴边微调。 | 未标版本 |  | bannerlord-ui-sprite-assets §5 L253 |
| f060 | 图标设计网格是 24x24、线宽 2–3、方端、交付 48/72（分类表 C5），与 Prefab 写多少 px 是两件事。 | 未标版本 |  | bannerlord-ui-sprite-assets §5 L257 |
| f067 | 材质用量 stddev：满版铺 23.6、淡化到 22% 为 7.4（最差解）、只做有边界的件为 4.4（对）。 | 未标版本 |  | bannerlord-ui-sprite-assets §6 L288 |
| f068 | 官方 paper_texture_tile 为 241,237,234 近白暖白（R>G>B）；stone_texture_continuous/_overlay 为中性灰 144/111 的叠加层。 | 未标版本 |  | bannerlord-ui-sprite-assets §6 |
| f077 | appinfo_log.txt 有多次 "Apps changed: 1393600=…"，说明该工具一直在库里、只是看不见。 | 未标版本 |  | bannerlord-ui-sprite-assets §7 L313 |
| f081 | 一次误跑的真实范围是 21:41:57–21:42:06 共 13 个文件：SimpleBank 2 个、AnimusForge 11 个（1 个 SpriteData + 10 张图集）。 | 未标版本 |  | bannerlord-ui-sprite-assets §8 L395 |
| f088 | 单件尺寸到图集尺寸的分档：40x40→128x64（4,283 B）、48→128x64、64→128x128、80→256x128、96→256x128。 | 未标版本 |  | bannerlord-ui-sprite-assets §8 |
| f089 | 34x40 单件产出 64x64 的 0 B 图集；给该分类加 <EdgeSize Value="32"/> 后图集 128x128、4,602 B 正常。 | 未标版本 |  | bannerlord-ui-sprite-assets §8 L336 |
| f091 | 内容级验收：按 SpriteData 的 SheetX/SheetY/Width/Height 切槽与源图逐像素比；Leverage 钱箱图标实测 100% 相同。 | 未标版本 |  | bannerlord-ui-sprite-assets §8 |
| f093 | 2026-09-19 实测 8,832 个外扩像素里 0 个与源图对应边缘像素不一致。 | 未标版本 |  | bannerlord-ui-sprite-assets §8 |
| f094 | 外扩环形包围盒 = (W+2E)x(H+2E)；34x40 加 E=32 时为 98x104。 | 未标版本 |  | bannerlord-ui-sprite-assets §8 L348 |
| f095 | 沙箱成本：wEditor 目录 1.0 GB / 5507 文件；robocopy /E 约 2 分钟；跑完近 20 分钟其它模组被改文件数为 0。 | 未标版本 |  | bannerlord-ui-sprite-assets §8 |
| f096 | AWAKE 21 sprite/3 分类产出：button 2048x64、frame 4096x1024（单张 2.7 MB）、ornament 4096x128。 | 未标版本 |  | bannerlord-ui-sprite-assets §8 |
| f099 | gauntlet_ui.tpac 里 ui_conversation_1 / ui_barter_1 / ui_bannericons_1 / ui_bannerbuilder_1 各 2 次。 | 未标版本 |  | bannerlord-ui-sprite-assets §9.1 |
| f100 | 全机 108 个模块，29 个有 AssetPackages/，只有 2 个有 GUI/SpriteSheets/。 | 未标版本 |  | bannerlord-ui-sprite-assets §9.1 L422 |
| f105 | 09-19 那 5 次启动（14:42/14:49/15:02/15:54/17:21）全部同一句缺纹理报错，最后一次在文件就位 7 分钟之后。 | 未标版本 |  | bannerlord-ui-sprite-assets §9.1 |
| f106 | 那张 ui_leverage_1.png 是 128x128 RGBA8，与 <SpriteSheetSize ID="1" Width="128" Height="128"/> 逐字对上。 | 未标版本 |  | bannerlord-ui-sprite-assets §9.1 |
| f113 | SimpleBank 四件套：Bank_1.png 2048x512、Bank_1_tex.tpac 479 B、pack0.tpac 37,890 B。 | 未标版本 |  | bannerlord-ui-sprite-assets §9.9 L526 |
| f114 | AWAKE 的 GUI/SpriteSheets 为 0、AssetSources 有 3 个、无 Assets、无 AssetPackages。 | 未标版本 |  | bannerlord-ui-sprite-assets §9.9 L527 |
| f115 | AnimusForge 的 SpriteSheets 有 1、AssetSources 有 10、Assets 是空目录、无 AssetPackages。 | 未标版本 |  | bannerlord-ui-sprite-assets §9.9 L528 |
| f116 | AnimusForge 的 SpriteSheets 散图是 1024x1024，其 SpriteData 声明 2048x512，尺寸对不上。 | 未标版本 |  | bannerlord-ui-sprite-assets §9.9 |
| f117 | AssetSources/GauntletUI PNG 尺寸与 SpriteData 的 SpriteSheetSize 对上（AWAKE 2048x64/4096x1024/4096x128）。 | 未标版本 |  | bannerlord-ui-sprite-assets §9.9 |
| f127 | Bank_1_tex.tpac 只有 479 字节，内容是名字 + $BASE/…png 源路径 + 格式的纯元数据壳，比带像素的包简单一个量级。 | 未标版本 |  | bannerlord-ui-sprite-assets §9.4 |
| f132 | 本机官方模块硬编码白名单共 7 个：Native/SandBoxCore/SandBox/CustomBattle/StoryMode/Multiplayer/NavalDLC。 | 未标版本 |  | bannerlord-ui-sprite-assets §10 |
| f135 | grep -l 'Official' Modules/*/SubModule.xml 得 43 命中，因为社区模组习惯写 <Official value="false" />。 | 未标版本 |  | bannerlord-ui-sprite-assets §10 L585 |
| f136 | grep -lE 'ModuleType[^>]*Official' 得 8 命中，多出 BirthAndDeath / FastMode 这类照抄模板标签的社区模组。 | 未标版本 |  | bannerlord-ui-sprite-assets §10 L586 |
| f138 | 本机生图工具的 size 面积须 ≥ 921600 px：1024x768 被拒，1280x768 通过。 | 未标版本 |  | bannerlord-ui-sprite-assets §11 L626 |
| f139 | 压暗到 UI 明度用 ImageStat 量均值后按比例缩放；台面/面板底目标均值约 50~55，否则上面的文字压不住。 | 未标版本 |  | bannerlord-ui-sprite-assets §11 L629 |
| f143 | 图标在 288 px（24x12 超采样）上作画再 LANCZOS 降到 48/72；直接小尺寸画会毛。 | 未标版本 |  | bannerlord-ui-sprite-assets §12 L643 |
| f144 | 24 格易读性三坑：点画粘连（气口需 ≥1 格）、齿轮齿细长读成星芒、方框+内线读成「面板」。 | 未标版本 |  | bannerlord-ui-sprite-assets §12 L648 |
| f146 | 图标交付形态为纯白 #F8F8F8 剪影、无渐变/无内部细节线/无描边色；源图 icon_<名>_48.png 与 _72.png（1x 像素、RGBA、透明底）。 | 未标版本 |  | bannerlord-ui-sprite-assets §12 |
| brush-13 | 实测计数：Native GUI/Brushes/*.xml 里 ExtendLeft 出现 109 处；全库 10 个 *SpriteData.xml 里 ExtendLeft 出现 0 处。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4（第 110 行） |
| brush-17 | button_canvas_9 的 TopHeight+BottomHeight=43+44=87 等于贴图全高，竖着没有可拉伸中段，不能拿它压小按钮。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4（第 128-129 行） |
| brush-26 | 原版五个官方模块的 Prefab 里 Color= 共出现 729 处，全部 8 位，零例外。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §6（第 182 行） |
| brush-31 | AWAKE 实际 Brush 文件 AWAKE/GUI/Brushes/AwakeBrushes.xml 含 11 条 Brush、21 个 sprite。 | 未标版本 | 是 | bannerlord-gauntlet-ui-brushes §9（第 218 行） |
| brush-34 | ButtonBrush1/2/3 的 Extend 均为 22/22/22/22，贴图为 main_button_done/_regular/button_cancel，原生 271×84。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4.7 表（第 162、167 行） |
| brush-35 | ButtonBrush4（big）的 Extend 为 22/12/22/12，最小可用高度只要 24。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4.7（第 167 行） |
| brush-37 | 全库只有 3 个 prefab 使用朴素 ButtonBrush，其中正经的只有 ButtonCancel.xml（100×80）。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4.7（第 161、166 行） |
| brush-38 | ButtonBrush 贴图 button_canvas_9 的内容只占 97×87 中间那 48×49，AWAKE UI Lab 渲染图会把它画小。 | 未标版本 | 是 | bannerlord-gauntlet-ui-brushes §4.7（第 168-169 行） |
| vm-04 | 旧下标回写 × 新列表导致「点 A 行动的是 B 行」，且 setter 里 Refresh() 重入使表体出现两段相同列表（实测 53 行 → 106 行）。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §2（第 27-38 行） |
| vm-27 | 实测 738 份预制件里带花括号的键名只有三个：DataSource（3537 次）、Parameter.*DataSource、DefaultValue（2 次）。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §9（第 173 行） |
| vm-28 | 八列表格表体每格空白的根因是写成 Text="{TownText}" 而非 Text="@TownText"；数据侧日志算出 947 行、绑了 120 行。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §9（第 156-158 行） |
| vm-34 | 原版 SandBox 预制件 25 处 VerticalScrollbar 全是 ..\ 形式，无一处把滚动条当直接子控件。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §10 正确写法（第 196-197 行） |
| vm-38 | 该门禁跑一次需用 ilspycmd 拆本模组 DLL 加若干原版程序集，实测约 3 分钟；前台默认 120 s 超时会被砍。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §12 末注（第 228-230 行） |
| F018 | 原版确认弹窗所在层（GauntletQueryManager）层序为 19501。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §5.1 |
| F019 | AWAKE 项目普通面板层序常在几百，LeveragePanelOverlay 为 546。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §5.1 |
| F031 | 原版 SingleQueryPopup.xml：SuggestedWidth=512、两颗按钮 251×64 等宽、PositionYOffset=-50。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §5.4 |
| F042 | Color 须 8 位十六进制 #RRGGBBAA：实测原版五个官方模块 Prefab 中 Color= 出现 729 处、全部 8 位、零例外。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §6 |
| F054 | 该程序集共 13 个 TextureProvider 子类，含 SceneTextureProvider 与 CharacterTableauTextureProvider。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §0、§7 取证手法 |
| F124 | 票面按字数卡的预算是 ≤ 38 字。 | v1.3.15 |  | bannerlord-ui-copy-parity-gate §1② |
| F125 | 最坏一串档位：价格六位、预算七位、地名取官方简中最长、件数带 ↑ 那一档。 | v1.3.15 |  | bannerlord-ui-copy-parity-gate §1② |

## 证据

- **F006** 把含 StretchToParent ＋ 保留 Suggested 的预制件丢进 AWAKE UI Lab 预览，量内容包围盒得 2306。
- **F014** 读该面板预制件的 SuggestedWidth 与滚动容器 MarginRight，并由脚本对账列宽合计。
- **F015** 逐块列出各块 MarginTop 与 SuggestedHeight 相加，写在预制件顶部注释里的硬账。
- **F016** 表体 SuggestedHeight 340 与行模板 SuggestedHeight 30 相除向下取整。
- **F020** 量渲染图每列墨迹右缘并与该列 SuggestedWidth 对比。
- **F021** 从游戏数据里取最长译名并按其字号量宽，与城名列设计宽 360 相减。
- **F022** 以列宽合计 925 为上限做减法：360→205 省 155，够容纳两列新数据。
- **F023** 用 ElementTree 解析预制件，对两张表的列头与行模板格宽分别求和，均为 925。
- **F024** 用 tools/_probe_panel_rows.py 把渲染 HTML 里 image 矩形按 (宽,高) 分组列 y 值。
- **F025** 同一支渲染量尺按 (宽,高)=(160,40) 分组读出 y=730。
- **F027** 在画布上按底色找面板原点得 (40,66)，同一面板在 HTML 里的底板矩形是 (260,74)/(300,140)。
- **F028** 按行取全部子控件建按钮表时出现守卫报没有这一矩形，量该多余矩形为 690×30。
- **F030** 逐颗读预制件里 FontSize：底栏 20、页签 17；统一用一个字号画图不报错只是看着小一号。
- **F031** 读预制件页区 SuggestedHeight；技能称其为硬账，加任何一行都从表体里扣。
- **F032** 对行情页逐块累加 MarginTop 与 SuggestedHeight，得 1280，超出页区 655 属正常。
- **F033** 对记账页逐块累加得 640，与页区 655 差 15，预制件注释说明为页底余量。
- **F034** 解析该 ListPanel 的子控件并按 IsVisible 归属两档，数出 6 与 5。
- **F035** 读预制件各格 SuggestedWidth 相加，按比价档的 IsVisible 筛出这 6 格。
- **F036** 读预制件各格 SuggestedWidth 与 MarginLeft 相加，按走向档的 IsVisible 筛出这 5 格。
- **F037** 用不按 IsVisible 筛格的 offsets() 量该筛选行，走向档控件落到面板外，且渲染不报错。
- **F038** 按 IsVisible 分档求和时发现某一档合计 1580；缺陷实际存在于预制件的 IsVisible 属性。
- **F040** 自写深度循环在子树内部一个 </Children> 处误判深度回 0；改用 expat 复核真实行数 175。
- **F047** 对比预览脚本两种摆法与实际引擎对齐方式的差异，量得 1~4 px。
- **F051** 取样本前先量宽：12 位数字 120 px < 205 px 格宽，40 位约 400 px 才确定溢出。
- **F059** 逐页解析预制件的块高，得到相同的四段骨架。
- **F060** 分别解析两页表体的列数与列宽合计，得七列与八列但合计同为 1245。
- **F064** 按原横排节距反推格宽常量，换算后逐像素位置不变。
- **f003** 对官方模块 GUI/ 下索引文件测字节数。
- **f004** 文件大小实测；§1 记 324 MB，§9.1 记 323 MB，两处不一致。
- **f007** 数三个官方 SpriteData.xml 的 <SpriteCategories> 条目并核对命名前缀。
- **f013** grep AlwaysLoad 官方 NativeSpriteData.xml 并按分类归类。
- **f020** 读该文件字节数与内容。
- **f023** 对比 Config.xml 声明值与生成器产出的图集实际像素尺寸。
- **f025** 从产出 SpriteData.xml 的 SheetX/SheetY 反推排版参数。
- **f030** 逐文件比对两处 GUI/ 目录，用 _cmp_gui_repo_vs_game.py。
- **f031** grep Extend* 各官方模块 GUI/Brushes/*.xml 并计数。
- **f034** grep -c NineRegionSprite 三个官方索引。
- **f036** 枚举官方模块里的 SpriteParts/*.xml；四个文件名：BlankWhiteSquare、launcher_button_frame、launcher_tooltip、popup_frame_small。
- **f038** 读该 sidecar 的 LeftWidth/RightWidth/TopHeight/BottomHeight。
- **f040** 逐像素图可见；1px 线框拉伸实测。
- **f041** 同上 1px 线框实测对照。
- **f044** 逐像素比四角区域，端头曲线/框带/上下光带均未走样。
- **f049** 数目录 PNG 数与索引分类条目。
- **f052** 读 AWAKESpriteData.xml 各分类的 SheetX+Width 最大值。
- **f058** 统计原版 Prefab 的 SuggestedWidth/SuggestedHeight 取值分布。
- **f060** 读 AWAKE docs/UI-ART-ASSET-TAXONOMY-20260913.md 分类表 C5。
- **f067** 对面板底铺材质后量 stddev 的三组对照实测。
- **f068** 从 tpac 抠出官方贴图后读像素值。
- **f077** 读 Steam appinfo_log.txt。
- **f081** 用 mtime 时间窗枚举 Modules 下被写文件。
- **f088** 逐档单件跑生成器量图集尺寸与字节数。
- **f089** 同一单件的对照实测。
- **f091** 脚本 _probe_atlas_slot_check.py 逐像素比对，期望最大差 0。
- **f093** 环形区域逐像素比源图边缘像素。
- **f094** 实测外扩环的几何尺寸。
- **f095** 实测拷贝耗时与被改文件抽样核查。
- **f096** 生成器产出实测。
- **f099** 在官方 tpac 内搜这四个纹理名并计数。
- **f100** 枚举全机 Modules 目录统计。
- **f105** 逐次读启动日志。
- **f106** 读图片属性并与索引声明比对。
- **f113** 逐目录枚举 SimpleBank 并量尺寸/字节数。
- **f114** §9.9 全机样本表实读。
- **f115** §9.9 全机样本表实读。
- **f116** 图片尺寸与其索引声明比对，判断为别的实验残留。
- **f117** 逐图尺寸与索引声明比对，判定 AssetSources 是源。
- **f127** 读该 tpac 字节数与内容结构。
- **f132** 逐个核对模块来源后固化为白名单。
- **f135** 在全机 Modules 上跑该 grep 计数。
- **f136** 在全机 Modules 上跑该 grep 并人工核对多出的两个。
- **f138** 对本机生图工具做尺寸边界实测。
- **f139** 落地三步中的压暗实测口径。
- **f143** 首批 14 枚图标实测。
- **f144** 首批 14 枚图标实测得出。
- **f146** 落盘与注册三件套的规格。
- **brush-13** 对游戏根 Native/GUI/Brushes/*.xml 与全库 *SpriteData.xml 做元素名计数。
- **brush-17** Native/GUI/NativeSpriteData.xml:40392 的 43/44 与贴图 97×87 尺寸相加比对。
- **brush-26** 对原版五个官方模块 GUI/Prefabs 的 Color= 逐处取值长度统计。
- **brush-31** 对工作区文件 AWAKE/GUI/Brushes/AwakeBrushes.xml 的条目与 sprite 引用计数。
- **brush-34** 读原版 Brush XML 中 ButtonBrush1/2/3 的 Extend* 取值与贴图尺寸。
- **brush-35** 读原版 Brush XML 中 ButtonBrush4 的 ExtendLeft/Top/Right/Bottom 取值。
- **brush-37** 对全库 prefab 检索 Brush="ButtonBrush" 的命中数并逐个查看。
- **brush-38** 源技能对 button_canvas_9 贴图内容区实测，并对照 awake-ui-lab 渲染结果。
- **vm-04** 真机日志实证：每次操作成对出现两条、第二条行数恰为第一条两倍（53→106）。
- **vm-27** 对 738 份原版预制件逐键统计花括号值的键名分布。
- **vm-28** 09-21 实测案例：数据侧日志 947 行/绑 120 行，Prefab 那八格用花括号。
- **vm-34** 对原版 SandBox 预制件 25 处 VerticalScrollbar 取值的形态统计。
- **vm-38** 源技能实测耗时 ≈3 分钟；前台运行被默认 120 s 超时中断。
- **F018** 读 TaleWorlds.MountAndBlade.GauntletUI.dll 的 GauntletQueryManager 构造参数（GlobalLayer 序）。
- **F019** SKILL §5.1 记录的仓内数值；属项目侧常量，随代码演进会变。
- **F031** 读 Modules/Native/GUI/Prefabs/Information/Inquiries/SingleQueryPopup.xml；另有全屏 canvas_gradient 压暗底。
- **F042** 对 Modules/{Native,SandBox,SandBoxCore,StoryMode,Multiplayer}/GUI/Prefabs 全量 grep 并统计长度。
- **F054** SKILL §0 记『同程序集另有 12 个同类』、§7 记『官方 13 个 provider』，同指这一批。
- **F124** SKILL §1② 举的按字数预算例子。
- **F125** SKILL §1② 列出的各列最坏一串取值。

---

# 静默失败（silent-failure）

> 共 54 条。`valid_for` 是这批事实**被测量时**的版本——引用前按当前版本复核。

| id | 事实 | 测量版本 | 待重验 | 出处 |
|---|---|---|---|---|
| F010 | 横排 ListPanel 一行放不下时，排在最后的那一颗会被静默裁掉：不折行、不缩略、不报错。 | 未标版本 | 是 | bannerlord-gauntlet-ui-prefab-layout §2.6 |
| F013 | 折行网格的容器多半是定高，多出来的行直接出画，不报错。 | 未标版本 |  | bannerlord-gauntlet-ui-prefab-layout §2.6 |
| F018 | 列宽合计超上限的表象是左边缘全部对齐、只有最后一列右边对不上、末位数字被切，而不是整行右移。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §1 |
| F039 | RichTextWidget 不换行，内容超宽只会被切，不报错。 | 未标版本 | 是 | bannerlord-gauntlet-ui-prefab-layout §5.3 |
| F043 | 09-20 预览脚本用 FILTER[4] 按序号取格，前面插进一格后它指向「排列」，灰提示被画到另一个下拉上，而所有自动化检查全绿。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §5.1 |
| F045 | 探针靠标记认元素时，一旦同标记的兄弟变多，「第一个命中的」就变成别人，探针不报错只是开始量别人。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §5.3 |
| F046 | page_geometry() 的推导与 check(工具行 y, PAGE_TOP) 的断言同源，推错了断言也平，属同义反复；必须另有一条从渲染结果量的旁证。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §6.3 |
| F048 | 探针循环写成 [CX[i-1], CX[i]] 而第 i 列实际是 [CX[i], CX[i+1]] 时，整轮标签错开一格，六列里报出四条假警报。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §7 |
| F049 | 09-20 的判据只读 Text，而对照组把坏打在 Brush.Color 上，于是报「没抓住」；真实缺陷是涨跌颜色绑反、字与数全对。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §7 |
| F054 | 绑定名拼错在游戏里表现为一声不响的空格，编译、XML 校验、图集全都不报。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §9.2 |
| F056 | 09-21 有八格空白因为绑定闸只跑「好件全过」的假绿，一路绿灯到了用户那里。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §9.2 |
| F057 | Text="{X}" 写成花括号会让属性根本没被应用，表象是表体整块空白而表头正常，几何与数据侧日志全对。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §10 |
| F068 | 预览摘「默认不可见」的名单里漏一个 Id，结果只是多画一根滚动条亮杆与方形把手，判据抓不到。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §6.2 |
| F069 | 前一列右对齐、后一列左对齐时边界上一条空档都不剩，两列的字会贴在一起，而两列合计仍然是对的。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §6.2 |
| F070 | 预览脚本里「按范围染列头次要色」的上界忘随列数改（CX[4]→CX[6]），新增列的列头会留着亮色、与其余列头不一致，且不报错。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §5 |
| f015 | Config.xml 带 <?xml version="1.0" encoding="utf-8"?> 声明时整份被忽略：带声明 AlwaysLoad=0，去掉=3。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §1 L90 |
| f016 | Config.xml 里任何注释（中文或 ASCII）都会让整份文件被忽略。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §1 L91 |
| f017 | Config.xml 根节点写成 <SpriteCategories> 会让整份文件被忽略（那是产出 SpriteData 的节点名）。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §1 L92 |
| f018 | 命中上述杀手时生成器照样 exit 0、照样出图集、毫无提示。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §1 L86 |
| f047 | 索引声明的分类在 GUI/SpriteSheets/<分类>/ 没有图集时，该分类图标全不显示。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §4 L235 |
| f048 | PNG 丢进 GUI/SpriteParts/<分类>/ 但索引里没有该分类时，读不到且不报错。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §4 L236 |
| f053 | 图集容量不够时新 sprite 静默丢失、不报错。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §4 L237 |
| f072 | 换图后预览不变时，先怀疑合成缓存；缓存键曾是「文件名+尺寸」。 | 未标版本 |  | bannerlord-ui-sprite-assets §6 L299 |
| f087 | 图集落到 64x64 这一档时生成器静默写出 0 字节 PNG，仍照常打印 Saving sheet…/COMPLETED/Generator finished successfully. | 未标版本 |  | bannerlord-ui-sprite-assets §8 |
| f090 | 字节数不为 0 只是存在级验收：图集排错位置、塞了空白照样是 4,602 字节。 | 未标版本 |  | bannerlord-ui-sprite-assets §8 / §9.6 L481 |
| brush-03 | Brush 状态名只许 Default/Hovered/Pressed/Disabled/Selected 五个，自造 Normal/Active/Focus 等会静默不生效。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §2「状态名只许这五个」 |
| brush-25 | Color= 与 Brush.FontColor= 走 8 位十六进制 #RRGGBBAA，写 10 位不报错、被静默丢弃并渲染成无色。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §6（第 179-182 行） |
| vm-05 | 刷新重入守卫若不用 finally 复位 _refreshing，该页从此再也刷不动且比崩溃更难查。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §2 修法①（第 50 行） |
| vm-07 | 用下拉项文字回查真实对象会因本地化文本重名而悄悄选错，必须用实例平行表 List<T> _order。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §3（第 93-94 行） |
| vm-11 | 日志必须写在提前返回之前，否则「点击没送到」与「参数不对」在日志里长得一模一样。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §4（第 106-109 行） |
| vm-24 | 花括号那条路只认 DataSource（Data 层 :1187~1192），别的键拿到花括号没有消费者。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §9（第 165-166 行） |
| vm-25 | WidgetTemplate.SetAttributes（PrefabSystem :1976~2017）只有 Default/Constant/Parameter 三分支，花括号不落任何分支。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §9（第 168-170 行） |
| vm-30 | SetWidgetAttributeFromStringAux（PrefabSystem :904~912）用 FindChild 找控件，返回 null 也照收不误、不抛不记。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §10 链条（第 186-187 行） |
| vm-32 | 滚动条若嵌进 ScrollablePanel 内部（孙控件），VerticalScrollbar 解析结果为 null。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §10 链条（第 190 行） |
| vm-35 | 预览图证明不了绑定：预览脚本第②步会把表体擦掉再用自己的演示数据重画，引擎绑没绑在成图上不留痕迹。 | 未标版本 | 是 | bannerlord-gauntlet-ui-viewmodel §11（第 202-208 行） |
| vm-36 | 预览脚本里把 @属性 换成演示值的替代表必须随新绑定属性补登记，否则预览会印出 @TownText 字面量（预览的假信号）。 | 未标版本 | 是 | bannerlord-gauntlet-ui-viewmodel §11 末条（第 214-215 行） |
| vm-37 | 门禁脚本 check_bindings.py 当时只判 @属性 与 DataSource="{属性}" 两种值形状，第三类（非 DataSource 键上的花括号）不落任何分支、一条都不报。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §12（第 219-221 行） |
| vm-39 | 该门禁 stdout 带缓冲，被超时砍掉之后日志是 0 字节，现场像脚本立刻崩了。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §12 末注（第 229-230 行） |
| F011 | 事件绑定前缀是 Command.（Click/TextEntered/FocusGained/FocusLost），写 Event. 前缀静默无效。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §4、§6 坑 |
| F012 | RealText 只读不写回时 VM 侧永远拿到空串，回车提交静默无效，外在表现是发送按钮恒灰。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-input-focus §4 |
| F029 | InformationManager.ShowInquiry 只是 OnShowInquiry?.Invoke 事件转发，无订阅者时调用等于没调（不报错、不开窗）。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §5.4 |
| F035 | 内容完全相同的 inquiry 会撞 Debug.FailedAssert('already present')。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §5.4 |
| F040 | GamepadNavigationIndex 单独写不生效，必须有同级 NavigationScopeTargeter 声明 scope。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-input-focus §6 |
| F043 | 10 位 Color 值被引擎静默丢弃，控件渲染成全白/无色（AWAKE 已犯两次）。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-input-focus §6 |
| F050 | 实测某 VM 的 HasContacts / HasActive / IsLoading 三者通知齐全，但 6 个 Prefab 全 0 消费 ⇒ 界面空白。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §9 两个高频误判 |
| F076 | provider 撞名抛异常后字典停在半填充，排在后面的程序集全部不再被扫描；症状是官方十几个 provider 突然大半找不到，日志只有一条 ArgumentException。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §4 |
| F079 | widget 类必须 public 且恰好一个 (UIContext) 构造函数，否则静默拿不到实例。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-runtime-textures §3 |
| F115 | RichTextWidget 不换行、超出直接切、不出省略号、不报错。 | v1.3.15 |  | bannerlord-ui-copy-parity-gate §0 |
| F116 | 输入框显示的是带千位逗号的串，读回仍用 int.TryParse ⇒ 直接失败，表现为预算悄悄退回默认值。 | v1.3.15 |  | bannerlord-ui-copy-parity-gate §0 |
| F117 | 实测 CNs 的 std_module_strings_xml-zho-CN.xml 是 UTF-16BE；grep 直接搜零命中，易被读成官方没翻译这一条。 | v1.3.15 |  | bannerlord-ui-copy-parity-gate §1② 改之前先量 |
| F121 | 只查新串在编译件里不足以证明旧串已删：新串常是旧串前缀（真实案例：新『 · 最新 』⊂ 旧『 · 最新 第 』）。 | v1.3.15 |  | bannerlord-ui-copy-parity-gate §1⑥ |
| F127 | 含中文或引号嵌套的脚本用 python -c 或 bash 内联会吃引号、坏编码。 | 未标版本 | 是 | bannerlord-ui-copy-parity-gate §4 附：写脚本的纪律 |
| F128 | 同一消息里对同一文件发两条 Edit 会互相覆盖（Claude Code 工具行为）。 | Claude Code harness（未标版本） | 是 | bannerlord-ui-copy-parity-gate §4 |
| F129 | 骨架归一化把数字都归成 #，因此把 20% 改成 25% 时两边骨架都是『保证金 #%』，两边都会绿。 | v1.3.15 |  | bannerlord-ui-copy-parity-gate §1④b 逐字相同 |

## 证据

- **F010** 在横排 ListPanel 里超量追加子控件后观察渲染结果：末位子控件消失且无任何日志或异常。
- **F013** 网格两轴 CoverChildren 但外层容器定高时，超出容器高度的行被裁掉，无日志。
- **F018** 把列宽合计调到超过内宽−让位后观察渲染：仅末列右侧溢出被裁，前几列位置不变。
- **F039** 把超宽字符串放进 RichTextWidget 渲染，只发生裁切，无日志无异常。
- **F043** 在筛选行前部插入一格后渲染预览图，肉眼可见错位；几何与绑定判据均无报警。
- **F045** 同一档下新增两个同标记 ListPanel 后，按 IsVisible=@IsViewTrend 认元素的探针量到了别的块。
- **F046** 审查闸的实现：断言值与期望值来自同一个从上沿累加的函数，因此无法发现推导本身出错。
- **F048** 拿左对齐的名字列去判右对齐的数字列，实测六列中四条报错，属探针自身下标错误。
- **F049** 复盘该轮判据与对照组：Text 骨架全对，Brush.Color 绑反，判据骨架未包含颜色因而漏检。
- **F054** Text="@StockTxt" 少一个字母后构建全过，进游戏该格为空白，无任何告警。
- **F056** 复盘该轮绑定闸：未做「改前坏件必须报错」的反向验证，只验了好件全过。
- **F057** 预制件里把 @ 绑定误写成花括号，渲染后表体无字、表头正常，构建与日志均无异常。
- **F068** 09-20 出的新页图里表体右边竖着一根亮杆，根因是摘除名单漏项；几何判据全绿。
- **F069** 09-20 预览图里出现「还有 3 天天鹅绒…」贴字，检查列宽合计无误。
- **F070** 加列后未同步染色范围上界，渲染图里新增列头颜色与其余不同，无任何判据报警。
- **f015** 二分定位实证：两份内容相同、只差 XML 声明的探针 Config.xml 跑生成器后数 AlwaysLoad。
- **f016** 先怀疑中文注释、改 ASCII 后仍失败，最终定位到 XML 声明；注释本身同为杀手。
- **f017** AWAKE 仓库 Config.xml 实测形态之一，跑生成器后 AlwaysLoad=0。
- **f018** 带声明探针跑完，进程返回 0 且图集正常生成，只有 AlwaysLoad 数变 0。
- **f047** 逐项对索引分类与图集目录的存在性。
- **f048** AWAKE 实测：目录存在不等于已登记，游戏无任何提示。
- **f053** 容量满时加新件的实测现象。
- **f072** 换图不生效的实测归因。
- **f087** 单件 34x40 实测：图集 64x64 → 0 B，日志全绿。
- **f090** 本项目实测：排版器产出的图集内容错误不影响字节数。
- **brush-03** 原版 GUI/Brushes/ 词频实测：1083 / 452 / 430 / 324 / 275；另有 SelectedDisabled、Opened、Invalid 少用。
- **brush-25** 对原版五个官方模块 Prefab 的 Color= 取值长度统计：729 处全 8 位、零例外。
- **vm-05** 源技能给出的重入守卫代码与「不复位 = 此页再也刷不动」的实测结论。
- **vm-07** 源技能对字符串回查路径的风险实证（本地化文本可能重名），并给出实例平行表接法。
- **vm-11** 源技能实测：缺该行即点击没送到；有该行且 index 正确但未换页即页面状态没更新。
- **vm-24** 反编译 Data 层 :1187~1192 的 DataSource 键处理分支，其他键无对应消费者。
- **vm-25** 反编译 PrefabSystem :1976~2017 SetAttributes 的三分支，花括号值不落任何分支。
- **vm-30** 反编译 PrefabSystem :904~912 SetWidgetAttributeFromStringAux 的 FindChild 调用与无 null 检查。
- **vm-32** 由 vm-30/vm-31 的 FindChild 直接子控件语义推得，源技能以真机症状（滚轮无反应）实证。
- **vm-35** 源技能对预览脚本流程的核对：表体区域被擦除并以演示数据重绘。
- **vm-36** 源技能对预览脚本替代表机制的说明与漏登记后果。
- **vm-37** 源技能对 check_bindings.py 判据分支的复核：仅两个分支，第三类被跳过。
- **vm-39** 源技能实测：被砍之后日志文件 0 字节，实为未跑完而非崩溃。
- **F011** SKILL §4/§6 列为踩过并验证的坑；原版用例 IntLayerValueProperty.xml 用 Command.TextEntered。
- **F012** AWAKE 2026-09-13 以『鼠标点发送本来可用 ⇒ 双绑必成立』自证法确认，随后给 NpcDialogue.xml / AwakeMessenger.xml 各加一行 Command.TextEntered。
- **F029** 反编译 TaleWorlds.Library 的 InformationManager 实现（SKILL §5.4 记录）。
- **F035** 反编译 GauntletQueryManager 的重复判定（SKILL §5.4 记录）。
- **F040** SKILL §6 列为踩过并验证的坑；scope 由兄弟节点 NavigationScopeTargeter 提供。
- **F043** SKILL §6 记为实测；6 位等其它长度行为未验。
- **F050** AWAKE 全仓 grep GUI/Prefabs/*.xml 该三个属性名，命中 0。
- **F076** 由 §4 的 Clear 先于循环 + 无 try/catch 推出，SKILL 记为极易认成别的问题。
- **F079** 由 §3 的 GetConstructor 绑定标志（Instance\|Public\|CreateInstance）推出，SKILL 记为静默失败。
- **F115** bannerlord-ui-copy-parity-gate §0 四种静默失效之一（长度）。
- **F116** bannerlord-ui-copy-parity-gate §0 第 4 种静默失效（解析）。
- **F117** SKILL §1② 记录 file 判定为 UTF-16BE 且 grep 零命中。
- **F121** SKILL §1⑥ 记录的真实案例，只查新串会全绿。
- **F127** SKILL §4 附录纪律记录；属否定式断言故列入复核。
- **F128** SKILL §4 记录；该工具属已死 harness。
- **F129** SKILL §1④b 记录的骨架盲区。

---

# API 怪癖（api-quirk）

> 共 44 条。`valid_for` 是这批事实**被测量时**的版本——引用前按当前版本复核。

| id | 事实 | 测量版本 | 待重验 | 出处 |
|---|---|---|---|---|
| F012 | GridWidget 的格位只有同时显式声明 ColumnCount 与 DefaultCellWidth 才可算准，缺任一项格位算式失效。 | 未标版本 |  | bannerlord-gauntlet-ui-prefab-layout §2.6 |
| F029 | 预制件里底栏那颗关闭按钮写的是 Text="@CloseText"，文字要经替换字典解析才得到真实标签。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §6.3 |
| F041 | xml.parsers.expat 的 Start/EndElementHandler 里用 CurrentLineNumber 可给出标签起止行号，行号 1 起、含首尾。 | 未标版本 |  | bannerlord-gauntlet-ui-prefab-layout §5.2 |
| F042 | 预览脚本把整数直接交给绘图库会报 'int' object has no attribute 'split'。 | 未标版本（本机预览脚本） |  | bannerlord-gauntlet-ui-prefab-layout §5 |
| F050 | 阴性对照若把属性插重，会产生 duplicate attribute / ParseError，堆栈看起来像本次改动引入的错。 | 未标版本 |  | bannerlord-gauntlet-ui-prefab-layout §7 |
| F052 | 原版模型 Campaign.Current.Models.* 几乎都是「抽象基类 + 实现类」，默认参数只写在实现类的 override 上。 | 未标版本 |  | bannerlord-gauntlet-ui-prefab-layout §8 |
| F053 | 照实现类签名写 GetDistance(a, b) 会编译报 CS1501「没有采用 2 个参数的重载」，因为 C# 默认参数按调用处静态类型解析。 | 未标版本 |  | bannerlord-gauntlet-ui-prefab-layout §8 |
| F066 | offsets() 从 X0 起累加 MarginLeft 与 SuggestedWidth，返回的坐标已经含左边距，画的时候不能再加 X0。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §6 |
| F067 | ElementTree 里 any(node.iter("Xxx")) 对没有孩子的空元素恒为假，必须写成 any(c.tag == "Xxx" for c in node.iter())。 | 未标版本 |  | bannerlord-gauntlet-ui-prefab-layout §6.5 |
| f043 | K=2 时把 Extend 写成 32 会切穿 34 高的源图（h-e=2<e），PIL 抛 "Coordinate 'lower' is less than 'upper'"。 | 未标版本 |  | bannerlord-ui-sprite-assets §2 |
| f064 | 跨盘用 os.path.relpath 会抛 ValueError: path is on mount 'D:', start on mount 'C:'。 | 未标版本 |  | bannerlord-ui-sprite-assets §6 |
| f076 | Steam 日志原文 No download URL available for app 1393600 and asset type Capsule/Hero/Logo/Header。 | 未标版本 |  | bannerlord-ui-sprite-assets §7 L312 |
| f085 | 生成器参数为 CollectionType= / OutputType=[Engine / Standalone] / SourceDirectory=。 | 未标版本 |  | bannerlord-ui-sprite-assets §8 L328 |
| f086 | stdin 被重定向时收尾的 Press any key 会抛 InvalidOperationException，无害，产物已落盘。 | 未标版本 |  | bannerlord-ui-sprite-assets §8 L329 |
| f141 | PIL ImageDraw.line(width=) 没有方端，24 格里立刻显圆头；方端要用四点多边形并把两端各外推 w/2。 | 未标版本 |  | bannerlord-ui-sprite-assets §12 L642 |
| f142 | PIL ellipse(width=) 各版本描边画在内还是外不一致；环用外圆填充减内圆，弧用环 × 扇形多边形求交得径向切断（方端）。 | 未标版本 |  | bannerlord-ui-sprite-assets §12 |
| f145 | 版式里第二段文字的 x 必须用 draw.textlength() 实测第一段宽度再加间距，硬编码偏移必压字。 | 未标版本 |  | bannerlord-ui-sprite-assets §12 L650 |
| brush-18 | BrushLayer.Rotation 可直接写在 XML：BrushFactory.cs:243-244 的 case "Rotation" 走 Convert.ToSingle。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4.5（第 142-144 行） |
| brush-19 | BrushLayer.Rotation 单位是角度不是弧度。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4.5（第 138-144 行） |
| brush-20 | BrushLayer.Rotation 是 public 可写（BrushLayer.cs:330），且 Brush.Clone()（Brush.cs:398）存在。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4.5（第 145-147 行） |
| brush-22 | BrushLayerState.Rotation 与 BrushAnimationProperty.Rotation 都存在，故 <Style> 层与 brush 动画都能带角度。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4.5（第 148-149 行） |
| brush-23 | Rotation 带 [Editor(false)] 特性，Prefab 编辑器不暴露它。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4.5（第 150 行） |
| vm-06 | SelectorItemVM 在 TaleWorlds.Core.ViewModelCollection，ctor 收 string，显示字在 StringItem，另有 IsSelected。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §3（第 91-92 行） |
| vm-08 | 下标 0 常留作「自动」，解析式为 value <= 0 ? null : _order[value - 1]；上界判据是 value > _order.Count 而非 >=。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §3（第 95-96 行） |
| vm-10 | CommandParameter.Click 传下来的是字符串，由 ViewModel.ExecuteCommand 按 VM 形参类型转换（ViewModel.cs:513）。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §4（第 102-104 行） |
| vm-29 | 值绑定一律 @、DataSource 一律 {列表名}，两者不通用；判据是 grep 'Text="{' 必须 0 条。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §9 守则（第 177-178 行） |
| F006 | GenericPanelGameKeyCategory 的 Exit=Esc、Confirm=Enter/NumpadEnter、SwitchToPreviousTab=Q。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §3 快捷键 |
| F007 | ConversationHotKeyCategory 键位：ContinueKey=Space/Enter/NumpadEnter，ContinueClick=LMB/ControllerRDown。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §3 |
| F008 | ChatLogHotKeyCategory 的 FinalizeChat=Enter 即发送，另有 InitiateAllChat=T、InitiateTeamChat=Y。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §3 |
| F009 | CombatHotKeyCategory 的 13 号键是交互/使用，在 SetupText 里高频出现。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §3 |
| F014 | 层输入遮罩签名为 layer.InputRestrictions.SetInputRestrictions(isMouseVisible, mask)。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §5 |
| F028 | InquiryData 的 ctor 含可选参 Func<(bool,string)> ⇒ net472 下缺显式 System.ValueTuple 引用会报 CS0012。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §5.4 |
| F036 | InformationManager.IsAnyInquiryActive() 是公开静态方法，可作当前有没有弹窗的精确判据。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §5.4 |
| F041 | sage 的 xml_documents_fts 用 unicode61 分词，不在 camelCase 处切词 ⇒ 检索属性名须写全。 | 未标版本 |  | bannerlord-gauntlet-ui-input-focus §6 |
| F045 | 扫 DLL 找 OnShowInquiry 引用方时第一版命中的是 TaleWorlds.Library.dll 自己（事件定义处，恒在）⇒ 判据必须排除定义处所在程序集。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §6 |
| F051 | AwakeLocalization.Resolve(key, 默认中文) 在 key 缺失时回落默认值 ⇒ 可在不改语言文件的前提下先落地文案。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §9 |
| F056 | TaleWorlds.Engine.Texture 共 11 个公开静态方法，覆盖落盘、内存字节、裸像素、资源名、渲染目标、读回像素与导出成图各一档。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §1 |
| F061 | Texture.GetPixelData(byte[]) 与 SaveToFile(path, isRelativePath) 都是公开的 ⇒ 通道序可以实测。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §6 |
| F072 | OnNewModuleLoad() → _areResourcesDirty → 下一 tick 再刷。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §4 |
| F072b | WidgetInfo.Refresh() 是 public static，Bannerlord.UIExtenderEx 就反射调它注入自定义 widget 类型。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §4 |
| F083 | 反编译产物里 WidgetAttributeValueType 只有 Constant / Default / Parameter 三种，搜不到 @ 的处理器。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §3 |
| F086 | 换图时官方写法是成对调用 SetTextureProviderProperty('IsReleased', true) / false（先释放后赋值），见 ImageIdentifierWidget。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §3 |
| F093 | 漏 using TaleWorlds.TwoDimension 时编译器同时报 CS0246 与 CS0534（未实现 OnGetTextureForRender）。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §5.4 |
| F098 | ImageIdentifierTextureProvider.IsReleased 是官方给 image identifier 路线用的，不是通用模板。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §5.2 |

## 证据

- **F012** 格位算式依赖列数与格宽两个显式属性；技能要求体检脚本把带条件的 GridWidget 记为良性。
- **F029** 读预制件该按钮的 Text 属性；直接取 Text 拿去画图会印出 @CloseText 五个字符。
- **F041** 写 expat 栈式处理器实测：out 得到 (标签, 开工行, 收尾行)，跨行自闭合标签也被正确归位。
- **F042** 脚本画表体时把示意数据整数直接塞给绘图库，实测抛出该确切错误串，须先 str()。
- **F050** 对照组把某属性换成另一个时插重，ElementTree 抛 ParseError；须 try/except 并注明来源。
- **F052** 读原版模型源码：抽象基类方法无默认值，实现类 override 上带默认值。
- **F053** 调用处声明的是抽象基类（无默认值），实测编译错误 CS1501；参数个数须按抽象基类数。
- **F066** 读解析模块的 offsets() 实现：起点即 X0，逐个累加边距与宽。
- **F067** 认块时用 iter("Tag") 直接做布尔判断永远认不出空元素，只落到兜底分支且不报错。
- **f043** PIL 报错原文；正解是先把 sprite 放大到目标倍率再九宫格。
- **f064** Python 跨盘 relpath 实测异常原文。
- **f076** 读 Steam 日志的该行原文。
- **f085** 从 SpriteSheetGenerator.exe 字符串中扒到。
- **f086** 沙箱跑生成器的实测现象。
- **f141** 24 格图标首批 14 枚作图实测。
- **f142** PIL 描边行为实测。
- **f145** 标本页版式实测。
- **brush-18** 反编译 BrushFactory.cs:243-244 的 Rotation 分支解析语句。
- **brush-19** BrushFactory.cs:243-244 直接 Convert.ToSingle 无弧度换算；官方用例 MapIncident.xml:76 Rotation="45" 转菱形。
- **brush-20** 反编译 BrushLayer.cs:330 的 setter 与 Brush.cs:398 的 Clone()。
- **brush-22** 反编译 BrushLayerState 与 BrushAnimationProperty 的成员表。
- **brush-23** 反编译 BrushLayer.Rotation 上的 [Editor(false)] 特性标注。
- **vm-06** 反编译 TaleWorlds.Core.ViewModelCollection.dll 的 SelectorItemVM 成员与构造函数签名。
- **vm-08** 源技能给出的解析式与边界判据（_order 平行表，索引 1 基）。
- **vm-10** 反编译 TaleWorlds.Library/ViewModel.cs:513 ExecuteCommand 的参数类型转换逻辑。
- **vm-29** 由 vm-21~vm-25 的两条类型判定路径推出的使用约束与静态判据。
- **F006** 读 TaleWorlds.MountAndBlade 下 *HotKeyCategory.cs：另有 SwitchToNextTab=E/RBumper、ToggleEscapeMenu=Esc/ControllerROption。
- **F007** 读 TaleWorlds.MountAndBlade 下 *HotKeyCategory.cs 原版键位注册。
- **F008** 读 *HotKeyCategory.cs：同类别还有 CycleChatTypes=Tab、SendMessage=ControllerRLeft。
- **F009** 读 TaleWorlds.MountAndBlade 下 *HotKeyCategory.cs 与其 SetupText 调用点。
- **F014** 读 TaleWorlds.ScreenSystem/InputRestrictions.cs 的公开方法签名。
- **F028** AWAKE 实测编译报 CS0012『类型 (, ) 在未引用的程序集中定义』，不是缺 NuGet 包；TaleWorlds.Library 走独立 4.0.0.0 标识。
- **F036** 读 TaleWorlds.Library 的 InformationManager 公开成员。
- **F041** SKILL §6 记录：GamepadNavigationIndex 不能只搜 Navigation。
- **F045** SKILL §6 记录：字节命中 ≠ 订阅方，第一版判据恒为真。
- **F051** 读 AWAKE 的 AwakeLocalization 实现（SKILL §9 记录）。
- **F056** 1.3.15 反编译一手核对 TaleWorlds.Engine.Texture 全部公开静态成员，逐个数出 11 个。
- **F061** 读 TaleWorlds.Engine.Texture 公开成员表（SKILL §6 记录）。
- **F072** 反编译 WidgetInfo 的脏标记路径（SKILL §4 记录）。
- **F072b** 反编译 WidgetInfo.Refresh 的可见性与 Bannerlord.UIExtenderEx 的调用点（SKILL §4 记录）。
- **F083** 对反编译出的 GauntletUI 工程树搜索 WidgetAttributeValueType 成员。
- **F086** 反编译 ImageIdentifierWidget 的换图路径（SKILL §3 记录）。
- **F093** SKILL §5.4 记录实际编译输出：CS0246 找不到 TwoDimensionContext，CS0534 未实现抽象成员。
- **F098** SKILL §5.2 记录其用途限定。

---

# 否定式断言（negative-claim）

> 共 56 条。`valid_for` 是这批事实**被测量时**的版本——引用前按当前版本复核。

| id | 事实 | 测量版本 | 待重验 | 出处 |
|---|---|---|---|---|
| F001 | Gauntlet 全库约 2.5 万处 WidthSizePolicy 只有 Fixed / StretchToParent / CoverChildren 三种取值，没有 =@绑定 这种写法。 | 未标版本 | 是 | bannerlord-gauntlet-ui-prefab-layout §2.5 |
| F011 | GridWidget 不是流式容器，按 MarginLeft 累加的 offsets() 去量它的格位是错的。 | 未标版本 | 是 | bannerlord-gauntlet-ui-prefab-layout §2.6 |
| F044 | 离线工作台不认 @ 绑定，也无视 IsVisible=false。 | 未标版本（本机工作台） | 是 | bannerlord-gauntlet-ui-prefab-layout §5.1 |
| F063 | 原版绑定没有取反写法，同矩形换出场那一侧必须为每一档各写一个布尔；漏一句 OnPropertyChangedWithValue 的症状是换了档表还在原地、两套叠在一起。 | 未标版本（AWAKE 项目） | 是 | bannerlord-gauntlet-ui-prefab-layout §6.4b |
| f002 | 7 个官方模块都没有 GUI/SpriteParts/、GUI/SpriteSheets/、AssetSources/、Assets/ 目录。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §1 L57 |
| f006 | SubModule.xml 里完全没有资产/资源声明节点，资产全靠路径约定自动发现。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §1 L59 / §9.5 L474 |
| f014 | SandBox 15 个界面分类一个都没用 AlwaysLoad，界面分类按需加载。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §1 L79 |
| f021 | 7 个官方模块没有一个 ship Config.xml，但 NativeSpriteData.xml 里有 4 处 <AlwaysLoad />。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §1 L148 |
| f022 | 全库 5 份 Config.xml 里 SpriteSheetSize 出现 0 次；写进 Config.xml 不报错也不生效。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §1 |
| f027 | tools/sync_module.ps1 只管 7 个 Prefab，不碰 SpriteParts/，所以不存在同步覆盖风险。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §1 L127 |
| f033 | 旧结论"边距只能写 Brush、sprite 侧写 Extend 都错"是错的：在 *SpriteData.xml 搜 Extend 得 0 命中只是搜错 token。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §2 L152 |
| f045 | 面板 Prefab 常只写 Brush= + 文字、没有任何 Sprite=，说明它没用图标。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §3 L228 |
| f050 | 那 28 张 PNG 没有任何 SpritePart / GenericSprite / Brush 引用，属于"做出了但没接线"。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §4 L236 |
| f056 | 本机只有 bin/Win64_Shipping_Client、没有 bin/Win64_Shipping_wEditor，走不了官方生成流程。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §4 L241 |
| f059 | 6 个 Prefab 里 grep '="12"\\|"24"' 零命中，"12/24 系"说法从未实证。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §5 L254 |
| f066 | "官方 UI 全在 .tpac 内无法旁窥"已作废：官方 UI 贴图能抠出来。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §6 L283/L291 |
| f073 | app 1393600 不是 DLC：dlc/261550 只有 4456490/2927200/2194520/2240110 四条，没有它。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §7 L310 |
| f074 | app 1393600 没有商店页，访问 store/app/1393600 直接跳回商店首页。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §7 L311 |
| f075 | app 1393600 没有封面图：librarycache/1393600/ 只有一张图标 jpg，无 header/library_hero/library_600x900/logo。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §7 L312 |
| f098 | 原版没有 GUI/SpriteSheets/ 目录；Native/GUI 只有 Brushes/Fonts/Prefabs/NativeSpriteData.xml。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §9.1 |
| f101 | 那 2 个有 GUI/SpriteSheets/ 的模组（AnimusForge / Leverage）没有任何一个有成功记录。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §9.1 L423 |
| f104 | 把 ui_leverage_1.png 摆进 GUI/SpriteSheets/ui_leverage/ 后启动，日志仍报 Cannot find texture: ui_leverage_1。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §9.1 |
| f107 | AnimusForge 在本机留存的 6 份启动日志里模块列表一次都没出现（5 次含 Leverage、1 次含 AWAKE），它没被加载。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §9.1 |
| f124 | resource.show_resource_browser 在 107 个托管 dll 里零命中，它在 native TaleWorlds.Native.dll，没有可脚本化的 Import 入口。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §9.7 L492 / §9.9 L567 |
| f126 | tpac 路径之后「尺寸/格式区」的可变长度对不上（两个真实样本 Δ 分别为 22 / 25），对齐规则未吃透，不要手写 tpac。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §9.8 L496 |
| f128 | 这种纯元数据壳能否被运行时接受尚未验证。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §9.4 |
| f130 | SubModule.xml 不声明资产包、也不声明图集，全靠目录约定自动发现。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §9.5 L474 |
| f134 | NavalDLC 没有 SubModule.xml、只有 EmAssetPackages/，但仍算官方。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §10 L582 |
| f137 | 用 grep 判官方两个方向都会错，只能认 7 个白名单。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §10 |
| f140 | AI 出的整体概念图只能当母题/语言参照，不能直接当 sprite（尺寸、九宫格、背景都不对）。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §11 L631 |
| brush-02 | 全库 0 个模组在 SubModule.xml 里声明过 Brush 或 SpriteData。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §1（第 31 行） |
| brush-16 | NineRegionSprite 消费代码在 TaleWorlds.TwoDimension，本机 10 个反编译程序集里没有、NineRegion 零命中，两套机制优先级未验。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4（第 126-127 行） |
| brush-24 | 旋转的支点位置（中心或左上角）以及旋转后是否被 ClipContents 裁剪，均未验证。 | 未标版本 | 是 | bannerlord-gauntlet-ui-brushes §4.5（第 155 行） |
| brush-32 | 真机 E4（贴图是否真被引擎加载）未验，本地预览不等于真机渲染。 | 未标版本 | 是 | bannerlord-gauntlet-ui-brushes §9（第 221 行） |
| brush-40 | 此前「写在 sprite 索引上的 Extend 一定无效」是错的，2026-09-19 已勘误收回。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4（第 111 行） |
| vm-02 | 点路径 @A.B / {A.B} 在原版 Prefab 里零命中，写了会整块空白。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §1（第 20-21 行） |
| vm-26 | 原版 Modules\SandBox\GUI\Prefabs 里 Text="{...}" 一处都没有，Text="@..." 到处是。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §9（第 172 行） |
| vm-40 | 验证门禁是否有牙必须两向都跑：改之前坏件要报错、改之后好件要全过；只有好件全过是假绿。 | 未标版本 |  | bannerlord-gauntlet-ui-viewmodel §12（第 224-226 行） |
| F001 | sage 反编译索引只收 10 个程序集，TaleWorlds.InputSystem 不在其中。 | 未标版本 | 是 | bannerlord-gauntlet-ui-input-focus §1 源码在哪（只读） |
| F017 | SortedLayers 把 TopScreen.Layers 与全部 GlobalLayer 合并按 Order 排序 ⇒ 全局层与屏上层共用一个序，「全局层永远在下」不成立。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-input-focus §5.1 |
| F025 | 非焦点层的 IsKeyPressed(Escape) 恒为 false（不是没触发，是问了也是 false）。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-input-focus §5.3 |
| F026 | 因按键按层认领，同一帧不会出现弹窗取消与自己界面一起关的 Esc 双触发。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-input-focus §5.3 |
| F038 | FocusRequestId / AutoFocus / SubmitOnEnter 不是引擎内置属性：全库 grep TaleWorlds.GauntletUI 为 0 命中。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-input-focus §6 坑 |
| F046 | 按类型名扫字节找不到构造点（new X() 走元数据 token，不重复类型名字符串），须另跑 ilspycmd 找 new。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-input-focus §6 |
| F047 | Gauntlet 的 XML 绑定不支持取反或表达式，只有 @属性名 一种写法；实测全 Modules 扫描取反/逻辑表达式命中数为 0。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-input-focus §9 数据绑定 |
| F059 | CreateFromMemory 具体接受哪些格式族（PNG / JPG / BMP）仍未证。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-runtime-textures §6 |
| F060 | CreateFromByteArray(data, w, h) 的通道序（RGBA / BGRA）与行序仍未证。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-runtime-textures §6 |
| F063 | Texture.LoadTextureFromPath(fileName, folder) 的 folder 语义未验。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-runtime-textures §6 |
| F064 | Texture.SaveToFile(path, isRelativePath) 能否在无渲染上下文的进程里用未验，这决定离线把 3D 肖像渲成 png 可不可行。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-runtime-textures §6 |
| F067 | 读写两侧共用同一套平台路径解析（Texture.CreateTextureFromPath 与 FileHelper 同源）⇒ 手拼绝对路径必然分叉。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-runtime-textures §6 |
| F100 | Texture.Release() 第一句就摸 RenderTargetComponent.OnTargetReleased()，对文件来的纹理那东西是不是 null 未验。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-runtime-textures §5.2 |
| F101 | 反复换图是否累积 GPU 纹理未验。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-runtime-textures §5.2 |
| F104 | ilspycmd 是原生 exe，不认 MSYS 的 /d/ 路径，必须传 D:\... 否则静默不产出。 | 未标版本 | 是 | bannerlord-gauntlet-ui-runtime-textures §7 取证手法（本机） |
| F106 | tr -c '[:print:]' 这类字节探针实测会漏：阳性对照 GauntletMovie 给 0 命中，而它确实存在。 | 未标版本 | 是 | bannerlord-gauntlet-ui-runtime-textures §7 |
| F107 | 恒 0 命中的探针等于没验过，必须先做阳性对照再信结果。 | 未标版本 | 是 | bannerlord-gauntlet-ui-runtime-textures §7 |
| F108 | bash 里 grep 的花括号 {n,m} 会被 shell 吃掉。 | bash（本机已无） | 是 | bannerlord-gauntlet-ui-runtime-textures §7 |

## 证据

- **F001** 对全库预制件统计 WidthSizePolicy 取值分布（约 2.5 万处），未发现绑定式取值；技能自述的普查结果。
- **F011** 对比流式容器的 offsets() 算法与 GridLayout 的格位算式，两者对 GridWidget 结果不一致。
- **F044** 预览脚本里新增列若不物理摘掉或不落成字面量，@ 绑定与 IsVisible=false 都不会生效。
- **F063** 记账页注释记录的坑：绑定表达式不支持取反，漏通知后两档控件同时可见。
- **f002** 同上逐目录枚举，7 个官方模块四类目录全部不存在。
- **f006** 读官方与模组 SubModule.xml，未见资产包或图集声明节点。
- **f014** grep AlwaysLoad SandBoxSpriteData.xml 得 0 命中。
- **f021** 逐官方模块找 Config.xml 全无；grep NativeSpriteData.xml 得 4 处 AlwaysLoad。
- **f022** 逐个查 5 份 Config.xml（含官方 LauncherGUI 那份）搜 SpriteSheetSize，零命中。
- **f027** 读 sync_module.ps1 的托管文件清单。
- **f033** sprite 侧机制名叫 <NineRegionSprite> 不叫 Extend，官方实际大量使用。
- **f045** grep Prefab 的 Sprite=/Brush= 结果对照。
- **f050** 在索引与 Brushes 里搜这些 sprite 名，零引用。
- **f056** ls -d <游戏>/bin/Win64_Shipping_wEditor 无结果。
- **f059** 在 6 个 Prefab 上跑该 grep，零命中。
- **f066** 2026-09-14 夜抠出官方材质贴图成品，推翻原否定结论。
- **f073** 访问 store.steampowered.com/dlc/261550 得 4 条，无 1393600。
- **f074** Tool 类 app 不设商店页，访问实测跳转。
- **f075** 枚举 librarycache 目录，对照游戏本体五项俱全。
- **f098** 枚举 Modules/Native/GUI/ 实际内容。
- **f101** 启动日志与资源加载结果核查。
- **f104** [17:21:23.026] 日志原文；目录 mtime 17:14，文件已就位 7 分钟。
- **f107** 逐份查启动日志的模块列表；口径仅限留存日志（七月日志已轮转）。
- **f124** grep 107 个托管 dll 零命中。
- **f126** 两样本字段长度差实测。
- **f128** 技能自标「没验过，别当结论」。
- **f130** 读各模组 SubModule.xml 未见相关节点。
- **f134** 枚举 NavalDLC 目录实测。
- **f137** f135/f136 两个反例实测。
- **f140** AI 生图做贴图的实测边界。
- **brush-02** 反编译 BrushFactory 加载路径后对全库模块 SubModule.xml 统计，Brush/SpriteData 声明数为 0。
- **brush-16** 在本地 10 个反编译程序集里检索 NineRegion / LeftWidth，命中数为 0。
- **brush-24** 源技能自述「仍未验」，无实测记录。
- **brush-32** 源技能自述未验；本地预览与真机渲染无对照记录。
- **brush-40** 源技能勘误注记，依据是 NativeSpriteData.xml 的 NineRegionSprite 注册表机制存在。
- **vm-02** 对原版 GUI/Prefabs 检索点路径绑定，命中数为 0。
- **vm-26** 对原版 SandBox GUI/Prefabs 检索 Text="{ 与 Text="@ 的命中对比。
- **vm-40** 源技能对本次漏检的复盘：单向（好件全过）通过导致了假绿。
- **F001** SKILL §1 明标；sage 索引覆盖面属工具属性，与游戏版本无关，但为否定式断言故列入复核。
- **F017** 2026-09-24 查证 TaleWorlds.ScreenSystem/ScreenManager.cs 的 SortedLayers 构造逻辑。
- **F025** 由 §5.3 的 _usedInputs 置位条件推出，SKILL 未记为逐帧实测。
- **F026** SKILL §5.3 记为好处，由 _usedInputs 机制推出。
- **F038** 对反编译出的 TaleWorlds.GauntletUI 全库 grep 三个属性名，命中数为 0。
- **F046** SKILL §6 同族记录：字节扫描对构造点恒不命中。
- **F047** 全 Modules 扫描 --include=*.xml，@ 后含 ! & \| < > 的绑定表达式命中数为 0。
- **F059** SKILL §6：那串字节来自网络头像，未追到生产者。
- **F060** SKILL §6 明标仍未证，并建议整条绕开改用编码文件字节。
- **F063** SKILL §6 列为未验证项。
- **F064** SKILL §6 列为未验证项。
- **F067** SKILL §6 记录两侧同源，故一律用 PlatformFilePath。
- **F100** 反编译 Texture.Release 方法体 + SKILL §5.2 明标未验。
- **F101** SKILL §5.2 明标待验，要求量产出后再下结论。
- **F104** SKILL §7 取证手法记录实测静默失败。
- **F106** SKILL §7 记录阳性对照失败（GauntletMovie 确实在 TaleWorlds.Engine.GauntletUI.dll），探针不可信。
- **F107** SKILL §7 结论，由 F106 的对照实验得出。
- **F108** SKILL §7 记录；本机已无 bash 工具。

---

# 版本事实（version-fact）

> 共 2 条。`valid_for` 是这批事实**被测量时**的版本——引用前按当前版本复核。

| id | 事实 | 测量版本 | 待重验 | 出处 |
|---|---|---|---|---|
| f079 | War Sails Modding Kit(4456490) 是 DLC 附加包，需 War Sails 扩展(2927200, $24.99)，配套 BL v1.4.8，装了给不了编辑器。 | v1.4.8 | 是 | bannerlord-ui-sprite-assets §7 L319 |
| vm-19 | 源技能声称其规则来自本机反编译源码 v1.3.15.110062。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel 头部（第 9 行） |

## 证据

- **f079** 官方原文称其提供的是在 Modding Kit 内使用 War Sails 内容的文件。
- **vm-19** 源技能头部自述版本号 v1.3.15.110062，未附版本读取命令或文件证据。

---

# 路径事实（path-fact）

> 共 32 条。`valid_for` 是这批事实**被测量时**的版本——引用前按当前版本复核。

| id | 事实 | 测量版本 | 待重验 | 出处 |
|---|---|---|---|---|
| F061 | 记账页「家底／收支」两档共用同一套四列 ItemTemplate，只换 @LedgerHead1..4 与行内容。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §6.4b |
| F062 | 记账页「表 ⇄ 图」用同一矩形两套控件出场（IsLedgerTable / IsLedgerChart），行情页两档用 IsViewCompare / IsViewTrend。 | 未标版本（AWAKE 项目） |  | bannerlord-gauntlet-ui-prefab-layout §6.4b |
| f001 | 7 个官方模块 ship 了 GUI/<模块名>SpriteData.xml、GUI/Brushes/、GUI/Prefabs/、AssetPackages/*.tpac 四类资产。 | 未标版本 |  | bannerlord-ui-sprite-assets §1 |
| f005 | Native/LauncherGUI/ 是官方唯一 ship 完整制作树的样本（SpriteParts+SpriteSheets+Brushes+Prefabs+Fonts）。 | 未标版本 |  | bannerlord-ui-sprite-assets §1 L61 |
| f026 | AWAKE 仓库 GUI/SpriteParts/Config.xml 与游戏目录 Modules/AWAKE/ 那份是内容不同的两个文件，部署那份才是可用形态。 | 未标版本 |  | bannerlord-ui-sprite-assets §1 |
| f061 | 三个根：乙方 C:/Users/26811/WorkBuddy/LocalAIPictureGeneration/、甲方 D:/AWAKE-Dev/AWAKE/、部署 Modules/AWAKE/。 | 未标版本 |  | bannerlord-ui-sprite-assets §6 |
| f062 | 交付产物落 D:/AWAKE-Dev/AWAKE/tools/awake-art-lab/out/<批次>/delivered/。 | 未标版本 |  | bannerlord-ui-sprite-assets §6 L269 |
| f063 | awake-art-lab 的 out/ 被 .gitignore 排除，交付产物不在 git 里、要直接从磁盘取。 | 未标版本 |  | bannerlord-ui-sprite-assets §6 L270 |
| f065 | UI 美术文档全在 D:/AWAKE-Dev/AWAKE/docs/，含控件施工契约 UI-CONTROL-CONTRACT-20260914.md（11 条 Brush + 缺口 10 件）。 | 未标版本 |  | bannerlord-ui-sprite-assets §6 |
| f069 | 官方材质贴图成品在 projects/awake/library/official_ui/（本地生图仓库）。 | 未标版本 |  | bannerlord-ui-sprite-assets §6 L292 |
| f070 | tools/awake-ui-lab/preview/ui_sprite_atlas.py 支持直读 GUI/SpriteParts/**/<名>.png，免图集、免索引、免 Modding Kit。 | 未标版本 |  | bannerlord-ui-sprite-assets §6 L296 |
| f071 | 旧法仍可用：把 PNG 丢进 out/atlas/custom/<sprite 名>.png 覆盖原版。 | 未标版本 |  | bannerlord-ui-sprite-assets §6 L298 |
| f110 | SpriteSheetGenerator.Library.dll 只存在于 bin/Win64_Shipping_wEditor/，所以 GUI/SpriteSheets/ 是生成器的输出目录。 | 未标版本 |  | bannerlord-ui-sprite-assets §9.9 |
| f147 | Config.xml 是共享文件；tools/sync_module.ps1 的 $managedGuiFiles 白名单若要覆盖美术资产，得由 UI/codex 侧扩。 | 未标版本 |  | bannerlord-ui-sprite-assets §12 L659 |
| f148 | 参考实现 AWAKE/tools/awake-art-lab/make_icon_system.py 含几何层、14 枚图标定义、3 张无缝贴图与标本页版式。 | 未标版本 |  | bannerlord-ui-sprite-assets §12 L661 |
| brush-28 | AWAKE 预览器 awake-ui-lab 能直读 GUI/SpriteParts/**/<名>.png，免图集、免索引、免 Modding Kit 即可预览自绘贴图。 | 未标版本 | 是 | bannerlord-gauntlet-ui-brushes §7A（第 188 行） |
| brush-29 | AWAKE 预览器把本模块自有 GUI/Brushes/ 排在加载最前，且报告口径拆为自有命中/原版命中/未命中三项。 | 未标版本 | 是 | bannerlord-gauntlet-ui-brushes §7A（第 189-190 行） |
| brush-36 | 官方标准按钮样板 Native/GUI/Prefabs/Standard/Standard.Button.xml 尺寸 227×40、Brush="ButtonBrush1"。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4.7（第 162、164 行） |
| vm-18 | ilspycmd 路径必须写 Windows 形式 D:/…，给 /d/… 它会报不存在。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel 头部（第 12 行） |
| vm-20 | 反编译对象为 TaleWorlds.GauntletUI.dll（控件）、TaleWorlds.Library.dll（ViewModel）、Core.ViewModelCollection.dll。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel 头部（第 13 行） |
| F002 | TaleWorlds.InputSystem.dll 的 InputContext 含 CanUse / IsKeyPressed / RegisterDownKeys 实现。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §1 |
| F003 | TaleWorlds.Engine.GauntletUI.dll 的 GauntletLayer 含 HitTest / FocusTest 实现，sage 索引未收该程序集。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §1 |
| F027 | InformationManager 与 InquiryData 都定义在 TaleWorlds.Library，引用该程序集即可。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §5.4 原版确认弹窗 |
| F052 | 本仓正向绑定先例：AwakeMessenger.xml 角色卡面板 IsVisible=@SelectedCard.Visible（子 VM 暴露正向 Visible）。 | v1.3.15 |  | bannerlord-gauntlet-ui-input-focus §9 本仓既有正向绑定先例 |
| F065 | Application 展开到 C:\ProgramData\<应用名>\，User 到 Documents\<应用名>\，Temporary 到 Documents\<应用名>\Temp\。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §6 平台路径 |
| F066 | PlatformFilePath 最终路径 = Path.Combine(展开基目录, directoryPath.Path)。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §6 |
| F109 | TextureProviderFactory、TextureWidget、TextureProvider、WidgetInfo 位于 TaleWorlds.GauntletUI.dll。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §7 |
| F110 | EngineTexture、UIResourceManager、GauntletLayer 位于 TaleWorlds.Engine.GauntletUI.dll。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §7 |
| F111 | WidgetFactory 位于 TaleWorlds.GauntletUI.PrefabSystem.dll。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §7 |
| F112 | TwoDimension.Texture 位于 TaleWorlds.TwoDimension.dll。 | v1.3.15 |  | bannerlord-gauntlet-ui-runtime-textures §7 |
| F119 | 格式串通常不在代码里，而在 Modules\Native\ModuleData\module_strings.xml（英文底）与 Languages\<语言>\*.xml（译文）。 | v1.3.15 |  | bannerlord-ui-copy-parity-gate §1② |
| F126 | 量宽用游戏真字体（Modules\Native\GUI\Fonts\...，回退 simkai.ttf）加该格真字号；拿一个字号量全部会假通过或假报警。 | v1.3.15 |  | bannerlord-ui-copy-parity-gate §1② |

## 证据

- **F061** 读记账页预制件：两档列数相同、每格类型相同，共用模板并替换列头绑定。
- **F062** 读预制件里两页的分档绑定名与出场结构。
- **f001** 逐个查本机 Modules 下 7 个官方模块目录，按路径存在性判定（§10 第二步脚本口径）。
- **f005** 枚举 7 个官方模块，只有 Native/LauncherGUI 具备 GUI/ 形状的完整目录。
- **f026** 两处 md5 不同：仓库那份带声明+注释+根写 SpriteCategories，部署那份是干净 <Config>。
- **f061** 混用三根导致在甲方仓库凭空建目录后实测厘清；部署根＝<游戏根>/Modules/AWAKE/。
- **f062** 技能记录的落地坐标。
- **f063** 读该 lab 的 .gitignore。
- **f065** 技能列出的 7 份 docs 清单。
- **f069** 技能记录的抠图产物落点。
- **f070** 2026-09-13 起本地预览实测能画出自绘贴图。
- **f071** 技能记录的旧预览落点。
- **f110** 全盘搜该 DLL 的落点。
- **f147** 读 sync_module.ps1 的白名单变量。
- **f148** 读该脚本结构。
- **brush-28** AWAKE 项目 tools/awake-ui-lab/ 的 sprite 解析实现；来源为项目内工具而非引擎反编译。
- **brush-29** AWAKE 项目 tools/awake-ui-lab/ 的 Brush 加载顺序与报告口径实现。
- **brush-36** 读 Native/GUI/Prefabs/Standard/Standard.Button.xml 的尺寸与 Brush 属性。
- **vm-18** 源技能自述实测：~/.dotnet/tools/ilspycmd.exe 收 /d/… 形式路径报不存在。
- **vm-20** 源技能头部列出的反编译目标程序集清单，命令为 ilspycmd -t <全名> <DLL>。
- **F002** 2026-09-24 实测 ilspycmd -t TaleWorlds.InputSystem.InputContext 可出完整方法体；该 DLL 在 <game>/bin/Win64_Shipping_Client/。
- **F003** §1 明示索引未收 TaleWorlds.InputSystem 与 TaleWorlds.Engine.GauntletUI；同法用 ilspycmd 可直查。
- **F027** 读 TaleWorlds.Library.dll 的类型归属（SKILL §5.4 记录）。
- **F052** 读 AWAKE/GUI/Prefabs/AwakeMessenger.xml。
- **F065** 实测 PlatformFileHelperPC.GetDirectoryFullPath；官方 OnlineImageTextureProvider 用的是 Application。
- **F066** 实测 PlatformFileHelperPC.GetDirectoryFullPath 拼接逻辑。
- **F109** SKILL §7 关键类型坐标，反编译核对。
- **F110** SKILL §7 关键类型坐标，反编译核对。
- **F111** SKILL §7 关键类型坐标，反编译核对。
- **F112** SKILL §7 关键类型坐标，反编译核对。
- **F119** SKILL §1② 记录格式串来源文件位置。
- **F126** SKILL §1② 记录量宽所用字体与字号来源。

---

> **待重验**：本文件中标 `待重验=是` 的共 **150** 条。
> 完整队列（跨全部领域）见 `AWAKE/docs/reference/reverify-queue.md` —— 本文件不重复内联，避免同一事实出现两次。

---

## 程序要点（从既有 skill 提取）

**仍可执行**

- 用 ElementTree 解析预制件，打印列头与行模板每格 SuggestedWidth 及合计，两行必须相同（§3）
- 量宽取极值样本（最长译名、99999、+99999、9999），按该列真字号量并留 8 px 余量（§4）
- 多处改动走脚本：按行号建 OPS 表、从下往上执行、逐处断言，收尾做结构/接线/硬账三检（§5.1）
- 搬或摘子树用 xml.parsers.expat 量起止行，并把片段单独解析一遍当阴性对照（§5.2）
- 预览几何从预制件解析，预览脚本与几何闸 import 同一解析模块，不手写名单（§6）
- 收尾看产物时间戳是否更新，并确认全套判据与阴性对照仍能报红（§6.5、§9）
- 用 pwsh 的 Select-String/grep 工具从 Prefab 抓 Sprite=/Brush=，替代 bash grep
- 九宫格验收：按 SpriteData 的 SheetX/SheetY/W/H 切槽，与 SpriteParts 源图逐像素比，差须为 0
- 生成器只在独立根沙箱跑，工作目录设为 bin/Win64_Shipping_wEditor，产物手工搬回仓库
- 改 Config.xml 后重跑生成器，用 grep -c AlwaysLoad 核对数量＝声明的分类数
- 预览换图不生效先清合成缓存；或让预览脚本直读 GUI/SpriteParts/**/<名>.png
- 图标按 24 格作图、12x 超采样 LANCZOS 降到 48/72，纯白剪影透明底
- 新建 Modules/<模块>/GUI/Brushes/<任意名>.xml 即生效；无需在 SubModule.xml 声明 Brush/SpriteData。
- Extend* 只在 <Layers> 写一次，状态 <Style> 里只覆盖 Sprite；四态共用同一画布与内容框。
- 按钮默认不写 ButtonType，避免引擎在命令跑完后改写 IsSelected；设置类按钮才用 Radio。
- 绑定值一律 @属性、DataSource 一律 {列表名}；门禁对每种值形状给键白名单，名单外报错。
- 滚动条放 </ScrollablePanel> 之后、同一 <Children>，属性写 "..\Name"。
- 改完按 编译 → 部署 → 反编译部署件做内容级校验 → 才进游戏；门禁挂后台跑再读日志。
- 用 ilspycmd -t / -p 直查未入 sage 索引的程序集（InputSystem、Engine.GauntletUI），路径传 D:\。
- 读原版 Prefab（Modules/{Native,SandBox,SandBoxCore,...}/GUI/Prefabs）找焦点、导航、弹窗样板。
- 自定义 TextureProvider + TextureWidget：类名带模块前缀、构造 public (UIContext)、两边属性同名。
- 运行时图片走 落盘 png → CreateTextureFromPath → EngineTexture → TwoDimension.Texture。
- 界面接没接输入：grep GamepadNavigationIndex\|NavigationScopeTargeter\|IsFocusable。
- 判据一律配阴性对照（--neg），并在部署件上核新串、旧串、阳性、阴性四件事。

**已失效**（因 harness 变更）

- grep -rn "925\\|340" tools/ docs/ 搜第二处真值（§5）——DSH 无 bash，改用 grep 工具
- ls -l 看预览图落盘时间戳（§6.3）——DSH 无 bash，改用 pwsh Get-ChildItem
- 直接跑 python 预览/探针脚本（§6、§7）——本机无 python 保证，须先确认解释器
- A/B 量画面时用带图像库的解释器起子进程（§2.5）——DSH 无 bash 子进程编排
- 照 tools\_add_view_switch.py 等旧工作区样板脚本直接跑（§5.1）——路径与运行方式需重写
- 按 pwsh 7 语法写脚本步骤（§5.1）——本机 pwsh 工具实为 Windows PowerShell 5.1
- 无参数直跑 SpriteSheetGenerator（会扫全库改写他人产物，必须沙箱）
- bash 版 grep -o … \| /usr/bin/sort -u 与 for f in *.xml 反查 Prefab（本机无 bash）
- 用 md5sum 比仓库与部署两份 Config.xml（本机无 bash/md5sum）
- 用 git-bash cp -r 拷 wEditor 沙箱（改用 robocopy /E）
- 用 ls -d bin/Win64_Shipping_wEditor 判断有无 Modding Kit（本机无 ls）
- 手写 tpac 壳（字段对齐未吃透，技能已明确不做）
- grep -o 'Brush="[^"]*"' *.xml 查面板引用了哪些 brush（在 Modules/<模块>/GUI/Prefabs 下）。
- bash grep 查残留 sprite：grep -o 'Sprite="[^"]*"' *.xml \| /usr/bin/sort -u。
- python 单行验证 DLL 字符串：'某句中文'.encode('utf-16-le') in open(dll,'rb').read()。
- 用 ~/.dotnet/tools/ilspycmd.exe -t <全名> <DLL> 直调反编译（本机无 dotnet 工具链确认）。
- 用 check_bindings.py 做绑定门禁并打印「绑定自检全过 OK」。
- 用 cmp 对比部署件做内容级校验。
- bash 与 grep -r/-c/-rhoE 流水线：本机无 bash 工具，改用 pwsh 与 grep 工具。
- 用 python -c 或 bash 内联跑含中文/引号的脚本（吃引号、坏编码）：改落 .py 再跑。
- tr -c '[:print:]' 字节探针与 bash 花括号 {n,m} 正则：前者实测漏报，后者被 shell 吃掉。
- 同一消息里对同一文件发两条 Edit（Claude Code 工具语义，DSH 无该工具）。
- Codex shell / apply_patch / Task / TodoWrite 相关步骤：本 harness 不存在这些工具。
- strings -e l 读 .NET 串堆（读不出 UTF-16LE）：改按字节在部署 DLL 里找。
