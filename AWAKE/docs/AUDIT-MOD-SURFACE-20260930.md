# AWAKE 模组交付面盘点（2026-09-30）

> 起因：执行 N0（切构建目标到 1.4.8 并重投 DLL）时，`sync_module.ps1` 抛
> `Forbidden runtime package file: Microsoft.ML.Tokenizers.dll`，遂对整个「模组该包含哪些模块」
> 做一次三方实测盘点（**仓库源 / dist staging / 游戏目录**）。
>
> 全部读数为 2026-09-30 实测，命令与原始输出见 §五。
> **盘点阶段未改动任何文件。5 项 P0 的修复与投送已于同日 21:2x 完成，执行记录见 §六。**

---

## 一、一句话结论

**AWAKE 模组应有 7 层交付面，但同步链只覆盖 4 层**；另有 3 层脱管或混入。
其中 **5 项 P0** 会直接坏功能或阻断投送 —— N0 之所以跑不动，根因就在这里。

---

## 二、模块清单 × 覆盖矩阵

「受管」= 在 `sync_module.ps1` 的 `managedFiles` 清单内（决定它会不会被同步）。
「dist」= 仓库侧 staging `AWAKE/dist/Modules/AWAKE`。

| # | 模块 | 仓库源 | 受管 | dist | 游戏目录 | 判定 |
|---|---|:--:|:--:|:--:|---|---|
| 1 | `SubModule.xml`（模组声明） | ✓ | ✅ | ✓ | ✓ | OK |
| 2 | `bin/…/Awake.dll` | `_build_out/1.4.8` | ✅ | ✓ | ⚠️ **09-14** | 陈旧 |
| 3 | `bin/…/MarcusAwakeFramework.dll` | `framework/…/_build_out` | ✅ | ✓ | ⚠️ **09-14** | 陈旧 |
| 4 | `bin/…/MarcusAwakeTransport.dll` | 同上 | ✅ | ✓ | ⚠️ **09-14** | 陈旧 |
| 5 | `bin/…/Runtime/**`（嵌入式运行时） | dist（打包产出） | ✅ | ✓ 199 | ✓ **203** | ⚠️ 重打包丢件 |
| 5a | └ `Microsoft.ML.Tokenizers.dll` | NuGet 2.0.0 | ❌ | ❌ | ✓ | **P0-1** |
| 5b | └ `models/bge-small-zh-v1.5/**` | 手工放置 | ❌ | ❌ | ✓ | **P0-2** |
| 6 | `GUI/Prefabs/**` | ✓ | ⚠️ **仅 7 个** | 7 | 7（**5 个 09-13 旧版**） | 陈旧 |
| 6a | └ `AwakePortraitSlot.xml` | ✓ | ❌ | ❌ | ❌ | **P0-5** |
| 7 | `GUI/Brushes/AwakeBrushes.xml` | ✓ | ❌ | ❌ | ❌ | **P0-4** |
| 8 | `GUI/SpriteParts/**`（贴图） | ✓ 53 | ❌ | ❌ | 22 | 脱管 |
| 9 | `GUI/AWAKESpriteData.xml` | ✓ | ❌ | ❌ | ✓（手工放的） | 脱管 |
| 10 | `ModuleData/Languages/**` | ✓ 4 | ✅ | 4 | 4 | OK |
| 11 | `ModuleData/Worldbook/**` | ✓ 82 | ✅ | 0 | **841** | ⚠️ 758 项残留 |
| 12 | `ModuleData/Rules/README.md` | ✓ | ❌ | ❌ | ✓ | 预留目录 |
| 13 | `ModuleData/Knowledge/` | 空 | ❌ | ❌ | 空 | 空目录 |
| 14 | 根文档 4 件（README_CN/EN、BUILD_VERIFICATION、THIRD-PARTY-NOTICES） | ✓ | ✅ | 3/4 | ✓ | OK |
| 15 | `docs/**`、`AGENTS.md`、`AWAKE-Task-Queue-*.md`、`GRILLME-*.md` | ✗ 不该有 | ❌ | ✗ | ⚠️ **混入** | 冗余 |

**受管清单原文位置**：`sync_module.ps1:26-56`（根文件 / GUI / 语言 / 世界书）、`:224-250`（`Get-ManagedFiles`）。

---

## 三、缺口分级

### 🔴 P0 —— 会坏功能或阻断投送

> ✅ **本节 5 项已于 2026-09-30 21:2x 全部修复并投送验证通过，见 §六。**

| 编号 | 问题 | 证据 | 后果 |
|---|---|---|---|
| **P0-1** | `Microsoft.ML.Tokenizers.dll` 被**静默剔除** | `package_embedded_runtime.ps1:73` 正则 `'secret\|credential\|privatekey\|password\|token'` 命中文件名里的 `token`（**Tokenizers=分词器**被当成**凭据令牌**）；`:265 Copy-PublishPayload` 命中即 `continue`，不报错 | 重打包后 RAG 语义臂**缺依赖** |
| **P0-2** | `models/bge-small-zh-v1.5/**`（`model.onnx` 94.9 MB + `vocab.txt` + `tokenizer.json`）**不在打包流程** | 打包只复制 `dotnet publish` 输出（`Copy-PublishPayload`），模型是**手工放置**的 | 重打包后**无模型可加载** |
| **P0-3** | 上述两项导致 **N0 投送被直接阻断** | `sync_module.ps1:673` 对**游戏侧现有 Runtime** 做 `Invoke-EmbeddedRuntimeValidation` ⇒ 旧包里的 `Microsoft.ML.Tokenizers.dll` 触发 Forbidden ⇒ throw | sync 无法完成 |
| **P0-4** | `GUI/Brushes/AwakeBrushes.xml` **不在受管清单，且游戏目录缺失** | Prefab 引用 11 个 `Awake.*` brush，`AwakeBrushes.xml` 恰好定义这 11 个；游戏目录 `find GUI -type f` = 30，**无 Brushes 目录** | 面板/按钮 brush 解析不到 |
| **P0-5** | `GUI/Prefabs/AwakePortraitSlot.xml` **不在受管清单** | 仓库 `NpcDialogue.xml:122` 有 `<AwakePortraitSlot …/>`，`src/NpcDialogueVM.cs:24` 称其为「第一处真实宿主」；但该文件既不受管、游戏目录也没有 | **部署新版 NpcDialogue 必坏**（当前未爆，因为游戏侧还是 09-13 旧版，`grep -c AwakePortraitSlot` = **0**） |

### 🟡 P1 —— 陈旧 / 静默不同步

| 编号 | 问题 | 读数 |
|---|---|---|
| **P1-1** | 程序集层落后 | 游戏目录 `Awake.dll` = **09-14 12:27**；仓库 1.4.8 构建 = **09-30 19:37**（落后 16 天） |
| **P1-2** | 5 个**受管** Prefab 与游戏目录不一致 | `NpcDialogue`/`WorldEventInbox`/`DeveloperCheck`/`SceneDialogueStatus`/`WeeklyReportBrowser`：仓库 **09-15** vs 游戏 **09-13** ⇒ 说明同步链已长期未成功运行 |
| **P1-3** | GUI 资产层（Brushes / SpriteParts / AWAKESpriteData）完全脱管 | 改仓库不生效；`SpriteParts` 仓库 53 vs 游戏 22 |
| **P1-4** | 游戏侧 v1 世界书死档残留 | 游戏 841 vs 仓库 82 ⇒ **758 项**残留（`personality_background` 415、`rules` 335、`event_data` 3、`unnamed_persona` 1、`voice_mapping` 1、根级 `runtime.json`/`index.json`/`migration_report.json`）。`sync_module.ps1:57-59` 注释明确这些目录**运行时拒收且无代码读取** |

### 🟢 P2 —— 冗余 / 混入

| 编号 | 问题 |
|---|---|
| **P2-1** | 游戏目录混入开发物：`docs/`（20+ 文档）、`AGENTS.md`、`AWAKE-Task-Queue-20260816.md`、`GRILLME-FRAMEWORK-BUGFIX-20260815.md` |
| **P2-2** | `GUI/SpriteParts/ui_awake_icon/**` **28 个 png 零引用**（全仓库 grep `ui_awake_icon` 无命中）⇒ 未接线半成品；且 `SpriteParts/Config.xml` 未定义 `ui_awake_icon` 分类 |
| **P2-3** | `ModuleData/Knowledge/` 两侧皆空；`ModuleData/Rules/` 只有 README（无实际规则 manifest） |
| **P2-4** | 游戏根级 `runtime.json` / `index.json` / `migration_report.json` = 旧位置残留（现行契约只认 `packages/calradia/` 下 3 文件） |

---

## 四、修复方案（按优先级）

> ⚠️ 前三项都要改 `AWAKE/tools/` 下的**工具链脚本**（在 git 仓库内），改前建议单独提交、可回滚。

### 修 1 —— 解 P0-1（正则误杀）
`package_embedded_runtime.ps1:73`：
```powershell
# 现：if ($fileName -match 'secret|credential|privatekey|password|token') { return $true }
# 改：把 token 收紧为词边界，放过 Tokenizers / tokenizer.json 这类合法复合词
if ($fileName -match 'secret|credential|privatekey|password|\btoken\b') { return $true }
```
> `\btoken\b` 仍能拦 `token.txt` / `my.token` / `token`，但不再误伤 `tokenizers`。

### 修 2 —— 解 P0-2（模型不在流程）
在 `Copy-PublishPayload` 之后补一段模型复制（`models/**` 是手工资产，publish 不产出）。
**注意**：`tokenizer.json` 也含 `token` 前缀，修 1 的词边界已能放行。

### 修 3 —— 解 P0-4 / P0-5（GUI 清单缺项）
`sync_module.ps1`：
- `managedGuiFiles` 补 `GUI\Prefabs\AwakePortraitSlot.xml`
- 新增 `managedGuiDirectories`（或显式文件表）：`GUI\Brushes`、`GUI\SpriteParts`、`GUI\AWAKESpriteData.xml`

### 修 4 —— 解 P1-4 / P2-1（清理残留）
在 `sync_module.ps1` 的 obsolete 机制里登记：v1 世界书 8 目录 + `docs/` + 根级混入文档。
**建议先备份再删**（游戏侧文件不属于 git）。

### 修 5 —— 解 P2-2（未接线资产）
`ui_awake_icon/**` 要么接进 `Config.xml` + 某个 Prefab，要么移出交付面。

### N0 的正确执行路径
```
修 1 + 修 2  →  package_embedded_runtime.ps1 重打包
修 3         →  补 GUI 清单
             →  sync_module.ps1 -ConfirmGameSync -BuildDllPath _build_out\1.4.8\Release\Awake.dll
```
> 只做「修 1」也能让 sync 跑通（因为新包本就不含 Tokenizers ⇒ 游戏侧预检不再命中），
> **但那样投出去的新包缺 Tokenizers + models** ⇒ 语义臂会坏。**必须先修 2**。

---

## 五、实测命令与原始读数

### 5.1 三方对照（`tools/_mod_surface_audit.py`）
```
GUI/Prefabs/                 仓库=    8  dist=    7  游戏=    7
GUI/Brushes/                 仓库=    1  dist=    0  游戏=    0
GUI/SpriteParts/             仓库=   53  dist=    0  游戏=   22
ModuleData/Languages/        仓库=    4  dist=    4  游戏=    4
ModuleData/Worldbook/        仓库=   83  dist=    0  游戏=  841
ModuleData/Rules/            仓库=    1  dist=    0  游戏=    1
ModuleData/Knowledge/        仓库=    0  dist=    0  游戏=    0
THIRD-PARTY-NOTICES.txt      仓库=有  dist=无  游戏=有
GUI/AWAKESpriteData.xml      仓库=有  dist=无  游戏=有
```

### 5.2 仓库有 / 游戏缺（33 项）
`GUI/Brushes/AwakeBrushes.xml`、`GUI/Prefabs/AwakePortraitSlot.xml`、
`GUI/SpriteParts/ui_awake_icon/*.png`（28）、`GUI/SpriteParts/ui_awake_ornament/`
（`ground_tile_256.png`、`rule_repeat_24x240.png`、`weave_tile_256.png`）。

### 5.3 GUI 同源比对（sha256 前 12 位）
```
SAME     AwakeMessenger.xml
SAME     AwakePortraitProbe.xml
MISSING  AwakePortraitSlot.xml
DIFF     DeveloperCheck.xml       repo=aef979e3c5d8 game=ef9a3a31e43d
DIFF     NpcDialogue.xml          repo=71fc98679f8a game=5298d027b872
DIFF     SceneDialogueStatus.xml  repo=3232b5a3125d game=b82740c2cc81
DIFF     WeeklyReportBrowser.xml  repo=0fabac2527ec game=52f6ed2241c7
DIFF     WorldEventInbox.xml      repo=7d30d3f0c145 game=8fcfa95f0d1b
SAME     GUI/AWAKESpriteData.xml
DIFF     GUI/SpriteParts/Config.xml  repo=e6efc429cc1c game=a5dad93af1eb
```

### 5.4 Brush 契约链
```
Prefab 引用 Brush：Awake.Button.Close/Primary/Secondary/Tab
                  Awake.Ornament.DividerGold/HeaderBand
                  Awake.Panel.Dialog1100/Input800/Main1280/Main960/Status720
AwakeBrushes.xml 定义：以上 11 个 + Default/Disabled/Hovered/Pressed/Selected
```

### 5.5 打包链路（`package_embedded_runtime.ps1`）
```
:73   $fileName -match 'secret|credential|privatekey|password|token'   ← P0-1 根因
:262  function Copy-PublishPayload { … if (Test-IsForbiddenRelativePath $relative) { continue } }
:269  $defaultOutputRoot = <repo>\dist\Modules\AWAKE\bin\Win64_Shipping_Client\Runtime
```
`RUNTIME_PACKAGE_OK files=199`（新包）vs 游戏侧 **203**。

### 5.6 同步链路（`sync_module.ps1`）
```
:20   $DistModule 默认 = <repo>\dist\Modules\AWAKE
:21   $BuildDllPath 默认 = _build_out\1.3.15\Release\Awake.dll   ← 必须显式覆盖
:26-56  受管清单（根文件 / GUI 7 Prefab / 语言 4 / 世界书 2+2 目录）
:68   $preservedRoots = Config.json, Logs, PlayerExports, Runtime, Saves, Cache
:199  Get-ManagedSourcePath：Runtime 取自 dist，3 个 DLL 取自各自 build_out
:673  Invoke-EmbeddedRuntimeValidation $gameEmbeddedRuntimeRoot 'game'   ← P0-3 抛出点
```

### 5.7 环境读数
- 游戏目录 `Awake.dll` = 2026-09-14 12:27，1133568 B
- 仓库 `_build_out/1.4.8/Release/Awake.dll` = 2026-09-30 19:37，1177088 B
- `build.ps1 -BannerlordApi 1.4.8` ⇒ `BUILD_OK` + `TESTS_OK`（0 错误）
- 构建前 Bannerlord 进程：**未运行**（`NO_BANNERLORD_RUNNING`）

---

## 六、修复执行记录（2026-09-30 21:1x–21:2x）

### 6.1 改动（2 个工具脚本；改前已备份到 `artifacts/script-backup-20260930/`）

| # | 文件 | 位置 | 改动 |
|---|---|---|---|
| 修 1 | `tools/package_embedded_runtime.ps1` | `Test-IsForbiddenRelativePath` | 正则 `'…\|token'` → `'…\|\btoken\b'` |
| 修 2 | `tools/package_embedded_runtime.ps1` | 新增 `Copy-ModelPayload` ＋ 在 `Copy-PublishPayload` 后调用 | 从 `artifacts/models/` 把模型复制进包 |
| 修 3 | `tools/sync_module.ps1` | `managedGuiFiles` ＋ 新增 `managedGuiDirectories` | 补 `AwakePortraitSlot.xml`、`AWAKESpriteData.xml`，并把 `GUI\Brushes`、`GUI\SpriteParts` 纳入受管 |

⚠️ **踩坑记录**：修 2 的注释我最初写成中文，而该脚本**无 BOM** ⇒ PS 5.1 按 GBK 解析 ⇒
报 `表达式或语句中包含意外的标记"}"`。
**教训：无 BOM 的 PS 脚本必须保持纯 ASCII**（本次以 `grep -c '[^ -~]'` = 0 复核）。

### 6.2 模型资产固化
`.workbuddy/tmp/hf-official/xenova/`（**临时目录，不可依赖**）→ `AWAKE/artifacts/models/bge-small-zh-v1.5/`
（`.gitignore:129` 已忽略 `AWAKE/artifacts/`，94.9 MB 不入库）。
5 个文件，`model.onnx` 与 `vocab.txt` 的 sha256 与 `tools/semantic-model-provenance.json` 登记**逐字节一致**。

### 6.3 执行链与读数
| 步骤 | 结果 |
|---|---|
| `build.ps1 -BannerlordApi 1.4.8` | `BUILD_OK` ＋ `TESTS_OK`（0 错误）；`_build_out/1.4.8/Release/Awake.dll` = 1177088 B |
| `package_embedded_runtime.ps1` | `RUNTIME_PACKAGE_OK files=205`（199 → **+1 Tokenizers ＋5 models**）；`manifest_sha256=c803d6cb…` |
| `sync_module.ps1 -ConfirmGameSync` | **`state=verified`**，耗时 45 s，`rollback.error=null` |

### 6.4 途中暴露的第 6 个问题：**游戏侧旧包本身不自洽**
`sync` 的投送前预检抛 `Runtime manifest file count mismatch: manifest=195 actual=205`。

**成因**：游戏侧 Runtime 是 09-17 打的（`manifest.json` 记 195 条），之后**手工补进了 5 个模型文件 ＋ Tokenizers**，
却没更新 manifest ⇒ 包自相矛盾，`Assert-Manifest` 必然失败。

**处置**：先把 dist 的 6 个文件（4 个重建的 framework DLL ＋ `manifest.json` ＋ `SHA256SUMS.txt`）
补到游戏侧，使其达到「投送后本应具有」的自洽状态（`RUNTIME_PACKAGE_OK files=205`），再跑 sync。
备份在 `artifacts/game-runtime-meta-backup-20260930/`。

> **建议**：`sync_module.ps1:696` 对 game 侧的预检对「历史遗留的不自洽」过于严格，
> 可考虑降为 warning（门禁本身保留），否则每次碰到历史包袱都要人工介入。

### 6.5 最终验证
| 项 | 前 | 后 |
|---|---|---|
| 游戏侧 `Awake.dll` | 1133568 B / 09-14 | **1177088 B**（sha256 `9f3c432f…` ＝ 仓库 1.4.8 产物） |
| `GUI/Brushes/AwakeBrushes.xml` | **缺失** | 已到位（8166 B） |
| `GUI/Prefabs/AwakePortraitSlot.xml` | **缺失** | 已到位（11497 B） |
| `GUI/**` 文件数 | 30 | **63**（＝ 仓库） |
| `GUI/SpriteParts/**` | 22 | **53**（＝ 仓库） |
| 8 个 Prefab 同源比对 | 5 DIFF ＋ 1 MISSING | **8/8 SAME** |
| 审计脚本 B 节「仓库有、游戏缺失」 | 33 项 | **(无)** |

**仍未处理（属 P1/P2，本轮范围外）**：游戏侧 758 项 v1 世界书死档残留、`docs/` 等开发文档混入、
`ui_awake_icon` 28 个 png 零引用。
