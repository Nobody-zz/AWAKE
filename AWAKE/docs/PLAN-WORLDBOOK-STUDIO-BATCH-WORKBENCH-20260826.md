# AWAKE Worldbook Studio 批量创作工作台实施计划

- task_id: `WORLDBOOK-STUDIO-BATCH-WORKBENCH-20260826`
- design: `docs/superpowers/specs/2026-08-26-worldbook-studio-batch-workbench-design.md`
- contract_authority: R14；不修改 `2026-08-25-worldbook-studio-batch-authoring-contracts-r14.json`
- status: completed
- independent_review: VERDICT: APPROVED；R14 revision 14 未修改
- verification: Studio 101/101、Batch 13/13、Draft 9/9、Draft/Batch HTTP Smoke、release-check 均通过
- user_signoff: 用户已确认“可以，做”，本计划只覆盖此前已确认的批量自动化工作台切片

## 目标

把现有 R14 批量 AI 后端接成内容编辑者可用的工作台，完成：

`入口 → 批量导入 → 扫描 → 创建/授权 → 事实生产 → 事实审核 → 元数据生产 → 元数据审核 → needs_review 草稿`。

## 实施任务

### 1. 独立前端资源

- 新增 `wwwroot/studio-batch.css`。
- 新增 `wwwroot/studio-batch.js`。
- 默认三栏：批次/项目、详情/审核、AI/批量操作。
- 实现左右栏拖拽调整和 `localStorage` 宽度记忆。
- 实现小窗口上下布局和单一主滚动区域。

### 2. 主页面最小接线

- 在 `index.html` 增加批量创作入口、工作台容器和资源引用。
- 不把批量状态继续堆进主编辑器的现有内联脚本。
- 关闭/返回批量工作台时保留主编辑器状态。

### 3. R14 路由适配

- 复用现有 session/CSRF 与 `requestJson`，不新增路由。
- 支持 multipart scan、batch create、consent、start、poll、report、item detail、review、retry、pause、cancel、create-documents。
- 所有写操作使用服务端返回的 revision、consent revision、claim generation 和 item revision。
- 首次 consent 固定使用 `facts_and_metadata`；刷新只恢复已创建的 batch，不承诺恢复未建批次的 scan。
- 对错误码显示编辑者可理解的中文解释和下一步。

### 4. 人工审核闭环

- 事实卡显示事实、证据、来源原文、风险和逐条决策。
- 多项目快速审核只按 R14 单项目 review 路由顺序提交；显示数量并确认，revision 冲突即停止，不宣称原子批量成功。
- 元数据候选只处理 R14 支持的标题、摘要、领域、二级主题；批次内只允许采用当前候选，拒绝或修改要回主编辑器处理；时期和确定程度在主编辑器人工补充。
- 只有符合状态机的项目才能生成草稿。
- 草稿入口固定为 `needs_review`，不显示发布/绑定选项。

### 5. 验证

- 添加/更新前端工作台静态 harness 或浏览器脚本检查。
- 运行现有 Studio harness、BatchTests、Draft tests、Draft/Batch HTTP Smoke。
- 启动本机 Studio 做真实渲染，核对入口、拖拽、响应式、批量流程和错误提示。
- 运行 Release build、package、release-check。
- 做本次变更范围的 post-change code-debt audit。

## 证据门槛

- 修订后再次取得 `VERDICT: APPROVED` 的独立只读设计审查。
- UI 入口可达且真实调用至少一条 scan/get/report 路径。
- 事实审核、元数据审核、create-documents 的正向与拒绝路径均有证据。
- R14 contract、BatchTests、DraftTests、Smoke 和 Release build 不回归。
- 真实渲染截图/浏览器交互证明面板可见、可拖拽、可恢复。
- 最终交付明确区分离线模拟 Provider、真实云端 Provider、游戏内验证和未验证项。

## 非目标

- 不修改 R14 合同和后端状态机。
- 不新增真正的后台作业/取消令牌。
- 不修改 AWAKE 候选、游戏目录、Bannerlord 状态或发布正典。
- 不生成 expressions，不绑定人物、家族、身份。

## 停止条件

- 独立审查出现 P0/P1，先修订设计，不写代码。
- 发现现有 API 无法表达某个 UI 行为，优先缩小 UI 行为，不偷偷改契约。
- 浏览器实机发现批量流程不可达或状态语义不诚实，不宣布完成。
