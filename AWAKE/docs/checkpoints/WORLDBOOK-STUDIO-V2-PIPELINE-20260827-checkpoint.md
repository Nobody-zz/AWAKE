# Worldbook Studio v2 编译/发布链路检查点

- task_id: WORLDBOOK-STUDIO-V2-PIPELINE-20260827
- batch_id: studio-v2-compile-publish-runtime-offline-20260827
- status: offline_verified
- evidence_level: E2
- execution_lease: released
- scope: 仅修复 Worldbook Studio 作者源文件到 v2 编译包、候选发布，以及 AWAKE 完整性校验、加载器和知识查询的离线消费链路；未触碰 AWAKE 主工程运行时代码、当前 ModuleData、dist、游戏目录、冻结候选、Marcus 线或存档系统。

## Implementation

- 默认 `compile` 输出使用 `compiled` 目录同级事务目录，提交后再替换目标，避免默认目标既是输出目录又被错误当成临时目录父级。
- `export` 在发布前统一补齐 `content_tier` 与成人确认哈希，重新计算 manifest/package 哈希，并将最终同一份清单写入 `manifest.json` 与 `package-manifest.json`。
- 每次 `export` 生成的 v2 内容候选包包含 `runtime.json`、`index.json`、`manifest.json`、`package-manifest.json` 和完整性校验文件；AWAKE 入口按 `manifest.json` 消费。`WorldbookStudio-win-x64.zip` 是 Studio 程序发行包，不是内容候选包。
- 测试项目以隔离方式链接 AWAKE 完整性、加载器、身份评估器和查询服务源码，只用于离线验证，不代表这些文件已经接入 AWAKE 生产工程。

## Files Changed

- `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/Workspace.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/Application.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/Publisher.cs`
- `tools/worldbook-studio/tests/Awake.WorldbookStudio.Tests/Program.cs`
- `tools/worldbook-studio/tests/Awake.WorldbookStudio.Tests/AwakeWorldbookQueryStub.cs`
- `tools/worldbook-studio/tests/Awake.WorldbookStudio.Tests/Awake.WorldbookStudio.Tests.csproj`
- 三个项目的 `packages.lock.json` 由构建过程更新；未修改 AWAKE 运行时工程。

## Verification

- `tools/worldbook-studio/scripts/test.ps1`：Studio harness `108/108`（含 F79–F81）、Editor content core `7/7`、BatchTests `13/13`、Draft tests `10/10`、Release build `0 warnings / 0 errors`、Draft/Authoring/Batch HTTP Smoke 全部通过。
- A4 CLI/Web Smoke：`18/18`，此前证据仍有效。
- `dotnet run --project tools/worldbook-runtime-smoke/WorldbookRuntimeSmoke.csproj --configuration Release`：`PASS: runtime loader/query/overlay/identity/registry/weekly-report/event-ledger smoke`。
- `tools/worldbook-studio/scripts/release-check.ps1 -Package artifacts\\content-candidate-20260827-v2-boundary-repair\\WorldbookStudio`：`PASS`。
- 当前修复包：`tools/worldbook-studio/artifacts/content-candidate-20260827-v2-boundary-repair/WorldbookStudio-win-x64.zip`。
- 当前修复包 ZIP SHA-256：`51C928768D01869FF176D432CF19B22407EB0DD322FDA77F13B2130EBAADD11D`。
- 当前修复包 `manifest.json` 文件 SHA-256：`D3BE69B942AB246CCB6CE147116D9E97A08E27BC80DCD2B6C509F660FE2A3BA0`。
- 当前修复包 `SHA256SUMS.txt` 文件 SHA-256：`6E446B4310661A63DCD258CDDF8D75583FA1F250CAE785BFCE09FFCB5BCF16E8`。
- 当前修复包 `.sha256` 侧车文件与 ZIP 实际哈希一致。
- F76–F78 使用 Studio 实际 `Compile()`/`Publish()` 生成的临时 v2 内容候选，随后由 AWAKE 完整性校验、加载器和查询服务消费；该内容候选不是发行 ZIP 的顶层文件。
- 游戏目录触碰：`false`；Bannerlord 未启动；Studio/Worker Smoke 进程已清理。

## Bounded Code-Debt Audit

- 审查范围：本批变更文件及 `Application.cs`、`RuntimePackageCompiler.cs`、`ContractHashing.cs`、AWAKE loader/query 的一层调用与 JSON 绑定；排除 `bin`、`obj`、生成包、历史归档和整仓库重构。
- 业务实现分母：`Workspace.cs`、`Application.cs`、`Publisher.cs` 合计约 `1,397` 行非空非注释逻辑代码；测试代码单独计量，不混入业务屎山比例。
- confirmed_removable_lines: `0`；没有证据证明本批新增代码存在可以安全删除的孤儿路径。
- confirmed_findings:
- `Application.cs:538-561` 先构造一份旧 `index` 与 `contentHash`，随后由 `RuntimePackageCompiler.Build` 在 `Application.cs:568-570` 覆盖同名 `index.json`；这是重复计算和双重索引权威路径，当前不改变结果，但应在未来拆分编译管线时移除前一份。
- `Workspace.cs:69-73`、`Workspace.cs:178-185` 以及 `Workspace.cs:152-163` 与 `RuntimeContracts.cs:157-232` 存在重复的根目录/reparse 校验层；其中 `WorkspaceService` 和 `WorkspaceWritePolicy` 还会重复执行根校验。它目前是防御性重复，不在本批直接合并，以免改变 `WB-ROOT-*` 兼容错误边界。
- `Publisher.cs` 新增 `CompiledPackageGuard` 统一检查 `Snapshot`、`runtime.json`、`index.json`、manifest/package 清单和内容哈希；F79–F81 已覆盖缺失快照、篡改 runtime 与发布锁释放。
- suspected_findings:
  - `Publisher.cs:81-99` 在候选目录已完成移动后才替换 `current.json`；指针替换失败会留下未被指针引用的完整候选目录，属于可清理的孤儿候选风险，不影响旧指针保持。
  - 测试项目直接链接 AWAKE 源码并用 `AwakeWorldbookQueryStub.cs` 补齐 `WorldbookQuery`，同时引用游戏目录 `Newtonsoft.Json.dll`；这适合当前离线隔离，但存在模型漂移和环境耦合风险，暂列测试隔离改进项。
- efficiency_risks: 未发现已证明的游戏 tick、网络或高频 I/O 热路径风险；上述索引重复和根校验重复均为 compile/初始化冷路径。
- confidence: 高（完整 Studio harness、Smoke、发布检查和离线 AWAKE 消费链路均通过）；中（未做真实浏览器 DOM、真实云端/Worker 或 Bannerlord E4/E5）。

## Known Limitations

- 当前修复包是 Studio/离线链路候选，不是新的 AWAKE 游戏运行时发布版。
- `AWAKE\\ModuleData\\Worldbook\\manifest.json` 仍是旧 `awake.worldbook.v1`；本批没有迁移它，也没有改写当前游戏目录。
- 未启动 Bannerlord，未执行 E3 同步、E4 游戏内入口、E5 存读档回归；不能宣称游戏内世界知识系统已完成。
- 未处理 AI apply 连续 CAS、未保存编辑覆盖、真实 Provider/Worker 部署等其他审查批次问题。

## Next Action

为真实 v2 世界知识内容建立单独、可审查的内容迁移批次：用实际作者源文件生成正式 v2 包，先在离线 AWAKE 读取器上验证，再由用户明确授权后另建运行时同步/游戏验证批次。当前 v2 链路 checkpoint 不再继续修改冻结 AWAKE 候选。

## Last Error

- 本批原始阻塞已修复：默认 `compile` 输出路径事务校验错误、`export` 最终 manifest 哈希失效，以及发布包入口文件名与 AWAKE 读取入口不一致。
- 当前没有新的失败证据。
