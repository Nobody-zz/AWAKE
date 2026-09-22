# 交接单：存储与一致性（CODEX 审计降级五项 的证法）

> 主控线 · 2026-09-22。上游：`AWAKE/docs/AUDIT-SYSTEM-CODEX-20260922.md`（首轮）、`AUDIT-SYSTEM-CODEX-20260922-R2.md`（第二轮）。
> 本件用途：把第二轮**降级为"未证风险"的五项**，转成可直接派工的交接单。
> **派工件已拆为两张独立单**（各自整份即提示词，打开即复制、无需挑选）：
> - 主干线 → `AWAKE/docs/HANDOFF-STORAGE-20260922-MAIN.md`
> - 世界书线 → `AWAKE/docs/HANDOFF-STORAGE-20260922-WORLDBOOK.md`
> 本件保留**主控判断与逐条复核记录**（§零／§一／§三），供查证，**不用于派工**（避免两处副本各自腐烂）。

---

## 零 主控判断：这五条现在一条都不该修

第二轮的自评是 **REVISE（证据收口未完成，不是要求立即改生产代码）**——这个自评是对的，理由比它写的更硬：

**"没验过的东西不该修，因为不知道修什么。"**

但这不等于不动。五项按**判据能不能写出来**分三类，三类的对应动作完全不同：

| 类 | 项 | 为什么 | 对应动作 |
| --- | --- | --- | --- |
| ① 材料齐，只差人写 | **P1-02** 坏分片恢复<br>**P1-05** 信件半提交 | 夹具基础设施**早已存在**：`AwakeTestFakes.cs:16-19` 的 `FakeKeyValueStore` 带 `FailSet`／`FailSetAfter`／`CommitUnknownAfterWrite` | 写判据 → 让它是红的 → 再谈修 |
| ② 判据写不出，因为"什么算对"没人定 | **P1-04** Overlay 部分导入<br>**P1-06** unbound campaign | 不是夹具难造，是**没人裁决过"正确行为"是什么** | 先拿裁决，再写判据 |
| ③ 没有红绿，只有贵不贵 | **P1-03** revision 写放大 | 不是正确性问题，是成本问题 | **计量**，不是判据 |

**P1-01 是唯一的例外**：它已实证（我按它的方法独立复现过，带阳性对照），该修。但它的修法**不是局部修补，是框架契约变更**——见派工单 A 末。

### 0.1 一条硬读数：能力早就准备好了，判据没人写

```
$ grep -rn "CommitUnknownAfterWrite\|FailSetAfter" AWAKE.Tests/*.cs
AWAKE.Tests/AwakeTestFakes.cs:17:    internal int FailSetAfter { get; set; } = 1;
AWAKE.Tests/AwakeTestFakes.cs:19:    internal bool CommitUnknownAfterWrite { get; set; }
AWAKE.Tests/AwakeTestFakes.cs:53:        if (FailSet && SetCount >= FailSetAfter)
AWAKE.Tests/AwakeTestFakes.cs:62:        if (CommitUnknownAfterWrite)
```

**四个命中全在定义处，零个使用处。** 这两个开关正好对应：

- `FailSetAfter` → "第 N 次写失败" ⇒ **P1-05 要的场景**
- `CommitUnknownAfterWrite` → "值已提交但返回失败" ⇒ **P1-01 三态里的 unknown**

替身的设计者（09-15 那批，`AwakeTestFakes.cs:44-46` 有当时留的定案注释）明明想到了这两个场景、写进了替身，**从那一天起没有任何一条用例用过它们**。

⇒ 所以"这五条证不出来"的原因，**不是缺工具、不是能力不够**，是**没人去写**。这决定了交接单的语气：不是"你去研究"，是"你去写"。

**同一个病在流程层还有一份**：`AWAKE/tools/` 下 **25 个常驻脚本，零个跑测试**（逐个 grep `SdkSmoke`，25/25 不提）。
工具都在，就是没串成一条链——这与 P1-01 的"判据没人写"是同一种失败：
**能力齐备，接不上流程。**

---

## 一 已经定过的，别再当悬案

CODEX Q11 列了七条"未冻结的产品语义"，并说"没有先冻结这些，P1-02 至 P1-06 只能是风险陈述"。这个说法**有一条不成立**——其中一条早就定过：

### 1.1 Q11-1（journal 损坏时优先可写性还是保持历史可查询）——**已定，09-15**

证据两处，都在代码里：

```
WorldStateStore.cs:3673-3675
    // 坏账本不能让这本账永久写不进去（读坏 ⇒ 放弃写入 ⇒ 永远坏，是同一个死锁的另一半）。
    // 先把坏值隔离到旁路 key（只复制、不删原件，数据不丢），再按空账本继续 ——
    // 写入流程末尾会用新 root 覆盖掉坏值。2026-09-15 定案。
```

```
$ git log -1 --pretty='%h %ad %s' --date=format:'%m-%d %H:%M' -- AWAKE/src/WorldStateStore.cs
437bedd 09-15 20:04 周报坏根第二刀：坏账本自愈（隔离 ⇒ 重建）＋ IsStorageKeyNotFound 定案（刻意不动行为）
```

⇒ **答案：优先可写性。** 做法＝隔离坏 root（只复制不删）⇒ 按空账本继续 ⇒ 新 root 覆盖。
⇒ CODEX 说"未冻结"，是因为**它没找到这个决定**——决定在代码注释和提交信息里，不在任何设计文档里。
⇒ 这本身就是"状态记录会腐烂"的又一例，而且这次是**腐蚀在正式文档、保存在代码注释**。

**但只定了半边。** 未定的那半边，`WorldStateStore.cs:3756-3758` 的注释把它当成优点写了：

```
    // 若本次写入最终失败，坏值仍在原位，下次重试会再隔离一次（同名覆盖，不会膨胀）。
```

隔离件的 key 是 **`RootKey + ".quarantine"`，固定名字、只存一份**（`:3771`）。所以：

- **连续两次坏根 ⇒ 第一次的现场被第二次覆盖。**
- 注释作者把它读作"不会膨胀"（优点）；代价是**第一现场永久丢失**（没人写下来）。

⇒ 这是**取舍，不是无意 bug**。要不要改留给人裁：若要保第一现场，隔离件 key 加时间戳或序号，代价是存储会累积。

### 1.2 主控直接定的四条（不给专线留悬案）

凡"能定的不定、往下推"就是又一次"知而不行"。以下四条**当场定**，依据都是项目既有口径；**任何一条若有反证，拿反证来推翻**。

| # | 问题 | 判定 | 依据 |
| --- | --- | --- | --- |
| Q11-6 | 是否允许"已提交但返回失败" | **允许，但必须暴露** | 项目口径「静默成功比抛错危险」。且 `OperationResult` 已有 `IsSuccess`／`Value`／`Error` 三字段 ⇒ 契约本身有空间，**缺的是语义约定**，不是签名 |
| Q11-7 | 未跟踪发布产物是否属 smoke 必要输入 | **不属于。smoke 只许依赖 Git tree** | `AWAKE/release/` 是被 `.gitignore:60` **有意**排除的（同处注释：`All regenerable by their own scripts and never part of the source of truth`，并记着一条血教训——C 盘工作区曾让这类产物涨到 **33+ GB** 才在 09-11 清掉）。修法是把生成接进构建入口，**不是**把包塞进仓库 |
| Q11-3 | 信件正文／账本／未读统计谁权威 | **账本为权威**（界面读账本）；正文是内容载体 | 半提交时正文孤儿**无害但应可清理**；不得因正文存在就报发送成功 |
| Q11-4 | campaign 未绑定时存储怎么办 | **拒绝写入并报可重试**（**行为变更**，现行是照写） | 现行落 `unbound` 目录（`AwakeFileStorageService.cs:71` ＋ `:99-103`）⇒ 写入成功、开局后读不到（`ResolveCampaignId()` 届时返回真 id）⇒ **必然错位** |

### 1.3 要甲方一句话的唯一一条

**Q11-5：journal 的保留／压缩／回收策略与容量目标（预期玩多久、多少条事实）。**

这是**成本决策**，不取决于技术：**改不改 P1-03、改到什么程度，全看这个数字**。我不替他定。

**09-22 处置**：甲方未答，选择直接派工 ⇒ P1-03 在派工单里**只要求交数字、不动实现**；
容量目标仍挂着，等甲方给数（或等实测量级出现意外时回问）。

---

## 二 派工件（已拆分为两张独立单）

**派工的实际动作是"整段粘贴给对应会话"。** 合在一处，接收方要在一份长文档里找自己那一节——**多一道人工挑选，就多一次选错的机会**。故拆开：一份文件＝一个接收方，打开即复制。

| 线 | 文件 | 覆盖 |
| --- | --- | --- |
| 主干线 | `AWAKE/docs/HANDOFF-STORAGE-20260922-MAIN.md` | P1-05 ／ P1-02 ／ P1-01 ／ P1-03 ／ P1-06 |
| 世界书线 | `AWAKE/docs/HANDOFF-STORAGE-20260922-WORLDBOOK.md` | P1-04 Overlay 半提交 |

每张单自包含（背景／第一原则／事实与行号／已定口径／红线／验收／出处），**整份即提示词**，无需裁剪。

---

## 三 出处与复核记录（本件的每个断言怎么来的）

| 断言 | 复核方式 | 结果 |
| --- | --- | --- |
| `FakeKeyValueStore` 的故障开关**零使用** | `grep -rn "CommitUnknownAfterWrite\|FailSetAfter" AWAKE.Tests/*.cs` | 4 个命中**全在定义处**（`AwakeTestFakes.cs:17,19,53,62`），使用处 0 |
| `AwakeLetterService.cs` 不在测试工程 | `grep -n "AwakeLetterService" AWAKE.Tests.csproj` | `:89-90` 是**注释**而非 `<Compile>` ⇒ 不在。⚠ 注意：`grep -q "文件名"` 会命中注释，**"文件名字符串出现"≠"文件被纳入"** |
| 三个世界书／存储类**在**测试工程 | 同上 | `:64` WorldStateStore ／ `:151` WorldKnowledgeQueryService ／ `:46` AwakeFileStorageService ／ `:116` WorldFactJournal |
| P1-02 隔离只复制 root | 读 `WorldStateStore.cs:3761-3778` | `:3771` key = `RootKey + ".quarantine"`，不动 chunks |
| Q11-1 已定（09-15） | 读 `:3673-3675` ＋ `git log -- AWAKE/src/WorldStateStore.cs` | 注释含「2026-09-15 定案」；提交 `437bedd 09-15 20:04` |
| P1-01 缓存污染机制 | 读 `AwakeFileStorageService.cs:140,163-165,168-172,209-231,245-248` | `Load()` 返回同一字典实例 ⇒ `SetAsync` 改的就是缓存；`catch` 不回滚 |
| P1-01 上游受害点 | 读 `WorldStateStore.cs:3714-3718,3737-3742` | `:3717` 的重读确认被脏缓存骗过；`:3737` 的 `rootStored` 只在失败分支用到 |
| `IKeyValueStore` 是框架契约 | `grep -rn "interface IKeyValueStore"` | `AWAKE/framework/MarcusAwakeFramework/src/StorageAndRagApi.cs:48-53` |
| 世界书线在途改动 | `git diff --stat -- AWAKE/src/WorldKnowledge*.cs` | 三件 `+94/-1`（`QueryService` +53） |
| 旧 chunk 未见清理 | `grep -rn "DeleteAsync" --include=*.cs AWAKE/src` | 只命中 `AwakeFileStorageService.cs:176` 的**定义处** ⇒ **在 src 现行链路上未见调用**（口径已写在交接单里，不写"永不删除"） |
| `build.ps1` 只编译不跑测试 | `grep -n "dotnet\|SdkSmoke" AWAKE/tools/build.ps1` | `:45` 只有 `dotnet build`，无 run |
| **25 个常驻工具脚本中，零个跑测试** | 逐个 `grep -q "SdkSmoke"`（分母＝`tools/*.ps1` 去掉 `_` 前缀，25 个） | **25/25 不提**。仅两处 `_` 前缀临时脚本提及，其中 `_persona_golden_fixture_regen_20260919.py:18` 那处**还是注释里的用法示例**（`./Awake.SdkSmoke.exe > run.txt`），不是真调用 ⇒ **没有常驻的、正式的跑测试入口**（与"第 64 项依赖未跟踪产物"同源：构建链不完整） |

**未复核／存疑**（不要当既定事实用）：
- P1-02 里"root 好但某 chunk 坏时会怎样"——本件只给了推测（`:3671` 只看 root ⇒ 推测隔离分支不触发），**未实测**，已在交接单里要求专线去测。
- P1-03 的写放大倍率——**未计量**，交接单里要的是数字，本件不给数字。
- 09-14 现场故障与 P1-06 的因果关系——审计明确说"不能归因"，本件同样不归因。

---

*—— 全局主控线 · 2026-09-22*
