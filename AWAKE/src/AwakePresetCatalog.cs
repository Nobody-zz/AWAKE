using System.Collections.Generic;
using MCM.Abstractions;
using MCM.Abstractions.Base;

namespace Awake;

internal sealed class AwakeSettingsPreset : ISettingsPreset
{
    private readonly string _id;
    private readonly string _name;
    private readonly AwakeConfig _template;

    internal AwakeSettingsPreset(string id, string name, AwakeConfig template)
    {
        _id = id ?? string.Empty;
        _name = name ?? string.Empty;
        _template = template ?? new AwakeConfig();
    }

    public string SettingsId => AwakeConfig.SettingsId;
    public string Id => _id;
    public string Name => _name;

    public BaseSettings LoadPreset()
    {
        return _template;
    }

    public bool SavePreset(BaseSettings settings)
    {
        return false;
    }
}

internal static class AwakePresetCatalog
{
    internal static IEnumerable<ISettingsPreset> Build()
    {
        return new[]
        {
            new AwakeSettingsPreset(
                "default",
                "Default",
                new AwakeConfig
                {
                    EnableNpcProactive = true,
                    NpcProactiveChance = 35,
                    EnableEventEngine = true,
                    EnableCloudExport = true,
                    AllowCloudExportPlayerState = true,
                    EnableDeveloperMenu = false
                }),
            new AwakeSettingsPreset(
                "easy",
                "Easy",
                new AwakeConfig
                {
                    EnableNpcProactive = true,
                    NpcProactiveChance = 60,
                    EnableEventEngine = true,
                    EnableCloudExport = true,
                    AllowCloudExportPlayerState = true,
                    EnableDeveloperMenu = false
                }),
            new AwakeSettingsPreset(
                "standard",
                "Standard",
                new AwakeConfig
                {
                    EnableNpcProactive = true,
                    NpcProactiveChance = 35,
                    EnableEventEngine = true,
                    EnableCloudExport = true,
                    AllowCloudExportPlayerState = true,
                    EnableDeveloperMenu = false
                }),
            new AwakeSettingsPreset(
                "slow_burn",
                "Slow Burn",
                new AwakeConfig
                {
                    EnableNpcProactive = true,
                    NpcProactiveChance = 15,
                    EnableEventEngine = true,
                    EnableCloudExport = true,
                    AllowCloudExportPlayerState = true,
                    EnableDeveloperMenu = false
                }),
            new AwakeSettingsPreset(
                "strict",
                "Strict",
                new AwakeConfig
                {
                    EnableNpcProactive = false,
                    NpcProactiveChance = 5,
                    EnableEventEngine = true,
                    EnableCloudExport = true,
                    AllowCloudExportPlayerState = true,
                    EnableDeveloperMenu = false
                })
        };
    }
}
