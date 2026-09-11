# AWAKE Worldbook Studio MVP 状态

更新时间：2026-08-21

## 已完成

- 独立 `.NET 10` Core、CLI、Web、Tests 工程：`tools/worldbook-studio`
- 本地包源、锁定 SDK/依赖、离线 locked restore
- 安全 YAML 读取、JSON Schema 校验、规范化 JSON 与 SHA-256
- 工作区初始化、`authoring/compiled/export/fixtures` 边界和 reparse path guard
- `init`、`validate`、`compile`、`preview`、`export`、`doctor` CLI
- loopback-only 中文 Web：健康、工作区、校验、编译、预览
- v2 候选发布：独占发布锁、临时目录、flush、`complete.marker`、`current.json`、清单
- 本地 fixture smoke、CLI smoke、Web HTTP smoke、成人层拒绝、`--out` 越界拒绝
- `build.ps1`、`test.ps1`、`package.ps1`、`release-check.ps1`、`restore-offline.ps1`

## 当前边界

- 该 MVP 只生成独立 `export/WorldbookV2` 候选，不会被当前 AWAKE v1 读取器自动发现。
- NPC 动态学习、周报传播、AI Provider、完整审计事件/ID ledger/tier 图闭包 UI 尚未接入运行时。
- 未启动 Bannerlord，未调用同步脚本，未修改游戏目录和冻结 AWAKE 候选。

## 验证

- Release build：0 warnings / 0 errors
- Core fixture smoke：PASS
- CLI init/validate/compile/preview/export：PASS
- Web loopback HTTP smoke：PASS
- `adult_optional` 未确认：退出码 3，PASS
- `--out` 工作区外路径：退出码 3，PASS
- 发布检查：PASS
