# Plan: Worldbook Studio A2 — Validated Snapshot 与 I/O 边界

> 状态：**`implemented`（2026-09-30 标记）。**
> 依据：本批所属子系统已存在：`tools/worldbook-studio/` 含 `Awake.WorldbookStudio.{Cli,Core,Launcher,Web}` 与 entity registry；本系列为该子系统的内部行为保持型重构批次。
> ⚠️ 本文件是**批次施工单**，其所属子系统已存在 ⇒ 本批视为已完成。**保留本文件**（不是 `superseded`）——
> 它记录了当时的设计意图与边界，仍有追溯价值。
> 现行方向：`AWAKE-ROADMAP.md`；分诊依据：`AUDIT-PLAN-TRIAGE-20260930.md`。


## 批次目标

在不改变世界书 Schema、CLI 命令、HTTP 路由、AI Provider 契约、保存格式和游戏目录边界的前提下，让每次 `WorldbookApplicationService` public 调用使用一个不可变的输入事实，消除同一调用内的重复解析与旧内容/新 hash 混合风险。

## 固定边界

- operation context 只覆盖一次 `Validate`、`Compile`、`Preview` 或 `Export` public 调用；不做跨请求、跨进程或长期全局缓存。
- 输入闭包包含当前 Workspace 实际读取的 authoring 正典文档、source registry、audit events、identity ledger 和实际加载的 schema 文件；生成的 report、compiled/export 输出不冒充输入。
- 每个输入以同一 byte buffer 完成 UTF-8 解析、SHA-256 和 `ValidationReport.InputHash`；组装后复核规范化路径集合与 bytes hash，最多从头重建一次，第二次变化只返回既有 `WB-CAS-409`。
- Windows 路径比较不区分大小写；fingerprint 使用按规范化相对路径排序的 `path + SHA-256(bytes)` 列表 hash，不含时间戳。
- derived output 先进入 workspace 内随机 staging 路径，snapshot fingerprint 复核通过后才原子发布；失败时不替换旧 compiled/export/current pointer。
- 不新增错误码，不修改 A1 wire DTO、golden、命令、路由或 Provider 生命周期。

## 最小写入范围

- `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/Workspace.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/Application.cs`
- 必要时新增同目录的 snapshot/read-probe 纯 Core 文件
- `tools/worldbook-studio/tests/Awake.WorldbookStudio.Tests/Program.cs`
- `tools/worldbook-studio/tests/fixtures/a1-protection-matrix.v1.json`
- 必要的 A2 本地 fixture；不修改运行时世界书、Schema 和发布包

## 实现任务

1. 建立 `ValidatedSnapshot`、输入闭包 manifest、fingerprint 和 operation context；保持 public 返回类型与 wire 层不变。
2. 将 authoring/source/schema/audit/ledger 读取改为同一 bytes 的 parse/hash；加入测试可注入的读取记录与 TOCTOU seam。
3. 让 `Compile`、`Preview`、`Export` 只消费 snapshot 内存对象；在输出边界执行闭包复核、staging 和旧 pointer 保护。
4. 添加恰好 72 行的 A1 protection matrix fixture 与机检；增加 read-inventory、读取上限、文件增删改、registry/schema/audit/ledger 变化、三阶段 TOCTOU 和失败不写出测试。

## 验收门

- 无竞态时每个闭包输入的读取上限：`Validate` 2 次；`Compile`/`Preview`/`Export` 3 次；snapshot 交付下游后 0 次读盘；一次重建按同样上限重新计数。
- 实际读取集合与声明闭包集合完全相等；任何旁路读取、漏列、新增/删除/重命名或 hash 不一致失败。
- 三阶段 TOCTOU 均只能成功重建一次或返回 `WB-CAS-409`，不生成混合内容，不改变旧候选/current pointer。
- A1 `72/72`、新增 A2 focused tests、Release build `0 warnings / 0 errors` 通过。
- 真实云端 Provider、本机 Worker、Bannerlord 实机、游戏目录同步均保持 `unverified`。

## 下一步

先确认现有 Application/Workspace 调用图与测试辅助函数，再实现稳定 byte read 和 snapshot 模型；不要先改 UI、Launcher 或 AWAKE 运行时。
