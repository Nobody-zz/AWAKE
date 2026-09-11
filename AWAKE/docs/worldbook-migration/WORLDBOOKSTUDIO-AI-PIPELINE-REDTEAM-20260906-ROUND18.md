# Worldbook Studio AI 生成管线持续红队——第十八轮

> 日期：2026-09-06  
> 范围：Quick Authoring UI 的不可信文本渲染、候选详情和重渲染安全。  
> 状态：`NO_NEW_P0_P1`  
> 本轮性质：只读静态复核 + EditorContent/DOM 基线；未修改实现。

## 1. 执行边界与基线

本轮未：

- 启动 Bannerlord；
- 同步游戏目录；
- 访问真实 Cloud Provider、API Key、Token、Worker 或网络；
- 读取/修改 Downloads 源目录；
- 修改五个世界书迁移候选；
- 修改 Worldbook Studio 实现。

本轮基线：

- Editor content harness：`7/7 PASS`
- Draft DOM/state harness：`2/2 PASS`

## 2. 攻击范围

复核了以下字段注入和渲染路径：

```text
candidate.metadata.title
candidate.metadata.summary
fact.text
expression.text
source quote
source locator
segmentation reason code
profile display
warning text
candidate ID
```

重点检查：

- `innerHTML` 模板是否统一调用 `h()`；
- `data-*` 属性是否转义；
- 详情面板与列表重渲染是否保留安全边界；
- localStorage 恢复内容是否直接变成 HTML；
- 用户输入是否通过 text area/value 而非 HTML 注入。

## 3. 本轮结论

在当前已实现的 Draft UI 范围内，没有发现新的 P0/P1。

当前渲染使用：

```text
h(...)
textContent/value
固定枚举标签
```

候选标题、事实、表达、来源定位和 reason code 均经过转义后才插入 HTML 模板；编辑内容使用 textarea/value，不作为可执行 HTML。

```text
P0 = 0
本轮新增 P0/P1 = 0
连续无新增 P0/P1 = 2 轮
```

## 4. 收敛限制

本轮只能证明当前 UI 实现没有新增可确认的 P0/P1，不能证明前面已发现的 P1 已处置。

仍未处置的历史 P1 包括：

- Quick Authoring 输入契约和用户意图未接入；
- unknown content tier 默认风险；
- CandidateSet lifecycle/stale/superseded 建档边界；
- warnings/unresolved 生命周期和落盘；
- Draft state version/migration；
- local draft 多任务隔离和服务端 Draft 续接；
- evidence exact/normalized/duplicate locator；
- candidate ID 冲突和 merge 静默去重；
- complete candidate 分类/语义字段落盘保真。

因此红队目标仍不能标记完成：

```text
P1 处置证据 = 不足
实现修改 = 0
下一步 = 进入已授权修复批次，随后重新红队
```

