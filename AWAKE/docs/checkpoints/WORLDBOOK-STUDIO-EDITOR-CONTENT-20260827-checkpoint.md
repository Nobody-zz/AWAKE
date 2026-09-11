# Worldbook Studio 编辑器内容一致性修复检查点

- task_id: WORLDBOOK-STUDIO-EDITOR-CONTENT-20260827
- status: offline_verified
- plan: docs/PLAN-WorldbookStudio-EditorContent-20260827-REPAIR.md
- execution_lease: released
- scope: 仅世界书编辑器作者模式、AI 候选应用边界、候选失效和 A4 CLI/Web Smoke 夹具；未触碰 AWAKE 主工程、Marcus、游戏目录、运行时读取器或冻结候选。
- implementation:
  - AI 同批候选成功应用一条后，其余候选失效，避免基于旧缓冲区覆盖内容。
  - 作者或高级模式发生手工输入/选择后，旧 AI 同意令牌、范围和候选清除。
  - 来源型/冲突档案拒绝 Web AI 候选应用，必须进入高级模式手工维护。
  - 多操作补丁不再伪装成单字段作者模式跳转，只进入高级缓冲区。
  - A4 使用独立无来源作者草稿夹具；来源正典夹具继续用于来源链测试。
- files_changed:
  - tools/worldbook-studio/src/Awake.WorldbookStudio.Core/AiCandidateGuards.cs
  - tools/worldbook-studio/src/Awake.WorldbookStudio.Core/SuggestionStore.cs
  - tools/worldbook-studio/src/Awake.WorldbookStudio.Core/AuthoringCandidateProjection.cs
  - tools/worldbook-studio/src/Awake.WorldbookStudio.Web/Program.cs
  - tools/worldbook-studio/src/Awake.WorldbookStudio.Web/wwwroot/studio-ai.js
  - tools/worldbook-studio/src/Awake.WorldbookStudio.Web/wwwroot/studio-authoring-ux.js
  - tools/worldbook-studio/tests/Awake.WorldbookStudio.EditorContent.Tests/Program.cs
  - tools/worldbook-studio/tests/frontend/editor-content.test.js
  - tools/worldbook-studio/scripts/a1-authority-smoke.ps1
  - docs/worldbook-studio-plan/fixture-valid-authoring-minimal.yaml
  - docs/worldbook-studio-plan/FIXTURE-MATRIX.md
- verification:
  - Release build: 0 warnings / 0 errors。
  - Worldbook Studio harness: 101/101 PASS。
  - Editor content core: 7/7 PASS。
  - Frontend editor-content static checks: PASS。
  - BatchTests: 13/13 PASS；Draft tests: 10/10 PASS。
  - A4 CLI/Web Smoke: 18/18 cases PASS；正式证据：docs/evidence/WORLDBOOK-STUDIO-A4-CLI-WEB-20260823-smoke.json。
  - Release check: PASS；独立修复包：tools/worldbook-studio/artifacts/content-candidate-20260827-editor-repair/WorldbookStudio-win-x64.zip。
  - 修复包 ZIP SHA-256：645c8fb301aa906c9c78275638f86c8f0c4eb3daed16efddc2cfd5bb75353208。
  - 修复包 manifest/SHA256SUMS 文件 SHA-256：86dd1b71f82e29f5b1e69a637d23cf4aefc86a1ca26eec28cccc1d9bd0f0647e / 46de0d58ed1f86e78eef353332eec16adc77568007df9445cbaa038630abb8d9。
  - 游戏目录触碰：false；Bannerlord 未启动；Studio/Worker Smoke 进程均已清理。
- post_change_code_debt_audit:
  - scope: 本轮变更文件及一层调用/前端加载关系；bin、obj、历史包和旧调试证据排除。
  - confirmed_removable_lines: 0；新增 guard、失效逻辑、作者投影和测试均有调用或契约绑定。
  - suspected_findings: 2；内联 index 页面仍偏长，AI 应用/拒绝路径仍有少量重复 DTO 组装；当前没有错误行为证据，不在本轮扩大重构。
  - efficiency_risks: 0 个已证明热路径风险；候选失效为用户操作路径，不在游戏 tick。
  - confidence: 高（离线测试、A4 Smoke、独立包检查）；中（未验证真实浏览器 DOM 和真实云端/本机 Worker）。
- known_limitations:
  - 未验证真实云端 Provider、真实本机 Worker、真实浏览器 DOM 交互。
  - 未启动 Bannerlord，未同步游戏目录，未进行 E3/E4/E5 实机验证。

## Next action

将独立修复包交给世界书作者做离线试用，重点观察来源档案提示、AI 候选采纳后重新检查、手工修改后旧候选清除、多操作建议进入高级模式和连续切换档案；反馈前不修改 AWAKE 运行时线。
