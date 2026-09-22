# 交接单：存储与一致性（CODEX 审计降级五项 的证法）

> 主控线 · 2026-09-22。上游：`AWAKE/docs/AUDIT-SYSTEM-CODEX-20260922.md`（首轮）、`AUDIT-SYSTEM-CODEX-20260922-R2.md`（第二轮）。
> 本件用途：把第二轮**降级为"未证风险"的五项**，转成可直接派工的交接单。§二 与 §三 是**两个可整节粘贴的提示词**。

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

**P1-01 是唯一的例外**：它已实证（我按它的方法独立复现过，带阳性对照），该修。但它的修法**不是局部修补，是框架契约变更**——见 §二 末。

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

---

## 二 交接单 A —— 主干线（以下整节可直接粘贴）

```text
【背景】
项目：AWAKE（骑砍2 AI 运行时模组），工作区 D:\AWAKE-Dev，游戏 API v1.3.15。
你负责主干线（运行时／存储／信件／事件）。这是一份**证伪任务**，不是修 bug 任务。

起因：外部审计（CODEX）两轮跑完，把六项"疑似缺陷"中的五项降级为"未证风险"。
降级是诚实的，但降级不等于问题不存在——变的是证据强度，不是风险。
本任务＝**把这四项（P1-02／03／05／06）的判据补上**，以及 P1-01 的修法落地。

【第一原则：先写一条能红的判据，再谈修】
现在离线 64 例全绿。这五条没有一条被判据覆盖 ⇒ 现在修等于"修完还是全绿"，
无法证明修好了。**顺序不许反：先让判据红，再改实现让它绿。**

注意：**材料早就齐了，缺的是人写。**
AwakeTestFakes.cs:16-19 的 FakeKeyValueStore 已带四个故障注入开关：
    FailSet / FailSetAfter（第 N 次起写失败）/ FailGetWithKeyNotFound / CommitUnknownAfterWrite
实测（grep 全 AWAKE.Tests）：这四个开关**只出现在定义处，零使用处**。
其中 FailSetAfter 正好是下面 P1-05 要的场景，CommitUnknownAfterWrite 正好是 P1-01 三态里的 unknown。

【事实：三项的现状机制（行号对应 HEAD；动手前自行复核，别照抄）】

■ P1-02 坏分片恢复 —— 现状：root 坏 ≠ 账本坏，但代码把两者当同一件事
WorldStateStore.cs:3671   只在 root 读成 Corrupt 时进隔离分支
WorldStateStore.cs:3771   隔离只复制 root（key = RootKey + ".quarantine"），不动 chunks
WorldStateStore.cs:3678   隔离后当作 Missing，facts 从空开始
WorldStateStore.cs:3707   随后用新 revision 重建 chunks（key 含 revision ⇒ 新 key 与旧的不同）
WorldStateStore.cs:920/934/937  chunk 侧的三种坏法：chunk_missing / chunk_key_mismatch / chunk_json_invalid
要回答：
  (a) **root 好、但某一个 chunk 坏**时，读返回什么？写会发生什么？隔离分支会不会被触发？
      （注意 :3671 只看 root 的状态 ⇒ 推测不触发，但那只是推测，去测）
  (b) 坏 root 被隔离、新 revision 写入后，**旧的健康 chunks 还在不在存储里**？
      如果在、又没人再引用它们 ⇒ 它们是永不可达的孤儿。读一次说出来。
  (c) 连续两次坏根，第一次的 .quarantine 还在不在？（:3756-3758 的注释说同名覆盖，去验）

■ P1-03 revision 写放大 —— 这不是正确性问题，是成本问题，别给它写"红/绿"
WorldStateStore.cs:3707   每次追加一条 fact，都 BuildJournalChunks(facts, revision) 重建**全部** chunks
WorldStateStore.cs:3800   BuildJournalChunks 的实体
  + key 含 revision ⇒ 每次追加产生 N 个新 chunk，旧 N 个变孤儿；
    口径（否定断言，已当场 grep）：**在 AWAKE/src 现行链路上，未见任何 DeleteAsync 调用**
    （`grep -rn "DeleteAsync" --include=*.cs AWAKE/src` 只命中 AwakeFileStorageService.cs:176 的定义处）
    ⇒ **在这个口径下，未见对旧 chunk 的清理**。
  + WorldStateStore.cs:3713-3725 每个 chunk 写完后要两次读回（一次失败分支、一次无条件 :3720）
要产出的是**数字**，不是结论：
  (d) 追加第 k 条事实时，实际发生多少次 Set / 多少次 Get；写入字节量的增长曲线。
      用真存储适配器或 FakeKeyValueStore 的 SetCount/GetCount 都行，**给读数，别给形容**。
  (e) 顺带量一次：追加 100 条事实后，存储里累积的孤儿字节有多少。
注：改不改、改到什么程度，取决于甲方给的容量目标（尚未给）。你只负责把数字拿出来。

■ P1-05 信件半提交 —— 夹具卡点已查明
AwakeLetterService.cs:423  第 1 步 AppendLetterAsync（写**正文** transcript）
AwakeLetterService.cs:437  第 2 步 RecordAsync（写**账本** ledger）
两行之间无事务。第 2 步失败 ⇒ 正文已落盘、账本无记录、返回 false。
**卡点**：AwakeLetterService.cs **不在** AWAKE.Tests.csproj（该文件第 89-90 行的注释写明原因：
  它经 AwakeMessengerService → NpcDialogueLauncher 触及 Gauntlet UI overlay，测试工程不引用
  TaleWorlds.MountAndBlade，故只收了零依赖的 AwakeLetterLedger.cs 与 AwakeLetterRequestValidation.cs）。
⇒ 直接 new 不出来。
要回答：
  (f) 这一步的**编排**（写正文 ⇒ 写账本）能不能从 UI 依赖里摘出来、纳入测试工程？
      摘出来之后，用 FakeKeyValueStore.FailSetAfter 精确让第 2 次写失败，判据写在哪。
  (g) 两步都带幂等键（:429 用 idempotencyKey；:450 用 "letter-add|"+idempotencyKey）
      ⇒ **重试能不能收敛？** 造"第 2 步失败 ⇒ 重试一次"的场景，读实测结果。
      这条决定严重性：能收敛就只是窗口期不一致，不能收敛才是永久损坏。
  (h) :431 的分支（正文写失败 ⇒ 反而去写账本留痕 RecordFailedAsync）是有意设计（:459 注释说明）。
      验一下它会不会让账本里出现"只有生命周期、没有正文"的记录，以及读侧怎么显示。

■ P1-06 unbound campaign —— 先判"这条到底能不能离线证"
AwakeFileStorageService.cs:99-103  ResolveCampaignId()：Campaign.Current 为 null 或 id 空 ⇒ 返回 "unbound"
AwakeFileStorageService.cs:71       路径 = <module>/PlayerExports/AwakeState/<campaign>/<ns>.json
要回答：
  (i) ResolveCampaignId 读的是**游戏全局状态**（Campaign.Current）⇒ 离线造不出"两次绑定切换"。
      这条**能不能离线证**？能就写判据；不能就**明说必须真机**，
      **不许造一个替身去代理它**（替身与真件不同源 ⇒ 离线全绿、真机必坏，这个坑本项目踩过）。
  (j) 若真机才能证：把"要在游戏里看什么"写成一句话的可观察现象交给主控，
      不要自己去启动游戏（见约束）。

■ P1-01 写盘假成功 —— 已实证，该修；但修法是框架契约变更，不是局部补丁
机制（我独立复现过，带阳性对照；比审计报告写的更精确）：
  AwakeFileStorageService.cs:209-231  Load() 惰性加载，一旦 values != null 就**永远返回同一个字典实例**
  AwakeFileStorageService.cs:163-165  SetAsync 拿到 current = Load() ⇒ **current 就是缓存本身**，
                                      改 current[key] 就是改缓存；随后 Save() 抛异常
  AwakeFileStorageService.cs:168-172  catch 分支只写日志 + 返回失败，**不恢复 values**
  AwakeFileStorageService.cs:140      GetAsync 也走 Load() ⇒ 返回那个已被改脏的缓存
  ⇒ 同一实例内，Get 永远看不到写失败。
  AwakeFileStorageService.cs:245-248  Save() 是 temp 写入 + File.Replace(temp, path, null)：
                                      Replace 在 Windows 上**可能已经替换成功、然后才抛** ⇒ 这就是 unknown 态
  上游受害点：WorldStateStore.cs:3714-3718 的"写失败后重读确认"——它建在会撒谎的缓存上 ⇒ **恒为真**。
              按本项目口径「恒 True＝没测」，这道冗余确认已经退化成装饰。
              WorldStateStore.cs:3737-3740 更直接：取了 rootStored 却**只在失败分支用它取错误码**，
              判定完全靠"读回来等不等于我写的"。
要回答（顺序不许反）：
  (k) **先写判据**：现在 64 例全绿，这条缺陷无人覆盖。用真文件适配器造"写失败"，
      判据必须能红。参考已有的真跑过的探针：AWAKE/tools/_verify_p1_01_20260922.ps1
      （带阳性对照：可写目标必须真落盘），读数是 _verify_p1_01_20260922.txt。
      ⚠ 夹具卡点：JsonFileKeyValueStore 是**私有嵌套类**（AwakeFileStorageService.cs:116），
      而 AwakeModulePaths.ResolveModuleDirectory() 会从汇编目录往上找 SubModule.xml
      ⇒ 离线可能落到仓库内的真实目录、**污染工作区**。这两点你得先解决再写判据。
  (l) **再改实现**。已知的错误修法（审计已指出，我确认它是对的）：
      「SetAsync 里加 if (!rootStored) return」——这会把"已写但报错"的合法结果误报为失败。
      目标契约是**三态**：definitely_not_written / written / unknown；
      只有"原子替换完成"之后才发布新缓存；unknown 时用**绕过实例缓存的全新磁盘读回**确认。
      只回滚缓存不够（替换可能真成功了）；只加直读磁盘也不够（API 把两种失败压成一个 Failure）。
  (m) **成本要说清**：IKeyValueStore 是框架侧公开契约
      （AWAKE/framework/MarcusAwakeFramework/src/StorageAndRagApi.cs:48-53），
      三个方法全返回 OperationResult<bool>。要区分"必未写"与"结果未知"，
      要么扩展契约、要么约定错误码语义。**这是框架契约变更，影响所有实现方**，
      不是 AWAKE/src 里的一次局部修补。先把方案与影响面写出来，别直接动手。

【已定的口径（不必再问，照此执行）】
1. journal 损坏时：**优先可写性**（隔离坏 root ⇒ 按空账本继续 ⇒ 新 root 覆盖）。
   出处：WorldStateStore.cs:3673-3675 注释「2026-09-15 定案」＋ 提交 437bedd。
   ——但"隔离件只存一份、连续两次坏根会覆盖第一现场"是**取舍**，不是 bug；要不要改留给人裁。
2. 是否允许"已提交但返回失败"：**允许，但必须暴露**。依据项目口径「静默成功比抛错危险」。
3. 信件：**账本为权威**（界面读账本），正文是内容载体。半提交时正文孤儿无害，但应可清理；
   **不得因正文存在就报发送成功**。
4. campaign 未绑定时：**拒绝写入并报可重试**（这是行为变更，现行是照写进 unbound 目录）。
5. smoke **只许依赖 Git tree**；`AWAKE/release/` 是被有意排除的，不许把产物塞进仓库。

【约束（红线）】
- **不许启动游戏**。要真机结论的，把"进游戏看什么"写成一句话交回主控。
- 只改主干线自己的文件。**src/WorldKnowledge*.cs 属世界书线**，且**正在被他线改**，一律不碰。
- `AWAKE/src/` 在 AWAKE.csproj 里是**通配编译**（整个 src 是单一编译单元）；
  而 AWAKE.Tests.csproj 是**显式清单**——往 src/ 新增任何源文件，必须同步加进测试工程的清单，
  否则只在第二步炸（该项目内已犯三次，见 csproj:95-96、140-144、146-148、155-158 四处注释）。
- 提交纪律：**绝不 `git add -A`／不许用宽目录 pathspec**（`tools/`、`docs/` 是多线共父，
  会连带提交他线在途改动）；用精确路径。**仓库零自定义钩子，工具层一道关都没有，全靠人守。**
  提交后用 `git log -1 -- <你的路径>` 复核确实进了历史（"提交成功的输出"不等于进了历史）。
- 构建走 `AWAKE/tools/build.ps1 -Configuration Debug`（必须显式给 Configuration）；
  MSBuild/dotnet 直调会被本机安全策略拦。
- 🚨 `assertions[].text` 不进上线包、不参与三哈希 ⇒ 只改断言正文不必重发（与本任务相关时记得）。
- **变异检验**：任何新判据，都要证明它**仍能 FAIL**（注释掉被测那一行／传不存在的参数），
  把方法与结果写出来。全绿不算证据。

【验收（硬条件）】
- P1-02／P1-05：每条判据**先给红的读数**（缺陷存在时 FAIL），再给修后绿的读数。
  只交绿的不算完成。
- P1-03：交**数字**（Set/Get 次数、字节增长曲线），不交形容。
- P1-06：二选一——要么离线判据，要么一句"必须真机＋进游戏看什么"。
  **不许用替身代理真机行为。**
- P1-01：先给红的判据读数，再给三态方案与影响面。
- 全程附 file:line。凡"没有／未见"这类否定断言，**必须当场 grep 并限定口径**
  （例：写"在现行链路上未见 X"，不写"没有 X"）。
- 顺带交一条：**你自己在做的过程中发现的、上面没提到的问题**。这一节必须非空。

【出处】
- AWAKE/docs/AUDIT-SYSTEM-CODEX-20260922.md（首轮，含 P1-01～P1-06 原始描述）
- AWAKE/docs/AUDIT-SYSTEM-CODEX-20260922-R2.md（第二轮，降级表与 Q9 修法）
- AWAKE/tools/_verify_p1_01_20260922.ps1（已跑过的真复现探针，带阳性对照）
- AWAKE/tools/_verify_p1_01_20260922.txt（其读数）
- AWAKE/docs/WORLDBOOK-UPDATE-SAFETY-20260920.md（另一条线的同类问题，可参考"先跑探针再改"的写法）
```

---

## 三 交接单 B —— 世界书线（以下整节可直接粘贴）

```text
【背景】
项目：AWAKE（骑砍2 AI 运行时模组），工作区 D:\AWAKE-Dev。
你负责世界书线（检索／条目／覆盖）。这是一份短任务：**一条**未证风险的裁决与判据。

【事实：现状机制】
WorldKnowledgeQueryService.cs  TryImportOverlay 对 live snapshot **逐条**调用 TryApplyOverlay。
⇒ 前一条合法、后一条非法时，前面的已经生效、后面的失败 ⇒ **半提交**。
外部审计（CODEX）两轮跑完，把这条从"真缺陷"降级为"未证·存疑"，
理由写的是：现有 smoke 没有构造"前一条合法、后一条非法"的可运行 fixture，
**且该文件为他线在途**——这一半，指的是你自己。

【先说约束，再说任务】
⚠ 这个文件**正在被改**（你手上 +53 行的在途改动，涉及互引边召回）。
本任务**不要覆盖、不要 stash、不要回退别人的在途改动**；在它之上继续。
若你判断必须先收口在途改动才能动手，就**先只交一份收口说明**，别硬做。
⚠ 审计方刻意没有碰这个文件，就是为了不搅乱你的在途改动。同样克制。

【已定的口径（照此执行，不必再问）】
Overlay 导入 = **逐条尽力应用，允许部分成功**（不做全批原子事务）。
理由：玩家覆盖是以"单条"为粒度产生的；且存储层没有事务能力，硬做原子只能靠上一层补，
成本远大于收益。
——但必须补上这一半：**哪一条没进去，要让玩家看得见**（不许静默部分成功；
   项目口径「静默成功比抛错危险」）。

【任务（三步，按序）】
1. **判据先红**。构造"前一条合法、后一条非法"的夹具，证明现在的行为确实会产生半提交。
   材料：WorldStateStore 的 InjectStoreForTesting（WorldStateStore.cs:319，internal）
   ＋ AWAKE.Tests/AwakeTestFakes.cs 的 FakeKeyValueStore（已带 FailSet／FailSetAfter 等开关）。
   WorldKnowledgeQueryService.cs 已在 AWAKE.Tests.csproj 的显式清单里（第 151 行）⇒ 编得出来。
   给红的读数。**若夹具写完发现它是绿的（即其实不会半提交），那也是有效产出，如实交。**
2. **按上面口径改**：逐条尽力 + 失败清单可观察。改完给绿的读数。
3. **变异检验**：注释掉新加的那行，确认判据重回红。方法与结果写出来。

【你自己必须回答的一条（不许空着）】
第 1 步里你实际会撞到的第一堵墙是什么？（例如：夹具需要的某个注入点不存在／
某个方法签名在打桩时暴露了它其实是替身友好的／在途改动让某段逻辑不可达……）
**这一节必须有内容，且不许只是把上面任务复述一遍。**

【验收】
- 判据的红读数 ＋ 修后绿读数 ＋ 变异检验，三样齐。
- 全程附 file:line。凡"未见／没有"类否定断言，**当场 grep 并限定口径**
  （写"在现行链路上未见 X"，不写"没有 X"）。注意 src/ 里躺着两代链路，
  否定断言必须限定"在现行链路上"。
- 只改你自己的文件。提交用精确 pathspec，**绝不 `git add -A`、绝不用宽目录**
  （`src/` 是多线共父，会连带提交他线在途改动）。
- 提交后 `git log -1 -- <路径>` 复核确实进了历史。

【出处】
- AWAKE/docs/AUDIT-SYSTEM-CODEX-20260922-R2.md（降级表 P1-04 行）
- AWAKE/docs/WORLDBOOK-UPDATE-SAFETY-20260920.md（本线同类问题的前一份记录）
```

---

## 四 出处与复核记录（本件的每个断言怎么来的）

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
