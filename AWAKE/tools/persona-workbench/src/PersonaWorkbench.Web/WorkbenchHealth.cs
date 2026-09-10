using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace PersonaWorkbench.Web;

public sealed class WorkbenchHealth
{
    [JsonPropertyName("ok")] public bool Ok { get; init; }
    [JsonPropertyName("workstation_id")] public string WorkstationId { get; init; } = string.Empty;
    [JsonPropertyName("instance_id")] public string InstanceId { get; init; } = string.Empty;
    [JsonPropertyName("protocol_version")] public string ProtocolVersion { get; init; } = string.Empty;
    [JsonPropertyName("build_id")] public string BuildId { get; init; } = string.Empty;
    [JsonPropertyName("workspace_id")] public string WorkspaceId { get; init; } = string.Empty;
    [JsonPropertyName("state")] public string State { get; init; } = string.Empty;
    [JsonPropertyName("product")] public string Product { get; init; } = string.Empty;
    [JsonPropertyName("protocolVersion")] public string LegacyProtocolVersion { get; init; } = string.Empty;
    [JsonPropertyName("instanceId")] public string LegacyInstanceId { get; init; } = string.Empty;
    [JsonPropertyName("workspaceHash")] public string LegacyWorkspaceHash { get; init; } = string.Empty;
    [JsonPropertyName("port")] public int Port { get; init; }

    public static WorkbenchHealth Create(string instanceId, int port, string state = "ready")
    {
        const string workspaceId = "persona-workbench";
        const string protocolVersion = "1";
        string buildId = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            ?? "development";
        string workspaceHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(workspaceId)))[..16].ToLowerInvariant();

        return new WorkbenchHealth
        {
            Ok = string.Equals(state, "ready", StringComparison.Ordinal),
            Product = "AWAKE.PersonaWorkbench",
            WorkstationId = "persona_workbench",
            InstanceId = instanceId,
            ProtocolVersion = protocolVersion,
            BuildId = buildId,
            WorkspaceId = workspaceId,
            State = state,
            LegacyProtocolVersion = protocolVersion,
            LegacyInstanceId = instanceId,
            LegacyWorkspaceHash = workspaceHash,
            Port = port,
        };
    }
}
