using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MarcusAIFramework.Api;

namespace Awake;

internal static class AwakeDeveloperReport
{
    internal const int MaximumReportLength = 4000;

    internal static string Build(IMarcusAiFrameworkHost host, AwakeConfig config)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("AWAKE · 开发者检查");
        builder.AppendLine("版本=" + AwakeVersion.Version);
        builder.AppendLine("Host=" + (host == null ? "不可用" : "已连接"));
        builder.AppendLine("会话=" + (host?.CurrentSession == null ? "未就绪" : "已就绪"));
        builder.AppendLine("玩家=" + (string.IsNullOrWhiteSpace(AwakeRuntime.CurrentHeroId) ? "未绑定" : "已绑定"));
        builder.AppendLine("世界状态=" + (AwakeRuntime.WorldStateStore == null ? "未启动" : "已就绪"));
        builder.AppendLine("权限目录=" + PermissionCatalog.All.Length + " 条");
        builder.AppendLine("云外发=" + (config != null && config.EnableCloudExport ? "已启用" : "已禁用"));
        builder.AppendLine("云外发分类=" + CloudExportPolicy.DescribeAllowed(config));
        builder.AppendLine("快捷键冲突=" + BuildSceneKeyConflictText(config));
        if (host == null || host.Diagnostics == null)
        {
            return Truncate(builder.ToString());
        }
        AppendHealth(host.Diagnostics, builder);
        AppendCompatibility(host.Diagnostics, builder);
        AppendExtensions(host.Diagnostics, builder);
        return Truncate(builder.ToString());
    }

    internal static IReadOnlyList<KeyValuePair<string, string>> BuildRows(
        IMarcusAiFrameworkHost host,
        AwakeConfig config)
    {
        List<KeyValuePair<string, string>> rows = new List<KeyValuePair<string, string>>();
        rows.Add(NewRow("version", AwakeVersion.Version));
        rows.Add(NewRow("host", host == null ? "unavailable" : "connected"));
        rows.Add(NewRow("session", host?.CurrentSession == null ? "not_ready" : "ready"));
        rows.Add(NewRow("player", string.IsNullOrWhiteSpace(AwakeRuntime.CurrentHeroId) ? "unbound" : "bound"));
        rows.Add(NewRow("world_state", AwakeRuntime.WorldStateStore == null ? "not_started" : "ready"));
        rows.Add(NewRow("permissions", PermissionCatalog.All.Length.ToString()));
        rows.Add(NewRow("cloud_export", config != null && config.EnableCloudExport ? "enabled" : "disabled"));
        rows.Add(NewRow("cloud_categories", CloudExportPolicy.DescribeAllowed(config)));
        rows.Add(NewRow("scene_key_conflicts", BuildSceneKeyConflictText(config)));
        if (host == null || host.Diagnostics == null)
        {
            rows.Add(NewRow("health", "unavailable"));
            return rows;
        }
        try
        {
            HealthSnapshot health = host.Diagnostics.GetHealth();
            int componentCount = health?.Components?.Count ?? 0;
            rows.Add(NewRow("health_count", componentCount.ToString()));
            if (health?.Components != null)
            {
                int shown = 0;
                foreach (HealthComponent component in health.Components)
                {
                    if (component == null || shown >= 10) continue;
                    rows.Add(NewRow(
                        "health." + component.Id,
                        component.Level.ToString()
                        + (string.IsNullOrWhiteSpace(component.Code) ? string.Empty : " (" + component.Code + ")")
                        + (string.IsNullOrWhiteSpace(component.Summary) ? string.Empty : " " + component.Summary)));
                    shown++;
                }
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("developer_report_rows_health_failed error=" + ex.Message);
            rows.Add(NewRow("health", "unavailable"));
        }
        try
        {
            IReadOnlyList<CapabilityDescriptor> report = host.Diagnostics.GetCompatibilityReport();
            int count = report?.Count ?? 0;
            rows.Add(NewRow("compatibility_count", count.ToString()));
            if (report != null)
            {
                int shown = 0;
                foreach (CapabilityDescriptor descriptor in report)
                {
                    if (descriptor == null || shown >= 10) continue;
                    rows.Add(NewRow(
                        "compatibility." + (descriptor.Id?.Value ?? "unknown"),
                        (descriptor.Availability.ToString() ?? "unknown")));
                    shown++;
                }
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("developer_report_rows_compatibility_failed error=" + ex.Message);
            rows.Add(NewRow("compatibility", "unavailable"));
        }
        try
        {
            IReadOnlyList<ExtensionManifest> extensions = host.Diagnostics.GetExtensions();
            int count = extensions?.Count ?? 0;
            rows.Add(NewRow("extensions_count", count.ToString()));
            if (extensions != null)
            {
                int shown = 0;
                foreach (ExtensionManifest manifest in extensions)
                {
                    if (manifest == null || shown >= 10) continue;
                    rows.Add(NewRow(
                        "extension." + (manifest.ExtensionId?.Value ?? "unknown"),
                        manifest.Version ?? "unknown"));
                    shown++;
                }
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("developer_report_rows_extensions_failed error=" + ex.Message);
            rows.Add(NewRow("extensions", "unavailable"));
        }
        return rows;
    }

    private static KeyValuePair<string, string> NewRow(string name, string value)
    {
        return new KeyValuePair<string, string>(name ?? string.Empty, value ?? string.Empty);
    }

    private static void AppendHealth(IDiagnosticsService diagnostics, StringBuilder builder)
    {
        try
        {
            HealthSnapshot health = diagnostics.GetHealth();
            if (health == null || health.Components == null || health.Components.Count == 0)
            {
                builder.AppendLine("健康=无组件");
                return;
            }
            builder.AppendLine("健康=" + health.Components.Count + " 项");
            foreach (HealthComponent component in health.Components)
            {
                if (component == null) continue;
                builder.AppendLine("- " + component.Id + "=" + component.Level.ToString()
                    + (string.IsNullOrWhiteSpace(component.Code) ? string.Empty : "(" + component.Code + ")"));
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("developer_report_health_failed error=" + ex.Message);
            builder.AppendLine("健康=诊断不可用");
        }
    }

    private static void AppendCompatibility(IDiagnosticsService diagnostics, StringBuilder builder)
    {
        try
        {
            IReadOnlyList<CapabilityDescriptor> report = diagnostics.GetCompatibilityReport();
            if (report == null || report.Count == 0)
            {
                builder.AppendLine("兼容报告=无条目");
                return;
            }
            builder.AppendLine("兼容报告=" + report.Count + " 项");
            int shown = 0;
            foreach (CapabilityDescriptor descriptor in report)
            {
                if (descriptor == null || shown >= 5) continue;
                builder.AppendLine("- " + (descriptor.Id?.Value ?? "unknown")
                    + "=" + (descriptor.Availability.ToString() ?? "unknown"));
                shown++;
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("developer_report_compatibility_failed error=" + ex.Message);
            builder.AppendLine("兼容报告=诊断不可用");
        }
    }

    private static void AppendExtensions(IDiagnosticsService diagnostics, StringBuilder builder)
    {
        try
        {
            IReadOnlyList<ExtensionManifest> extensions = diagnostics.GetExtensions();
            if (extensions == null || extensions.Count == 0)
            {
                builder.AppendLine("扩展=无条目");
                return;
            }
            builder.AppendLine("扩展=" + extensions.Count + " 项");
            int shown = 0;
            foreach (ExtensionManifest manifest in extensions)
            {
                if (manifest == null || shown >= 5) continue;
                builder.AppendLine("- " + (manifest.ExtensionId?.Value ?? "unknown") + "=" + manifest.Version);
                shown++;
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("developer_report_extensions_failed error=" + ex.Message);
            builder.AppendLine("扩展=诊断不可用");
        }
    }

    private static string BuildSceneKeyConflictText(AwakeConfig config)
    {
        if (config == null) return "config_unavailable";
        string shout = (config.SceneShoutKey ?? string.Empty).Trim().ToUpperInvariant();
        if (StringComparer.Ordinal.Equals(shout, "C"))
        {
            return "C 已由游戏占用";
        }
        if (StringComparer.Ordinal.Equals(shout, "U"))
        {
            return "U 与命令台冲突";
        }
        return "ok";
    }

    private static string Truncate(string value)
    {
        if (value == null) return string.Empty;
        if (Encoding.UTF8.GetByteCount(value) <= MaximumReportLength) return value;
        int bytes = 0;
        StringBuilder builder = new StringBuilder();
        TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(value);
        while (enumerator.MoveNext())
        {
            string element = enumerator.GetTextElement();
            int next = Encoding.UTF8.GetByteCount(element);
            if (bytes + next > MaximumReportLength) break;
            builder.Append(element);
            bytes += next;
        }
        return builder.ToString();
    }
}
