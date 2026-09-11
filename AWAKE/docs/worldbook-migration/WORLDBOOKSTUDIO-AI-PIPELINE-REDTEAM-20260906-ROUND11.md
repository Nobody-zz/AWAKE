# Worldbook Studio AI 生成管线持续红队——第十一轮

> 日期：2026-09-06  
> 范围：本地 Draft 防抖写入竞态、AI evidence hash 绑定和关闭/重置边界。  
> 状态：`REVISE`  
> 本轮性质：只读静态攻击追踪 + Editor safety 基线；未修改实现。

## 1. 执行边界与基线

本轮未：

- 启动 Bannerlord；
- 同步游戏目录；
- 访问真实 Cloud Provider、API Key、Token、Worker 或网络；
- 读取/修改 Downloads 源目录；
- 修改五个世界书迁移候选；
- 修改 Worldbook Studio 实现。

本轮基线：

- Editor safety harness：`4/4 PASS`

该基线覆盖 local draft sanitize 和 raw editor 保存，但没有覆盖 Draft dialog 关闭/重置时的延迟写入，也没有覆盖 Normalizer 对 provider quote hash 的严格绑定。

## 2. 结论

| 严重度 | 数量 | 结论 |
|---|---:|---|
| P0 | 0 | 未发现直接进入 canon/runtime 的无权限路径 |
| P1 | 2 | 本地草稿关闭竞态和 evidence hash 修复可能造成内容/证据误判 |
| P2 | 1 | 本地 key 使用短非加密 hash，存在低概率碰撞风险 |

```text
PLAN_STATUS = NEEDS_REVISION
IMPLEMENTATION = NOT_APPROVED
CONTINUE REDTEAM
```

## 3. 新增发现

### RT-R11-01：关闭或重置 Draft dialog 后，旧防抖计时器仍可能写回内容

**严重度：P1**

**代码链路**

```text
输入事件
→ draftPersist()
→ LocalDraftStore.schedule(..., 450)
→ closeDraftDialog()/resetDraftState()
→ draftInvalidate()
→ 只 abort 网络请求，不 cancel LocalDraftStore timer
→ 450ms 后旧 payload 仍写入 localStorage
```

**代码证据**

- `studio-draft.js:51-56` 的 `draftInvalidate()` 没有调用 `draftLocalStore.cancel(draftContext())`；
- `studio-draft.js:60-63` 使用防抖 `schedule`；
- `studio-draft.js:347-353` 关闭对话框只调用 `draftInvalidate()`；
- `studio-local-drafts.js:48-53` 的 `cancel` 存在，但没有被 `draftInvalidate` 使用。

**攻击步骤**

1. 打开“从参考资料开始创建”；
2. 输入资料 A；
3. 在 450ms 防抖写入前关闭对话框；
4. 立即重新打开并输入资料 B；
5. 旧 timer 写入资料 A，可能覆盖资料 B 的 localStorage；
6. 重新加载后恢复到错误内容。

**结果**

- 用户主动关闭的内容可能被重新恢复；
- 新草稿可能被旧 payload 覆盖；
- 与第九轮固定 `"new-reference"` key 问题叠加后，内容错写概率更高；
- 生成中的旧状态可能在下一次草稿会话中复活。

**修复要求**

- `draftInvalidate()` 在改变 generation 前后都必须取消当前 context 的 local timer；
- timer callback 需要检查 generation/token，过期 payload 不得写入；
- 打开新草稿时生成新的 draft-local identity；
- 关闭时明确区分“保存草稿并退出”和“放弃本地草稿”。

**回归用例**

```text
输入 A → 立即关闭 → 等待 timer → localStorage 不应新增/覆盖旧会话
输入 A → 关闭 → 新建 B → B 不被 A 覆盖
显式保存草稿 → 关闭 → 可恢复 A
```

---

### RT-R11-02：Normalizer 忽略 Provider 提供的 quote_hash，静默重算并接受错误 hash

**严重度：P1**

**攻击输入**

```json
{
  "evidence": {
    "quote": "source 中真实存在的句子",
    "quote_hash": "错误或伪造的 SHA-256"
  }
}
```

**代码证据**

`AuthoringDraftResponseNormalizer.NormalizeEvidence` 在 `308-350` 行读取 quote、reference_id 和 locator，但没有读取或比较 evidence 中的 `quote_hash`。随后 `BuildEvidence` 在 `353-363` 行直接使用本地重算值：

```csharp
["quote_hash"] = Hashing.Sha256Text(quote)
```

**结果**

- Provider 提交错误 quote hash 不会被拒绝；
- normalized result 看起来拥有正确 hash；
- 审计无法区分 Provider 给出的 hash 与系统修复后的 hash；
- 如果未来 quote hash 用于跨系统证据绑定，当前静默修复会隐藏上游篡改或协议不一致。

当前 quote 必须能在 source 中定位，因此未发现直接发布绕过；但这违反“Provider 证据绑定错误应显式失败或告警”的审计要求。

**修复要求**

- Provider 明确提供 `quote_hash` 时，必须比较并拒绝不匹配；
- 缺失 quote_hash 可按版本化兼容规则处理；
- 自动修复必须输出结构化 `evidence_hash_repaired` warning，并保持 `support_status=unresolved`；
- 不能把重算后的 hash 标记为完整 provider-verified。

**回归用例**

```text
正确 quote + 正确 hash → pass
正确 quote + 错误 hash → reject 或 blocking unresolved
正确 quote + 缺 hash → 版本化兼容处理并告警
不存在 quote + 任意 hash → unverified/manual review
```

---

### RT-R11-03：localStorage key 使用 32-bit FNV 风格 hash，存在跨上下文碰撞

**严重度：P2**

`studio-local-drafts.js:10-14` 的 `hash()` 返回 8 位十六进制短 hash，用于拼接 localStorage key。不同的：

```text
workspace + path + kind
```

在足够多的工作区/路径下可能发生碰撞，造成草稿覆盖。

这不是当前普通用户场景下的已证实可利用路径，但对长期使用多个项目、多个客户包和多个草稿的工作站来说，碰撞后果是静默内容替换。

**修复要求**

- 使用 SHA-256 前缀或浏览器可用的更长稳定 hash；
- key 生成算法版本化；
- key 碰撞时存储 context 并拒绝覆盖不匹配记录；
- 不把短 hash 当作唯一身份。

## 4. 保留的正向边界

- local draft 有过期、大小和字段 sanitize；
- 网络请求有 generation/AbortController 保护；
- 服务端 create-document 仍会重新验证 accepted 内容；
- quote 定位本身仍在 source text 上执行；
- 未发现 localStorage 或错误 hash 可直接写入 canon/runtime 的路径。

## 5. 收敛状态

```text
P0 = 0
本轮新增 P1 = 2
累计待处置 P1 = 27
连续无新增 P0/P1 = 0 轮
下一步 = 继续红队或进入有授权的修复批次
```

