# AWAKE UI 资产目录（统一状态清单）

- 批次：`AWAKE-UI-ASSET-CATALOG-20260913`
- 口径：只标注**状态与可复用性**，不复制任何资产
- 状态标签（互斥，取最严）：`可直接复用` / `仅可参考` / `当前损坏` / `待迁移` / `生产界面`
- 证据：本文件所有判断均来自本次实际读取与哈希比对，命令与结果见文末

## 一句话结论

AWAKE 现在**只有一套生产界面**（`NpcDialogue` 及另外 5 个 Prefab），**只有一套本地夹具骨架**（`tools/awake-ui-lab`）。
旧的 UI 工作站链路（`tools/ui-workstation`）**没有产出、没有打包物**，`dist/` 副本已过期。
凡是外部资料（Marcus 参考模组、旧 Lab 宿主）一律**仅可参考、禁止复制**。

## 1. 可直接复用

| 资产 | 位置 | 现有保障 |
| --- | --- | --- |
| 通用 Prefab 基线 | `AWAKE/tools/awake-ui-lab/assets/GauntletUILab.baseline.xml` | 来源 hash 与清单一致（`5B6ECCC7…42E3`）；结构已验证：列表/详情为兄弟层级、返回按钮不在详情滚动区 |
| 固定夹具状态 ID | `AWAKE/tools/awake-ui-lab/fixtures/fixture-state-ids.v1.json` | 17 个状态；与代码内集合逐一比对（E2 `fixture_state_ids_match_json`） |
| 本地夹具 DTO 与宿主会话契约 | `AWAKE/tools/awake-ui-lab/src/Awake.UiLab.Core/` | 纯 C#（`netstandard2.0`，无游戏依赖）；`dotnet build` 0 警告 |
| 本地生命周期用例 | `AWAKE/tools/awake-ui-lab/tests/Awake.UiLab.Tests/` | 16 条 E2 用例全绿（离线，`game_launch=false`） |
| 本地全量夹具驱动器 | `AWAKE/tools/awake-ui-lab/src/Awake.UiLab.Core/UiLabFixtureRunner.cs` | 17 个登记状态全跑通；确认/拒绝只改夹具内部状态，不触发 Close |
| **本地几何线框预览器** | `AWAKE/tools/awake-ui-lab/preview/` | 把 6 个生产 Prefab 推导成线框（含悬停显示属性）；纯只读、不启动游戏；**推导几何 ≠ 渲染**，真机 E4 仍需单独验收 |
| 导入守卫 | `AWAKE/tools/awake-ui-lab/verify-import.ps1` + `import-manifest.v1.json` | 递归白名单 + 类别拒绝 + 来源哈希；`status=passed`，且反向注入测试能逐项报错 |

用法：

```
dotnet run --project tools/awake-ui-lab/tests/Awake.UiLab.Tests
dotnet run --project tools/awake-ui-lab/tests/Awake.UiLab.Tests -- --drive-fixtures
python tools/awake-ui-lab/preview/preview_prefab_geometry.py
powershell -File tools/awake-ui-lab/verify-import.ps1
```

## 2. 仅可参考（AWAKE 之外；只许读，不许复制）

| 资产 | 位置 | 为什么只可参考 |
| --- | --- | --- |
| Marcus 参考模组对话覆盖层 | `MarcusAIFramework_Reference/AuthorSource/src_20260813/MarcusAINpc/GUI/Prefabs/MarcusAINpcDialogueOverlay.xml`（源、`artifacts/v1.3.15`、`artifacts/v1.4.8` 三份完全相同，`1136B6B1…`） | 外部模组资料；`docs/grill/Grill-Correction3-NoArtReuse-20260816.md` 已定「不得复用美术」 |
| Marcus 内容管理器 Prefab | `…/MarcusAINpc/GUI/Prefabs/MarcusAINpcContentManager.xml` | 同上；只作版式与绑定命名参考 |
| 旧 UI Lab 宿主 | 旧工作区 `host/GauntletUILabHost/`（DLL 与已部署副本一致 `9E54A9A9…EF4C`） | 旧宿主本身不迁；只吸收经证据支持的生命周期约束 |
| 旧生命周期契约源码 | 旧工作区 `shared/UiLabRuntime.cs`（`322B9E7D…E05F`） | 来源哈希已登记进清单，只做参考比对，不入库 |
| 旧 E2 生命周期报告 | 旧工作区 `state/validation/ui-lab-owner-lifecycle-20260831.json`（`952B4F4D…A2197`） | 只证明旧实现；不构成对 AWAKE 实现的验证 |
| 接触面板概念稿 | `AWAKE/docs/AWAKE-ContactPanel-Concept-20260816.md` | 概念设计，未实现 |
| 对话可行性论证 | `AWAKE/docs/AWAKE-Dialogue-Feasibility-20260816.md` | 论证文档 |

## 3. 当前损坏 / 不可用

| 资产 | 位置 | 问题 |
| --- | --- | --- |
| UI 工作站 | `AWAKE/tools/ui-workstation/` | `artifacts/` 为空目录；`docs/ui-workstation-artifacts/` 不存在 → **没有任何打包产物**，其「UI 工作站」定位目前无产物支撑 |
| 工作站适配器 | `AWAKE/tools/ui-workstation/UiWorkstation.Adapter.ps1` | 是需要 `WorkspaceRoot` / `WbsBaseUrl` / `GameRoot` 的工作站桥接脚本（协议 `awake.workstation.v1`），**不是界面预览器**；单独跑没有意义 |
| 暂存副本（dist） | `AWAKE/dist/Modules/AWAKE/GUI/Prefabs/NpcDialogue.xml` | 时间 Aug 16，与源 `AWAKE/GUI/Prefabs/NpcDialogue.xml`（Sep 12）**不一致**（`38F62CB1…` vs `8BE75104…`）→ 副本已过期，**不可当作当前界面**；且 `dist/` 被 `.gitignore` 的 `**/dist/` 忽略，不入库 |

> 注意：`dist/` 整目录都是本地暂存区。任何"从 dist 看界面"的判断都可能看到旧版。

## 4. 待迁移 / 待新建

| 目标 | 现状 | 归属 |
| --- | --- | --- |
| 「对话指令二次确认」Prefab（含确认/拒绝控件） | **不存在**。已导入的基线 Prefab 没有确认/拒绝控件 | E4 前置项 |
| 游戏内 F10 验收壳 | **不存在**。全库搜 `F10` 在 `AWAKE/src` 无命中 —— 现有对话入口不是 F10 | E4 |
| 本地端的渲染与截图 | **不存在**。本地端目前只做夹具状态机、生命周期契约与 Prefab 结构/绑定校验 | 待定 |
| 旧 Lab 的 fixture 目录与宿主入口 | 只迁入了通用 Prefab + 状态清单 + 证据；旧 `profiles/`、`schemas/`、`tests/` 未迁 | 视需要再评估 |

## 5. 生产界面（真机入口；夹具不得复活、不得替换）

| 资产 | 位置 | 关键口径 |
| --- | --- | --- |
| 对话界面 Prefab | `AWAKE/GUI/Prefabs/NpcDialogue.xml`（7860B，Sep 12） | movie/layer 名 `NpcDialogue`；`localOrder=541`；命令：`ExecuteClose` ×2、`ExecuteSetChatMode`、`ExecuteSetNegotiationMode`、`ExecuteSend` |
| 对话界面宿主 | `AWAKE/src/NpcDialogueOverlay.cs` | 打开：`GauntletLayer("NpcDialogue", 541, false)` → `LoadMovie("NpcDialogue", vm)` → `AddLayer` → `SetInputRestrictions(true, InputUsageMask.All)` → `IsFocusLayer=true` → `TrySetFocus`；关闭为逆向 |
| 对话 ViewModel | `AWAKE/src/NpcDialogueVM.cs` | 与 Prefab 绑定配套 |
| 对话入口 | `AWAKE/src/NpcDialogueLauncher.cs` | `TryOpenDialogue` → `NpcDialogueOverlay.Open(...)`，共 3 处调用 |
| 关闭与 tick 驱动 | `AWAKE/src/AwakeDialogueHubLifecycle.cs`（`CloseActive`）、`AWAKE/src/SubModule.cs:113`（`NpcDialogueOverlay.OnApplicationTick`） | UI Lab 不触碰这两处 |
| 其它生产 Prefab | `AWAKE/GUI/Prefabs/`：`AwakeMessenger.xml`、`DeveloperCheck.xml`、`SceneDialogueStatus.xml`、`WeeklyReportBrowser.xml`、`WorldEventInbox.xml` | 各自 movie 名与层序独立；UI Lab 不得借用 |

**硬红线**：`Awake.UiLab` 的 movie/layer 名固定为 `AwakeUiLab`，构造函数会**拒绝** `NpcDialogue` 这个保留名（E2 用例 `reserved_production_movie_rejected` 覆盖）。

## 复现命令

```
# 源/暂存副本漂移
sha256sum AWAKE/GUI/Prefabs/NpcDialogue.xml AWAKE/dist/Modules/AWAKE/GUI/Prefabs/NpcDialogue.xml

# 工作站无产物
ls AWAKE/tools/ui-workstation/artifacts/ ; ls AWAKE/docs/ui-workstation-artifacts/

# 外部参考模组三份副本一致性
sha256sum MarcusAIFramework_Reference/AuthorSource/src_20260813/MarcusAINpc/GUI/Prefabs/MarcusAINpcDialogueOverlay.xml \
          MarcusAIFramework_Reference/AuthorSource/src_20260813/MarcusAINpc/artifacts/v1.3.15/Package/Modules/MarcusAINpc/GUI/Prefabs/MarcusAINpcDialogueOverlay.xml \
          MarcusAIFramework_Reference/AuthorSource/src_20260813/MarcusAINpc/artifacts/v1.4.8/Package/Modules/MarcusAINpc/GUI/Prefabs/MarcusAINpcDialogueOverlay.xml

# 无 F10 入口
grep -rn "F10" AWAKE/src/
```
