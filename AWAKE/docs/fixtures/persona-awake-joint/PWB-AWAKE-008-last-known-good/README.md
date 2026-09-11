# PWB-AWAKE-008-last-known-good

验证坏 candidate reload 在激活前被拒绝，同时旧 RuntimeBundle、activation metadata 和已发布引用保持不变。当前离线 runner 已接入 `reload-runtime-bundle` 专用处理器，因此该夹具应报告 `pass/0`；`persona.registry_digest_mismatch` 作为候选校验警告记录，不构成对当前 bundle 的覆盖或晋级。
