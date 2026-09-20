# 世界书条目更新安全边界（v1）

> 立档 2026-09-20。触发：甲方 09-20「世界书知识条目只做了一小部分，后面肯定会有陆陆续续的更新的」。
> 本文回答一件事：**内容会长期增改的前提下，哪些改法是安全的，哪些会打坏玩家存档。**
> 体例：每条结论后面挂 `file:line`；凡"现行链路上没有"一律写明检索范围（结论只在这范围内成立）。

---

## 〇 一句话结论

**加档、改正文、改标题、改别名 —— 安全，随便改。**
**改档名（id 的 slug 段）、改 domain —— 会打坏存档，且是静默的、不可逆的。**
**"玩家覆盖撞上游更新"这件事，在官方包范畴内搁置不做**（甲方 09-20 14:38 口径，见 §三）。

口径（甲方 09-20 14:38）：
> 「现在在做的还在官方包范畴，不能把确认什么的做进来。未来包应该是长期稳定的。」

⇒ 两条读法，贯穿全篇：
1. **官方包长期稳定** ⇒ 「改 id / 删档」不是常态操作，而是**发布事故**。所以防线放在**发布前检查**（§1.4 那道离线校验、§1.2 那个探针），**不放在运行时问玩家**。
2. **不把确认类交互做进来** ⇒ 任何"读档时让玩家选"、"弹窗问要不要保留"的机制，本阶段一律不做。

---

## 一 边界一：条目 id（改哪些字会换掉 id）

### 1.1 id 只有一条生成规则，落点唯一

```
AWAKE/tools/worldbook-studio/src/Awake.WorldbookStudio.Core/RuntimePackageCompiler.cs:504
    StableEntry(v) = "awake:entry:" + Sanitize(v.Replace("doc.", ""))
AWAKE/tools/worldbook-studio/src/Awake.WorldbookStudio.Core/RuntimePackageCompiler.cs:508
    Sanitize(v)    = v.Trim().ToLowerInvariant()，保留 [字母数字 _ - .]，其余换 _，两端去 _
调用点：同文件 :110-111    sourceId = 档里的 id:；entryId = StableEntry(sourceId)
```

即：**档里写 `doc.geography.castles-ain-baliq-castle`，包里就是 `awake:entry:geography.castles-ain-baliq-castle`。**
`doc.` 这个前缀被去掉、换成 `awake:entry:`，其余一字不动。

> 注（低优先）：实现用的是 `Replace("doc.", "")`，删的是**所有**出现的 `doc.`，不只是开头那个。
> 2026-09-20 核过全部 558 档，`doc.` 每档只出现一次，所以今天没问题。
> 但将来若有档的 slug 里带 `doc.`（例如 `doc.geography.doc-archive`），id 会被静默削掉一段。写档名时避开即可。

### 1.2 实测读数（2026-09-20，跑 `AWAKE/tools/_wb_id_stability_probe_20260920.py`）

同源复算（把上面那条规则原样实现一遍，拿真产物对）：

```
产物（ModuleData/Worldbook/packages/calradia/runtime.json）
    entries 总数                     = 482
    前缀/pre 不符或异形 id            = 0
    非规范形（Sanitize 会改写它）      = 0
    域分布：geography 411 / war 37 / economy 21 / politics 7 / culture 6

源（docs/worldbook-migration/projection/authoring-out/*.yaml）
    带 id: 的档数                     = 558
    前缀分布：doc.× 558

对照
    源与产物对得上                    = 482   （100%）
    产物有 / 源侧复算不上             = 0
    源有 / 产物没有（未编译进包）      = 76
```

⇒ **映射目前是纯可预测的一对一。** 任何一个档，改之前就能离线算出 id 会变成什么、会不会撞车。

探针自证（`_wb_id_stability_mutation_20260920.py`）：把 `Sanitize` 白名单里的 `-` 去掉一格，非规范形读数由 **0 → 482**。说明上面那个 0 是真读数，不是判据恒假。

### 1.3 因此，可以/不可以这样改

**安全（id 完全不动）**
- 改 `title`（中/英）、`aliases`、`summary`
- 改 `assertions`（正文断言、`kind`、`conditions`）
- 改 `content_tier` / `status` / `era`
- **新增**档（新 `id:` 没出现过）
- 改已有档的 `revision`

**危险（id 跟着变）**
- 改档名里点号后那段：`doc.geography.castles-ain-baliq-castle` → `...ain-baliq` ⇒ id 变，**该条在存档里的玩家覆盖变悬空**
- 改 domain（点号前那段）：`doc.geography.x` → `doc.geo.x` ⇒ id 变
- **一次改 `Sanitize` 或 `StableEntry` 的规则**（比如允许更多字符、或把 `.` 换成 `-`）⇒ **482 条一起变**
  ⇒ 这种改动的探测器就是 §1.2 那一行「非规范形」；它一变成非 0，全仓 id 已经漂了，**不要提交**。

**删档** ⇒ 同"改 id"，且更彻底：那条目在整个包里消失。

### 1.4 改 id 会在哪里先炸（离线就炸，不会拖到游戏里）

```
AWAKE/src/WorldbookPackageIntegrity.cs:127-130
    index.json 的 entryIds 必须与 runtime.json 的 entries[].id 逐一相等（含顺序）
AWAKE/src/WorldbookPackageIntegrity.cs:132-134
    keywordToEntryIds / domainToEntryIds 必须与 runtime 侧同构
```

⇒ 改 id **必须整包重编译**，不能只改一个文件。手改单文件会当场被这条挡下（错误码 `WB2-INDEX-MISMATCH:entry_ids`）。

---

## 二 边界二：存档里的悬空（机制有洞，官方包下不易触发）

**先定级。** 悬空只可能由"上游改 id / 删档"造成——玩家自己在游戏里改不出悬空（写入时目标必须存在，见 `WorldKnowledgeQueryService.cs:173`）。
既然官方包长期稳定（§〇 口径），那么这个洞**在官方包范畴内不是常态风险，而是发布事故的后果**。
所以处置方式是"**发布前挡住**"，不是"运行时兜底问人"。下面这条链仍要写清，因为它决定了"万一发生，代价有多大"。

### 2.1 存档面：只有一处真正存了条目 id

| 存档 key | 谁写 | 里面有没有条目 id |
|---|---|---|
| `awake_worldbook_overlay_v1` | `AWAKE/src/AwakeTerminalBehavior.cs:70` | **有**，`operations[].targetId` 就是条目 id |
| `awake_worldbook_activation_v1` | `AWAKE/src/AwakeTerminalBehavior.cs:78` | 没有；存的是包身份 `packageId/version/packageHash` |

补充口径（防误读）：
- **事件记录与周报的 `entryId` 字段在现行链路上是空的预留位。** `AWAKE/src` 里只有 schema 白名单与格式校验四处：`AWAKE/src/WorldEventContracts.cs:889,894,963,976`；检索 `AWAKE/src`、`AWAKE.Tests`、`tools/`、`AWAKE/tools/` 全树（排除 `bin/`、`obj/`），除这四处与测试里自造的条目（如 `AWAKE/tools/worldbook-runtime-smoke/Program.cs:1883`）之外，**没有任何生产者往事件/周报里写 `entryId`，也没有任何消费者按它取条目**（2026-09-20 检索）。
  ⇒ 所以本文的悬空风险目前只在 overlay 这一面；将来若把 `entryId` 真填上，同一套问题会原样复制到事件与周报上。
- overlay 的写入入口只有一个，且是开发者测试菜单：`AWAKE/src/AwakeDeveloperTestActions.cs:171 → :192 → :206`。玩家正式 UI 目前不接。

### 2.2 悬空时会怎样（这条链每一步都有落点）

```
AWAKE/src/WorldKnowledgeQueryService.cs:173   回放某条 op：Entries.TryGetValue(targetId, …) 找不到 ⇒ changed 仍为 false
AWAKE/src/WorldKnowledgeQueryService.cs:191   ⇒ error = "WB2-REFERENCE-MISSING"，return false
AWAKE/src/WorldKnowledgeQueryService.cs:237   调用方 TryImportOverlay 在 foreach 里 return false ⇒ 整条回放就地中断
AWAKE/src/AwakeTerminalBehavior.cs:74          上层只写一行日志 awake_worldbook_overlay_load_error；玩家侧无任何提示
AWAKE/src/AwakeTerminalBehavior.cs:68-70       下一次存档：ExportOverlayJson() 返回的是「回放了一半」的那份，非空 ⇒ 覆盖存档字段
```

**实际表现（推演，见 §2.4 待验证）**：
1. 玩家读档；overlay 里有一条 op 指向一个已被改名/删除的条目；
2. 按存档里的 op 顺序回放，**在那一条断掉，它后面的 op 全部不再执行**（哪怕它们指向的条目都还在）；
3. 前面已执行的那些 op 已经改进内存、`revision` 也已递增，**且不会回滚**；
4. 这次游戏里再存档，写回去的就是这份半截 overlay —— **被丢掉的 op 从此消失，不可逆**。

### 2.3 但数据本身没丢——是回放逻辑把它扔了

```
AWAKE/src/WorldKnowledgeQueryService.cs:199   op 里存了 ["value"] = value
```
⇒ 每条 op 自带"玩家当年改成什么"。找到不目标不是"数据没了"，而是**回放器不会把它放一边等**。
这一条决定了 §2.5 的方案 B 能不能做，而且很便宜。

### 2.4 待验证（不要在游戏外当成已证）

以上链路是**代码面已定**（每一步都有落点）。但**「读档时确实会走到这条分支、玩家确实看不到提示」还没有在游戏内验过**。
⇒ 归入 B 档（必开一次游戏）：在游戏内改一条摘要 → 退出 → 把那个条目改名重编 → 读档，观察日志与界面。
**在那之前，"半截不可逆"只能算高置信推演，不算已证。**

### 2.5 如果要动，就动这两条（都不是"问玩家"类）

**A. 断点不再连带（最小改动，一处）**
`TryImportOverlay` 的循环改成「坏 op 跳过、记进清单、继续跑」，别 `return false` 整条停。
→ 只丢坏的那几条，不再连带后面全废。改动面：`WorldKnowledgeQueryService.cs:231-239`。

**B. 孤儿暂存（治本，且便宜）**
因为 op 自带 `value`（§2.3），找不到目标时**不丢，改存一份 orphan 列表**；等条目回来再回放。
→ 真正消除不可逆丢失。改动面：`WorldKnowledgeQueryService.cs` 新增状态 + `Export/Import` 带上它；
另加一条：`AwakeTerminalBehavior.cs:68-70` 在导入未完成时**不要**用内存值覆盖存档字段。

**要不要做、什么时候做，由甲方定。** 按 §〇 口径，官方包稳定期它们**不是必须项**：
它们是"事故不至于不可逆"的保险，而不是把冲突搬到玩家面前。
**不做也成立**——前提是 §四 第 3 条那道发布检查真的在跑。

**（原方案 C 撤销）** 曾拟"填真 `baseActivationId`、读档比包指纹、让玩家选保留谁"——按甲方 09-20 口径**不做**，见 §三。

---

## 三 边界三：玩家覆盖 vs 上游更新（本阶段搁置）

**甲方口径（2026-09-20 14:38）**：
> 「现在在做的还在官方包范畴，不能把确认什么的做进来。未来包应该是长期稳定的。」

⇒ **本节结论：不做。** 官方包长期稳定 ⇒ "上游更新撞玩家覆盖"不是常态事件；
即便发生，也**不把它搬到玩家面前要一个选择**（那正是"确认什么的"）。
下面 3.1 / 3.2 保留为事实记录：**契约确实写了这件事、实现确实没兑现**——供未来做第三方包/工坊包时接手，不是现在的欠账单。

### 3.1 契约写了（原话，不是我的新主意）
```
AWAKE/docs/AWAKE-Worldbook-Contract-v1.md:46
    「每个操作带 baseRevision，不匹配时拒绝写入，避免覆盖基础包升级或其他修改。」
```
契约 schema 也把这件事列为必填：
```
AWAKE/tools/worldbook-contract/v1/overlay.schema.json
    required: [..., "baseActivationId", ...]   ← 必须写明"我是基于哪一次激活改的"
```

### 3.2 实现里的三个缺口

```
缺口 1  AWAKE/src/WorldKnowledgeQueryService.cs:214
        ["baseActivationId"] = "awake:activation:current"   ← 写死的常量
        而 WorldbookActivationState.ActivationId 的默认值正是这一串（AWAKE/src/WorldbookPackageRegistry.cs:11）
        ⇒ 它恒等于默认值，不携带任何版本信息。

缺口 2  AWAKE/src/WorldbookPackageRegistry.cs:29  激活状态里确实存了 packageHash
        AWAKE/src/WorldbookRuntime.cs:93          读档时只取 world.packageId，packageHash 读出来就丢
        ⇒ 手上有一把能比的钥匙，没用。

缺口 3  AWAKE/src/WorldKnowledgeQueryService.cs:227-228  导入只比 revision
        ⇒ 只要存档的 revision 更大就全量回放，跟"包是不是换过"毫无关系。

        并且 AWAKE/src/WorldKnowledgeQueryService.cs:175-176 的落地方式是就地改写快照里的条目对象
        （entry.Summary = value），不是叠加层
        ⇒ 上游的新正文永远输给玩家的旧覆盖，而且没有任何标记说明"这条被玩家改过"。
```

`baseRevision` 那条 CAS（`WorldKnowledgeQueryService.cs:171`）只在**同一次进程会话内**有意义：回放时逐条递增，天然匹配。**跨包升级它一次都不会拦。**

### 3.3 本阶段怎么处理（不做交互）

**不设计任何"让玩家选保留谁"的界面，不做读档冲突提示。** 理由见本节开头甲方口径。

只留一条纪律，代价为零：**官方包内改已有条目的正文/标题/别名，不必管玩家覆盖**——因为官方包稳定，不会有"上游新版本"来撞它；
真正要守的是 §一那条（不许改 id），那守住了，玩家覆盖就永远对得上。

---

## 四 建议的动作顺序

1. **必做，且只是纪律（不用改代码）**：
   - 档名（`id:` 里点号后那段）与 `domain` **进包后不得再改**。确需改，先跑出新旧 id 对照表，按"改 id"当发布事故处理。
   - 每次发布前跑一次 `_wb_id_stability_probe`，**「非规范形」必须为 0**；非 0 说明 id 生成规则被动过，全仓 id 已漂 ⇒ 不许发。
2. **可选（不是必须项，甲方定）**：§2.5 的 A + B。都在 `WorldKnowledgeQueryService` 与 `AwakeTerminalBehavior` 两处，属世界书线／运行时线。官方包稳定期不做也成立。
3. **不做**：§三 的玩家覆盖冲突交互。
4. **顺带（低优先，别混进上面）**：`overlay.schema.json` 要求 `revision` 与 `baseRevision` ≥ 1，而实现从 0 起（`WorldKnowledgeQueryService.cs:192-199`）。这份 schema 目前只被 `export.schema.json` 引用，运行时没有校验器 ⇒ 是"写在那里没在执行"的一处，记档即可。

---

## 五 官方包 / 未来包的分野（甲方 09-20 口径）

| | 官方包（现在做的） | 未来包（工坊／第三方，未开工） |
|---|---|---|
| 内容稳定性 | **长期稳定**，甲方口径 | **也应当按长期稳定设计**——这是准入要求，不是事后补丁 |
| id 谁保证 | 发布纪律（§四 第 1 条） | 包的准入校验（同一道探针，作为收包门禁） |
| 玩家覆盖冲突 | 搁置，不做交互 | 到那天再定：`baseActivationId` + 包指纹已在契约里等着（§3.1） |

⇒ 一句话：**现在不写兜底，靠"包不瞎改"守住；将来做第三方包时，把同一道检查变成收包条件。**
实现里那三个缺口（§3.2）**仍然记在档上**，因为它们是未来包必需的地基；只是现在不修。

---

## 附：本次取证用到的过程脚本（留档，可复跑）

- `AWAKE/tools/_wb_id_stability_probe_20260920.py` —— id 映射同源复算与对照；读数落 `_wb_id_stability_probe_20260920.txt`
- `AWAKE/tools/_wb_id_stability_mutation_20260920.py` —— 探针自证（变异检验）
