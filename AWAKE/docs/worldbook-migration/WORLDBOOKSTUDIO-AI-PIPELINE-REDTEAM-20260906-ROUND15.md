# Worldbook Studio AI 生成管线持续红队——第十五轮

> 日期：2026-09-06  
> 范围：Draft 输入字段的独立长度限制、Prompt/持久化资源放大和请求体边界。  
> 状态：`REVISE`  
> 本轮性质：只读静态攻击追踪；未修改实现。

## 1. 执行边界

本轮未：

- 启动 Bannerlord；
- 同步游戏目录；
- 访问真实 Cloud Provider、API Key、Token、Worker 或网络；
- 读取/修改 Downloads 源目录；
- 修改五个世界书迁移候选；
- 修改 Worldbook Studio 实现。

## 2. 结论

| 严重度 | 数量 | 结论 |
|---|---:|---|
| P0 | 0 | 未发现直接进入 canon/runtime 的无权限路径 |
| P1 | 1 | 非 source 字段缺少独立边界，可放大 Prompt、状态文件和重试资源 |
| P2 | 1 | 字段数量限制与语义容量限制未完全区分 |

```text
PLAN_STATUS = NEEDS_REVISION
IMPLEMENTATION = NOT_APPROVED
CONTINUE REDTEAM
```

## 3. 新增发现

### RT-R15-01：sourceName/sourceNature/perspectives 缺少独立长度限制

**严重度：P1**

`AuthoringDraftRequestFactory.Create` 只对 `sourceText` 做 `80,000` 字符限制：

```csharp
if (normalizedText.Length > MaxSourceCharacters) ...
```

但以下字段只做 trim、去重或数量限制：

```text
sourceName
sourceNature
perspectives
```

代码位置：

- `AuthoringDraftContracts.cs:131-153`
- `AuthoringDraftStore.cs:89-104`

当前行为：

```csharp
sourceName?.Trim()
sourceNature?.Trim()
perspectives.Take(32)
```

没有对：

```text
sourceName 单项长度
sourceNature 单项长度
perspective 单项长度
perspectives 总字符数
```

执行限制。

**攻击步骤**

1. 提交接近上限的 source text；
2. 追加 32 个超长 perspectives；
3. 使用超长 sourceName/sourceNature；
4. prepare 生成 request；
5. request 被写入 consent、attempt 和 state；
6. Provider 请求、retry、错误响应和持久化均携带放大的字段。

**结果**

- Prompt 请求体可以被非 source 字段显著放大；
- Draft state 和 attempt state 膨胀；
- 本地 Worker/Provider 处理时间增加；
- retry 会重复消耗同一份放大请求；
- UI local draft 也可能保存大量 perspectives/source metadata；
- source text 上限并不能防止整体请求超限。

这属于资源耗尽/拒绝服务风险，但当前没有证明能绕过人工审核进入 canon，因此定为 P1 而非 P0。

**修复要求**

为每个字段定义独立上限：

```text
sourceName：单项字符上限
sourceNature：单项字符上限
perspectives：数量上限 + 单项上限 + 总字符上限
acceptedFacts：数量上限 + 总文本/证据字符上限
registrySummary：对象深度、字段数和序列化字节上限
```

prepare 阶段应在 Provider 调用前拒绝超限输入。不能只依赖 HTTP body 或 Provider 自己截断。

**回归用例**

```text
每个字段单独超限 → stable 413/422
总请求体超限 → prepare 阶段拒绝
32 个正常 perspective → pass
32 个超长 perspective → reject
retry 不得重新接受被截断的字段
```

---

### RT-R15-02：perspectives 只按字符串去重，未限制总语义容量

**严重度：P2**

当前 `perspectives` 处理为：

```csharp
Select(trim)
Where(non-empty)
Distinct
Take(32)
```

即使每项长度后来增加限制，仍需明确：

```text
视角名称
身份权限
受众
文化/地域说明
```

不能只用字符串数量代表 Prompt 容量。不同视角可能携带复杂的语义约束，Quick Authoring 方案若直接把它们全部放进 Prompt，仍可能超出模型上下文或造成结果截断。

**修复要求**

- 明确 perspectives 是简短标签还是结构化对象；
- 如果是标签，限制单项和总字节数；
- 如果需要复杂说明，另建受控字段；
- Provider 请求前计算序列化字节预算；
- 超出预算时阻断，不静默 `Take` 或截断。

## 4. 保留的正向边界

- source text 有明确长度限制；
- facts、expressions、candidates、warnings 等数组已有数量限制；
- Provider response 有最大字节限制；
- 当前未发现输入放大可以直接写入 canon/runtime 的路径。

## 5. 收敛状态

```text
P0 = 0
本轮新增 P1 = 1
累计待处置 P1 = 34
连续无新增 P0/P1 = 0 轮
下一步 = 继续红队或进入有授权的修复批次
```

