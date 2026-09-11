# Worldbook Studio AI 生成管线持续红队——第九轮

> 日期：2026-09-06  
> 范围：Quick Authoring 本地草稿恢复、并行草稿隔离、来源 stale 检测和服务端 Draft 续接。  
> 状态：`REVISE`  
> 本轮性质：只读静态攻击追踪 + DOM/编辑器基线；未修改实现。

## 1. 执行边界与基线

本轮未：

- 启动 Bannerlord；
- 同步游戏目录；
- 访问真实 Cloud Provider、API Key、Token、Worker 或网络；
- 读取/修改 Downloads 源目录；
- 修改五个世界书迁移候选；
- 修改 Worldbook Studio 实现。

本轮基线：

- Draft DOM/state harness：`2/2 PASS`
- Editor safety harness：`4/4 PASS`

现有基线主要验证单个本地草稿的状态恢复和字段清理，没有覆盖多个“新建参考资料”草稿并存及服务端 Draft 续接。

## 2. 结论

| 严重度 | 数量 | 结论 |
|---|---:|---|
| P0 | 0 | 未发现直接进入 canon/runtime 的无权限路径 |
| P1 | 2 | 本地草稿隔离和恢复链路可能造成用户内容丢失或错误续接 |
| P2 | 2 | stale 语义和本地候选可信度在恢复后不够明确 |

```text
PLAN_STATUS = NEEDS_REVISION
IMPLEMENTATION = NOT_APPROVED
CONTINUE REDTEAM
```

## 3. 新增发现

### RT-R9-01：所有“新建参考资料”草稿共享同一个 localStorage key

**严重度：P1**

**代码证据**

`studio-draft.js:58`：

```javascript
draftContext() {
  return {
    workspaceHash: state.workspaceHash || "local",
    documentPath: state.currentPath || "new-reference",
    kind: "reference"
  }
}
```

`studio-local-drafts.js:44-46` 的 key 只基于：

```text
workspace + path + kind
```

因此所有新建参考资料都使用：

```text
workspace + "new-reference" + "reference"
```

**攻击步骤**

1. 打开“从参考资料开始创建”；
2. 输入资料 A，关闭或切换；
3. 再打开“从参考资料开始创建”；
4. 输入资料 B；
5. B 覆盖 A 的 local draft；
6. 恢复时只能看到最后一次保存的数据。

**结果**

多个未完成 Quick Authoring 任务无法并存，用户可能丢失一整份尚未保存的参考资料和审核状态。

**修复要求**

- 新建参考资料必须生成稳定的 local draft ID；
- key 至少包含 draft session ID；
- UI 应列出多个未完成草稿，而不是只恢复一份；
- 用户主动关闭时才删除对应草稿；
- 不得使用固定 `"new-reference"` 作为所有任务的唯一身份。

**回归用例**

```text
草稿 A 与草稿 B 并存
→ A/B 分别恢复
→ 互不覆盖 source/facts/candidates
```

---

### RT-R9-02：恢复本地草稿时丢弃服务端 draftId，无法继续当前 Draft

**严重度：P1**

**代码证据**

`studio-draft.js:71` 从 local draft 恢复内容后执行：

```javascript
draftState.token = "";
draftState.draftId = "";
```

而 `draftPayload()` 没有持久化服务端 `draftId`、`attemptId`、`requestHash` 或 candidate generation。

**攻击步骤**

1. 用户完成一次生成，获得候选；
2. 页面关闭或刷新；
3. 本地草稿恢复；
4. 候选、事实和表达仍显示；
5. 点击“创建档案”；
6. `draftCanCreate()` 因 `draftState.draftId` 为空而阻止；
7. 用户必须重新生成，可能产生新的 attempt 和候选。

**结果**

界面声称“恢复上次未完成草稿”，但恢复的是脱离服务端权威 Draft 的副本。用户无法继续原 attempt，也不能直接完成已审核内容的建档。

**修复要求**

- 本地草稿记录服务端 draftId、candidate generation、source hash、request hash；
- 恢复后先向服务端 GET Draft/attempt 状态；
- 服务端 Draft 仍有效时继续原 Draft；
- 服务端 Draft 过期或 hash 不匹配时明确标记 stale，并要求重新生成；
- 不得把本地副本直接当作可提交的权威候选。

**回归用例**

```text
生成 → 刷新 → 恢复
→ GET 原 Draft
→ candidate generation 一致
→ 可继续审核/建档
```

---

### RT-R9-03：stale 检查能力存在，但调用方没有传入 current baseline

**严重度：P2**

`LocalDraftStore.load(context, currentBaseline)` 支持比较：

```text
sourceHash
revision
```

但 `draftRestoreLocal()` 调用的是：

```javascript
draftLocalStore.load(draftContext())
```

没有传入当前 source hash 或 revision baseline。

**结果**

local draft store 的 stale 检查在 Quick Authoring 新参考资料场景中实际不会触发。恢复的 source/candidate 可能来自旧资料或旧 UI 状态，却只显示为普通 `available`。

**修复要求**

- 恢复前计算当前 source baseline；
- 参考资料草稿至少比较 source hash；
- candidate 还需比较 request hash/generation/prompt revision；
- 无法比较时应显示 `stale_unknown`，不能显示为可信 available。

---

### RT-R9-04：本地恢复候选的 review 状态没有重新向服务端确认

**严重度：P2**

`draftRestoreLocal()` 直接把 localStorage 中的 candidates 通过 `draftNormalizeCandidate` 放回前端状态：

```javascript
draftState.candidates = payload.candidates.map(...);
```

没有先读取服务端 `review-projection/{draftId}` 或 attempt result。

**结果**

本地缓存可以显示旧的 pending/accepted/discarded 状态；即使服务端候选已 stale、superseded 或被新的 generation 替换，UI 仍可能先显示旧状态。

当前 create-document 仍由服务端校验，因此未证明可直接越权；但用户看到的审查状态不可信。

**修复要求**

- 恢复后服务端回读优先；
- 本地状态只作为草稿输入，不作为 candidate authority；
- 服务端回读失败时标记 `needs_reconcile`，不允许显示“当前候选”；
- candidate status 必须携带 generation/fingerprint。

## 4. 保留的正向边界

- localStorage payload 有大小限制和过期清理；
- local draft sanitize 会移除阻塞字段，现有安全测试通过；
- 当前 UI 有 generation 检查，能避免部分异步响应覆盖新状态；
- 服务端 create-document 仍以 DraftStore 内容做最终校验；
- 未发现本地缓存可直接写入 canon/runtime 的路径。

## 5. 收敛状态

```text
P0 = 0
本轮新增 P1 = 2
累计待处置 P1 = 23
连续无新增 P0/P1 = 0 轮
下一步 = 继续红队或进入有授权的修复批次
```

