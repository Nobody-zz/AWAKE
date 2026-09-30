# 待重验队列 · 全部领域

> 由 `AWAKE/tools/gen_skill_refs.py` 从既有 skill 的发现提取生成。**不要手改**——改了会被下次重跑覆盖。
>
> **这是跨 agent 的共享知识**（见 `AWAKE/AGENTS.md`〈跨 agent 共享知识〉）。任何 harness 的 agent 都应读这里，不要各自维护私有副本。
>
> 共 **1182** 条发现，其中 **367** 条标了 `needs_reverify`（测量于旧版本，引用前复核）。

## 头部规则

1. **`valid_for` 是测量版本，不是当前版本。** 本机游戏已从 v1.3.15 升到 **v1.4.8**；凡 `valid_for=v1.3.15` 的，引用前先复核。
2. **`未标版本` 的条目 = 提取时未记录版本**，请**按 v1.3.15 对待**（即同样需要复核）。
3. **否定式断言（`negative-claim`）风险最高**——最容易因升级变成假话。先看文末〈待重验队列〉。
4. **`出处` 只到「旧 skill 名 # 小节名」一级。** 旧 skill 在各 harness 的私有目录里（`~/.workbuddy/skills` 等），**不在本仓库**，故不保留行号——留着是假的可点性。
5. 本文件是**世界里的事实**，不是程序。怎么做任务看对应 skill 或 `AWAKE/AGENTS.md`。

# 待重验队列

> 共 367 条。`valid_for` 是这批事实**被测量时**的版本——引用前按当前版本复核。

| id | 事实 | 测量版本 | 待重验 | 出处 |
|---|---|---|---|---|
| bgda-021 | localization_entries 主键是 (language,stringId,filePath)，没有 key 列。 | v1.3.15 | 是 | bannerlord-game-data-access#陷阱 |
| bgda-022 | bannerlord_items 没有 id 列，主键是 entityId。 | v1.3.15 | 是 | bannerlord-game-data-access#英雄名中文查法 |
| bgda-023 | bannerlord_heroes 没有 name 列。 | v1.3.15 | 是 | bannerlord-game-data-access#英雄名中文查法 |
| bgda-025 | 英雄名不能按 Hero.name.<heroId> 之类规则拼 stringId 查，实测全返回 0 行。 | v1.3.15 | 是 | bannerlord-game-data-access#英雄名中文查法 |
| bgda-026 | 索引库没有 EN 语言，实际为 TR/CNs/CNt/BR/DE/FR/IT/JP 等。 | v1.3.15 | 是 | bannerlord-game-data-access#英雄名中文查法 |
| bgda-027 | 用 language=EN 反查会静默返回 0 行，不报错。 | v1.3.15 | 是 | bannerlord-game-data-access#英雄名中文查法 |
| bgda-041 | 按 text=? 0 行判非官方不可靠：Sarapios 曾被误判非官方，实为官方聚落描述文中人物。 | v1.3.15 | 是 | bannerlord-game-data-access#陷阱 |
| bgda-044 | csharp_types 表只有签名级信息，不给方法体。 | v1.3.15 | 是 | bannerlord-game-data-access#读原版 C# 源码 |
| bgda-045 | bannerlord_policies 表没有任何数值列。 | v1.3.15 | 是 | bannerlord-game-data-access#给游戏加数值效果 |
| bgda-047 | 『加一条政策就带一个百分比』在原版做不到。 | v1.3.15 | 是 | bannerlord-game-data-access#给游戏加数值效果 |
| bgda-048 | 文化数值属性 militia_bonus/prosperity_bonus 不在 bannerlord_cultures 表，只在 spcultures.xml 的 Culture 元素属性上。 | v1.3.15 | 是 | bannerlord-game-data-access#给游戏加数值效果 |
| bgda-062 | 源码实测 Loan/Credit/Lend 三个词真 0 命中。 | v1.3.15 | 是 | bannerlord-game-data-access#机械穷举四件套 |
| bgda-063 | Interest 的命中只是 interesting/Interface 的子串，不是机制。 | v1.3.15 | 是 | bannerlord-game-data-access#机械穷举四件套 |
| bgda-065 | rg -c 管道接 awk -F: 统计词频，在 Windows 绝对路径下计数全变 0（盘符冒号被当分隔符）。 | 未标版本 | 是 | bannerlord-game-data-access#机械穷举四件套 |
| bgda-068 | 查 SizePolicy 只 1 条命中，是别的文件里小写 sizePolicy 的误命中。 | v1.3.15 | 是 | bannerlord-game-data-access#FTS 切词 |
| bgda-069 | FTS 查属性名必须写全名，写子串会静默给 0 或 1 且不报错。 | v1.3.15 | 是 | bannerlord-game-data-access#FTS 切词 |
| bgda-076 | MCP 的 search_source 在本机报 Executable not found in $PATH: rg，本机未装 ripgrep。 | 未标版本 | 是 | bannerlord-game-data-access#MCP 直连 |
| bgda-077 | read_csharp_type 只认识被索引的类型：查 CampaignSpeedModel 一律 type was not found，不代表该类不存在。 | v1.3.15 | 是 | bannerlord-game-data-access#MCP 直连 |
| bgda-079 | read_csharp_type 返回的方法体是 implementation collapsed，看不到真实实现。 | 未标版本 | 是 | bannerlord-game-data-access#MCP 直连 |
| bgda-083 | read_gauntlet_ui 只返回 data_sources 与 click_commands 两张清单，不解析尺寸/对齐/几何。 | 未标版本 | 是 | bannerlord-game-data-access#MCP 直连 |
| bgda-084 | stringId 大小写敏感，且查不到时静默返回 0 行。 | v1.3.15 | 是 | bannerlord-game-data-access#陷阱 |
| bgda-086 | 城堡没有描述文本：Settlements.Settlement.text.castle_V6 为 0 行，城堡只有 .name.* 条目。 | v1.3.15 | 是 | bannerlord-game-data-access#陷阱 |
| bgda-087 | 不能用 .text.* 是否命中断定某地是不是游戏实体，会把所有城堡误判成非游戏实体。 | v1.3.15 | 是 | bannerlord-game-data-access#陷阱 |
| bgda-088 | 聚落定义在 bannerlord_settlements（来自 ModuleData/settlements.xml），在 xml_entities 里查不到。 | v1.3.15 | 是 | bannerlord-game-data-access#陷阱 |
| bgda-092 | descriptionText 格式为 {=hash}English text，不进 localization_entries；按 .text.* 查反而 0 行。 | v1.3.15 | 是 | bannerlord-game-data-access#陷阱 |
| bgda-095 | 直调 rg.exe 不加 --color never 时，ANSI 高亮会把命中词本身替换成一个 n。 | 未标版本 | 是 | bannerlord-game-data-access#陷阱 |
| bgda-096 | 反编译只有 10 个程序集，游戏自己的模块 SandBox/Native/StoryMode 不在里面。 | v1.3.15 | 是 | bannerlord-game-data-access#陷阱 |
| bgda-098 | settlements.xml 元素名是 Settlement（大写 S）且属性一行一个；小写 settlement 的正则写法 0 命中。 | v1.3.15 | 是 | bannerlord-game-data-access#解析 settlements.xml 坐标 |
| bgda-103 | ItemCategory 类里没有 IsFood，食物是逐件标记 ItemObject.IsFood（Core）。 | v1.3.15 | 是 | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-114 | 官方 CNs XML 是 UTF-16＋BOM，按 utf-8 或按字节 grep 会静默 0 命中、误判没本地化。 | v1.3.15 | 是 | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-126 | 『某城下属村不产某货 ⇒ 该货必贵』是错的；没有哪座城能自给。 | v1.3.15 | 是 | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-149 | 这台机器没有 strings，用 strings 管道 grep 扫 DLL 会静默零命中、看着像干净阴性。 | 未标版本 | 是 | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-152 | Town.TradeBoundVillages 只有界面一个读者（str_trade_bound_village，中文 hash q7xpz1xb）。 | v1.3.15 | 是 | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-154 | 本地化两段式：str_* → {=hash}English，中文按 hash 存在 std_module_strings_xml-zho-CN.xml，拿 str_* 直接搜 CNs 必零命中。 | v1.3.15 | 是 | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-155 | 要读界面代码得单独反编译 TaleWorlds.CampaignSystem.ViewModelCollection.dll，sage 索引不收它。 | v1.3.15 | 是 | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-160 | 索引里查不到某件货不等于游戏里没有：grain/meat 是代码造的，XML 里没有 Item id=grain。 | v1.3.15 | 是 | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-161 | 家族→王国映射在 SandBox/ModuleData/spclans.xml 的 super_faction，但元素名是 Faction 不是 Clan。 | v1.3.15 | 是 | bannerlord-game-data-access#村庄产出与可食用判定 |
| bgda-164 | 旧文档里的类名在 1.3.15 里常已改名。 | v1.3.15 | 是 | bannerlord-game-data-access#默认使用 |
| bgda-167 | 索引零命中不等于游戏里没有：判『没有』前必须先跑机械穷举四件套。 | v1.3.15 | 是 | bannerlord-game-data-access#默认使用 |
| F-006 | ilspycmd 是原生 exe，不认 MSYS 的 /d/ 路径；传错时退出码 0 但完全不产出任何文件 | 旧 Git Bash harness（v1.3.15 期） | 是 | bannerlord-vanilla-mechanism-recon §2 / §6 工具坑表 |
| F-009 | GraphWidget 在 v1.3.15 出货 DLL 里只有定义方自引，全游戏没有其它调用方 | v1.3.15 | 是 | bannerlord-vanilla-mechanism-recon §4 |
| F-010 | 旧 harness 里 bash for 循环扫全游戏目录的 grep 会在几分钟后被 SIGTERM 杀掉，且截断输出看起来像扫完了 | 旧 Git Bash harness（v1.3.15 期） | 是 | bannerlord-vanilla-mechanism-recon §4 / §6 |
| F-013 | 旧 harness 里 find 与 sort 命中的是 C:\Windows\system32 的同名 exe：find 报参数格式不正确，sort -u 报找不到指定的文件 | 旧 Git Bash harness（v1.3.15 期） | 是 | bannerlord-vanilla-mechanism-recon §6 工具坑表 |
| F-014 | 旧 harness 里 grep -E 的 {0,24} 量词被 shell 吃掉，报 [A-Za-z0-9_]0MapBar... No such file or directory | 旧 Git Bash harness（v1.3.15 期） | 是 | bannerlord-vanilla-mechanism-recon §6 |
| F-016 | rg 不加 --color never 时命中词会被 ANSI 高亮码吃掉，显示成 n | 未标版本 | 是 | bannerlord-vanilla-mechanism-recon §6 |
| F-017 | 旧 harness 的 Git Bash 里没有 strings 命令，strings x.dll \| grep -q 永不命中 | 旧 Git Bash harness（v1.3.15 期） | 是 | bannerlord-vanilla-mechanism-recon §6 |
| F-018 | sage 索引只收 bin\Win64_Shipping_Client 下的 15 个 DLL，Modules\Native\bin\... 里的程序集不在索引内 | v1.3.15 | 是 | bannerlord-vanilla-mechanism-recon §6 工具坑表末行 |
| F-023 | GraphWidget 的 LineBrush 是只存不读的死属性，设了不生效 | v1.3.15 | 是 | bannerlord-vanilla-mechanism-recon §7 第 3 问 |
| F-024 | 官方没有任何图表控件用到贴图资源 | v1.3.15 | 是 | bannerlord-vanilla-mechanism-recon §7 第 3 问 |
| F-029 | CampaignEvents.PlayerInventoryExchangeEvent 在反编译的 10 个程序集里只有订阅者，找不到触发方 | v1.3.15 | 是 | bannerlord-vanilla-mechanism-recon §7.5 第 4 步 |
| F-035 | DefaultPerks.Trade.RapidDevelopment.PrimaryBonus（工坊所在城被攻占返还 5000）全树只命中定义处，没有实现 | v1.3.15 | 是 | bannerlord-vanilla-mechanism-recon §7.6 第 3 步实证 |
| F-036 | WorkshopModel.DaysForPlayerSaveWorkshopFromBankruptcy 全树只命中定义处，没有实现 | v1.3.15 | 是 | bannerlord-vanilla-mechanism-recon §7.6 第 3 步实证 |
| F-056 | 谁 new MapNavigationHandler() 在 v1.3.15 未找到，构造点不在 SandBox.View.dll 里 | v1.3.15 | 是 | bannerlord-vanilla-mechanism-recon §8 未决 |
| F-057 | ItemTemplate 内部能否用 Id 反引子控件，在 v1.3.15 未验证 | v1.3.15 | 是 | bannerlord-vanilla-mechanism-recon §9 未验 |
| F-062 | AnimusForge 的门控证据只在 FreezeWatchdog_Timeline.txt 里，Mod_Logic.txt 等业务日志一个都没有 | 未标版本 | 是 | bannerlord-mod-recon §1 与 §4 第 7 条 |
| F-069 | 旧 harness 可用 tr -d '\000' < DLL \| grep -o -- '串' \| wc -l 提取 UTF-16LE 串；strings -a 会静默给 0 | 旧 Git Bash harness（v1.3.15 期） | 是 | bannerlord-mod-recon §4 第 1 条 |
| F-070 | 旧 harness 里 grep -r 与 -n/-o 连用会被拼成 -rn 当命令执行，静默空输出且 exit 0 | 旧 Git Bash harness（v1.3.15 期） | 是 | bannerlord-mod-recon §4 第 2 条 |
| F-071 | timeout 900 python xxx.py 命中的是 C:\Windows\system32\timeout.exe，报『错误: 无效语法。默认选项不允许超过 '1' 次。』 | Windows 未标版本 | 是 | bannerlord-mod-recon §4 第 4 条 |
| F-073 | 把 /d/... 形式的路径喂给原生 Python 时，os.walk 会扫到 0 个文件却像正常结果 | 旧 Git Bash harness（v1.3.15 期） | 是 | bannerlord-mod-recon §4 第 5 条 |
| F-074 | 在 Mod_Logic.txt／SETS.log／Event_Logs.txt 里搜 mode=onnx 得 0 命中，但该 onnx 机制实际是生效的 | 未标版本 | 是 | bannerlord-mod-recon §4 第 7 条 |
| T02 | TPAC 头部偏移 24 的 u32 计数不总是条目数：gauntlet_ui.tpac 报 97，实扫只有 23 条。 | v1.3.15 | 是 | bannerlord-tpac-extraction §1 容器格式表（偏移 24 行） |
| T03 | core.tpac 体积 217MB，但 TOC 只有 611157 字节，TOC 区与数据区分离。 | v1.3.15 | 是 | bannerlord-tpac-extraction §1 TOC 与数据区是分开的 |
| T04 | core_game.tpac 体积 3GB，但 TOC 只有 5.5MB。 | v1.3.15 | 是 | bannerlord-tpac-extraction §1 TOC 与数据区是分开的 |
| T06 | 一个 tpac 的 TOC 里混着多个来源包的 guid，不能用固定偏移 d[36:52] 取条目 guid。 | v1.3.15 | 是 | bannerlord-tpac-extraction §1 条目布局（★ 坑） |
| T10 | 现行 gauntlet_ui.tpac 实测：ui_textures_1/_2/_3/_6/_7 的载荷已压缩，_4/_5/_8 未压缩。 | v1.3.15 | 是 | bannerlord-tpac-extraction §0.1 坑 2、§2（现行 gauntlet_ui.tpac 实测） |
| T11 | stored == expanded 的条目硬喂 lz4.block.decompress 会抛 LZ4BlockError: Decompression failed: corrupt input。 | v1.3.15 | 是 | bannerlord-tpac-extraction §0.1 坑 2、§2 |
| T13 | 把 BGRA 当 RGBA 解会把红蓝对调：纸贴图修正前均值 (234,237,241)，修正后 (241,237,234)。 | v1.3.15 | 是 | bannerlord-tpac-extraction §0.1 坑 3 |
| T14 | stone_texture_continuous 是中性灰 (144,144,144)，通道对调在灰度图上完全看不出来。 | v1.3.15 | 是 | bannerlord-tpac-extraction §0.1 坑 3、§4 表 |
| T15 | warm_overlay 实测均值 (120,115,101)，R>G>B，可作通道序正确的旁证。 | v1.3.15 | 是 | bannerlord-tpac-extraction §0.1 坑 3（旁证手法） |
| T16 | 图集 sheet 尺寸随版本变且 sheet 序号会挪位：ui_textures_1 由 build31530 的 887×890 变为 build110062 的 888×888。 | v1.3.15 (build 110062) / 对照 build 31530 | 是 | bannerlord-tpac-extraction §0.1 坑 1 对照表 |
| T17 | ui_textures_2（paper_texture_tile）尺寸由 build31530 的 1185×396 变为 build110062 的 1184×396。 | v1.3.15 (build 110062) / 对照 build 31530 | 是 | bannerlord-tpac-extraction §0.1 坑 1 对照表 |
| T18 | ui_textures_3（popup_canvas_texture）尺寸由 build31530 的 492×602 变为 build110062 的 492×600。 | v1.3.15 (build 110062) / 对照 build 31530 | 是 | bannerlord-tpac-extraction §0.1 坑 1 对照表 |
| T19 | sheet 7 由旧版 warm_overlay 30×30 变为现行 gradient_texture 100×100；旧版无 sheet 8，现行为 warm_overlay 32×32。 | v1.3.15 (build 110062) / 对照 build 31530 | 是 | bannerlord-tpac-extraction §0.1 坑 1 对照表 |
| T22 | expanded 可能含 mip 链而大于 W*H：ui_textures_4 实测 expanded=349552，而 512×512=262144。 | v1.3.15 | 是 | bannerlord-tpac-extraction §2（⚠ expanded 可能含 mip 链） |
| T23 | UI 图集的 sheet 尺寸常常就是该 sprite 自身尺寸（ui_textures 分类 sheet2 = 1184×396 = paper_texture_tile 尺寸）。 | v1.3.15 | 是 | bannerlord-tpac-extraction §2 尺寸从哪来 |
| T24 | 解错也是纯色，不能用熵或解出来全是纯色来判断解码对错。 | 未标版本 | 是 | bannerlord-tpac-extraction §2（不要用熵判断） |
| T25 | 图标图集正确解码的统计特征是 60%~87% 的 alpha=0；实测 82% 透明 + 15% 实心。 | v1.3.15 | 是 | bannerlord-tpac-extraction §2 判据是统计特征 |
| T26 | texture2ddecoder 提供 decode_bc1/bc3/bc4/bc5/bc6/bc7/astc 解码函数。 | 未标版本 | 是 | bannerlord-tpac-extraction §3 环境（本机已验证） |
| T26b | 本机默认 python venv 在 C:\Users\26811\.workbuddy\binaries\python\envs\default。 | 未标版本 | 是 | bannerlord-tpac-extraction §3 环境（本机已验证） |
| T27 | Bash 沙箱无外网，pip 装不上包。 | 未标版本 | 是 | bannerlord-tpac-extraction §3 环境 |
| T28 | custom_banner_icons 图集共 16 张 2048²，每张 4×4=16 格、每格 512²。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 已验证的落点表 |
| T29 | 旗帜图标图集的条目名是 custom_banner_icons 与 custom_banner_icons_02..16，位于 core.tpac。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 已验证的落点表 |
| T30 | Native/AssetPackages/Banner.tpac 只有 8 个 mesh 条目。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 已验证的落点表 |
| T31 | gauntlet_ui.tpac 现行 324MB、TOC 仅 40542 字节；旧版 253MB；SandBox 那份 66MB。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 已验证的落点表 |
| T32 | UI 图集条目名 = <SpriteCategory 分类名>_<sheetID>。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 ★ UI 图集的命名与落点 |
| T33 | 现行 gauntlet_ui.tpac 实测 23 条，条目形如 ui_textures_5、ui_loading_3、ui_fonts_1。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 ★ UI 图集的命名与落点 |
| T34 | ui_textures 是原版唯一一族 AlwaysLoad 的材质，一张一个 sprite，sheet 尺寸等于 sprite 尺寸。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 ★ UI 图集的命名与落点 |
| T35 | ui_textures_1 = game_over_mask，888×888，BC7，纯白蒙版。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 ui_textures 表 |
| T36 | ui_textures_2 = paper_texture_tile，1184×396，BC7，实测均值 (241,237,234) 近白暖白。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 ui_textures 表 |
| T37 | ui_textures_3 = popup_canvas_texture，492×600，BC7，实测均值 (46,46,46) 暗。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 ui_textures 表 |
| T38 | ui_textures_4 = stone_texture_continuous，512×512，DXT5 未压缩，实测均值 (144,144,144)。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 ui_textures 表 |
| T39 | ui_textures_5 = stone_texture_overlay，512×512，BC7 未压缩，实测均值 (111,111,111)。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 ui_textures 表 |
| T40 | ui_textures_6 = slider_progress_small，160×80，DXT5，解出为白色。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 ui_textures 表 |
| T41 | ui_textures_7 = gradient_texture，100×100，DXT5，实测均值 (19,15,9) 近黑。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 ui_textures 表 |
| T42 | ui_textures_8 = warm_overlay，32×32，DXT5 未压缩，实测均值 (120,115,101)。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 ui_textures 表 |
| T43 | 新旧 build 重抠逐像素复核：stone 两条 MAE=0.00，纸 MAE 1.06 且 94% 像素差 ≤4。 | v1.3.15 (build 110062) 对照 build 31530 | 是 | bannerlord-tpac-extraction §4 复核结论（09-15） |
| T44 | 纹理源文件路径写在定义体里，形如 $BASE/Modules/Native/AssetSources/GauntletUI/<条目名>.png。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 源文件路径在定义体里 |
| T45 | 命名坑：XML 里材质叫 custom_banner_icons_01，但包里资产名是 custom_banner_icons（没有 _01）。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4 命名坑 |
| T46 | BrushLayer 的 Name 是代码里的 StringId，Sprite 才是图；两者编号常不一致（主控栏 icon6=王国、icon7=家族）。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4.5 步骤 1（⚠ 层的 Name） |
| T47 | SpritePart 已给出 SheetID/Name/Width/Height/SheetX/SheetY/CategoryName，切片区域不必自己算格子。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4.5 步骤 2 |
| T48 | 主控栏条目 ui_mapbar_1 位于 Modules/SandBox/AssetPackages/gauntlet_ui.tpac，尺寸 4096×128。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4.5 步骤 3 |
| T49 | 图集由 SpriteSheetGenerator 自动排版，格子大小不等：主控栏图标散在 X=3267~3755 之间，Y 恒为 4。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4.5 ★ 为什么必须现读 SpriteData.xml |
| T50 | 同一分类的不同模块各有一份自己的图集，ui_mapbar 只可能在 SandBox 包里。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4.5 ★ 为什么必须现读 SpriteData.xml |
| T51 | graphics 层的 manage_fleet 用了 Map\ship（另一分类的图），一个 brush 的层可以跨分类引用精灵。 | v1.3.15 | 是 | bannerlord-tpac-extraction §4.5 顺手可用的旁证 |
| T53 | 旗帜画布 1528×1528、中心 (764,764)；8 家王国 key 的 Pos=764 恒等、Icon Size≈512 占 33.5%。 | v1.3.15 | 是 | bannerlord-tpac-extraction §5 画布 1528×1528 |
| T54 | MeshId=11 即 banner_background_test_11，带 is_base_background，是纯色平底，王国旗都用它。 | v1.3.15 | 是 | bannerlord-tpac-extraction §5（MeshId=11） |
| T56 | banner_icons.xml 的 <Background> 共 36 个，即 banner_background_test_1..36。 | v1.3.15 | 是 | bannerlord-tpac-extraction §5（<Background id= mesh_name=> 共 36 个） |
| T57 | banner_icons.xml 尾部 <BannerColors> 共 229 个颜色。 | v1.3.15 | 是 | bannerlord-tpac-extraction §5 配色 |
| T58 | 图标贴图通道约定：绿通道=ColorId、蓝通道=ColorId2、alpha=形状；合成=绿×color1+蓝×color2，实心像素只有 (0,255,0) 与 (0,0,255)。 | v1.3.15 | 是 | bannerlord-tpac-extraction §5 ★ 图标贴图的通道约定 |
| T59 | 图标描边（蓝通道）吃 ColorId2：Lake Rats 的斧子用 c2=B57A1E 的橘铜色描边。 | v1.3.15 | 是 | bannerlord-tpac-extraction §5 实测印证 |
| T61 | mesh 顶点坐标范围 ±12 是一个单元方、按 banner_key 的 Size 缩放；顶点 0..3 是底块、4+ 是花纹且 z 抬高 ±0.02。 | v1.3.15 | 是 | bannerlord-tpac-extraction §5.1（顶点 0..3 = 底块） |
| T63 | 原版实际用到的底纹只有 8 个：_1 左半、_11 平底、_14 宽斜分、_16 十字/星、_17 放射星芒、_24 中央竖条、_34 中央圆、_35 带城垛锯齿的横向分割。 | v1.3.15 | 是 | bannerlord-tpac-extraction §5.1 8 个实际被用到的底纹实测 |
| T64 | GetPrimaryColorId() 取层的 ColorId，GetSecondaryColorId() 取 ColorId2。 | v1.3.15 | 是 | bannerlord-tpac-extraction §5.1 ★ 底纹双色 |
| T64b | ChangePrimaryColor() 把两色设成同一个，这是 93 面平底旗 c1==c2 的原因。 | v1.3.15 | 是 | bannerlord-tpac-extraction §5.1 ★ 底纹双色 |
| T65 | BannerVisual.ConvertToMultiMesh() 对背景层交换两色：mesh.Color=ColorId2、mesh.Color2=ColorId。 | v1.3.15 | 是 | bannerlord-tpac-extraction §5.1 BannerVisual.ConvertToMultiMesh() |
| T66 | 随机生成图标时 ColorId2 取 ReadOnlyColorPalette.Last().Key，即 116 近黑。 | v1.3.15 | 是 | bannerlord-tpac-extraction §5.1 旁证 |
| T67 | banner_background_test_a 那张 2048² 图不是底纹网格采样的贴图，是编辑器用的形状表，不能当 UV 源反推格子。 | v1.3.15 | 是 | bannerlord-tpac-extraction §5.1 坑（绕了两轮） |
| T68 | tpac 里 mesh 资产的 LOD/法线/UV 流没有被细拆，只取了顶点+索引。 | v1.3.15 | 是 | bannerlord-tpac-extraction §7 还没吃到的 |
| T69 | EmAssetPackages 与 AssetPackages 两套并存，同一资产可能两边都有，优先取 EmAssetPackages。 | v1.3.15 | 是 | bannerlord-tpac-extraction §7 还没吃到的 |
| T70 | bannerlord-sage MCP 的 search_source 与 search_bannerlord_knowledge 全废，因本机 PATH 无 rg。 | 未标版本 | 是 | bannerlord-tpac-extraction §7 还没吃到的 |
| T71 | read_csharp_type 会把长方法体折叠掉，需要改用 read_file(path,startLine,lineCount) 按行补读。 | 未标版本 | 是 | bannerlord-tpac-extraction §7 还没吃到的 |
| S01 | 游戏本体在 D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord，本批记录的项目版本是 v1.3.15.110062。 | v1.3.15.110062（当前已升级到 v1.4.8） | 是 | bannerlord-mod-skeleton §0 先决条件表 |
| S02 | 游戏程序集位于游戏根的 bin\Win64_Shipping_Client\TaleWorlds.*.dll。 | v1.3.15 | 是 | bannerlord-mod-skeleton §0 先决条件表 |
| S04 | 本机 dotnet.exe 在 C:\Program Files\dotnet\dotnet.exe，装有 9.0.306 与 10.0.301 两个 SDK。 | 未标版本 | 是 | bannerlord-mod-skeleton §0 先决条件表 |
| S05 | 反编译工具 ilspycmd 在 ~/.dotnet/tools/ilspycmd.exe。 | 未标版本 | 是 | bannerlord-mod-skeleton §0 先决条件表 |
| S06 | 本机已有一个能编译能跑的同代模组模板 D:\AWAKE-Dev\AWAKE\（含 AWAKE.csproj、SubModule.xml、src\SubModule.cs）。 | 未标版本 | 是 | bannerlord-mod-skeleton §0 先决条件表（现成模板） |
| S14 | CampaignBehaviorBase 没有 OnSessionLaunched 虚方法。 | v1.3.15 | 是 | bannerlord-mod-skeleton §4（⚠ CampaignBehaviorBase） |
| S14b | 会话启动只能监听 CampaignEvents.OnSessionLaunchedEvent，回调签名 Action<CampaignGameStarter>。 | v1.3.15 | 是 | bannerlord-mod-skeleton §4 最小写法 |
| S15 | Campaign 没有 Heroes 属性，要用 Campaign.Current.AliveHeroes / .Settlements。 | v1.3.15 | 是 | bannerlord-mod-skeleton §4（⚠ Campaign 没有 Heroes 属性） |
| S16 | 模组日志落点是程序集所在目录，即 Modules\<ModId>\bin\Win64_Shipping_Client\<mod>.log。 | v1.3.15 | 是 | bannerlord-mod-skeleton §4（日志落哪） |
| S17 | 日志写入不 try/catch 吞掉异常，日志失败会把游戏带崩。 | v1.3.15 | 是 | bannerlord-mod-skeleton §4（日志写一定要 try/catch） |
| S18 | 存档系统是类型驱动的：SyncData 每个字段必须能在启动时那份类型定义表里查到整个闭合泛型，查不到则整个存档失败。 | v1.3.15 | 是 | bannerlord-mod-skeleton §6.1 根因 |
| S19 | 存档失败日志串：Cant find definition for System.Collections.Generic.Dictionary`2[[Town],[Int32]]。 | v1.3.15 | 是 | bannerlord-mod-skeleton §6.1 症状（日志块） |
| S19b | 存档失败同时打印 Couldn't save because of errors listed below. 与 [0]SaveContext Error。 | v1.3.15 | 是 | bannerlord-mod-skeleton §6.1 症状（日志块） |
| S20 | SaveableBasicTypeDefiner 的 saveBaseId=30000，装基础类型与 List<int>、Dictionary<string,int>。 | v1.3.15 | 是 | bannerlord-mod-skeleton §6.1 两张主表 |
| S21 | SaveableCampaignTypeDefiner 的 saveBaseId=2000，注册战役类型与 Dictionary<Settlement,int>、List<Town>。 | v1.3.15 | 是 | bannerlord-mod-skeleton §6.1 两张主表 |
| S22 | Dictionary<Town,int> 这个闭合泛型没有被原版注册，即使 Town 与 int 各自都已注册。 | v1.3.15 | 是 | bannerlord-mod-skeleton §6.1（⚠ 里面的类型认识≠这个容器认识） |
| S23 | Dictionary<Settlement,int> 已在战役定义表注册，可作 Dictionary<Town,int> 的替代组合。 | v1.3.15 | 是 | bannerlord-mod-skeleton §6.1 改法甲 |
| S24 | CraftingCampaignBehaviorTypeDefiner 继承 SaveableTypeDefiner(150000)，用 AddClassDefinition 注册。 | v1.3.15 | 是 | bannerlord-mod-skeleton §6.1 改法乙 |
| S25 | Dictionary<Town, CraftingOrderSlots> 确实存在，但它住在 CraftingCampaignBehavior 自己的 definer 里，不是「不用注册」的先例。 | v1.3.15 | 是 | bannerlord-mod-skeleton §6.1（⚠⚠ 别把先例读反） |
| S26 | 自定义 SaveableTypeDefiner 要占一段 saveId，必须自行保证不与其他模组撞号。 | v1.3.15 | 是 | bannerlord-mod-skeleton §6.1 改法乙（⚠ 要占一段 saveId） |
| S27 | 主控栏按钮是数据驱动的：MapBar.xml:64 的 ListPanel DataSource 绑 MapNavigation\NavigationItems。 | v1.3.15 | 是 | bannerlord-mod-skeleton §8（MapBar.xml:64） |
| S28 | MapNavigationVM 构造函数遍历 GetElements()，逐个 new MapNavigationItemVM 加进 NavigationItems。 | v1.3.15 | 是 | bannerlord-mod-skeleton §8（MapNavigationVM 构造函数） |
| S29 | 主控栏上一个按钮 = 一个 INavigationElement，点击入口是 OpenView()。 | v1.3.15 | 是 | bannerlord-mod-skeleton §8（⇒ 一个按钮 = 一个 INavigationElement） |
| S30 | INavigationElement 成员含 StringId、Permission、IsActive、Tooltip、OpenView()、GoToLink() 等。 | v1.3.15 | 是 | bannerlord-mod-skeleton §8.3 |
| S31 | NavigationPermissionItem 是 struct，构造签名为 (bool isAuthorized, TextObject reasonString)。 | v1.3.15 | 是 | bannerlord-mod-skeleton §8.3 |
| S32 | MapNavigationItemVM(INavigationElement) 是公开构造，内部 ItemId = StringId。 | v1.3.15 | 是 | bannerlord-mod-skeleton §8.3 |
| S33 | TextObject 没有 Empty，要用 TextObject.GetEmpty()。 | v1.3.15 | 是 | bannerlord-mod-skeleton §8.3（⚠ TextObject 没有 Empty） |
| S34 | 预制件里 IconID="@ItemId" 使按钮图标的查表键就是 StringId；自造 id 在原版 MapBar.Left.Icons 里没有帧，结果是按钮有底板、能点、有提示但图标为空。 | v1.3.15 | 是 | bannerlord-mod-skeleton §8.3（★ 预制件里 IconID） |
| S35 | MapNavigationElementBase 的构造函数要具体类型 MapNavigationHandler，继承它就得引用 SandBox.View.dll。 | v1.3.15 | 是 | bannerlord-mod-skeleton §8.1 步 1 |
| S36 | 预制件里 Text="@X" 绑定的属性必须在 setter 里调用 OnPropertyChangedWithValue，光改字段界面不动。 | v1.3.15 | 是 | bannerlord-mod-skeleton §8.3（面板 VM 继承 ViewModel） |
| S38 | TaleWorlds.CampaignSystem.ViewModelCollection.dll 不在反编译源码树里，源码树只有 9 个程序集。 | v1.3.15 | 是 | bannerlord-mod-skeleton §8.4 |
| S39 | MapNavigationHandler 位于 SandBox.View.dll。 | v1.3.15 | 是 | bannerlord-mod-skeleton §8.4（MapNavigationHandler 就是这么捞出来的） |
| S40 | GauntletLayer 用 (name, 546, false) 构造，原版层优先级取 540~547 区间。 | v1.3.15 | 是 | bannerlord-mod-skeleton §8.1 步 3 |
| S42 | Harmony postfix MapBarVM 构造函数追加 NavigationItems 时，读档或重开战役会反复走，追加前必须按 ItemId 查重。 | v1.3.15 | 是 | bannerlord-mod-skeleton §8.1 步 2 |
| S43 | 离线五道验证验不到「游戏真的加载了模组」，必须留给人启动游戏看 <mod>.log 出没出现。 | v1.3.15 | 是 | bannerlord-mod-skeleton §6（验不到的） |
| S44 | 日志文件没出现就等于模组没加载，多半是三个名字对不上或两处落点缺一处。 | v1.3.15 | 是 | bannerlord-mod-skeleton §6（日志文件没出现 = 模组没加载） |
| S45 | 模组 csproj 的目标配置为 net472 / x64 / Library / LangVersion 10。 | v1.3.15 | 是 | bannerlord-mod-skeleton §1 目录结构 |
| S46 | 现成存档字段闸脚本在 D:\卡拉迪亚金融大鳄\tools\gate_save_fields.py，含阳性对照用例。 | 未标版本 | 是 | bannerlord-mod-skeleton §6.1（现成模板） |
| S47 | 现成构建脚本 tools\build_mod.py 一条命令完成编译 + 部署 + 自检。 | 未标版本 | 是 | bannerlord-mod-skeleton §1、§5 |
| F001 | Gauntlet 全库约 2.5 万处 WidthSizePolicy 只有 Fixed / StretchToParent / CoverChildren 三种取值，没有 =@绑定 这种写法。 | 未标版本 | 是 | bannerlord-gauntlet-ui-prefab-layout §2.5 |
| F004 | 引擎在 StretchToParent 下不读元素自己的 SuggestedWidth / SuggestedHeight，该值此时只作设计档记录。 | 未标版本 | 是 | bannerlord-gauntlet-ui-prefab-layout §2.5 |
| F005 | 离线工作台 AWAKE UI Lab 不实现栈里吸余量：preview_prefab_geometry.py 里 pw_ = 父宽 − 边距，不减去兄弟的宽。 | 未标版本（本机工作台） | 是 | bannerlord-gauntlet-ui-prefab-layout §2.5 |
| F010 | 横排 ListPanel 一行放不下时，排在最后的那一颗会被静默裁掉：不折行、不缩略、不报错。 | 未标版本 | 是 | bannerlord-gauntlet-ui-prefab-layout §2.6 |
| F011 | GridWidget 不是流式容器，按 MarginLeft 累加的 offsets() 去量它的格位是错的。 | 未标版本 | 是 | bannerlord-gauntlet-ui-prefab-layout §2.6 |
| F039 | RichTextWidget 不换行，内容超宽只会被切，不报错。 | 未标版本 | 是 | bannerlord-gauntlet-ui-prefab-layout §5.3 |
| F044 | 离线工作台不认 @ 绑定，也无视 IsVisible=false。 | 未标版本（本机工作台） | 是 | bannerlord-gauntlet-ui-prefab-layout §5.1 |
| F063 | 原版绑定没有取反写法，同矩形换出场那一侧必须为每一档各写一个布尔；漏一句 OnPropertyChangedWithValue 的症状是换了档表还在原地、两套叠在一起。 | 未标版本（AWAKE 项目） | 是 | bannerlord-gauntlet-ui-prefab-layout §6.4b |
| f002 | 7 个官方模块都没有 GUI/SpriteParts/、GUI/SpriteSheets/、AssetSources/、Assets/ 目录。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §1 L57 |
| f006 | SubModule.xml 里完全没有资产/资源声明节点，资产全靠路径约定自动发现。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §1 L59 / §9.5 L474 |
| f014 | SandBox 15 个界面分类一个都没用 AlwaysLoad，界面分类按需加载。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §1 L79 |
| f015 | Config.xml 带 <?xml version="1.0" encoding="utf-8"?> 声明时整份被忽略：带声明 AlwaysLoad=0，去掉=3。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §1 L90 |
| f016 | Config.xml 里任何注释（中文或 ASCII）都会让整份文件被忽略。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §1 L91 |
| f017 | Config.xml 根节点写成 <SpriteCategories> 会让整份文件被忽略（那是产出 SpriteData 的节点名）。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §1 L92 |
| f018 | 命中上述杀手时生成器照样 exit 0、照样出图集、毫无提示。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §1 L86 |
| f019 | 未解矛盾：AWAKE 仓库那份 Config.xml 三条杀手全中，产出却带着 AlwaysLoad x3。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §1 |
| f021 | 7 个官方模块没有一个 ship Config.xml，但 NativeSpriteData.xml 里有 4 处 <AlwaysLoad />。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §1 L148 |
| f022 | 全库 5 份 Config.xml 里 SpriteSheetSize 出现 0 次；写进 Config.xml 不报错也不生效。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §1 |
| f027 | tools/sync_module.ps1 只管 7 个 Prefab，不碰 SpriteParts/，所以不存在同步覆盖风险。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §1 L127 |
| f033 | 旧结论"边距只能写 Brush、sprite 侧写 Extend 都错"是错的：在 *SpriteData.xml 搜 Extend 得 0 命中只是搜错 token。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §2 L152 |
| f045 | 面板 Prefab 常只写 Brush= + 文字、没有任何 Sprite=，说明它没用图标。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §3 L228 |
| f047 | 索引声明的分类在 GUI/SpriteSheets/<分类>/ 没有图集时，该分类图标全不显示。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §4 L235 |
| f048 | PNG 丢进 GUI/SpriteParts/<分类>/ 但索引里没有该分类时，读不到且不报错。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §4 L236 |
| f050 | 那 28 张 PNG 没有任何 SpritePart / GenericSprite / Brush 引用，属于"做出了但没接线"。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §4 L236 |
| f052 | AWAKE ui_awake_button 2048x64 已用至 1388；ui_awake_frame 4096x1024 已用至 4088/4096 已满，212x360 的件装不下。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §4 L237 |
| f053 | 图集容量不够时新 sprite 静默丢失、不报错。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §4 L237 |
| f056 | 本机只有 bin/Win64_Shipping_Client、没有 bin/Win64_Shipping_wEditor，走不了官方生成流程。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §4 L241 |
| f059 | 6 个 Prefab 里 grep '="12"\\|"24"' 零命中，"12/24 系"说法从未实证。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §5 L254 |
| f066 | "官方 UI 全在 .tpac 内无法旁窥"已作废：官方 UI 贴图能抠出来。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §6 L283/L291 |
| f073 | app 1393600 不是 DLC：dlc/261550 只有 4456490/2927200/2194520/2240110 四条，没有它。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §7 L310 |
| f074 | app 1393600 没有商店页，访问 store/app/1393600 直接跳回商店首页。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §7 L311 |
| f075 | app 1393600 没有封面图：librarycache/1393600/ 只有一张图标 jpg，无 header/library_hero/library_600x900/logo。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §7 L312 |
| f078 | 游戏当前分支取自 appmanifest_<261550>.acf 的 UserConfig/BetaKey，Modding Kit 的 BETAS 必须选同一分支，否则崩。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §7 L317 |
| f079 | War Sails Modding Kit(4456490) 是 DLC 附加包，需 War Sails 扩展(2927200, $24.99)，配套 BL v1.4.8，装了给不了编辑器。 | v1.4.8 | 是 | bannerlord-ui-sprite-assets §7 L319 |
| f084 | 沙箱里生成器的工作目录必须是 bin/Win64_Shipping_wEditor（从当前目录往上两级接 Modules/），在沙箱根跑会 DirectoryNotFoundException。 | 未标版本 | 是 | bannerlord-ui-sprite-assets §8 |
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
| brush-01 | Brush 自动加载：BrushFactory.LoadBrushes 经 ResourceDepot.GetFiles 扫每个模块 GUI/Brushes/*.xml 全量加载，无需声明。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §1 Brush 文件是自动加载的 |
| brush-02 | 全库 0 个模组在 SubModule.xml 里声明过 Brush 或 SpriteData。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §1（第 31 行） |
| brush-03 | Brush 状态名只许 Default/Hovered/Pressed/Disabled/Selected 五个，自造 Normal/Active/Focus 等会静默不生效。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §2「状态名只许这五个」 |
| brush-04 | RefreshState 状态优先级：Disabled > Selected(true 时) > Pressed > Hovered > Selected(false 时) > Default。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §2「谁决定按钮落在哪个状态」 |
| brush-05 | ButtonWidget.DominantSelectedState 默认值为 true，故选中态压得住 Hovered 与 Pressed。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §2（第 70-71 行） |
| brush-06 | HandleClick 先跑完所有 ClickEventHandlers（VM 命令），之后才按 IsToggle 翻转 / 按 IsRadio 强制置真 IsSelected。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §2（第 74-81 行） |
| brush-07 | ButtonType 缺省为 Push，此时引擎完全不碰 IsSelected；Toggle 翻转、Radio 强制置真并调 OnChildSelected 让兄弟取消。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §2（第 84-89 行） |
| brush-08 | IsSelected 可由外部写入：原版 CircleActionSelectorWidget.cs:307 直接写它。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §2（第 86 行） |
| brush-09 | IsEnabled=false 不只是变灰：EventManager.CollectEnableWidgetsAt 见 !IsEnabled 直接 return，该控件不进鼠标命中表。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §2（第 91-93 行） |
| brush-10 | Brush 的 <Style> 是部分覆盖：LoadStyleInto 取 style.GetLayer(name) 后由 LoadBrushLayerInto 对已有层增量赋值。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §3 <Style> 是部分覆盖 |
| brush-11 | 同一 Brush 四个状态的九宫格边距必须逐字节一致，否则状态切换时内衬会跳。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §3（第 101-102 行） |
| brush-12 | ExtendLeft/Top/Right/Bottom 四个全写才是九宫格，缺一个退回整体拉伸；单位为 sprite 像素。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4（第 108-109 行） |
| brush-13 | 实测计数：Native GUI/Brushes/*.xml 里 ExtendLeft 出现 109 处；全库 10 个 *SpriteData.xml 里 ExtendLeft 出现 0 处。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4（第 110 行） |
| brush-14 | 贴图注册表另有 NineRegionSprite：NativeSpriteData.xml:40392 的 button_canvas_9 为 L25/R24/T43/B44。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4（第 111-121 行） |
| brush-15 | 老式 brush 只能靠贴图注册表拿九宫格：Native/GUI/Brushes/Brush.xml:2-6 的 BrushLayer 上一个 Extend* 都没有，而官方拿它当按钮底板。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4（第 123-125 行） |
| brush-16 | NineRegionSprite 消费代码在 TaleWorlds.TwoDimension，本机 10 个反编译程序集里没有、NineRegion 零命中，两套机制优先级未验。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4（第 126-127 行） |
| brush-17 | button_canvas_9 的 TopHeight+BottomHeight=43+44=87 等于贴图全高，竖着没有可拉伸中段，不能拿它压小按钮。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4（第 128-129 行） |
| brush-18 | BrushLayer.Rotation 可直接写在 XML：BrushFactory.cs:243-244 的 case "Rotation" 走 Convert.ToSingle。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4.5（第 142-144 行） |
| brush-19 | BrushLayer.Rotation 单位是角度不是弧度。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4.5（第 138-144 行） |
| brush-20 | BrushLayer.Rotation 是 public 可写（BrushLayer.cs:330），且 Brush.Clone()（Brush.cs:398）存在。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4.5（第 145-147 行） |
| brush-21 | Widget 基类没有任何旋转/变换成员，旋转只在 BrushLayer 上；只查 Widget 会误判为界面不能旋转。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4.5（第 132-135 行） |
| brush-22 | BrushLayerState.Rotation 与 BrushAnimationProperty.Rotation 都存在，故 <Style> 层与 brush 动画都能带角度。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4.5（第 148-149 行） |
| brush-23 | Rotation 带 [Editor(false)] 特性，Prefab 编辑器不暴露它。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4.5（第 150 行） |
| brush-24 | 旋转的支点位置（中心或左上角）以及旋转后是否被 ClipContents 裁剪，均未验证。 | 未标版本 | 是 | bannerlord-gauntlet-ui-brushes §4.5（第 155 行） |
| brush-25 | Color= 与 Brush.FontColor= 走 8 位十六进制 #RRGGBBAA，写 10 位不报错、被静默丢弃并渲染成无色。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §6（第 179-182 行） |
| brush-26 | 原版五个官方模块的 Prefab 里 Color= 共出现 729 处，全部 8 位，零例外。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §6（第 182 行） |
| brush-27 | AlphaFactor 是叠乘，与 Color 的逐通道相乘语义不同。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §6（第 183 行） |
| brush-28 | AWAKE 预览器 awake-ui-lab 能直读 GUI/SpriteParts/**/<名>.png，免图集、免索引、免 Modding Kit 即可预览自绘贴图。 | 未标版本 | 是 | bannerlord-gauntlet-ui-brushes §7A（第 188 行） |
| brush-29 | AWAKE 预览器把本模块自有 GUI/Brushes/ 排在加载最前，且报告口径拆为自有命中/原版命中/未命中三项。 | 未标版本 | 是 | bannerlord-gauntlet-ui-brushes §7A（第 189-190 行） |
| brush-30 | 预览合成缓存按「文件名+尺寸」为键；换图后 mtime/size 未变则预览不变。 | 未标版本 | 是 | bannerlord-gauntlet-ui-brushes §7B（第 198-199 行） |
| brush-31 | AWAKE 实际 Brush 文件 AWAKE/GUI/Brushes/AwakeBrushes.xml 含 11 条 Brush、21 个 sprite。 | 未标版本 | 是 | bannerlord-gauntlet-ui-brushes §9（第 218 行） |
| brush-32 | 真机 E4（贴图是否真被引擎加载）未验，本地预览不等于真机渲染。 | 未标版本 | 是 | bannerlord-gauntlet-ui-brushes §9（第 221 行） |
| brush-33 | ButtonBrush 用贴图 button_canvas_9，九宫格写在贴图注册表（BrushLayer 上无 Extend*）。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4.7 表（第 161 行） |
| brush-34 | ButtonBrush1/2/3 的 Extend 均为 22/22/22/22，贴图为 main_button_done/_regular/button_cancel，原生 271×84。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4.7 表（第 162、167 行） |
| brush-35 | ButtonBrush4（big）的 Extend 为 22/12/22/12，最小可用高度只要 24。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4.7（第 167 行） |
| brush-36 | 官方标准按钮样板 Native/GUI/Prefabs/Standard/Standard.Button.xml 尺寸 227×40、Brush="ButtonBrush1"。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4.7（第 162、164 行） |
| brush-37 | 全库只有 3 个 prefab 使用朴素 ButtonBrush，其中正经的只有 ButtonCancel.xml（100×80）。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4.7（第 161、166 行） |
| brush-38 | ButtonBrush 贴图 button_canvas_9 的内容只占 97×87 中间那 48×49，AWAKE UI Lab 渲染图会把它画小。 | 未标版本 | 是 | bannerlord-gauntlet-ui-brushes §4.7（第 168-169 行） |
| brush-39 | brush 名在引擎里是全局命名空间，所有模块共享，改原版同名 brush 会污染别的面板。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §5（第 173-176 行） |
| brush-40 | 此前「写在 sprite 索引上的 Extend 一定无效」是错的，2026-09-19 已勘误收回。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-brushes §4（第 111 行） |
| vm-01 | 改 VM 字段界面不会动，必须在 setter 里调 OnPropertyChangedWithValue(值, nameof(Foo))。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §1（第 17-19 行） |
| vm-02 | 点路径 @A.B / {A.B} 在原版 Prefab 里零命中，写了会整块空白。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §1（第 20-21 行） |
| vm-03 | DropdownWidget 换 DataSource 时会把 CurrentSelectedIndex 经 ListPanelValue 当场回写进 VM 的 BasisIndex。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §2 链条第 2 步 |
| vm-04 | 旧下标回写 × 新列表导致「点 A 行动的是 B 行」，且 setter 里 Refresh() 重入使表体出现两段相同列表（实测 53 行 → 106 行）。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §2（第 27-38 行） |
| vm-05 | 刷新重入守卫若不用 finally 复位 _refreshing，该页从此再也刷不动且比崩溃更难查。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §2 修法①（第 50 行） |
| vm-06 | SelectorItemVM 在 TaleWorlds.Core.ViewModelCollection，ctor 收 string，显示字在 StringItem，另有 IsSelected。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §3（第 91-92 行） |
| vm-07 | 用下拉项文字回查真实对象会因本地化文本重名而悄悄选错，必须用实例平行表 List<T> _order。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §3（第 93-94 行） |
| vm-08 | 下标 0 常留作「自动」，解析式为 value <= 0 ? null : _order[value - 1]；上界判据是 value > _order.Count 而非 >=。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §3（第 95-96 行） |
| vm-09 | 控件的 OnListItemAdded/Removed 只置 dirty 标记，真正刷新发生在 OnUpdate（下一帧），赋值完不会立刻一致。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §3（第 97-98 行） |
| vm-10 | CommandParameter.Click 传下来的是字符串，由 ViewModel.ExecuteCommand 按 VM 形参类型转换（ViewModel.cs:513）。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §4（第 102-104 行） |
| vm-11 | 日志必须写在提前返回之前，否则「点击没送到」与「参数不对」在日志里长得一模一样。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §4（第 106-109 行） |
| vm-12 | EditableTextWidget 的 Text setter 会发通知，回发则把光标弹到行尾；原版 MultiSelectionQueryPopUpVM.SearchText 也这么做。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §5（第 113-116 行） |
| vm-13 | 自做页签用 VM 上三个人工只读属性（=> _page == 0 等）并在切页时三个一起通知，不需要原版 TabControl。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §6（第 120-123 行） |
| vm-14 | 点亮当前页应走原版 ButtonBrush2 自带的 Selected 档加 IsSelected，不要自造刷子。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §6（第 124 行） |
| vm-15 | 多页直接排在竖直流里时外面必须套定高页区盒子，否则换到矮的一页底栏会往上飘。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §6（第 125 行） |
| vm-16 | 验证日志判据需含行数/池子大小（rows=53/53、pool=53/53）等字段；同一时刻两条且第二条数量翻倍即为重入。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §7（第 129-133 行） |
| vm-17 | 改完的验证顺序为编译 → 部署 → 内容级校验（反编译部署件/cmp）→ 才进游戏；DLL 内字符串是 UTF-16LE。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §7（第 135-136 行） |
| vm-18 | ilspycmd 路径必须写 Windows 形式 D:/…，给 /d/… 它会报不存在。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel 头部（第 12 行） |
| vm-19 | 源技能声称其规则来自本机反编译源码 v1.3.15.110062。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel 头部（第 9 行） |
| vm-20 | 反编译对象为 TaleWorlds.GauntletUI.dll（控件）、TaleWorlds.Library.dll（ViewModel）、Core.ViewModelCollection.dll。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel 头部（第 13 行） |
| vm-21 | WidgetAttributeValueTypeBinding.CheckValueType 以 value.StartsWith("@") 判为 Binding 型。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §9（第 161 行） |
| vm-22 | WidgetAttributeValueTypeBindingPath.CheckValueType 要求首字符 { 且末字符 } 才算 BindingPath 型。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §9（第 162 行） |
| vm-23 | Data 层只有一处建立绑定（:1280）gauntletView.BindData(key2, new BindingPath(value5))，只有 @ 进 BindData。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §9（第 163-164 行） |
| vm-24 | 花括号那条路只认 DataSource（Data 层 :1187~1192），别的键拿到花括号没有消费者。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §9（第 165-166 行） |
| vm-25 | WidgetTemplate.SetAttributes（PrefabSystem :1976~2017）只有 Default/Constant/Parameter 三分支，花括号不落任何分支。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §9（第 168-170 行） |
| vm-26 | 原版 Modules\SandBox\GUI\Prefabs 里 Text="{...}" 一处都没有，Text="@..." 到处是。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §9（第 172 行） |
| vm-27 | 实测 738 份预制件里带花括号的键名只有三个：DataSource（3537 次）、Parameter.*DataSource、DefaultValue（2 次）。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §9（第 173 行） |
| vm-28 | 八列表格表体每格空白的根因是写成 Text="{TownText}" 而非 Text="@TownText"；数据侧日志算出 947 行、绑了 120 行。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §9（第 156-158 行） |
| vm-29 | 值绑定一律 @、DataSource 一律 {列表名}，两者不通用；判据是 grep 'Text="{' 必须 0 条。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §9 守则（第 177-178 行） |
| vm-30 | SetWidgetAttributeFromStringAux（PrefabSystem :904~912）用 FindChild 找控件，返回 null 也照收不误、不抛不记。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §10 链条（第 186-187 行） |
| vm-31 | Widget.FindChild（GauntletUI :22686）只有 .. 走 ParentWidget，其余只按 Id 在直接子控件里找，找不到 return null。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §10 链条（第 188-189 行） |
| vm-32 | 滚动条若嵌进 ScrollablePanel 内部（孙控件），VerticalScrollbar 解析结果为 null。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §10 链条（第 190 行） |
| vm-33 | UpdateScrollablePanel（:19239）把竖直逻辑挂在 VerticalScrollbar != null 内；两滚动条皆 null 时 OnMouseScroll（:19103）无路走。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §10 链条（第 191-194 行） |
| vm-34 | 原版 SandBox 预制件 25 处 VerticalScrollbar 全是 ..\ 形式，无一处把滚动条当直接子控件。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §10 正确写法（第 196-197 行） |
| vm-35 | 预览图证明不了绑定：预览脚本第②步会把表体擦掉再用自己的演示数据重画，引擎绑没绑在成图上不留痕迹。 | 未标版本 | 是 | bannerlord-gauntlet-ui-viewmodel §11（第 202-208 行） |
| vm-36 | 预览脚本里把 @属性 换成演示值的替代表必须随新绑定属性补登记，否则预览会印出 @TownText 字面量（预览的假信号）。 | 未标版本 | 是 | bannerlord-gauntlet-ui-viewmodel §11 末条（第 214-215 行） |
| vm-37 | 门禁脚本 check_bindings.py 当时只判 @属性 与 DataSource="{属性}" 两种值形状，第三类（非 DataSource 键上的花括号）不落任何分支、一条都不报。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §12（第 219-221 行） |
| vm-38 | 该门禁跑一次需用 ilspycmd 拆本模组 DLL 加若干原版程序集，实测约 3 分钟；前台默认 120 s 超时会被砍。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §12 末注（第 228-230 行） |
| vm-39 | 该门禁 stdout 带缓冲，被超时砍掉之后日志是 0 字节，现场像脚本立刻崩了。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-viewmodel §12 末注（第 229-230 行） |
| F001 | sage 反编译索引只收 10 个程序集，TaleWorlds.InputSystem 不在其中。 | 未标版本 | 是 | bannerlord-gauntlet-ui-input-focus §1 源码在哪（只读） |
| F012 | RealText 只读不写回时 VM 侧永远拿到空串，回车提交静默无效，外在表现是发送按钮恒灰。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-input-focus §4 |
| F017 | SortedLayers 把 TopScreen.Layers 与全部 GlobalLayer 合并按 Order 排序 ⇒ 全局层与屏上层共用一个序，「全局层永远在下」不成立。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-input-focus §5.1 |
| F025 | 非焦点层的 IsKeyPressed(Escape) 恒为 false（不是没触发，是问了也是 false）。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-input-focus §5.3 |
| F026 | 因按键按层认领，同一帧不会出现弹窗取消与自己界面一起关的 Esc 双触发。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-input-focus §5.3 |
| F038 | FocusRequestId / AutoFocus / SubmitOnEnter 不是引擎内置属性：全库 grep TaleWorlds.GauntletUI 为 0 命中。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-input-focus §6 坑 |
| F040 | GamepadNavigationIndex 单独写不生效，必须有同级 NavigationScopeTargeter 声明 scope。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-input-focus §6 |
| F043 | 10 位 Color 值被引擎静默丢弃，控件渲染成全白/无色（AWAKE 已犯两次）。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-input-focus §6 |
| F046 | 按类型名扫字节找不到构造点（new X() 走元数据 token，不重复类型名字符串），须另跑 ilspycmd 找 new。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-input-focus §6 |
| F047 | Gauntlet 的 XML 绑定不支持取反或表达式，只有 @属性名 一种写法；实测全 Modules 扫描取反/逻辑表达式命中数为 0。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-input-focus §9 数据绑定 |
| F059 | CreateFromMemory 具体接受哪些格式族（PNG / JPG / BMP）仍未证。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-runtime-textures §6 |
| F060 | CreateFromByteArray(data, w, h) 的通道序（RGBA / BGRA）与行序仍未证。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-runtime-textures §6 |
| F063 | Texture.LoadTextureFromPath(fileName, folder) 的 folder 语义未验。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-runtime-textures §6 |
| F064 | Texture.SaveToFile(path, isRelativePath) 能否在无渲染上下文的进程里用未验，这决定离线把 3D 肖像渲成 png 可不可行。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-runtime-textures §6 |
| F067 | 读写两侧共用同一套平台路径解析（Texture.CreateTextureFromPath 与 FileHelper 同源）⇒ 手拼绝对路径必然分叉。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-runtime-textures §6 |
| F079 | widget 类必须 public 且恰好一个 (UIContext) 构造函数，否则静默拿不到实例。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-runtime-textures §3 |
| F100 | Texture.Release() 第一句就摸 RenderTargetComponent.OnTargetReleased()，对文件来的纹理那东西是不是 null 未验。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-runtime-textures §5.2 |
| F101 | 反复换图是否累积 GPU 纹理未验。 | v1.3.15 | 是 | bannerlord-gauntlet-ui-runtime-textures §5.2 |
| F104 | ilspycmd 是原生 exe，不认 MSYS 的 /d/ 路径，必须传 D:\... 否则静默不产出。 | 未标版本 | 是 | bannerlord-gauntlet-ui-runtime-textures §7 取证手法（本机） |
| F106 | tr -c '[:print:]' 这类字节探针实测会漏：阳性对照 GauntletMovie 给 0 命中，而它确实存在。 | 未标版本 | 是 | bannerlord-gauntlet-ui-runtime-textures §7 |
| F107 | 恒 0 命中的探针等于没验过，必须先做阳性对照再信结果。 | 未标版本 | 是 | bannerlord-gauntlet-ui-runtime-textures §7 |
| F108 | bash 里 grep 的花括号 {n,m} 会被 shell 吃掉。 | bash（本机已无） | 是 | bannerlord-gauntlet-ui-runtime-textures §7 |
| F122 | 部署 DLL 的 .NET 串堆是 UTF-16LE，strings -e l 读不出来，得按字节找。 | v1.3.15 | 是 | bannerlord-ui-copy-parity-gate §1⑥ |
| F127 | 含中文或引号嵌套的脚本用 python -c 或 bash 内联会吃引号、坏编码。 | 未标版本 | 是 | bannerlord-ui-copy-parity-gate §4 附：写脚本的纪律 |
| F128 | 同一消息里对同一文件发两条 Edit 会互相覆盖（Claude Code 工具行为）。 | Claude Code harness（未标版本） | 是 | bannerlord-ui-copy-parity-gate §4 |
| F008 | 检索链路上没有拼音通道，拼音首字母（gk／bmjfk）不是设计内的输入。 | 未标版本 | 是 | worldbook-retrieval-redtest §2 样本表·拼音首字母行 |
| F027 | 当前包 R3 实测剔 0 个，grep -c '"doc.' runtime.json 也是 0，编译器 K1 已生效。 | 09-17 当前包 | 是 | worldbook-retrieval-redtest §5c ⚠️R3 实测纠偏 |
| F047 | 「城堡」「城镇」问出去是 not_found（空手），与「村庄」的行为不一致。 | 09-17 当前包 | 是 | worldbook-retrieval-redtest §5f 症状 |
| F057 | WrapsGenericCategoryWord 在真语料 0 条命中，是一条空转的防复发闸。 | 09-17 当前包 | 是 | worldbook-retrieval-redtest §5f 现状 |
| F094 | 仓里没有可信的繁简对照表来源。 | 未标版本 | 是 | worldbook-retrieval-redtest §7d 不造表 |
| F098 | 用 Add-Type 探 LCMapStringEx 会被安全策略拦，报 compiles and loads .NET code at runtime。 | 09-17 本机策略 | 是 | worldbook-retrieval-redtest §7d ⚠️Add-Type |
| F100 | 字序差异折不了：斯特基亚 那类（斯特 vs 斯提）不是繁简问题。 | 09-17 起 | 是 | worldbook-retrieval-redtest §7d 边界 |
| F104 | RagHit（framework/MarcusAwakeFramework/src/StorageAndRagApi.cs:135）只有 Rank，协议里没有分数 ⇒ 游戏侧拿不到分数、没法再卡一道。 | v1.3.15 源码基线 | 是 | worldbook-retrieval-redtest §7e ② |
| F011 | 后台任务的 TaskOutput 可能返回 not found 且不发出完成通知 | 未标版本（旧 harness） | 是 | worldbook-encyclopedia-rollout-batch 步骤3·后台跑进度只看文件系统 |
| F037 | validate 的 --path 是无效参数：service.Validate() 不吃 path，恒校验整个工作区 | 未标版本 | 是 | worldbook-encyclopedia-rollout-batch 步骤9·三级验证2 |
| F045 | entity.lore.* 锚点不可编译（WB-DOC-003：entity_ids 类型仅支持 hero/clan/settlement）；全库 56 档概念型条目同样不写 entity_ids | 未标版本 | 是 | worldbook-encyclopedia-rollout-batch 坑清单·entity.lore 锚点不可编译 |
| F054 | 游戏城堡英文名恒为「XXX Castle」，所以全文替换 castle→castles 会让 67 个 castle 档的词尾专名一起变 | v1.3.15 | 是 | worldbook-encyclopedia-rollout-batch 步骤9·坑1 |
| F078 | DB 路径 <...>/BannerlordSage-main/dist/games/bannerlord/bannerlord.db，以 mode=ro 只读打开 | v1.3.15 数据快照 | 是 | worldbook-encyclopedia-rollout-batch 步骤1·取数 |
| F079 | bannerlord_settlements 含 settlementType/culture/name/descriptionText/boundSettlement/villageType | v1.3.15 | 是 | worldbook-encyclopedia-rollout-batch 步骤1·取数 |
| F080 | name/descriptionText 是 {=token} 形式，token 可与 id 不同名，必须从字段本身解析 token 再查 CNs，不能按 id 猜 | v1.3.15 | 是 | worldbook-encyclopedia-rollout-batch 步骤1·取数 |
| F081 | bannerlord_items 表的列名是 entityId（不是 itemId） | v1.3.15 | 是 | worldbook-encyclopedia-rollout-batch 坑清单·兵种装备取数 |
| F082 | bannerlord_items 漏了近战武器（刀剑枪斧锤在 <CraftedItem> 里，索引器没收），要一手 XML | v1.3.15 | 是 | worldbook-encyclopedia-rollout-batch 坑清单·兵种装备取数 |
| F083 | 城堡的 descriptionText 全空（游戏不给城堡写官方描述文） | v1.3.15 | 是 | worldbook-encyclopedia-rollout-batch 步骤8·城堡档 |
| F084 | localization 里 text.castle* 全是 castle_village_*，没有城堡自身的描述文 | v1.3.15 | 是 | worldbook-encyclopedia-rollout-batch 步骤8·城堡档 |
| F085 | 城堡的 A 级锚＝官方名 Settlement.name.castle_*；快照 game-castles-desc.txt 每堡一行含官方 CN 名 | v1.3.15 | 是 | worldbook-encyclopedia-rollout-batch 步骤8·城堡档 |
| F086 | 查城堡下辖村的 SQL 必须补 Settlement. 前缀：boundSettlement='Settlement.'+castleSid 才 join 得上 | v1.3.15 | 是 | worldbook-encyclopedia-rollout-batch 步骤8·下辖村 |
| F087 | 官方数据坑：个别村的官方 CNs 错挂他文（castle_village_A7_1 乌格巴） | v1.3.15 | 是 | worldbook-encyclopedia-rollout-batch 步骤8·两个官方数据坑 |
| F088 | 堡名与首村名 CN 常不一致（castle_S3＝涅维扬斯克堡 vs 村＝涅夫扬斯克），照官方串写不得修正 | v1.3.15 | 是 | worldbook-encyclopedia-rollout-batch 步骤8·两个官方数据坑 |
| F089 | 67 堡里 66 座与下属村同名（堡名＝村名＋「堡」），唯一例外 castle_S3 | v1.3.15 | 是 | worldbook-encyclopedia-rollout-batch 步骤8·同名聚落与检索权重 |
| F090 | 67 堡全量联系探针共 135 条 query，09-14 实测 135/135 命中目标条目 | v1.3.15 | 是 | worldbook-encyclopedia-rollout-batch 步骤8·全量联系探针 |
| F106 | 档名规范报告 §一 的词表原表不含 castle 前缀（标为 P5 待补） | 未标版本 | 是 | worldbook-encyclopedia-rollout-batch 步骤5·文件名前缀不是分类轴 |
| F109 | 09-20 实测 ls *.yaml \| wc -l 给出 411，真值 483，静默少算 | 未标版本 | 是 | worldbook-encyclopedia-rollout-batch 步骤5·🪤数档别用 ls|wc -l |
| F110 | Windows 自带 find.exe 与 Git Bash /usr/bin/find 同名冲突，不走 /usr/bin/ 会报「参数格式不正确」 | 未标版本（旧 Git Bash harness） | 是 | worldbook-encyclopedia-rollout-batch 步骤5·🪤数档 |
| F111 | Git Bash 的 grep 对含中文的正则可能静默 0 命中（09-14 误判「日志无条目」） | 未标版本（旧 Git Bash harness） | 是 | worldbook-encyclopedia-rollout-batch 坑清单·Git Bash grep 中文 |
| F113 | 单次 Write 一次写太多中文（如 60+ 条 L2）会被长度上限截断，症状是文件尾半句戛然而止 | 未标版本 | 是 | worldbook-encyclopedia-rollout-batch 坑清单·单次 Write 长度上限 |
| F123 | game-villages-desc-*.txt 里 village.village_B2_1 与 village.castle_village_B2_1 两行都有 | v1.3.15 快照 | 是 | worldbook-encyclopedia-rollout-batch 坑清单·按 id 找来源行 |
| F124 | 兵种装备取数 XML：spnpccharacters.xml 的 <NPCCharacter> → <Equipments> → <EquipmentRoster> → <equipment> | v1.3.15 | 是 | worldbook-encyclopedia-rollout-batch 坑清单·兵种装备取数 |
| F125 | 武器部件三处落点：items/weapons.xml、Native/crafting_pieces.xml、Native/crafting_templates.xml | v1.3.15 | 是 | worldbook-encyclopedia-rollout-batch 坑清单·武器部件 |
| F126 | 具装骑兵的 Item1 就是 heavy_horsemans_kite_shield，防御力全游最高档，能顶着箭雨推进 | v1.3.15 | 是 | worldbook-encyclopedia-rollout-batch 坑清单·B级编年史料须对表 |
| F128 | package.ps1 的 release-check 会被既有坏账拦住（09-14：A3.3 preview golden 是跨仓库副本快照，本仓库一直红） | 未标版本 | 是 | worldbook-encyclopedia-rollout-batch 坑清单·package.ps1 release-check |
| F02 | WorldbookRuntime.Current（旧代入口）在现行仓中已无任何消费者 | 未标版本 | 是 | worldbook-field-liveness-audit / L34 |
| F08 | ContextModes 在现行链路 WorldKnowledgeQueryService 上是死字段，只在旧代 WorldbookService 上是打分项 | 未标版本 | 是 | worldbook-field-liveness-audit / L45 |
| F12 | 运行时 HasMatchingDeny() 的否决分支因 denies 全空而从未执行过 | 未标版本 | 是 | worldbook-field-liveness-audit / L83 |
| F13 | fallback_referral_ids 未接线（记为 C13），且被 Studio golden 钉死，属跨线批次 | 未标版本 | 是 | worldbook-field-liveness-audit / L84 |
| F14 | RuntimePackageCompiler.cs:135 为无 expressions 文档合成 grants=[] 兜底，SelectExpression 需 grant 命中 ⇒ 不可达 | 未标版本 | 是 | worldbook-field-liveness-audit / L85 |
| F23 | 本项目 .py 入口硬调 pwsh，而本机只有 Windows PowerShell 5.1，该入口从来没跑过 | 未标版本 | 是 | worldbook-field-liveness-audit / L100 前置先验 |
| F31 | 仓库无 ModuleData\Worldbook\manifest.json ⇒ 不跳过时 Assert-SourceManifest:456 直接 throw | 未标版本 | 是 | worldbook-field-liveness-audit / L132 |
| F32 | LocateManifest() 返 null 只在仓库侧成立，游戏目录侧是有 manifest 的 | 未标版本 | 是 | worldbook-field-liveness-audit / L132 |
| F35 | 运行时只认包内单数 Definition，不按 characterId 挑卡 | 未标版本 | 是 | worldbook-field-liveness-audit / L134 |
| F39 | .gitignore 原文以 AWAKE/ModuleData/Worldbook/* 整条排除，导致组出的运行时包永远进不了库 | 未标版本 | 是 | worldbook-field-liveness-audit / L167 第六类失效 |
| F40 | sync_module.ps1 的 $managedWorldbookDirectories 里没有 packages，运行时包永远投不到游戏目录 | 未标版本 | 是 | worldbook-field-liveness-audit / L168 |
| F48 | 检索函数 FindCandidates 只查 _snapshot.KeywordIndex，完全不碰 Summary | 未标版本 | 是 | worldbook-field-liveness-audit / L211 |
| F50 | NpcMemoryService.RecordEventFactAsync（src/NpcMemoryService.cs:334）全仓零调用方 | 未标版本 | 是 | worldbook-field-liveness-audit / L232 第九类失效 9.1 |
| F60 | referral-registry.v1.json 与 profile-registry.v1.json 被 golden 钉死，改字节即打掉测试套件 | 未标版本 | 是 | worldbook-field-liveness-audit / L295 顺带必查 |
| f25 | 本代新增 76 档里有出边的从 0 升到 61 | 未标版本 | 是 | awake-worldbook-new-compile-input §〇-2 ＋ §五-读数 |
| f28 | 含 entity.lore.* 锚点的档案编译必失败（结构校验全过、Compile() 一跑才炸） | 未标版本 | 是 | worldbook-anchor-verification §三 |
| f33 | 城堡没有 .text.* 描述文；v1.4.8 实测 67 座城堡 0 条 text，城镇 53/53、城堡村庄 132/132、村庄 141/142 有 text | v1.4.8 | 是 | worldbook-anchor-verification §一-第2步 |
| f35 | settlementId 大小写敏感，写成小写会静默返回 0 行，看着像「不存在」 | 未标版本 | 是 | worldbook-anchor-verification §一-第2步 |
| f48 | 不区分 DF 高成因会把 398 条真边当噪声丢掉，技能称占应进图边数的 41% | 未标版本 | 是 | worldbook-should-link-judgement §二-坑① |
| f49 | 孤立档读数随口径变化：任何提及边＋归属边 13、专名＋高频专名＋归属边 18、再减概念词条 15、仅专名强边 34 | 未标版本 | 是 | worldbook-should-link-judgement §二-坑② |
| f51 | 技能称交叉验证「290 对里 161 对（56%）被正文互引覆盖」，但现行 hierarchy 文件只有 274 条边，分母对不上 | 未标版本 | 是 | worldbook-should-link-judgement §五 |
| f52 | v1 时代同一批边按「有向对」数 vs 按「边」数会差出 78 条 | 未标版本 | 是 | worldbook-should-link-judgement §四-纪律2 |

## 证据

- **bgda-021** 读建表定义与实测查询。
- **bgda-022** 读该表列清单。
- **bgda-023** 读该表列清单：只有 heroId/faction/clan/spouse/father/mother/alive/isTemplate/text。
- **bgda-025** 对多个 heroId 实测拼接查询，均 0 行。
- **bgda-026** 对 localization_entries.language 去重。
- **bgda-027** 实测 language='EN' AND text='Lucon' 返回 0 行。
- **bgda-041** 2026-09-12 实测翻车记录：官方文本还有 bannerlord_settlements.descriptionText 一处存放地。
- **bgda-044** 实测该表内容；要看 SizePolicy/ScaledSuggestedWidth 怎么算必须读 .cs。
- **bgda-045** 列只有 displayName/descriptionText/proposalText/effectsText/rulerSupport/lordsSupport/commonsSupport/defaultCultureIdsJson。
- **bgda-047** 政策表无数值列 + 数值硬编码在模型，两条合证。
- **bgda-048** 该表无这些列；属性写在 Modules/SandBoxCore/ModuleData/spcultures.xml。
- **bgda-062** 在 assets/Source/ 全量 grep 后计数为 0。
- **bgda-063** 逐条看命中上下文后排除。
- **bgda-065** 实测 C:/Users/... 路径导致字段错位；改用 awk -F: '{s+=$NF}' 取最后一段。
- **bgda-068** 实测查询 SizePolicy* 返回 1 条。
- **bgda-069** SizePolicy / LayoutMethod 这类子串直觉查询的实测结果。
- **bgda-076** src/tools/search-source.ts:27 硬编码 rg，无 env 可覆盖。
- **bgda-077** 实测查询报错；1.3.15 里真名已改。
- **bgda-079** 实测该工具返回内容。
- **bgda-083** 实测其正则只抓 DataSource= 与 Command.Click=。
- **bgda-084** 实测 castle_v6 返回 0 行，实为大小写写错。
- **bgda-086** 实测该 stringId 查询返回 0 行。
- **bgda-087** 由 castle_V6 无描述文这一事实推出。
- **bgda-088** 实测按文本搜 xml_entities 找不到聚落。
- **bgda-092** 实测聚落描述文只在该列，取中文需剥 hash 再查 CNs。
- **bgda-095** 2026-09-18 实测：OwnedAlleys 显示成 n、OnAlleyOwnerChanged 显示成 nOwnerChanged，极易误判为混淆。
- **bgda-096** assets/Source/ 下目录清单实测。
- **bgda-098** 实测 <settlement\b[^>]*> 因属性跨行而 0 命中。
- **bgda-103** 读 ItemCategory 与 ItemObject 源码。
- **bgda-114** 2026-09-18 实测读 ModuleData/Languages/CNs/*.xml 得到 \x00 交错乱码。
- **bgda-126** 供给取货架值而非本地产量，由第 6 条公式推出。
- **bgda-149** 实测命令不存在导致的空结果。
- **bgda-152** 全库 grep 后确认唯一读者，且仅 extended 且是城时显示。
- **bgda-154** 读 Native/ModuleData/module_strings.xml 与 Languages/CNs/std_module_strings_xml-zho-CN.xml。
- **bgda-155** 查 SettlementDecision 零命中，属索引未覆盖而非类不存在。
- **bgda-160** DefaultItems.Create("grain") → RegisterPresumedObject，故 bannerlord_items 零命中。
- **bgda-161** 该文件里一个 <Clan 都没有（曾为此白跑一轮）。
- **bgda-164** 实例：CampaignSpeedModel → DefaultPartySpeedCalculatingModel。
- **bgda-167** 由 grain/meat 零命中、Sarapios 误判两例反推。
- **F-006** 在 Git Bash 下用 /d/ 路径实跑 ilspycmd，命令 exit 0 且输出目录为空
- **F-009** grep -l -a GraphWidget 扫 bin 与各 Modules 的 Win64_Shipping_Client/*.dll，仅定义方命中
- **F-010** 实跑循环扫全目录，进程被 SIGTERM，输出不完整但形态与正常结束相同
- **F-013** 在 Git Bash 里跑 find . -name 与 sort -u，报出中文 Windows 错误文本
- **F-014** 实跑带花括号的 -E 正则，报错文本就是被曲解后的参数片段
- **F-016** 实跑 rg，输出里命中词显示为 n，加 --color never 后恢复正常文本
- **F-017** 实跑 strings --version 确认该命令不存在，写在循环里则每轮都失败
- **F-018** 对比 sage 索引覆盖范围与 Modules\Native\bin\Win64_Shipping_Client 的实际 DLL 清单
- **F-023** 通读 GraphWidget 的全部 Refresh* 方法，LineBrush 只有赋值处、没有消费处
- **F-024** 普查官方 Prefabs 里 GraphWidget 的用法，未见贴图引用
- **F-029** 在出货 DLL 反编译树里 grep 该事件的触发调用，无命中；触发方实际在 VM 层
- **F-035** rg 该字段名于反编译树，仅 1 处命中且就在定义文件内
- **F-036** rg 该属性名于反编译树，只命中定义处
- **F-056** 在 SandBox.View.dll 反编译树里搜该构造函数调用点，无命中
- **F-057** 官方 Id 反引用例 FillWidget="FillVisual" 不在 ItemTemplate 里，没有现成样板可依
- **F-062** 在 Mod_Logic.txt／SETS.log／Event_Logs.txt 搜 mode=onnx 得 0 命中，改查 Timeline 才见 onnx=True
- **F-069** 在 AnimusForge DLL 上跑该命令与 strings -a 对比，后者恒为 0
- **F-070** 实跑 grep -rn 形式，无任何输出且退出码为 0
- **F-071** 09-19 实跑带 timeout 前缀的命令，报错文本来自 Windows timeout.exe 而非脚本
- **F-073** 实跑 os.walk 于 /d/ 路径，返回 0 个文件
- **F-074** 同一批日志里 0 命中，而 FreezeWatchdog_Timeline.txt 有 onnx=True 的实证
- **T02** 对现行 gauntlet_ui.tpac 实扫条目与头部字段逐条比对，头部计数与实扫数不符。
- **T03** 读 Native/EmAssetPackages/core/core.tpac 文件头实测体积与偏移 28 的 TOC 长度。
- **T04** 读 Native/AssetPackages/core_game.tpac 文件头实测体积与 TOC 长度。
- **T06** 第一版按 d[36:52] 当条目 guid，结果漏掉 90% 条目；改用名字串反查才列全。
- **T10** 逐个条目比较 stored 与 expanded 字段得出压缩与否的清单。
- **T11** 在现行 gauntlet_ui.tpac 的未压缩条目 _4/_5/_8 上实测触发该异常串。
- **T13** 对 paper_texture_tile 解码结果按 RGBA 与换通道两种口径各算一次均值，恰为 R/B 互换。
- **T14** 解 ui_textures_4 后统计均值恒为 (144,144,144)，故该通道错误长期未被发现。
- **T15** 挑名字自带颜色方向的条目 warm_overlay 解码后算均值，得到暖色且 R>G>B。
- **T16** 对两个 build 的 NativeSpriteData.xml 与解出的图集尺寸做对照表。
- **T17** 同上版本对照表，宽差 1px 即会让整张图行错位。
- **T18** 同上版本对照表。
- **T19** 两版 NativeSpriteData.xml 的 sheet 序号与名字逐格对照。
- **T22** 读 ui_textures_4 的 36B 定位器 expanded 字段，与 sheet 尺寸相乘结果比较。
- **T23** 把 NativeSpriteData.xml 的 SpriteSheetSize 与 SpritePart 尺寸对照得出。
- **T24** 实测错误解码同样输出纯色图，故该判据无效，改用统计特征。
- **T25** 对解出的图标图集统计 alpha 分布得出透明/实心占比。
- **T26** 在该 venv 内 pip install lz4 zstandard numpy pillow texture2ddecoder 后实测这些函数可用。
- **T26b** skill §3 记录该 venv 路径，并说明用其中的 python.exe 装解码依赖。
- **T27** 原文记录 bash 沙箱无外网，只能走 PowerShell 装包并落日志再读。
- **T28** 解 Native/EmAssetPackages/core/core.tpac 的 custom_banner_icons 系列条目后统计尺寸与格数。
- **T29** 在 Native/EmAssetPackages/core/core.tpac 中按名字反查得到该条目名列表。
- **T30** 对该包列条目名后统计，只有 8 条且都是 mesh。
- **T31** 分别读 Native 与 SandBox 两处 gauntlet_ui.tpac 的文件体积与 TOC 长度。
- **T32** 用 NativeSpriteData.xml 的分类名与 sheetID 拼名后能在对应 tpac 里反查到条目。
- **T33** 对该包逐条列名并统计条目数，另有 ui_encyclopedia_1、ui_barter_1、ui_options_1、ui_mploading_*。
- **T34** 读 NativeSpriteData.xml 中 ui_textures 分类的 AlwaysLoad 标记与 sheet 定义。
- **T35** 反查条目解出图并统计，尺寸与格式取自现行 NativeSpriteData.xml 与定义体格式串。
- **T36** 解出后算全图均值，作为 UI 美术的纸面基准。
- **T37** 解出后算全图均值。
- **T38** 该条目 stored==expanded 未压缩，按 decode_bc3 解出后算均值。
- **T39** 该条目未压缩，按 BC7 解出后算均值。
- **T40** 解出后观察为纯白滑块底图。
- **T41** 解出后算均值；旧版该行是 warm_overlay 30×30。
- **T42** 解出后算均值；旧版没有这一格。
- **T43** 对 build 31530 与 build 110062 各解一遍同名条目后逐像素比较。
- **T44** 反查条目后打印定义体，读出其中的源路径串。
- **T45** 按 XML 名反查失败，剥掉尾部 _\d\d 后才在 core.tpac 命中。
- **T46** 读 SandBox\GUI\Brushes\MapBar.xml 的 BrushLayer 并与界面实际含义比对。
- **T47** 读 Modules\SandBox\GUI\SandBoxSpriteData.xml 的 <SpritePart> 节点字段。
- **T48** 按 CategoryName=ui_mapbar + SheetID=1 拼名反查该包并读出 sheet 尺寸。
- **T49** 读 SandBoxSpriteData.xml 中各 SpritePart 的 SheetX/SheetY 实测分布。
- **T50** 在 Native 与 SandBox 两处包内反查 ui_mapbar 分类，只在 SandBox 命中。
- **T51** 读 graphics 层既有实现的 Brush 定义，发现其 Sprite 指向另一分类。
- **T53** 反推 8 家王国 spkingdoms.xml 的 banner_key 数值分布。
- **T54** 对照 banner_icons.xml 的 Background 表与 8 家王国 key 得出。
- **T56** 统计 banner_icons.xml 中 Background 节点数。
- **T57** 统计 banner_icons.xml 尾部 BannerColors 段内 Color 节点数。
- **T58** 解出图标图集后统计实心像素颜色，只有两种纯色通道值。
- **T59** 按通道约定复算 Lake Rats 图标描边色，与 banner_key 里的 ColorId2 吻合。
- **T61** 解出 8 个被用到的底纹后观察坐标范围与顶点分组。
- **T63** 解出这 8 个网格并渲染后逐一辨认形状。
- **T64** 读 TaleWorlds.Core 的 Banner.cs 中两个取值方法的实现。
- **T64b** 反编译该方法并与旗面数据中 93 面平底旗 c1==c2 的现象对照。
- **T65** 反编译 BannerVisual.ConvertToMultiMesh() 读其赋值顺序；图标层则是正序 Color=ColorId。
- **T66** 反编译随机图标生成路径读到该取值，并核对调色板末项为 116。
- **T67** 该图不透明像素几乎只有蓝通道≈0.5、没有绿通道掩码，且为手工拼版非规则网格。
- **T68** 原文自述本轮只取顶点与索引，其余流未解。
- **T69** 在 Native 模块下同时观察到两套目录且内容有重叠。
- **T70** 原文记录该 MCP 源码检索依赖 rg 而本机 PATH 无 rg，只有 read_csharp_type 可用。
- **T71** 原文记录 read_csharp_type 输出折叠长方法体，改按行读才拿到完整实现。
- **S01** skill 开头先决条件表记录的本机实测路径与版本号。
- **S02** skill 先决条件表记录的实测路径。
- **S04** skill 先决条件表记录的本机实测 SDK 版本与路径。
- **S05** skill 先决条件表记录的实测工具路径。
- **S06** skill 先决条件表与 §7 起手顺序均以该目录为抄写来源。
- **S14** 反编译 CampaignBehaviorBase 未见该虚方法，会话启动只能靠事件监听。
- **S14b** skill §4 给出的事件注册写法与回调签名。
- **S15** skill 明确记录 Campaign 无 Heroes 属性及替代访问路径。
- **S16** skill §4 记录日志落点由 Assembly.GetExecutingAssembly().Location 决定。
- **S17** skill §4 明确要求日志写必须吞异常，否则拖垮游戏。
- **S18** 症状为每次保存弹「保存出错！」，反推 SaveContext 按类型查表而非按字段跳过。
- **S19** skill §6.1 抄录的游戏内实际日志原文。
- **S19b** skill §6.1 症状日志块中与上一条同时出现的两行。
- **S20** ilspycmd -t TaleWorlds.SaveSystem.SaveableBasicTypeDefiner 反编译读出定义；另有 int[]、Dictionary<int,int>。
- **S21** ilspycmd 反编译 TaleWorlds.CampaignSystem.dll 读出定义；另有 Hero/Settlement/MobileParty、Dictionary<Hero,int>。
- **S22** 对照两张定义表的 typeof(...) 建允许集后，Dictionary<Town,int> 不在其中，实测该字段导致存档全挂。
- **S23** 在 SaveableCampaignTypeDefiner 的定义里查到该组合，且它被判为 OK 的阳性对照。
- **S24** skill §6.1 改法乙引用的原版先例，另有 ConstructContainerDefinition 注册容器。
- **S25** 原文记录把这条先例读反的代价是让别人的存档坏掉。
- **S26** skill §6.1 改法乙的风险说明。
- **S27** 读该 XML 第 64 行起的 ListPanel；其 ItemTemplate 用 IconOffsetButtonWidget。
- **S28** 反编译 MapNavigationVM 构造函数读其循环逻辑，参数来自 navigationHandler.GetElements()。
- **S29** 由 MapNavigationVM 的填充逻辑与 INavigationElement 接口成员推出。
- **S30** 反编译接口读出成员清单，另有 IsLockingNavigation、HasAlert、AlertTooltip，见 §8.3。
- **S31** 反编译读出该类型为 struct 及其构造函数参数。
- **S32** 反编译 MapNavigationItemVM 读出构造函数可见性与 ItemId 赋值。
- **S33** 反编译 TextObject 未见 Empty 成员，只有 GetEmpty()。
- **S34** skill §8.3 记录该现象且不报错。
- **S35** skill §8.1 第 1 步记录的基类构造函数签名约束。
- **S36** skill §8.3 记录的面板 VM 绑定要求。
- **S38** skill §8.4 记录源码树缺该界面层程序集，凡涉及 VM 必须读 DLL。
- **S39** skill §8.4 记录在 bin\Win64_Shipping_Client 与内置模组 bin 里按类名捞到该类。
- **S40** skill §8.1 第 3 步记录的层构造与优先级区间。
- **S42** skill §8.1 第 2 步记录的重复进入路径与查重要求。
- **S43** skill §6 明确把这一步列为离线验证的盲区。
- **S44** skill §6 记录的排查结论。
- **S45** skill §1 目录结构里标注的 csproj 关键项。
- **S46** skill §6.1 末尾记录的现成模板路径与内容说明。
- **S47** skill §1 与 §5 记录的脚本职责与路径。
- **F001** 对全库预制件统计 WidthSizePolicy 取值分布（约 2.5 万处），未发现绑定式取值；技能自述的普查结果。
- **F004** 读引擎布局分支：StretchToParent 时尺寸取自 measureSpec 分配，Suggested 值不参与测量。
- **F005** 读本地预览脚本 preview_prefab_geometry.py 中那条 pw_ 计算，StretchToParent 孩子一律铺满父级。
- **F010** 在横排 ListPanel 里超量追加子控件后观察渲染结果：末位子控件消失且无任何日志或异常。
- **F011** 对比流式容器的 offsets() 算法与 GridLayout 的格位算式，两者对 GridWidget 结果不一致。
- **F039** 把超宽字符串放进 RichTextWidget 渲染，只发生裁切，无日志无异常。
- **F044** 预览脚本里新增列若不物理摘掉或不落成字面量，@ 绑定与 IsVisible=false 都不会生效。
- **F063** 记账页注释记录的坑：绑定表达式不支持取反，漏通知后两档控件同时可见。
- **f002** 同上逐目录枚举，7 个官方模块四类目录全部不存在。
- **f006** 读官方与模组 SubModule.xml，未见资产包或图集声明节点。
- **f014** grep AlwaysLoad SandBoxSpriteData.xml 得 0 命中。
- **f015** 二分定位实证：两份内容相同、只差 XML 声明的探针 Config.xml 跑生成器后数 AlwaysLoad。
- **f016** 先怀疑中文注释、改 ASCII 后仍失败，最终定位到 XML 声明；注释本身同为杀手。
- **f017** AWAKE 仓库 Config.xml 实测形态之一，跑生成器后 AlwaysLoad=0。
- **f018** 带声明探针跑完，进程返回 0 且图集正常生成，只有 AlwaysLoad 数变 0。
- **f019** 技能自记：无法判定生成器读的是干净部署副本，还是"命中即丢弃"在该版本不成立。
- **f021** 逐官方模块找 Config.xml 全无；grep NativeSpriteData.xml 得 4 处 AlwaysLoad。
- **f022** 逐个查 5 份 Config.xml（含官方 LauncherGUI 那份）搜 SpriteSheetSize，零命中。
- **f027** 读 sync_module.ps1 的托管文件清单。
- **f033** sprite 侧机制名叫 <NineRegionSprite> 不叫 Extend，官方实际大量使用。
- **f045** grep Prefab 的 Sprite=/Brush= 结果对照。
- **f047** 逐项对索引分类与图集目录的存在性。
- **f048** AWAKE 实测：目录存在不等于已登记，游戏无任何提示。
- **f050** 在索引与 Brushes 里搜这些 sprite 名，零引用。
- **f052** 读 AWAKESpriteData.xml 各分类的 SheetX+Width 最大值。
- **f053** 容量满时加新件的实测现象。
- **f056** ls -d <游戏>/bin/Win64_Shipping_wEditor 无结果。
- **f059** 在 6 个 Prefab 上跑该 grep，零命中。
- **f066** 2026-09-14 夜抠出官方材质贴图成品，推翻原否定结论。
- **f073** 访问 store.steampowered.com/dlc/261550 得 4 条，无 1393600。
- **f074** Tool 类 app 不设商店页，访问实测跳转。
- **f075** 枚举 librarycache 目录，对照游戏本体五项俱全。
- **f078** 技能记录的版本对齐要求与崩溃后果。
- **f079** 官方原文称其提供的是在 Modding Kit 内使用 War Sails 内容的文件。
- **f084** 2026-09-19 实测；异常类型原文 DirectoryNotFoundException。
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
- **brush-01** 反编译 TaleWorlds.GauntletUI.dll：BrushFactory.LoadBrushes() → GetBrushesNames()；Base.xml 由原版提供。
- **brush-02** 反编译 BrushFactory 加载路径后对全库模块 SubModule.xml 统计，Brush/SpriteData 声明数为 0。
- **brush-03** 原版 GUI/Brushes/ 词频实测：1083 / 452 / 430 / 324 / 275；另有 SelectedDisabled、Opened、Invalid 少用。
- **brush-04** 反编译 TaleWorlds.GauntletUI.BaseTypes/ButtonWidget.cs:111 RefreshState()。
- **brush-05** 反编译 ButtonWidget.cs 的 DominantSelectedState 默认值（同文件 RefreshState 优先级链）。
- **brush-06** 反编译 ButtonWidget.cs:287 HandleClick()：foreach(ClickEventHandlers) h(this); bool isSelected = IsSelected; if(IsToggle)…else if(IsRadio)…
- **brush-07** 反编译 ButtonWidget.cs:287 HandleClick() 的 if(IsToggle)/else if(IsRadio) 两分支，缺省不进入。
- **brush-08** 反编译 TaleWorlds.GauntletUI 的 CircleActionSelectorWidget.cs:307。
- **brush-09** 反编译 TaleWorlds.GauntletUI/EventManager.cs:879 CollectEnableWidgetsAt。
- **brush-10** 反编译 BrushFactory.LoadStyleInto → style.GetLayer(name) → LoadBrushLayerInto。
- **brush-11** 由 brush-10 的增量赋值语义推出；对照原版 Native/GUI/Brushes/Main.xml 的 Popup.CloseButton。
- **brush-12** 反编译 BrushLayer 的 Extend* 消费点；渲染语义为四角原样、四边单向拉伸、中心双向拉伸。
- **brush-13** 对游戏根 Native/GUI/Brushes/*.xml 与全库 *SpriteData.xml 做元素名计数。
- **brush-14** 读 Native/GUI/NativeSpriteData.xml:40392 的 <NineRegionSprite> 块（Name/SpritePartName/LeftWidth/RightWidth/TopHeight/BottomHeight）。
- **brush-15** 读 Native/GUI/Brushes/Brush.xml:2-6 与使用方 Native/GUI/Prefabs/ButtonCancel.xml。
- **brush-16** 在本地 10 个反编译程序集里检索 NineRegion / LeftWidth，命中数为 0。
- **brush-17** Native/GUI/NativeSpriteData.xml:40392 的 43/44 与贴图 97×87 尺寸相加比对。
- **brush-18** 反编译 BrushFactory.cs:243-244 的 Rotation 分支解析语句。
- **brush-19** BrushFactory.cs:243-244 直接 Convert.ToSingle 无弧度换算；官方用例 MapIncident.xml:76 Rotation="45" 转菱形。
- **brush-20** 反编译 BrushLayer.cs:330 的 setter 与 Brush.cs:398 的 Clone()。
- **brush-21** 对 Widget 基类成员逐项核查，旋转相关成员数为 0；对应成员在 BrushLayer.cs:330。
- **brush-22** 反编译 BrushLayerState 与 BrushAnimationProperty 的成员表。
- **brush-23** 反编译 BrushLayer.Rotation 上的 [Editor(false)] 特性标注。
- **brush-24** 源技能自述「仍未验」，无实测记录。
- **brush-25** 对原版五个官方模块 Prefab 的 Color= 取值长度统计：729 处全 8 位、零例外。
- **brush-26** 对原版五个官方模块 GUI/Prefabs 的 Color= 逐处取值长度统计。
- **brush-27** 反编译 Brush/Widget 的 Color 与 AlphaFactor 取值消费点，两者运算方式不同。
- **brush-28** AWAKE 项目 tools/awake-ui-lab/ 的 sprite 解析实现；来源为项目内工具而非引擎反编译。
- **brush-29** AWAKE 项目 tools/awake-ui-lab/ 的 Brush 加载顺序与报告口径实现。
- **brush-30** 源技能以「换 Default 图重跑预览看是否变」的交叉验证得出，属实测推断。
- **brush-31** 对工作区文件 AWAKE/GUI/Brushes/AwakeBrushes.xml 的条目与 sprite 引用计数。
- **brush-32** 源技能自述未验；本地预览与真机渲染无对照记录。
- **brush-33** 对照 Native/GUI/Brushes/Brush.xml 与 Native/GUI/NativeSpriteData.xml 的 button_canvas_9 条目。
- **brush-34** 读原版 Brush XML 中 ButtonBrush1/2/3 的 Extend* 取值与贴图尺寸。
- **brush-35** 读原版 Brush XML 中 ButtonBrush4 的 ExtendLeft/Top/Right/Bottom 取值。
- **brush-36** 读 Native/GUI/Prefabs/Standard/Standard.Button.xml 的尺寸与 Brush 属性。
- **brush-37** 对全库 prefab 检索 Brush="ButtonBrush" 的命中数并逐个查看。
- **brush-38** 源技能对 button_canvas_9 贴图内容区实测，并对照 awake-ui-lab 渲染结果。
- **brush-39** 由 BrushFactory 全模块全量加载（brush-01）推得的同名覆盖语义。
- **brush-40** 源技能勘误注记，依据是 NativeSpriteData.xml 的 NineRegionSprite 注册表机制存在。
- **vm-01** 反编译 TaleWorlds.Library.dll 的 ViewModel 通知机制与 BindingPath 订阅。
- **vm-02** 对原版 GUI/Prefabs 检索点路径绑定，命中数为 0。
- **vm-03** 反编译 TaleWorlds.GauntletUI.BaseTypes.DropdownWidget.cs:486 的 RefreshSelectedItem。
- **vm-04** 真机日志实证：每次操作成对出现两条、第二条行数恰为第一条两倍（53→106）。
- **vm-05** 源技能给出的重入守卫代码与「不复位 = 此页再也刷不动」的实测结论。
- **vm-06** 反编译 TaleWorlds.Core.ViewModelCollection.dll 的 SelectorItemVM 成员与构造函数签名。
- **vm-07** 源技能对字符串回查路径的风险实证（本地化文本可能重名），并给出实例平行表接法。
- **vm-08** 源技能给出的解析式与边界判据（_order 平行表，索引 1 基）。
- **vm-09** 反编译 GauntletUI 列表控件 OnListItemAdded/Removed 与 OnUpdate 的调用时序。
- **vm-10** 反编译 TaleWorlds.Library/ViewModel.cs:513 ExecuteCommand 的参数类型转换逻辑。
- **vm-11** 源技能实测：缺该行即点击没送到；有该行且 index 正确但未换页即页面状态没更新。
- **vm-12** 反编译 EditableTextWidget 的 Text setter，并对照原版 MultiSelectionQueryPopUpVM.SearchText 写法。
- **vm-13** 源技能给出的接法与原版 TabControl 语义对比（原版按子控件序号切显示）。
- **vm-14** 原版 ButtonBrush2 的 Selected 状态档存在，配合 ButtonWidget 的 IsSelected 语义。
- **vm-15** 源技能对换页后底栏位置的实测（缺定高时底栏跟随内容高度上移）。
- **vm-16** 源技能给出的日志字段清单与重入的成对翻倍特征（与 vm-04 同一实测）。
- **vm-17** 源技能给出的验证顺序与 '某句中文'.encode('utf-16-le') in open(dll,'rb').read() 判据。
- **vm-18** 源技能自述实测：~/.dotnet/tools/ilspycmd.exe 收 /d/… 形式路径报不存在。
- **vm-19** 源技能头部自述版本号 v1.3.15.110062，未附版本读取命令或文件证据。
- **vm-20** 源技能头部列出的反编译目标程序集清单，命令为 ilspycmd -t <全名> <DLL>。
- **vm-21** 反编译 TaleWorlds.GauntletUI.Data 层 WidgetAttributeValueTypeBinding.CheckValueType。
- **vm-22** 反编译 Data 层 WidgetAttributeValueTypeBindingPath.CheckValueType。
- **vm-23** 反编译 Data 层 :1280 的唯一 BindData 调用点及其 if (valueType is WidgetAttributeValueTypeBinding) 条件。
- **vm-24** 反编译 Data 层 :1187~1192 的 DataSource 键处理分支，其他键无对应消费者。
- **vm-25** 反编译 PrefabSystem :1976~2017 SetAttributes 的三分支，花括号值不落任何分支。
- **vm-26** 对原版 SandBox GUI/Prefabs 检索 Text="{ 与 Text="@ 的命中对比。
- **vm-27** 对 738 份原版预制件逐键统计花括号值的键名分布。
- **vm-28** 09-21 实测案例：数据侧日志 947 行/绑 120 行，Prefab 那八格用花括号。
- **vm-29** 由 vm-21~vm-25 的两条类型判定路径推出的使用约束与静态判据。
- **vm-30** 反编译 PrefabSystem :904~912 SetWidgetAttributeFromStringAux 的 FindChild 调用与无 null 检查。
- **vm-31** 反编译 GauntletUI :22686 Widget.FindChild(BindingPath) 的分支实现。
- **vm-32** 由 vm-30/vm-31 的 FindChild 直接子控件语义推得，源技能以真机症状（滚轮无反应）实证。
- **vm-33** 反编译 GauntletUI :19239 UpdateScrollablePanel 与 :19103 OnMouseScroll 的分支条件。
- **vm-34** 对原版 SandBox 预制件 25 处 VerticalScrollbar 取值的形态统计。
- **vm-35** 源技能对预览脚本流程的核对：表体区域被擦除并以演示数据重绘。
- **vm-36** 源技能对预览脚本替代表机制的说明与漏登记后果。
- **vm-37** 源技能对 check_bindings.py 判据分支的复核：仅两个分支，第三类被跳过。
- **vm-38** 源技能实测耗时 ≈3 分钟；前台运行被默认 120 s 超时中断。
- **vm-39** 源技能实测：被砍之后日志文件 0 字节，实为未跑完而非崩溃。
- **F001** SKILL §1 明标；sage 索引覆盖面属工具属性，与游戏版本无关，但为否定式断言故列入复核。
- **F012** AWAKE 2026-09-13 以『鼠标点发送本来可用 ⇒ 双绑必成立』自证法确认，随后给 NpcDialogue.xml / AwakeMessenger.xml 各加一行 Command.TextEntered。
- **F017** 2026-09-24 查证 TaleWorlds.ScreenSystem/ScreenManager.cs 的 SortedLayers 构造逻辑。
- **F025** 由 §5.3 的 _usedInputs 置位条件推出，SKILL 未记为逐帧实测。
- **F026** SKILL §5.3 记为好处，由 _usedInputs 机制推出。
- **F038** 对反编译出的 TaleWorlds.GauntletUI 全库 grep 三个属性名，命中数为 0。
- **F040** SKILL §6 列为踩过并验证的坑；scope 由兄弟节点 NavigationScopeTargeter 提供。
- **F043** SKILL §6 记为实测；6 位等其它长度行为未验。
- **F046** SKILL §6 同族记录：字节扫描对构造点恒不命中。
- **F047** 全 Modules 扫描 --include=*.xml，@ 后含 ! & \| < > 的绑定表达式命中数为 0。
- **F059** SKILL §6：那串字节来自网络头像，未追到生产者。
- **F060** SKILL §6 明标仍未证，并建议整条绕开改用编码文件字节。
- **F063** SKILL §6 列为未验证项。
- **F064** SKILL §6 列为未验证项。
- **F067** SKILL §6 记录两侧同源，故一律用 PlatformFilePath。
- **F079** 由 §3 的 GetConstructor 绑定标志（Instance\|Public\|CreateInstance）推出，SKILL 记为静默失败。
- **F100** 反编译 Texture.Release 方法体 + SKILL §5.2 明标未验。
- **F101** SKILL §5.2 明标待验，要求量产出后再下结论。
- **F104** SKILL §7 取证手法记录实测静默失败。
- **F106** SKILL §7 记录阳性对照失败（GauntletMovie 确实在 TaleWorlds.Engine.GauntletUI.dll），探针不可信。
- **F107** SKILL §7 结论，由 F106 的对照实验得出。
- **F108** SKILL §7 记录；本机已无 bash 工具。
- **F122** SKILL §1⑥ 第 2 条记录实测；属否定式断言故列入复核。
- **F127** SKILL §4 附录纪律记录；属否定式断言故列入复核。
- **F128** SKILL §4 记录；该工具属已死 harness。
- **F008** 甲方 09-17 裁定「拼音首字母别测，这个本来也不合理」，链路检查无拼音模块。
- **F027** _verify_index_hygiene_20260917.py 与 grep -c 双重读数，均为 0。
- **F047** 同一批泛问词跑探针，三个词读数分别为命中/空手/空手。
- **F057** A 项已把唯一那条「德里亚特·村庄」从数据清掉后复测，命中 0。
- **F094** 排查仓库无权威繁简对照数据，09-16 版脚本表为自造。
- **F098** 09-17 实测被拦的确切提示串。
- **F100** 对斯特基亚 类输入实测折叠后仍查不到，定位为字序差异。
- **F104** 读 StorageAndRagApi.cs:135 的 RagHit 定义，确认无分数字段。
- **F011** 09-15 后台跑六步编译链时观察到的工具行为
- **F037** 09-14/09-19 两次实测：传 --path 仍被其它档诊断淹没
- **F045** docs/worldbook-migration/projection/LORE-ENTITY-REGISTER-20260912.json 的 compiler_behavior（含 corrections_20260912.compile_blocker_lore_kind）
- **F054** 09-14 批量改名实测：castle-ab-comer-castle 词尾 castle 是地名一部分
- **F078** C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db（mode=ro）
- **F079** 步骤1 取数：同节另有 localization_entries（language='CNs'）与 bannerlord_items 表
- **F080** 步骤1 取数一节的实测口径
- **F081** 坑清单·兵种装备取数一节记录的表列名实测
- **F082** 坑清单·兵种装备取数一节：DB 里查不到近战武器，回到 weapons.xml
- **F083** 步骤8·城堡档一节：DB 实测 descriptionText 全空
- **F084** 步骤8·城堡档一节：查 localization_entries 得 text.castle* 全指向村条目
- **F085** 步骤8·城堡档一节记录的锚点来源与快照形态
- **F086** 步骤8·下辖村一节：不加前缀 join 不到任何村
- **F087** 步骤8·两个官方数据坑一节记录的 DB 实测个例
- **F088** 步骤8·两个官方数据坑一节的 DB 实测个例
- **F089** 09-14 全量比对 67 堡名与下辖村名，得 66 同名
- **F090** tools/_castle_link_probe_<date>.py 每堡 2 条 + 1 条对照跑 sim 后逐行判 hits
- **F106** 步骤5·文件名前缀不是分类轴一节记录的出处与缺口
- **F109** 09-20 对同一目录分别用 wc -l 与 grep -l '^id: doc\.' 计数对照
- **F110** 步骤5·🪤 记录：find 必须走 /usr/bin/ 才不撞 Windows 同名 exe
- **F111** 09-14 实测：同一文件用 Grep 工具有命中而 bash grep 0 命中
- **F113** 09-14 实测：一次性写 64 条 L2 进单个 .py 被截断
- **F123** 09-19 实测：用 if key in line 先命中城堡村那行且不报错，迪安托格麦尔被整条判漏；改为按 => 切左半边全等比较后归零
- **F124** 坑清单·兵种装备取数：SandBoxCore/ModuleData/spnpccharacters.xml；slot 名 Item0/Item1/Item2/Head/Body/Gloves/Leg/Horse/HorseHarness；技能在同一块 <skills>
- **F125** 坑清单·武器部件：weapons.xml 里 <CraftedItem> + <Piece Type>；部件本体 <Weapon> 属性在 crafting_pieces.xml；模板在 crafting_templates.xml
- **F126** 09-20 从 spnpccharacters.xml 的 <equipment slot="Item1"> 读得，与编年史 B 级说法相反
- **F128** 09-14 实测：release-check 红但包目录与 manifest.json/SHA256SUMS.txt 已先写好
- **F02** grep -rn 'WorldbookRuntime\.(Current\|Knowledge)' src/ 只见定义处；裁定文档 DECISION-20260916 宣布旧代作废
- **F08** 09-17 先跑 grep -rn 'WorldbookRuntime\.(Current\|Knowledge)' src/ 分辨活链路，再逐段查读取点得出
- **F12** 内容侧实测 40/40 denies 为空，对照 WorldKnowledgeQueryService 中 HasMatchingDeny() 调用路径
- **F13** 四段链路 grep 该字段：契约有、生成器/编译器无写入；golden 测试钉死相关文件
- **F14** 仅读码推断（技能原文自述「疑似静默点（仅读码推断）」），未在包上实跑
- **F23** 读 .py 启动参数 + 本机解释器实测；跑不起来的探针等于没验过
- **F31** 09-14 读 sync_module.ps1:53（该目录已列但 -SkipWorldbook 当前禁用）并核实仓库缺 manifest
- **F32** 09-14 两侧目录对比时在游戏目录找到 manifest，纠正「哪里都没有」的推断
- **F35** 09-14 读运行时选卡逻辑，grep 读侧认的单复数形态与写侧产出形态
- **F39** 09-14 用 git check-ignore -v 与 git status --porcelain -uall 核实新放文件不出现
- **F40** 09-14 对「部署脚本列的东西」与「产出的东西」做集合差
- **F48** 09-16 读 WorldKnowledgeQueryService.FindCandidates 方法体
- **F50** 09-17 grep -rn 完整方法名（含括号）并排除其定义处，命中 0
- **F60** 先 grep -rin <其 SHA-256 前 8 位> --include=*.cs --include=*.json 命中 golden 常量
- **f25** 报告 :13/:41 表；v3 counts.newEntries=76 已复算，61 与旧表的 0 未独立复算
- **f28** 白名单同 f26；validate-authoring-closure.ps1:296-307 把该项 C16 直接标 BLOCKED 而非 PASS
- **f33** python 解析 SandBox/ModuleData/settlements.xml：494 个 Settlement；castle 67 with_text 0；town 53/53；castle_village 132/132；village 141/142；hideout 99/0
- **f35** 技能 §一-第2步 声明；本次未定位到 BannerlordSage 库文件，未能独立复验
- **f48** v2 hubproper=398 已复算；41% 的分母来自技能正文，本次未复算
- **f49** 技能 §二-坑② 表；语料已从 482 档变为 558 档，本次未复算这四个口径
- **f51** 实测该文件 edges=274 与技能 290 不符；161 与 56% 未复算
- **f52** 技能 §四-纪律2 记载的历史口径分歧，现无法复现（v1 文件仍存 should-link.v1.json）
