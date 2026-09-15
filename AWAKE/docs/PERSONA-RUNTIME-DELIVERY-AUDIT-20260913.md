# 角色卡运行时投递链路审计（2026-09-13）

> 触发：Max「现在跟 codex 没什么关系了，现在都是你的任务了」——把运行时投递侧收归本线。
> 目的：查清**实机到底有没有把卡喂给模型**，以及喂的形态是什么。
> 结论一句话：**没有。一张都没有。角色卡在运行时完全没有上线。**

---

## 零、三句话结论

1. **运行时读的是"包内嵌的 persona"，不是那些散装卡文件。** 读的代码有，**写的代码不存在**——投递通路从未端到端实现。
2. **游戏里现装的是一个 3 条目的验收小样包**（`awake:pilot.worldbook`，显示名「AWAKE 对话链路验收小样」），`extensions` 里**没有 persona**。⇒ 每个 NPC 拿到的"人格"只有两行：ID 和名字。
3. **76 张卡全是 `status: draft`**，从来没被批准过。就算通路通了，也会被运行时直接丢掉。

⇒ **前几轮所有关于"卡写得好不好"的实测，测的都是一个游戏读不到的东西。**
⇒ **Max 说的"演话剧、两张卡都一样"，在实机上不是"两张卡都不好"，是"根本没有卡"**——所有 NPC 共用一句模板风格指令，当然都一样。

---

## 一、运行时唯一入口：v2 包里的 `extensions.persona`

`src/WorldbookRuntime.cs`

```
113  string entryPath = LocateManifest();          // ModuleData/Worldbook/manifest.json
134  verified = WorldbookPackageIntegrity.ReadAndVerify(entryPath);   // 验包（要 packageHash）
147  if (!PersonaRuntimeBundleLoader.TryLoad(verified, out personaBundle, out personaError))
```

`WorldbookRuntime.cs:228`

```csharp
JObject persona = (package.Runtime["extensions"] as JObject)?["persona"] as JObject;
if (persona == null) return true;          // ← 不报错，bundle 保持 null
```

- 契约：`runtime.json` → `extensions.persona.schemaVersion == "awake.persona.runtime-bundle.v1"`，内含**一个** `definition` + 一个 `registry`。
- 注意 `RuntimeBundle.Definition` 是**单数**（`PersonaModels.cs:365`）——一个包只带一张人格定义。

## 二、谁写这个 `extensions.persona`？——没人写

全仓库搜索 `runtime-bundle` 字面量，**只有读取方**：

| 命中 | 角色 |
|---|---|
| `src/WorldbookRuntime.cs:230` | 读（校验 schemaVersion） |
| `docs/fixtures/persona-awake-joint/.../input.json` | 测试夹具 |
| `docs/persona-contract/awake.persona.fixture-input.v1.schema.json` | 契约 |
| `docs/PLAN-AWAKE-OFFLINE-CLOSURE-G3A-REV2-20260903.md` | 计划文档 |

产出 v2 包的编译器是 `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/RuntimePackageCompiler.cs`，第 76 行：

```csharp
["extensions"] = new JsonObject { ["contentTier"] = contentTier, ["source"] = "awake.worldbook.studio" }
```

**只写 contentTier 和 source。**

⇒ **包编译器不会写 persona；运行时会读 persona。中间那段从来没有接上。**

## 三、游戏里实际装的是什么

`<游戏>/Modules/AWAKE/ModuleData/Worldbook/`

| 文件 | 值 |
|---|---|
| `manifest.json` | `schemaVersion=awake.worldbook.v2`、`packageId=awake:pilot.worldbook`、显示名 **「AWAKE 对话链路验收小样」**、`kind=universe` |
| `runtime.json` | `extensions = {contentTier:"base", source:"awake.dialogue-chain-010.pilot"}` ← **无 persona** |
| `index.json` | 3 个条目：村庄的日子 / 路上的险情 / 领主的本分 |
| `persona_definitions/definitions/` | **只有 1 个文件**：`hero_default.json`（2026-08-18） |

⇒ 这是 codex 的**对话链路验收小样**，不是世界书正式包；persona 段完全空缺。

### 实机日志（`<游戏>/Logs/Awake.log`）

```
1152  2026-09-11 15:59:50Z worldbook_runtime_initialized schema=awake.worldbook.v2
      build_id=awake-20260911-dialogue-chain-010 package=awake:pilot.worldbook
      entries=3 identities=6 referrals=0 warnings=0
```

对照更早的旧世界书（2026-08-23）：`rules=334 personas=415 persona_definitions=1`——那是**旧人格体系**的计数，不是我们的卡。

> **待验证**：日志里**找不到任何 `npc_persona_projection` 行**（该行只在 `persona.Warnings.Count > 0` 时打印）。可能因为当时跑的构建还没有这行日志。**要坐实"identity-only"，需用当前构建跑一次游戏看日志。** 但按代码路径推演，结果必然是 `RUNTIME_FALLBACK`。

## 四、代码推演：每个 NPC 拿到的人格是什么

`src/PersonalDslGenerator.cs`（实际文件名为 `PersonaDslGenerator.cs`）

```
295   if (string.IsNullOrWhiteSpace(snapshot.CharacterId) || _bundle == null || !_bundle.IsApproved)
296       return PersonaDslGenerator.BuildRuntimeFallback(snapshot, maximumBytes);
```

`BuildRuntimeFallback` 产出（第 37 行）：

```
[PERSONA_RUNTIME]
RUNTIME_FALLBACK
IDENTITY_ONLY
ID="hero:lord_1_177"
NAME="某某"
```

外加 warning `persona.runtime_fallback_identity_only`。

**就是两行 ID + 名字。** 无 core、无私我、无底线、无矛盾、无私念。

## 五、即便通路修好，卡也进不去：76 张全是 draft

`src/PersonaDslGenerator.cs:66`

```csharp
if (definition == null || !StringComparer.Ordinal.Equals(definition.Status, PersonaSchemaConstants.StatusApproved))
{
    result.UsedLegacyFallback = true;     // → 走旧人格文本，或 identity-only
```

`src/WorldbookService.cs:242`

```csharp
if (definition == null || !StringComparer.Ordinal.Equals(definition.Status, PersonaSchemaConstants.StatusApproved)) continue;
```

物化脚本 `tools/persona-workbench/tools/materialize-definitions.ps1:106`

```powershell
status = [string]$card.status      // 原样搬运，卡是 draft → definition 也是 draft
```

### 实测状态分布（仓库 `ModuleData/Worldbook/persona_definitions/definitions/`）

```
状态分布: {'approved': 1, 'draft': 76}      总 77
```

唯一的 `approved` 是手写的 `hero_default.json`（`scope=role` 的角色兜底模板），**不是那 76 张中的任何一张**。

### 游戏目录里

```
persona_definitions/definitions/  →  只有 hero_default.json（2026-08-18 14:11）
卡数: 1
```

仓库里那 76 张物化于 **2026-09-13 17:36**，**从未同步到游戏**。

原因：`tools/sync_module.ps1`

```
59  if ($SkipWorldbook) {
60      # 试点包投放期间：不把工程的 ModuleData\Worldbook\** 当受管文件，
61      # 避免用后备版 v1 覆盖游戏内的 v2 试点包。
62      $managedWorldbookFiles = @()
63      $managedWorldbookDirectories = @()
```

同步当前跑在 `-SkipWorldbook` 模式，整个 worldbook 目录被排除。

## 六、投递链：四段断三

```
① 工作台授权   tools/persona-workbench/characters/*.persona.json
               76 张，status=draft                    ← 【断】从未批准
        ↓ materialize-definitions.ps1
② 物化定义     ModuleData/Worldbook/persona_definitions/definitions/*.definition.json
               76 张，status 照搬 = draft               ← 【断】仍是 draft
        ↓ sync_module.ps1（当前 -SkipWorldbook）
③ 同步进游戏   游戏目录 definitions/ ＝ 1 张 hero_default  ← 【断】76 张从未到达
        ↓ RuntimePackageCompiler（Studio 编译器）
④ 包内嵌 persona  应该是 extensions.persona.runtime-bundle.v1
               实际只写 {contentTier, source}          ← 【断】写完就没了
        ↓
⑤ 运行时读取   WorldbookRuntime.cs:228 读 extensions.persona
               读到 null → bundle=null → IDENTITY_ONLY
```

**四道关卡，全部关闭。** 这不是"最后一公里没接"，是**从授权到打包四段各有各的断点**。

## 七、这解释了 Max 的听感

`src/Prompts/NpcPromptTemplate.cs:59`

```
- 用中世纪人物的语气说话，短句、具体、有画面感，字数80到180，不使用现代心理学术语或网络词，不做道德说教。
```

当 `persona_dsl` 只剩 `IDENTITY_ONLY` 时，**模型收到的人格输入对所有人完全相同**（只有名字不同）。唯一塑造说话风格的就是上面这句：

- 「**有画面感**」→ 要画面 ⇒ 要比喻、要对举
- 「**字数80到180**」→ 不许短 ⇒ 逼着把话说满、说漂亮

⇒ 结果就是**全员同一副话剧腔，且人格无差别**。

**Max 说的"不管是蒙楚格还是呼鲁纳格都一样"——在实机上是字面意义上的真**，因为两张卡谁都没加载。这不是卡的问题，是**"没有卡 + 一句要求文艺腔的模板"**的问题。

## 八、被推翻/需修正的旧结论

| 旧结论 | 修正 |
|---|---|
| R4：「实机是 1-1 对话，所以大概不会出话剧腔」 | **前半对，后半错**。实机确实是多轮（见下第九节），但**卡根本没加载**，所以"话剧腔与否"在实机是个伪命题；且模板自带的「有画面感/80-180字」是新发现的风险源。 |
| R4：「要改的是怎么喂，不是写什么」 | 方向仍成立，但要加一句：**先得有"喂"这个动作**。当前连喂的管道都是空的。 |
| 前几轮所有卡级实测 | 测的是**游戏读不到的产物**。指标本身可能没错，但**外推效力归零**——不能用来断言实机表现。 |

## 九、顺带查清的两件事（原先挂账）

**1. 实机是多轮还是单轮？——多轮，确认。**

`src/NpcDialogueService.cs`

```
1170  rawVariables["dialogue_history"] = ...   // 由 NpcDialoguePromptPipeline 注入
1184  ["player_turn"] = playerText,             // 本轮玩家原话
1186  ["dialogue_action_mode"] = chat / negotiation
1261  while (_history.Count > NpcDialogueConstants.HistoryCapacity) _history.RemoveAt(0);
```

`NpcDialogueConstants.cs:16` → `HistoryCapacity = 12`（6 轮来回）。

**2. 有没有"预生成台词"旁路？——没有。**

运行时是**当场生成**：`BuildPersonaProjection` → `PersonaDslGenerator` 现场产出 DSL → 渲染进模板 → 单次请求。未发现任何批量预生成台词的路径。前几轮担心的"预生成批台词=话剧腔工厂"在运行时**不存在**。

**3. 输出格式是 JSON 契约。**

```json
{ "reply": "...", "mood": "两到四字情绪", "effects": [...], "command": {...} }
```

`reply` 上限 4000 字符，但模板口头要求 80–180 字。

## 十、要怎么修（不动手，先给判断）

按依赖顺序，**四段都要接**：

1. **授权**：明确"批准"动作谁做、写到哪。合同已写明 Workbench 的 `local_approved ≠ AWAKE approval`——那 AWAKE 侧那道闸门在哪？**当前没有任何东西读这个状态并写下批准。**
2. **物化**：`materialize-definitions.ps1` 要能承接批准结果（或引入独立批准清单）。
3. **打包**：`RuntimePackageCompiler` 要新增 `extensions.persona` 的产出——**并且要面对"一个包只带一张 definition"这个结构性限制**。76 张卡怎么装进一个单数槽位？这是**设计问题，不是编码问题**。
4. **投放**：`-SkipWorldbook` 的试点期要不要结束、正式包怎么替换试点包。

**我的看法**：第 3 条那个"单数槽位"是真正的拦路虎。要么改契约让 bundle 带多张、要么改成按角色分包、要么引入选择器。**这件事得先定，再谈其他。**

## 十一、边界与声明

- 本报告**只读**，未改动任何运行时文件、未碰游戏目录、未提交。
- 归属：运行时投递侧原属 codex 线，**2026-09-13 经 Max 明确移交本线**。
- 未验证项已标「待验证」；代码推演与日志观察分开列。
