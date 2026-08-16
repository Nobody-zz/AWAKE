# AWAKE Rules

This directory is reserved for versioned rule manifests consumed by `AwakeRuleRegistry`.

Each `.json` file must use:

```json
{
  "schemaVersion": "awake.rule.v1",
  "id": "example.rule",
  "group": "core",
  "priority": 100,
  "enabled": true,
  "fingerprint": "sha256-or-stable-fingerprint",
  "payload": {}
}
```

Rules registered here are validated at runtime. Unknown or invalid manifests are rejected and logged; they never partially load.
