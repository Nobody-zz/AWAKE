# Worldbook Studio A5-UI Checkpoint

- `batch_id`: `worldbook-studio-a5-ui-20260823`
- `status`: `implemented_and_locally_verified`
- `scope`: 面向零基础内容编辑者的中文作者模式 UI 精进；不改变世界书 schema、HTTP 路由、AI 请求/结果契约或游戏运行时

## 本批次内容

- 新建档案只要求标题、主要分类和内容层；路径、内部编号、版本和注册表信息继续由程序维护。
- 作者表单继续使用中文领域、时期、确定程度、事实、NPC 表达和知识权限卡片，并保留高级技术模式。
- 诊断中的“立即前往”会切换步骤、滚动到对应卡片并聚焦第一个可编辑控件。
- 工作区为空或搜索无结果时，提供创建档案、清除搜索和新建档案按钮。
- 默认界面不显示档案内部 ID、revision 或 AI 作用域的技术身份字段；AI 建议的 patch 路径改为“档案标题 / 客观事实 / NPC 表达 / 知识权限”等中文说明。
- 未知 AI/档案错误不再直接把原始错误文本弹给内容编辑者；Provider 状态加载增加并发请求合并。
- 新手指引补充了“立即前往”、技术信息折叠和空工作区操作说明。

## 变更文件

- `tools/worldbook-studio/src/Awake.WorldbookStudio.Web/wwwroot/index.html`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Web/wwwroot/studio-ai.js`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Web/wwwroot/studio-authoring-ux.js`
- `tools/worldbook-studio/tests/Awake.WorldbookStudio.Tests/Program.cs`
- `tools/worldbook-studio/新手指引_世界书内容编辑者.md`

## 验证证据

- `tools/worldbook-studio/scripts/test.ps1`：Release 构建成功，`PASS: Worldbook Studio harness (99/99)`。
- Visual Studio bundled Node：`studio-ai.js` 和 `studio-authoring-ux.js` `--check` 通过。
- A5-UI DOM 契约断言并入既有 F32，未改变 A3.1 固定用例数量保护。
- 代码债务审查：`docs/evidence/worldbook-studio-a5-ui-debt-20260823.md`，确认性死代码为 0。

## 未验证与边界

- 没有浏览器 DOM 实机渲染证据；当前环境没有可用的 Playwright/Puppeteer 模块。
- 没有连接真实云端 Provider、真实本机 Worker 或进行人工对话验收。
- 没有启动 Bannerlord，不修改 `Modules\AWAKE`、`PlayerExports`、dist 或冻结候选。
- 既有 `artifacts\WorldbookStudio` 验证包没有被覆盖；本批次尚未执行 A6 发布打包。

## 下一批建议

先进入独立的 A5-AI/CAS 验收，固定 AI 建议缓冲区、人工确认、拒绝/应用、source hash、revision、session、consent、nonce 和 CAS 的可观察边界；通过后再执行 A6 候选打包和 Launcher/发布证据闭环。
