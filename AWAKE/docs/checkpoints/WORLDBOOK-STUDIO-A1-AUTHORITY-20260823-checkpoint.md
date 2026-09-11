# Worldbook Studio A1 Authority Checkpoint

- `task_id`: `WORLDBOOK-STUDIO-A1-AUTHORITY-20260823`
- `batch_id`: `worldbook-studio-a1-authority-20260823-01`
- `status`: `offline_verified`
- `scope`: Worldbook Studio 单一权威整理；合并 JSON canonicalization 委托与已证明等价的 Provider/Revision 纯逻辑；保留 CLI/Web wire DTO；不改变世界书 Schema、HTTP 路由、CLI 命令、AI 契约、游戏目录或冻结候选。
- `files_changed`: `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/InputNormalization.cs`、`tools/worldbook-studio/src/Awake.WorldbookStudio.Core/ContractHashing.cs`、`tools/worldbook-studio/src/Awake.WorldbookStudio.Cli/Program.cs`、`tools/worldbook-studio/src/Awake.WorldbookStudio.Web/Program.cs`、`tools/worldbook-studio/tests/Awake.WorldbookStudio.Tests/Program.cs`、A1 golden fixtures、`tools/worldbook-studio/scripts/a1-authority-smoke.ps1`。
- `implementation`: `ContractHashing.ContentHash` 现在委托唯一的 `CanonicalJson.Canonicalize`；CLI/Web 共用 `WorldbookInputNormalization.NormalizeProvider` 与 `ReadRevision`；CLI snake_case 与 Web camelCase Suggestion DTO 仍保持独立；数值 canonicalization 的既有行为由版本化 golden fixture 冻结。
- `verification`: `scripts/test.ps1` Release build `0 warnings / 0 errors`；Studio harness `72/72 PASS`；真实 CLI/Web authority smoke `PASS`；`scripts/package.ps1 -RunSmoke` 的 package/release-check/Launcher success smoke `PASS`；`scripts/smoke.ps1 -Browser failure` fallback `PASS`。
- `debt_audit`: `tools/code-debt-audit/reports/worldbook-studio-a1-authority-20260823.json`；status `passed`；denominator `1937` logical lines；confirmed `0`；suspected `8`；static risk `0`；unknown `0`；scope_limited `false`。两个 4-line duplicate-suspected 结果分别是有意保留的 CLI/Web wire adapter 与测试 helper，不作删除。
- `package`: `tools/worldbook-studio/artifacts/WorldbookStudio-win-x64.zip`；SHA-256 `eab759bee9a7a969e9ea457385bc229297b793579338b7bb136ef8b82fc8b80e`。
- `known_limitations`: A1 使用隔离假 Worker 完成协议 smoke，未调用真实云端 Provider 或真实本机 Worker；未启动 Bannerlord；未写入真实 API Key；未修改 `Modules\\AWAKE`、`PlayerExports`、dist 或冻结运行候选。
- `next_action`: 下一批精进必须另立范围并完成审查；如需人工确认，可解压并启动上述 Studio 包，但不要同步游戏目录。
