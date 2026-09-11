# Worldbook Studio 1B/1C 用户级安全修复检查点

- `task_id`: `WORLDBOOK-STUDIO-USER-BUGFIX-1B1C-20260826`
- `batch_id`: `WB-STUDIO-1B1C-ADVANCED-DRAFT-20260826-01`
- `status`: `offline_verified`
- `plan`: `docs/PLAN-WorldbookStudio-UserBugFix-1B1C-20260826.md`
- `review_status`: `APPROVED`
- `execution_lease`: `released`
- `lease_scope`: 高级模式安全保存、危险操作保存门禁、本地参考资料草稿恢复、发布包静态资源门禁；不含 Bannerlord、游戏目录、AWAKE 冻结候选、批量合同、Provider 协议或 Schema 迁移

## Implementation

- 高级保存现在执行写前 YAML/JSON、Schema 和分类校验，并强制检查 `sourceHash` 与 `revision`；写入采用 CAS 与原子替换，写后重新读取并核对字节、hash、revision 和作者投影。
- 保存失败映射为可恢复的 `400/422/409/503` 状态；写入后无法确认时返回 `resultUnknown=true`，前端不自动重试或覆盖当前编辑内容。
- 参考资料向导使用工作区/档案隔离的本地草稿，不保存 API Key、CSRF 或 Provider token；请求代次隔离迟到响应，关闭/重置才中止请求，建档后刷新列表会再次检查草稿代次。
- 新增真实 VM 竞态测试，确认请求未返回时用户修改草稿不会被 abort、不会自动切档，且刷新期间发生修改也不会打开错误档案。
- 发布检查要求源码和发布目录的 `studio-draft.js` 哈希一致；打包后再次打开 ZIP，要求该条目唯一且哈希匹配。所有 PowerShell 工具脚本使用 UTF-8 BOM，JSON 读取显式指定 UTF-8。

## Verification

- 前端编辑会话：`12/12 PASS`；前端安全：`4/4 PASS`；草稿竞态 VM：`2/2 PASS`。
- Core：`101/101 PASS`；Batch：`13/13 PASS`；Draft：`10/10 PASS`；Draft/Authoring Save/Batch HTTP Smoke：全部 PASS。
- Release build：`0 warnings / 0 errors`；Launcher tests：`14 PASS`；六个 Launcher Smoke 场景全部 PASS。
- Windows PowerShell 5.1：12 个脚本解析检查 PASS，`release-check.ps1` PASS，`package.ps1 -RunSmoke` 完整 PASS。
- 发布包：`tools/worldbook-studio/artifacts/WorldbookStudio-win-x64.zip`；ZIP SHA-256 `bd5177db9d2fc48abd15ffc4b887a37ae3ce6eaace0025338f82b5313abac601`；manifest/SHA256SUMS `637/637`。
- 源码与发布目录 `studio-draft.js` SHA-256 均为 `3243a14f1f9dc2765ad27d530ae13b98014e4d00615456238e36641a7f78a591`；故意篡改发布副本后 `release-check` 正确拒绝 `WB-RELEASE-035`。
- 本轮未启动 Bannerlord，未同步游戏目录，未结束用户进程，未修改冻结 AWAKE 候选；最高证据等级为 `E2`。

## Code debt audit

- `scope`: 本批变更文件及一层调用/绑定关系；生成的 `bin`、`obj`、`artifacts` 和历史包排除。
- `denominator`: 审查范围非空、非注释物理行约 `4,678` 行；不把单行模板字符串误计为可安全拆分的业务重复。
- `confirmed_removable_lines`: `0`；未发现本批新增孤儿入口、未接线服务或可安全删除的业务代码。
- `duplicate_logic`: `0` 个确认重复权威；源码→目录→ZIP 的三段校验分别承担不同边界，保留。
- `suspected_findings`: `1`；`studio-draft.js` 仍有较长内联模板/紧凑函数，维护性一般，但不是本批新增行为风险，不在本批重构。
- `efficiency_risks`: `0` 个已证明热路径风险；草稿写入为 450ms 防抖的本地存储，发布哈希只在构建/验证阶段运行。
- `confidence`: 高（Core/API、竞态接线、发布包）；中（未进行真实浏览器 DOM 与外部 Provider/Worker 验证）。

## Known limitations and next action

- 未做真实浏览器 DOM 自动化、真实云端 Provider/本机 Worker 调用、Bannerlord 实机、游戏目录同步、存档兼容或长时运行验证。
- 下一步将 ZIP 和 `新手指引_世界书内容编辑者.md` 交给世界书作者离线试用，重点收集高级模式保存冲突、参考资料草稿恢复、快速切档和关闭窗口时的体验反馈；反馈前不启动游戏、不修改冻结候选。
