# 图集（SpriteCategory）运行时到底从哪读 · 2026-09-19

> 起因：另一条线（Leverage）判定「图集放错了目录」，把图集从 `AssetSources/` 摆到了
> `Modules/Leverage/GUI/SpriteSheets/ui_leverage/`，并据此认定「Import 出 tpac 不是必需的」。
> 本文复检这个判定。**结论：命名规则对，落点结论错。**

## 0. 一句话

`<分类名>_<序号>` 这个**命名**是对的；但**位置**不是 `GUI/SpriteSheets/` ——
运行时是从 `Modules/<模组>/AssetPackages/*.tpac` 里的**打包纹理**取。
把 PNG 摆进 `GUI/SpriteSheets/`，什么也不会发生。

## 1. 触发：那个修法**没有生效**（一手，日志）

`ui_leverage_1.png` 摆进 `GUI/SpriteSheets/ui_leverage/`（目录 mtime **17:14**）之后：

| 日志 | 文件时间 | 缺的纹理 |
| --- | --- | --- |
| rgl_log_18812 | 09-19 14:42 | `ui_leverage_1` |
| rgl_log_17144 | 09-19 14:49 | `ui_leverage_1` |
| rgl_log_3772 | 09-19 15:02 | `ui_leverage_1` |
| rgl_log_11800 | 09-19 15:54 | `ui_leverage_1` |
| **rgl_log_33016** | **09-19 17:21 起，17:28 仍在写** | **`ui_leverage_1`** |
| rgl_log_33744 | 09-14 23:19（唯一含 AWAKE 的一次） | `ui_awake_button_1` / `frame_1` / `ornament_1` |

最后一次是 **17:21 启动、现在还活着**的那一局，**在文件已就位 7 分钟之后**，
报的还是同一句。日志里那句原文：

```
[17:21:22.598] ResourceDepot:CollectResources: ../../Modules/Leverage/GUI/
[17:21:23.026]
Cannot find texture: ui_leverage_1
```

文件本身没问题：128×128、RGBA8、PNG 合法，与 `LeverageSpriteData.xml` 里
`<SpriteSheetSize ID="1" Width="128" Height="128" />` **逐字对上**。

## 2. 三条证据

### 2.1 命名规则确实叫 `<分类名>_<序号>` —— 这条对方是对的

`SimpleBank/GUI/SimpleBankSpriteData.xml` 声明 `<Name>Bank</Name>`（2048×512），
而 `SimpleBank/AssetPackages/pack0.tpac` 里躺着的纹理条目就叫 **`Bank_1`**。
⇒ 名字规则没错。

### 2.2 但原版把它**打包**了，而且原版根本没有 `SpriteSheets/` 目录

- `Modules/Native/GUI/` 只有 `Brushes` / `Fonts` / `Prefabs` / `NativeSpriteData.xml`，
  **没有 `SpriteSheets/`**。
- 而 `Modules/Native/AssetPackages/gauntlet_ui.tpac`（**323 MB**）里能搜到
  `ui_conversation_1`、`ui_barter_1`、`ui_bannericons_1`、`ui_bannerbuilder_1`，**各出现 2 次**。

⇒ 名字在包里，不在磁盘上的 png 里。

### 2.3 全机唯一「四件套齐全」的样本，走的也是 tpac

`SimpleBank` 是这台机器上唯一一个资产管线完整的模组：

```
SimpleBank/AssetSources/GauntletUI/Bank_1.png        源图
SimpleBank/Assets/GauntletUI/Bank_1_tex.tpac         479 B   薄壳
SimpleBank/AssetPackages/pack0.tpac                  37,890 B
SimpleBank/GUI/SimpleBankSpriteData.xml
SimpleBank/GUI/{Brushes,Prefabs,SpriteParts}
                                         ← 没有 SpriteSheets
```

`pack0.tpac` 里能读出的东西：

```
TPAC  (magic) …
Bank_1
$BASE/Modules/SimpleBank/AssetSources/GauntletUI/Bank_1.png
DXT5   has_alpha   none
```

⇒ **包记录「源路径」，像素在包里（DXT5）。**
⇒ `AssetSources/` 是**源**，不是运行时落点。**这正是 AWAKE 现在把三张图集放的地方。**

## 3. 全机分布（口径写清）

- 模块总数 **108**。
- 有 `AssetPackages/` 的：**29 个**（含 `Native`、`Bannerlord.MBOptionScreen`、`ROT-*`、`OpenSourceArmory`…）。
- 有 `GUI/SpriteSheets/` 的：**2 个**（`AnimusForge`、`Leverage`）。
- 这 2 个里，**没有任何一个有成功记录**。

### 3.1 ⚠️ 「AnimusForge 的图一张都没缺过」是**空集**，不是证据

留存的 6 份启动日志里，`Command Args` 的模块列表**没有一次**包含 `AnimusForge`。

> 18812 / 17144 / 3772 / 11800 / 33016 → 含 `Leverage`，不含 `AnimusForge`
> 33744 → 含 `AWAKE`，不含 `AnimusForge`

它没跑过 ⇒ 它的纹理**不可能**出现在「缺纹理」名单里。
「名单里没有它」证明的是「它没被加载」，不是「它那条路通了」。
**这是阴性对照没做**，不是反证。

（限定口径：只说「**留存**日志里没有」，不等于「从没启动过」——
七月的日志已经轮转掉了。）

## 4. 所以「散装图集可行」现在**写不了**

- 我**不**写「松散的 `GUI/SpriteSheets/` PNG 永远不行」—— 我没读运行时加载器的源码，
  `TaleWorlds.TwoDimension.dll` 里确实有 `'SpriteSheets\'` 这个字面量（可能是编辑器/Standalone 那一侧用的）。
- 我能写的是：**本机 0 个成功样本；今天 5 次失败，全部发生在文件已就位之后。**

## 5. AWAKE 侧现状（一手）

- `Modules/AWAKE/GUI/SpriteSheets/` **存在但是空的** ⇒ **别往里放东西**。
- 三张图集**已经在** `AssetSources/GauntletUI/`，尺寸与声明**逐一对上**：

| 文件 | 实际 | 声明 | 字节 | 判定 |
| --- | --- | --- | --- | --- |
| `ui_awake_button_1.png` | 2048×64 | 2048×64 | 49,642 | ✅ |
| `ui_awake_frame_1.png` | 4096×1024 | 4096×1024 | 2,720,078 | ✅ |
| `ui_awake_ornament_1.png` | 4096×128 | 4096×128 | 54,906 | ✅ |

- 缺的是 **`Modules/AWAKE/AssetPackages/*.tpac`**（现在没有 `AssetPackages/`）。
- 三张都带 `<AlwaysLoad />` ⇒ 引擎一启动就找它们，找不到就写那三行日志。

## 6. 下一步（按代价排）

1. **最便宜、立刻做**：把「你的修法没生效」告诉 Leverage 线，附 §1 的表 ——
   别让他们在同一个假设上继续改。
2. **可以试、但别当结论**：`Bank_1_tex.tpac` 只有 **479 字节**，内容就是
   「名字 ＋ `$BASE/…png` 源路径 ＋ 格式」。若这种壳能被运行时接受，
   `AssetPackages/` 这一步就能**脚本化**。**未验。**
3. **一次做对**：走 Modding Kit 的 Import（原结论不变），
   或用 `bannerlord-tpac-extraction` 把 tpac 的写法定式摸出来。
4. AWAKE 自己等着的三件（sync 受管清单／`ui_awake_frame` 图集满／28 图标接线）
   **先按兵不动**，等第 2 或第 3 条有定论。

## 7. 给 Leverage 线的交接（可直接粘贴）

```
【复检：把图集从 AssetSources 摆到 GUI/SpriteSheets，这一步没有生效】

我这边（AWAKE UI 线）在本机复检了你的修法和结论，三条一手证据，供你复核。

一、摆好之后那局，日志报的还是同一句
  ui_leverage_1.png 落位目录 mtime 17:14。
  17:21 启动、现在还在跑的那一局（rgl_log_33016，17:28 仍在写）：
    [17:21:22.598] ResourceDepot:CollectResources: ../../Modules/Leverage/GUI/
    [17:21:23.026] Cannot find texture: ui_leverage_1
  09-19 的 5 次启动（14:42 / 14:49 / 15:02 / 15:54 / 17:21）全部报同一句。
  文件本身没问题：128×128 RGBA8，与 LeverageSpriteData.xml 的
  <SpriteSheetSize ID="1" Width="128" Height="128"/> 对上。

二、`<分类名>_<序号>` 这个命名你decompile 对了，但落点不对
  Modules/Native/GUI/ 只有 Brushes / Fonts / Prefabs / NativeSpriteData.xml，
  【没有 SpriteSheets 目录】。
  而 Modules/Native/AssetPackages/gauntlet_ui.tpac（323 MB）里搜得到
  ui_conversation_1 / ui_barter_1 / ui_bannericons_1 / ui_bannerbuilder_1，各 2 次。
  ⇒ 名字在【包里】。

三、全机唯一资产管线齐全的样本（SimpleBank）走的是 AssetPackages
  它同时有：
    AssetSources/GauntletUI/Bank_1.png
    Assets/GauntletUI/Bank_1_tex.tpac          479 B
    AssetPackages/pack0.tpac                   37,890 B
  pack0.tpac 里读出来是：
    Bank_1
    $BASE/Modules/SimpleBank/AssetSources/GauntletUI/Bank_1.png
    DXT5 / has_alpha
  它【没有 SpriteSheets 目录】。它的图集分类名就叫 Bank，包里那条纹理叫 Bank_1。

四、一个要提醒的对照问题
  「AnimusForge 的图一张都没缺过」不能当反证：留存的 6 份启动日志里，
  模块列表没有一次包含 AnimusForge（5 次含 Leverage，1 次含 AWAKE）。
  它没被加载过，所以它的纹理不可能出现在缺纹理名单里。这是阴性对照没做。

五、我这边能确定 / 不能确定
  能确定：本机 108 个模块里 29 个有 AssetPackages/，只有 2 个有 GUI/SpriteSheets/，
          这 2 个没有任何成功记录；今天 5 次失败都在文件就位之后。
  不能确定：我【没有】写「松散的 GUI/SpriteSheets PNG 永远不行」——
          我没读运行时加载器的源码，而 TaleWorlds.TwoDimension.dll 里确实有
          'SpriteSheets\' 这个字面量（可能是编辑器/Standalone 那一侧）。
  所以请你复核的正是这一条：你 decompile 的那段路径拼接，是【运行时】执行的吗？
  如果是 Standalone/编辑器那一侧，就能解释为什么摆了没用。

六、一条可以试的路（未验）
  Bank_1_tex.tpac 只有 479 字节，内容就是「名字 + $BASE/…png 源路径 + 格式」。
  如果这种壳能被运行时接受，AssetPackages/ 这一步就能脚本化。
  如果你们那边方便，这条比我这边试更省事（你们已经有打包脚本了）。
```

## 8. 相关文档

- `UI-DEPLOY-GAP-20260916.md` §2（部署缺口旧账，本文是对它的补充）
- `UI-ART-ASSET-INTERFACE-20260913.md` §...（美术线早前结论：图集只走 `AssetPackages/*.tpac`）
  —— **与该线今天的结论相反，本文证据站在早前那份这边。**
- `docs/AWAKE-ROADMAP.md`（状态权威在那边，本文不改它）
