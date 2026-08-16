# Marcus 一键配置可行性

> 日期：2026-08-16

## 结论

- 高可行：框架检测、路由状态、Provider 状态、引导入口。
- 中可行：一键同步路由，取决于 Marcus 是否暴露公开同步 API。
- 低可行：AWAKE 直接写入 Companion 的 Provider profile / platform.db，不建议做。

## 可落地操作

### AWAKE MCM “AI 链接”组

1. `框架状态`：检测 `FrameworkHostLocator.TryGetHost`，显示 Connected / Offline。
2. `路由状态`：显示已声明路由 `AWAKE.route.*` 数量。
3. `Provider 状态`：显示候选模型/连接是否可用。
4. `一键同步路由`：调用 Marcus 公开 API 触发路由同步；无公开 API 时降级为“打开 AI 设置台”。
5. `打开 AI 设置`：跳到 Marcus AI 设置。
6. `重新检测`：刷新上述状态。

### 启动引导

- 无 Host：显示“未检测到 Marcus AI Framework”，提示安装路径。
- 有 Host 但无路由：显示“路由缺失”，引导一键同步。
- 有路由但无 Provider：显示“尚未配置模型”，引导打开 AI 设置。

## 边界

- AWAKE 不写 `platform.db`。
- AWAKE 不直接调用 Companion CLI。
- Provider/Key 仍由 Marcus 负责。

## 待 Marcus 提供的能力

- 公开 Route Sync API。
- 公开 Provider Profile 读取/应用 API。
- 公开“打开 AI 设置台”入口。

## 建议第一步

先做“状态检测 + 引导入口 + 一键同步（能调 API 就调，不能就打开设置台）”。
