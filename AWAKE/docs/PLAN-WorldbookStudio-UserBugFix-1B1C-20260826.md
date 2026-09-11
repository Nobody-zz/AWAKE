# Plan: Worldbook Studio 1B/1C 用户级安全修复

**任务编号**：`WORLDBOOK-STUDIO-USER-BUGFIX-1B1C-20260826`  
**状态**：`COMPLETED_OFFLINE_VERIFIED`
**范围**：高级模式安全保存、危险操作门禁、本地临时草稿恢复。  
**不包含**：Bannerlord、游戏目录、AWAKE 冻结候选、批量工作台、Provider 协议、世界书 schema 迁移。

## 1. 已确认问题

- 高级模式 `/api/save-authoring` 当前没有提交原文件 hash/revision，也没有写前解析校验。
- 高级保存成功后没有完整刷新作者投影和保存基线。
- 编译、导出、预览、工作区校验没有统一处理当前编辑区的未保存修改。
- 切换、刷新和从参考资料创建等入口的 dirty guard 不统一。
- Web 重启或页面崩溃会丢失主编辑区和参考资料创建过程中的临时修改。

## 2. 选择的最小方案

### 2.1 高级保存

- 保留现有 `/api/save-authoring` 路由名；请求字段 `sourceHash`、`revision` 保持可反序列化兼容，但本安全路由缺任一字段必须拒绝，不能回退到无 CAS 写入。
- 服务端新增安全高级保存路径：先在内存解析 YAML/JSON 并执行现有 schema/taxonomy 校验；随后在同一写入边界重读并检查 hash/revision，成功后再原子写入并推进 revision。
- 非法内容只返回可定位诊断，不改变磁盘原文件；外部修改返回 CAS 冲突。
- 前端高级保存使用独立 `save-raw` 操作，捕获 raw 文本、路径、hash、revision、session token 和 edit generation；pending 回读按 raw 内容而非表单 projection 判定。
- 成功后同时更新 raw 文本、作者投影、snapshot、hash、revision 和列表状态；保存期间的新输入保留在当前编辑区。

### 2.2 高级保存错误契约

- 新增 `AuthoringFailure` 统一映射：前置请求错误、缺少 `sourceHash`/`revision`、空内容和路径错误返回 HTTP 400；解析失败或 Schema/分类校验失败返回 HTTP 422；外部 hash/revision 变化返回 HTTP 409；写入/读取结果无法确认、超时或连接中断返回 HTTP 503，并明确 `resultUnknown=true`。
- API 错误响应统一包含 `ok=false`、稳定 `error`、面向编辑者的 `message`、可选 `diagnostics` 和 `resultUnknown`；稳定错误码固定为 `WB-AUTHORING-SAVE-400`、`WB-AUTHORING-PARSE-422`、`WB-AUTHORING-SCHEMA-422`、`WB-AUTHORING-CAS-409`、`WB-AUTHORING-UNKNOWN-503`；不把内部异常堆栈返回给页面。
- 前端 `400/422` 进入 `failed`，保留编辑文本且允许修正后重试；`409` 进入 `conflict`，只提供重新读取/保留当前内容；`503` 或回读不完整进入 `pending-confirmation`，只允许检查结果，不自动再次写入。
- 高级保存成功响应必须返回 `editorDocument` 或等价的完整 raw 回读字段：路径、内容、hash、revision、诊断；前端只有在当前 `save-raw` session 和 generation 仍匹配时更新基线。写入前拒绝、写入后未知、回读不完整三类结果分别保留不同状态，且均有文件字节/hash/revision 断言。

### 2.3 危险操作门禁

- 编译、导出、预览、工作区校验在当前档案 dirty 时先询问是否保存，并在保存完成前不获取 confirmation token 或发送实际操作请求；实际请求携带当前档案路径、保存后的 source hash 和 revision，服务端在操作入口校验该三元组，操作结果可追溯到同一保存版本。
- 保存失败、冲突、结果待确认或保存后仍有新输入时，操作请求不发送。
- 切档、刷新、模式切换、从参考资料创建和页面关闭继续使用统一的“保存 / 放弃 / 取消”语义；本轮不引入新的复杂对话框组件。

### 2.3 本地临时草稿

- 使用浏览器 `localStorage` 分离保存主编辑器草稿和参考资料向导草稿，记录工作区、档案路径、保存基线 hash/revision、编辑模式和临时内容；不保存 API Key、CSRF、consent 或 Provider token。
- 按工作区 hash 与档案路径隔离，默认单条上限 1.5 MiB，写入失败不阻塞正式编辑。
- 输入停止约 450ms 后保存；只有保存 generation 与当前编辑 generation 相同，或用户明确放弃，才删除该档案临时草稿；保存旧版本但仍有新输入时保留草稿。
- 重新打开档案时，如果发现草稿，明确询问恢复或放弃；磁盘基线变化时标明“基于旧版本”，恢复后仍必须走正常 CAS 保存。
- 损坏、过期或无法解析的草稿只删除临时项，不影响正式档案；参考资料向导同时拒绝迟到结果应用到错误档案，并在生成阶段校验 draft/请求代次。

## 3. 不采用的方案

- 不在本轮做自动三方合并：会扩大冲突语义，且普通编辑者无法判断合并结果。
- 不把草稿写进 authoring 工作区：避免临时内容被编译器误读为正典。
- 不改存档格式、运行时读取器或游戏目录同步：这些属于另一条证据链。

## 4. 验收标准

- B1：非法高级 YAML/JSON/Schema 保存被拒，原文件 hash/字节不变，临时文件清理。
- B2：高级模式外部修改触发 CAS 冲突，不覆盖外部内容。
- B3：编译/导出/预览/校验遵守保存顺序；保存未确认时操作请求数为零；实际请求携带并由服务端校验保存后的路径、source hash、revision。
- B4：切档、刷新、模式切换、参考建档和关闭遇到 dirty 时可取消；确认放弃会真正恢复基线。
- B5：高级保存响应丢失时进入 pending，回读只接受路径、hash、revision 和内容均匹配的结果。
- B6：直接调用高级保存 API 也不能绕过解析、schema 和 CAS；缺字段、解析失败、校验失败、冲突和结果未知均返回对应稳定状态。
- C1：Web/浏览器重启后能按工作区和档案恢复未保存临时草稿。
- C2：草稿基线过期时不自动覆盖磁盘，恢复后仍显示冲突风险。
- C3：恢复内容继续走正常保存流程，失败仍保留草稿内容。
- C4：明确放弃只删除临时草稿，不删除正式档案。

## 5. 实现文件与证据

- 预计修改：`tools/worldbook-studio/src/Awake.WorldbookStudio.Core/Application.cs`、`Workspace.cs`、`tools/worldbook-studio/src/Awake.WorldbookStudio.Web/Program.cs`、`wwwroot/index.html`、`wwwroot/studio-editor-session.js`、`wwwroot/studio-draft.js`。
- 预计新增：`wwwroot/studio-local-drafts.js` 及对应聚焦测试。
- 先补 Core/API/会话测试，再运行既有 Studio、Batch、Draft、Launcher 和 release-check。
- 本轮最高证据等级为 E2；不启动 Bannerlord，不同步游戏目录。

## 6. 状态

```text
plan_status: APPROVED
review_status: APPROVED
user_signoff_required: satisfied_by_explicit_continue_request
code_change_authorized: true
review_corrections: satisfied; mandatory CAS fields; explicit AuthoringFailure HTTP/UI mapping; independent save-raw state; operation guard before token/request; separate editor/reference local drafts; draft response generation fence; operation baseline binding
```

## 7. 完成记录

- 1B 高级模式保存已完成：保存前解析/schema/分类校验、source hash/revision CAS、原子写入、写后回读一致性检查，以及 `400/422/409/503` 稳定错误映射均已接线。
- 1C 参考资料草稿恢复已完成：工作区/档案隔离、本地草稿去敏、过期基线提示、关闭时取消请求、生成代次隔离和建档后的二次代次检查均已接线。
- 发布链路已加固：`studio-draft.js` 必须与源码哈希一致，ZIP 必须包含且哈希匹配该资源；构建/验证脚本统一为 UTF-8 BOM，并显式按 UTF-8 读取 JSON，兼容 Windows PowerShell 5.1。
- 新增 `tests/frontend/draft-race.test.js`，覆盖请求未返回时修改草稿和刷新档案列表期间修改草稿两条竞态路径。
- 当前最高证据等级为 E2；未启动 Bannerlord，未同步游戏目录，未修改冻结 AWAKE 候选。






