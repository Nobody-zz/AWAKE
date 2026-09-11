# Worldbook Studio 首批完整功能实现记录

- 日期：2026-08-22
- 计划：`PLAN-AWAKE-WorldbookStudio-FULL-20260822.md`
- 审查：`PLAN-AWAKE-WorldbookStudio-FULL-REVIEW-LOG-20260822.md`，最终 `VERDICT: APPROVED`
- 范围：独立开发工具；不启动 Bannerlord，不调用同步脚本，不写游戏目录、AWAKE v1 `PlayerExports`、`ModuleData`、dist 或冻结候选。

## 已实现

- `WorkspaceRootGuard`：拒绝游戏目录、AWAKE v1 运行时树、`ModuleData`、`dist`、`_build_out`、冻结/待验候选及其祖先/子孙路径。
- 四类写入边界：`RequireAuthoring`、`RequireFixtures`、`RequireCompiled`、`RequireExport`；导出固定在 `export/WorldbookV2`。
- `WorldbookApplicationService` 唯一调用链：初始化、保存、校验、编译、预览、导出；CLI/Web 不再旁路写文件或直接创建发布器。
- 固定验证顺序：Schema → Source → Audit → Ledger/Migration → Registry → Authority/Canon → Timeline → Content Graph/Tier → Permission。
- 来源登记：source version、内容 hash、quote hash、locator、授权/使用状态和 registry hash。
- 审计与 ID ledger：事件 hash、对象 hash、链序列、previous hash、批准事件、record hash 和基础 redirect 循环检查。
- Profile/referral registry：继承链、未知 profile、继承环、权限引用与 fallback referral 校验。
- 权限预览：deny 优先、继承 profile、scope/年龄/steward/文化/国家/聚落条件；NPC DTO 与作者诊断 DTO 分离。
- Content Graph：document/assertion/expression/source/referral/redirect/registry/index 节点与关系，base 遇 adult/unknown 分层阻断。
- Confirmation token：绑定输入 hash、来源 registry、profile/referral registry、tier 和报告 hash；来源变化后失效。
- 原子候选发布：旧候选不删除、重复候选使用 suffix、current pointer 过 schema、保留 previous manifest hash、临时目录和故障注入点。
- Web API：health、workspace、documents、document、editor-catalog、validate、compile、save-authoring、document/new、export、confirmation-token、preview。
- Web 编写器：三栏档案工作台、中文身份/领域/状态标签、档案筛选、新建模板、原始 YAML/JSON 编辑、脏状态、保存后校验、错误路径诊断、NPC 身份预览、编译与候选导出结果面板。
- CLI：`init`、`doctor`、`validate`、`compile`、`preview`、`export`，支持工作区、输出路径、profile、fixture、content tier 和成人确认 token。

## 验证

- Release build：0 warnings / 0 errors。
- F01–F33 fixture harness：33/33 PASS。
- CLI：init、validate、compile、preview、base export PASS；工作区外 `--out` 返回路径错误码 3。
- Web loopback：health、validate、preview、compile PASS；服务仅绑定 `127.0.0.1`。
- package：PASS；release-check：PASS。
- 清理了旧 Web smoke 留下的 `src/Awake.WorldbookStudio.Web/workspace` 临时目录。
- 浏览器 loopback 实机闭环：新建档案 → 打开 → 修改 → 保存并校验 → 普通平民身份预览 → 编译 → 导出候选包；页面运行时错误 `0`。

## 已知边界

- 这是 Studio v2 独立候选工具，不会被当前 AWAKE v1 运行时自动消费。
- 未实现 NPC 游戏内动态学习、周报传播、AI Provider 和 KnowledgePatch；这些属于后续运行时/内容批次。
- 游戏内 E4/E5 尚未进行，也不应由本工具离线验证结果替代。
- 本批未启动 Bannerlord，未写入游戏目录、AWAKE v1 `PlayerExports`、dist 或冻结候选；浏览器测试工作区位于系统临时目录并已清理。
