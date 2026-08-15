# AWAKE · Provider 配置指南

本文说明 AWAKE 为什么不受单一模型供应商限制，以及如何用一体化 MarcusAIFramework 在游戏内配置 Ollama、OpenAI-compatible 或 Anthropic。

## 1. 模组侧不碰 Provider

AWAKE 的源码里只使用四条逻辑路由：

```text
新版框架从 manifest 自动声明并同步 Route；AI 设置台点击“同步路由”即可。旧版手动配置使用：
awake.route.npc.dialogue
awake.route.preprocess
awake.route.postprocess
awake.route.memory.daily
```

模组不提交 URL、API Key、模型名或厂商 DTO，也不解析 Provider 内部格式。Provider 连接、密钥和模型路由全部由 MarcusAIFramework 负责，因此更换 Provider 不需要重编译，也不需要改模组代码。

## 2. 一体化框架已内置 Companion

本机已安装一体化包 `MarcusAIFramework-v0.1.0-v1.3.15-protected.zip`：

- Companion 位于 `Modules/MarcusAIFramework/Companion/`，游戏启动时由主 Mod 静默拉起并自动连接，退出游戏后随父进程结束。
- 玩家不再需要手动运行黑色窗口、`setup` 或 `profiles apply`。
- API Key 使用 Windows 当前用户 DPAPI 保存在 `%LOCALAPPDATA%\MarcusAIFramework\credentials.dpapi`，不写入 MCM JSON 或平台数据库，也不回显。

## 3. 游戏内 MCM 配置

1. 启动游戏，进入 `MCM → Marcus AI Framework → 01 Companion`。
2. 选择 Provider，填写 API Base URL 与模型 ID；Ollama 可留空地址使用 `http://127.0.0.1:11434`，云端服务可使用内置默认地址。
3. 在 Route ID 栏填写本模组的四条逻辑路由，用英文逗号分隔：

```text
awake.route.npc.dialogue,awake.route.preprocess,awake.route.postprocess,awake.route.memory.daily
```

4. 云端或需要认证的服务点击“设置 / 替换 Key”，保存到 Companion；Ollama 跳过此步。
5. 云端 Provider 还需开启“全局允许云端外发”，并批准对应 route 权限。
6. 保存后可在游戏内 AWAKE 命令台查看路由能力与候选模型状态。

配置在下次启动仍然有效。切换服务时直接修改 MCM 字段并用相同 Route ID 保存即可；不再使用某个 Key 时点击“清除已保存 Key”。

### 实测界面文案（2026-08-13 已核对）

框架 MCM 的 `01 伴随服务` 页面实际字段如下，与上文配置步骤一一对应：

- `LLM 服务商`：Provider。截图实测为 `OpenAI`，用于接入 DeepSeek 等 OpenAI-compatible 云端服务。
- `API 基础地址`：OpenAI-compatible 端点地址。截图实测为 `https://api.deepseek.com`。
- `模型 ID`：模型名。截图实测为 `deepseek-v4-flash`。
- `子 Mod Route ID`：英文逗号分隔的 Route ID 列表。截图中的 `ionships,MarcusAIWorldEvents` 是作者示例路由被截断的尾部；本模组应填写上文四条逻辑路由。
- `OpenAI-compatible 端点位于云端`：云端服务勾选；底部提示明确 localhost 本地服务保持关闭。
- `模型支持结构化输出`：按模型能力勾选。
- `Provider 超时（秒）`：截图实测为 `90`。
- `LLM 配置`：选择 `保存到 Companion`；Key 经 DPAPI 落盘，不回显。

## 4. 三层配置（了解即可）

Companion 内部仍是 Connection → Model → Route 三层结构。普通玩家只需要在 MCM 填写 Provider、模型 ID 与 Route ID；开发者需要精确控制 fallback、固定模型或云端开关时才使用高级配置。

## 5. 高级 / 恢复入口

`docs/profiles.awake.multiprovider.example.json` 是开发者模板，不再是普通玩家安装入口。模板包含 Ollama、OpenAI-compatible、Anthropic 的 Connection/Model 示例，并给出四条路由的 route 条目。只有做批量配置、故障恢复或离线调试时才可参考：

```powershell
MarcusAIFramework.Companion.exe manage
MarcusAIFramework.Companion.exe profiles list
MarcusAIFramework.Companion.exe profiles apply profiles.awake.multiprovider.example.json
```

云端条目默认禁用；启用后仍需满足 route 权限、MCM 全局云端门与 Route `allowCloud`。

## 6. 与知识检索的关系

RAG 检索由框架侧的 SQLite FTS5 承载，不依赖具体模型厂商；AWAKE 另有内置本地关键词索引作为离线兜底。换 Provider 只影响“AI 用什么模型回答”，不会破坏知识检索与命令链路。

## 7. 不依赖模型的离线验证

框架自带 `framework.echo` 逻辑路由，不调用任何模型厂商。用它可以直接验证“逻辑路由链路本身工作正常”：

```text
ai.task.request route=framework.echo input={"reply":"echo","mood":"joy","effects":[]}
-> accepted -> started -> text_delta -> completed (resolvedProvider=framework.echo)
```

这条链路已在真实 Companion 命名管道上跑通，证据记录在 `BUILD_VERIFICATION.txt` G6。只要模组只使用逻辑路由，未来把路由切到任何受支持 Provider 都不会改动模组代码。
