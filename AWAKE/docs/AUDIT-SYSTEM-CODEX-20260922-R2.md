# AWAKE 全体系审计 · 第二轮补验

## 读数边界

- 时间：2026-09-22，Asia/Shanghai；共享树 HEAD 为 `bd2a69b`，存在其他线路的未提交修改。本报告没有修改生产、测试、GUI 或内容包。
- 三项 mutation 仅在 `C:\Users\26811\AppData\Local\Temp\awake-system-audit-r2` 的 detached worktree 中执行。每项均先构建成功、再运行 `Awake.SdkSmoke.exe --stop-on-first-failure`，随后逐字恢复；最终 `git diff --check` 和 `git status --short` 均为空。
- 该 clean worktree 的基线能构建，但完整 64 例在第 64 项 `dialogue-chain-redtest` 因 Git 中不存在 `AWAKE/release/awake-worldbook-pilot` 而停下。这是测试依赖了未纳入版本控制的发布产物；不是游戏运行结论，也不抵消前三项 mutation 的定点读数。

## 一、Q3：三项红测

| 组别 | 隔离改动 | 结果 | 证明范围 |
| --- | --- | --- | --- |
| build identity | `AwakeBuildIdentity.ComputeSha256Short` 改为恒空 | 构建成功；#1 `build-identity` 失败，固定 SHA fixture 不匹配 | 构建身份断言能红 |
| messenger request | 空正文不再返回 `text_missing` | 构建成功；#42 `messenger-history` 失败，拒绝原因不匹配 | 信使请求验证能红 |
| world fact journal | 空 journal root 从 Missing 改为 Corrupt | 构建成功；#56 `world-fact-journal` 失败，期望 Missing | 世界事实编码/读取契约能红 |

这三项只证明“选中的断言并非恒绿”，不证明套件会自动发现任何断言退化。当前没有 mutation score、断言存活率或 CI 自检；仍需人工或专门 mutation 工具定期做这种检查。

## 二、T2：首轮六项的证据重新分档

| 项 | 档 | 本轮读数与阳性对照 | 更新后的结论 |
| --- | --- | --- | --- |
| P1-01 写盘失败回显 | A | `tools/_verify_p1_01_20260922.ps1`：失败路径 `WRITE_SUCCESS=False`、同实例读 `[not-on-disk]`、重开读空；可写路径 `CONTROL_WRITE_SUCCESS=True`、重开读 `[on-disk]` | **已实证的真实适配器缺陷**。上层同实例读回确认会被污染缓存骗过。 |
| P1-02 坏分片恢复 | C | 现有 #56/#58 只覆盖空 root、坏 root 和单次恢复；没有多分片、坏一个 chunk、二次隔离的夹具 | **未证·存疑**。静态链路表明查询视图会在 Corrupt 后重建为仅含新事实；不能称“物理历史被清空”。 |
| P1-03 revision 写放大 | C | 静态可见每次 `BuildJournalChunks` 对全体 facts 重建，key 含 revision；本轮未做多轮真存储字节计量 | **未证·设计风险**，不是已测性能缺陷。 |
| P1-04 Overlay 部分导入 | C | `TryImportOverlay` 对 live snapshot 逐条调用 `TryApplyOverlay`；现有 smoke 没有构造“前一条合法、后一条非法”的可运行 fixture，且该文件为他线在途 | **未证·存疑**。语义取决于导入是否必须原子。 |
| P1-05 信件半提交 | C | 生产 `AwakeLetterService` 不在 `AWAKE.Tests.csproj`；64 例的 messenger-history 只测输入/账本模型，不能注入第二次真实写失败 | **未证·跨记录一致性风险**。需要可注入 transcript/ledger 的故障夹具或真机存档实验。 |
| P1-06 unbound campaign 路径 | C | `ResolveCampaignId()` 明确返回 `unbound`，但离线 stub 不能构造两次真实 Campaign 绑定切换；未获得早期开启后跨 campaign 的阳性/阴性对照 | **未证·生命周期风险**，不能归因 09-14 现场故障。 |

结论：首轮六项中，目前只有 P1-01 可保留“真缺陷”措辞；P1-02 至 P1-06 全部降为未证风险，等待对应夹具或真机证据。

## 三、Q9：P1-01 应怎样修

选择：以**三态提交结果**作为目标契约，并在文件适配器内部以“复制—持久化—发布缓存”实现它；当结果未知时，用绕过实例缓存的全新磁盘读回确认。

- `definitely_not_written`：如目录创建/临时文件写入失败，缓存保持旧快照。
- `written`：原子替换完成后才发布新缓存。
- `unknown`：替换阶段抛错或进程中断边界，不能用现有 `values` 判断；由 fresh reader 读取并比对 intent/hash，仍无法确认则上层返回 `CommitUnknown`。

只做缓存回滚不够：若替换实际成功、随后抛异常，盲目回滚会把已经落盘的写说成失败。只加“直读磁盘”也不够：现有 API 仍把所有失败压成一个 `Failure<bool>`，调用方无法区分必未写与结果未知，且读写锁/原子替换语义仍未定义。因而不要把 `if (!rootStored) return` 当修复；这会把“已写但报错”的合法结果误报为失败。

## 四、T3/Q10：框架服务调用口径

审计范围为 `AWAKE/src/**/*.cs`、`AWAKE.Tests/**/*.cs`、`AWAKE/ModuleData/**/*.xml`、`AWAKE/GUI/**/*.xml`；不把 `framework/` 自身实现、历史/废弃目录或外部模组算入“当前 AWAKE 调用者”。文本与反射入口扫描未发现以 host 服务名做 `GetProperty`/`GetMethod` 的注册；XML 只出现 UI 文本、绑定属性和事件名，未发现 host-service binding。

以成员访问及接口声明交叉筛查，五个候选是 `Capabilities`、`Tools`、`Media`、`Assets`、`Log`。这次**不能**把它们升级写成“符号级零调用”：临时 Roslyn SemanticModel 探针受当前编译图的外部 TaleWorlds/框架引用和 PowerShell overload 绑定影响，未产出可信表，故 Q4 只完成了入口/反射/XML 复核。

因此 Q10 答案是：**已证明“真的没人用”的数量为 0；在上述有限现行源码范围内“未见直接调用”的候选为 5。** 要把下限从 0 提高，需要在与项目引用一致的 MSBuild/Roslyn workspace 中输出 symbol id、调用位置和动态注册清单，再对反射/DI 边界人工复核。

## 五、Q11：本轮判断依赖而尚未冻结的产品语义

1. journal 损坏时，优先恢复可写性还是保持历史可查询；隔离副本的保留期限和索引语义是什么。
2. Overlay 导入是全批原子事务，还是允许逐条尽力应用并报告部分成功。
3. 信件正文、投递账本、未读统计谁是权威；半提交后重试应补写、去重还是隐藏正文。
4. campaign 尚未绑定时，存储应拒绝、暂存内存，还是使用临时 scope；后续如何迁移。
5. journal 的保留/压缩/回收策略及容量目标；“旧 revision 存在”是否有意作为恢复历史。
6. 存储写失败的语义是否允许“已实际提交但调用返回失败”；若允许，怎样暴露 unknown。
7. 未跟踪发布产物是否属于 smoke 的必要输入，还是 smoke 必须只依赖 Git tree。

没有先冻结这些语义，P1-02 至 P1-06 只能是风险陈述，不是缺陷裁决。

## 六、Q12/Q13：首轮报告的可信度与自我更正

首轮未运行即写下的判断包括：P1-02、P1-03、P1-04、P1-05、P1-06、P2-01；四条玩法链的 UI/实机缺口；Overlay schema/activation/ID 兼容、PATH 可移植性、发布准备度和各项评分。它们应读作静态观察或评估，不是运行事实。

首轮本身至少有四处需要更正：

1. §六表格中零命中实际是 **5** 个，不是正文写的 6 个。
2. §八所谓“独立探索”的六项与 §三问题清单六项完全重合；它证明使用了另一条取证路，不证明发现集合独立。
3. P1-02 的“整账本逻辑清空”过度：更准确是“当前查询视图可能只剩恢复后新事实”，物理 chunk 是否仍在未测。
4. 将 64/64 全绿写入首轮读数并不等于 clean HEAD 可复跑；本轮 clean worktree 暴露第 64 项依赖未跟踪 `release/awake-worldbook-pilot`。

## 七、Q14：发布门槛

- 自有 LICENSE：在仓库受检 `AWAKE` tree 中未找到根级或产品级 `LICENSE`/`COPYING`；`THIRD-PARTY-NOTICES.txt` 存在，但它不是自有许可证。
- 实际发布包：Git tree 中存在世界书源包 `ModuleData/Worldbook/packages/calradia` 的 manifest、index、runtime 入口和 hash；未找到可审计的、已生成客户发布 ZIP/目录。因此不能对“实际发布包是否含分级内容”作结论。只根据 manifest 的 `kind=universe`、schema 和文件清单不能推断分级；也没有复述或扫描正文来代替分级审核。

## 最终判断

第二轮完成了三项隔离 mutation 和 P1-01 的真实复现回灌；首轮其余五项已诚实降级。调用图的 Roslyn 符号级部分与 P1-02/04/05/06 的可执行夹具仍未闭合，故本轮状态为 **REVISE（证据收口未完成，不是要求立即改生产代码）**。
