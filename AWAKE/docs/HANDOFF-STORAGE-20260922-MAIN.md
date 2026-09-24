# 派工单 · 主干线 · 存储与一致性五条（AWAKE · 2026-09-22）

> **用法**：整份文件即提示词。全选复制、整段粘贴给主干线会话即可，无需删改。
> 上游分析与逐条复核见 `AWAKE/docs/HANDOFF-STORAGE-CONSISTENCY-20260922.md`（不必读，本件已自包含）。

【背景】
项目：AWAKE（骑砍2 AI 运行时模组），工作区 D:\AWAKE-Dev，游戏 API v1.3.15。
你负责主干线（运行时／存储／信件／事件）。这是一份**证伪任务**，不是修 bug 任务。

起因：外部审计（CODEX）两轮跑完，把六项"疑似缺陷"中的五项降级为"未证风险"。
降级是诚实的，但降级不等于问题不存在——变的是证据强度，不是风险。
本任务＝把这四项（P1-02／03／05／06）的判据补上，以及 P1-01 的修法落地。

【第一原则：先写一条能红的判据，再谈修】
现在离线 64 例全绿。这五条没有一条被判据覆盖 ⇒ 现在修等于"修完还是全绿"，
无法证明修好了。**顺序不许反：先让判据红，再改实现让它绿。**

注意：**材料早就齐了，缺的是人写。**
AwakeTestFakes.cs:16-19 的 FakeKeyValueStore 已带四个故障注入开关：
    FailSet / FailSetAfter（第 N 次起写失败）/ FailGetWithKeyNotFound / CommitUnknownAfterWrite
实测（grep 全 AWAKE.Tests）：这四个开关**只出现在定义处，零使用处**。
其中 FailSetAfter 正好是下面 P1-05 要的场景，CommitUnknownAfterWrite 正好是 P1-01 三态里的 unknown。

【建议顺序（时间有限就按这个做，做完一条交一条，不必等全做完）】
1. P1-05 信件半提交 —— 只差一个"把编排从 UI 依赖里摘出来"的动作，材料最齐。
2. P1-02 坏分片恢复 —— 全是读代码＋夹具，纯离线。
3. P1-01 写盘假成功 —— 已实证，但要先解决夹具卡点（私有嵌套类 + 模块路径污染）。
4. P1-03 revision 写放大 —— 只量数字，不写判据、不改实现。
5. P1-06 unbound campaign —— 先判"能不能离线证"，很可能只能交给主控去真机。
（P1-06 放最后不是因为它不重要，是因为它最可能以"一句结论"收尾，不占工期。）

【事实：五项各自的现状机制（行号对应 HEAD；动手前自行复核，别照抄）】

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

■ P1-01 写盘假成功 —— 已实证，该修；但修法是框架契约变更，不是局部补丁
机制（主控独立复现过，带阳性对照；比审计报告写的更精确）：
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
  (l) **再改实现**。已知的错误修法（审计已指出，主控确认它是对的）：
      「SetAsync 里加 if (!rootStored) return」——这会把"已写但报错"的合法结果误报为失败。
      目标契约是**三态**：definitely_not_written / written / unknown；
      只有"原子替换完成"之后才发布新缓存；unknown 时用**绕过实例缓存的全新磁盘读回**确认。
      只回滚缓存不够（替换可能真成功了）；只加直读磁盘也不够（API 把两种失败压成一个 Failure）。
  (m) **成本要说清**：IKeyValueStore 是框架侧公开契约
      （AWAKE/framework/MarcusAwakeFramework/src/StorageAndRagApi.cs:48-53），
      三个方法**并非**都返回 `OperationResult<bool>`——实际是
      `GetAsync → OperationResult<string>`、`SetAsync` / `DeleteAsync → OperationResult<bool>`
      ⇒ **要区分"必未写"与"结果未知"，`bool` 这个返回类型本身就承载不了**，
      要么扩展契约、要么约定错误码语义。**这是框架契约变更，影响所有实现方**，
      不是 AWAKE/src 里的一次局部修补。先把方案与影响面写出来，别直接动手。

      > ⚠️ **09-24 更正**：本段原先写作"三个方法全返回 `OperationResult<bool>`"，**是错的**（主控写错）。
      > 以本段现文为准，续单 `HANDOFF-STORAGE-20260924-MAIN-R2.md` 同。

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
注：此项**只交数字，不动实现**。容量目标（一本存档预期玩多久、记多少条）甲方尚未给，
    改不改由主控据此定夺。若你的读数出现**量级上的意外**（例如单条追加即写 MB 级），
    附一句实测量级说明，供主控判断。

■ P1-06 unbound campaign —— 先判"这条到底能不能离线证"
AwakeFileStorageService.cs:99-103  ResolveCampaignId()：Campaign.Current 为 null 或 id 空 ⇒ 返回 "unbound"
AwakeFileStorageService.cs:71       路径 = <module>/PlayerExports/AwakeState/<campaign>/<ns>.json
要回答：
  (i) ResolveCampaignId 读的是**游戏全局状态**（Campaign.Current）⇒ 离线造不出"两次绑定切换"。
      这条**能不能离线证**？能就写判据；不能就**明说必须真机**，
      **不许造一个替身去代理它**（替身与真件不同源 ⇒ 离线全绿、真机必坏，这个坑本项目踩过）。
  (j) 若真机才能证：把"要在游戏里看什么"写成一句话的可观察现象交给主控，
      不要自己去启动游戏（见约束）。

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
- P1-05／P1-02：每条判据**先给红的读数**（缺陷存在时 FAIL），再给修后绿的读数。
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
