# Worldbook Studio AI 生成链路用户化重构实现检查点

> 日期：2026-09-09  
> 对应方案：`WORLDBOOKSTUDIO-AI-UX-PIPELINE-REFORM-PLAN-20260909.md`  
> 审查状态：`WORLDBOOKSTUDIO-AI-UX-PIPELINE-REFORM-20260909.json` = `approved`  
> 当前证据：离线回归、包级 smoke、当前测试包 release-check

## 已落地

### Quick Authoring 用户流程

- Quick Authoring 使用独立模板，不再依赖旧 staged dialog 的动态 Quick facade。
- legacy staged 使用独立兼容模板和独立入口。
- Quick 默认不填充身份视角。
- Quick 生成按钮改为明确的“开始生成草稿”。
- 建档按钮改为“创建待审核档案”。
- 结果页显示待审核性质、来源数量、警告数量、阻断数量和待确认项。
- 高级候选审查操作移入折叠区域。
- Quick 选择候选后一次只允许一个 candidate 进入建档。

### 后端硬门

- Quick complete 必须有 `authoring_goal`。
- Quick complete 必须显式选择 `base` 或 `adult_optional`。
- Quick 最多允许 3 个身份视角。
- 未请求身份视角时，Provider 返回 expressions 会被确定性拒绝。
- Prompt 明确要求未请求视角不生成 expressions。
- `review_only/pending/needs_review`、CAS、consent、retry/recovery、semantic packet 和 candidate lifecycle 保持不变。

### 文档

- 更新用户指南，改用 Quick Authoring 的“资料 → 目标 → 生成待审核草稿 → 人工采纳 → 创建待审核档案”流程。
- 更新 README 的入口名称和 AI 使用说明。

## 修改文件

- `src/Awake.WorldbookStudio.Core/AuthoringDraftContracts.cs`
- `src/Awake.WorldbookStudio.Core/AuthoringDraftPromptCatalog.cs`
- `src/Awake.WorldbookStudio.Core/AuthoringDraftResponseNormalizer.cs`
- `src/Awake.WorldbookStudio.Web/QuickAuthoringOrchestrator.cs`
- `src/Awake.WorldbookStudio.Web/Program.cs`
- `src/Awake.WorldbookStudio.Web/wwwroot/studio-draft.js`
- `src/Awake.WorldbookStudio.Web/wwwroot/studio-draft.css`
- `src/Awake.WorldbookStudio.Web/wwwroot/index.html`
- `src/Awake.WorldbookStudio.Web/wwwroot/studio-authoring-ux.js`
- `tests/Awake.WorldbookStudio.Draft.Tests/Program.cs`
- `tests/frontend/editor-content.test.js`
- `tests/frontend/draft-dom-state.test.js`
- `新手指引_世界书内容编辑者.md`
- `README_使用说明.txt`

未修改：

- Semantic Migration 候选；
- AWAKE 模组本体；
- `ModuleData`、`dist`、游戏目录；
- 真实源目录；
- 真实 Cloud Provider、API Key、Token、Worker 和网络服务。

## 验证结果

- `Draft.Tests`：`79/79 PASS`
- `BatchTests`：`23/23 PASS`
- Worldbook Studio harness：`113/113 PASS`
- `Workstation.Tests`：`12/12 PASS`
- Frontend harnesses：全部通过
- Draft DOM/state harness：`5/5 PASS`
- Draft HTTP smoke：通过
- Workstation handoff loopback smoke：通过
- Authoring save smoke：通过
- Batch workflow smoke：通过
- Public contract smoke：通过
- `scripts\test.ps1 -Suite All`：通过
- Release build：`0 warnings / 0 errors`
- `package.ps1 -RunSmoke`：通过
- Launcher tests：`14` 项通过
- Package clean-start：通过
- Package browser-failure：通过
- Package stale-settings-missing：通过
- Package stale-settings-marker：安全阻断通过
- Package duplicate-launch：通过
- Package graceful-shutdown：通过

## 当前测试包

- Launcher：
  `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\worldbook-studio\artifacts\current-test\WorldbookStudio\Awake.WorldbookStudio.Launcher.exe`
- ZIP：
  `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\worldbook-studio\artifacts\current-test\WorldbookStudio-win-x64.zip`
- ZIP SHA-256：
  `ea3f07aa4b4dd6b2a30885fdda93994585a3ff66675290cd8a690438a7c27126`
- Package manifest SHA-256：
  `ff819a88fca1bc5cefe777c5f7efc6884666a31a7671ee270906154cb4f8538f`

## 未验证项与剩余风险

- 未使用真实本地 Worker 执行一次用户自定义世界知识生成；本轮只使用离线 fake/loopback harness 验证调用链、契约和安全边界。
- 未访问真实 Cloud Provider。
- 浏览器中已验证 Quick 入口、输入字段、内容层级、结果提示和档案检查入口可见；未在真实 Provider 上完成模型输出后的视觉审查。
- 仍需后续独立批次补充真实本地 Worker 的重新创作样本，不得使用固定短样本冒充用户验收。

## 边界声明

- 未启动 Bannerlord。
- 未同步游戏目录。
- 未访问真实 Provider、API Key、Token、Worker 或网络服务。
- 未修改 AWAKE 模组本体、`ModuleData`、`dist` 或冻结构建产物。
