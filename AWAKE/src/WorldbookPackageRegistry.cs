using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Awake;

internal sealed class WorldbookActivationState
{
    internal string ActivationId { get; set; } = "awake:activation:current";
    internal string Mode { get; set; } = "default";
    internal string PackageId { get; set; } = string.Empty;
    internal string Version { get; set; } = string.Empty;
    internal string PackageHash { get; set; } = string.Empty;
    internal string ManifestPath { get; set; } = string.Empty;

    internal JObject ToJson()
    {
        return new JObject
        {
            ["schemaVersion"] = "awake.worldbook.campaign-activation.v1",
            ["activationId"] = ActivationId,
            ["mode"] = Mode,
            ["world"] = new JObject
            {
                ["packageId"] = PackageId,
                ["version"] = Version,
                ["packageHash"] = PackageHash
            },
            ["extensions"] = new JArray(),
            ["contractVersion"] = "awake.worldbook.v2",
            ["revision"] = 1
        };
    }
}

internal sealed class WorldbookPackageRegistry
{
    private readonly string _registryPath;
    private readonly string _root;
    private readonly Dictionary<string, JObject> _packages = new Dictionary<string, JObject>(StringComparer.Ordinal);
    internal WorldbookVerifiedPackage LastSelectedPackage { get; private set; }

    private WorldbookPackageRegistry(string registryPath)
    {
        _registryPath = registryPath;
        _root = Path.GetDirectoryName(registryPath) ?? ".";
    }

    internal IReadOnlyDictionary<string, JObject> Packages => _packages;

    internal static WorldbookPackageRegistry Load(string registryPath)
    {
        if (string.IsNullOrWhiteSpace(registryPath)) throw new InvalidOperationException("WB2-MANIFEST-MISSING");
        var registry = new WorldbookPackageRegistry(registryPath);
        JObject document = JObject.Parse(File.ReadAllText(registryPath));
        if (!StringComparer.Ordinal.Equals(document["schemaVersion"]?.Value<string>(), "awake.worldbook.registry.v1"))
            throw new InvalidOperationException("WB2-SCHEMA-UNSUPPORTED:registry");
        foreach (JObject package in document["packages"]?.Children<JObject>() ?? Enumerable.Empty<JObject>())
        {
            string packageId = package["packageId"]?.Value<string>() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(packageId)) registry._packages[packageId] = package;
        }
        if (registry._packages.Count == 0) throw new InvalidOperationException("WB2-WORLD-CONFLICT:no_packages");
        return registry;
    }

    internal WorldbookActivationState Select(string requestedPackageId = null)
    {
        JObject package = null;
        if (!string.IsNullOrWhiteSpace(requestedPackageId))
        {
            _packages.TryGetValue(requestedPackageId, out package);
            if (package == null) throw new InvalidOperationException("WB2-WORLD-CONFLICT:requested_package_missing");
        }
        if (package == null)
        {
            package = _packages.Values
                .Where(x => StringComparer.Ordinal.Equals(x["kind"]?.Value<string>(), "universe"))
                .OrderByDescending(x => x["enabledByDefault"]?.Value<bool>() ?? false)
                .ThenBy(x => x["packageId"]?.Value<string>(), StringComparer.Ordinal)
                .FirstOrDefault();
        }
        if (package == null) throw new InvalidOperationException("WB2-WORLD-CONFLICT:no_universe");
        string packageId = package["packageId"]?.Value<string>() ?? string.Empty;
        string manifestPath = ResolvePackageManifest(package["relativePath"]?.Value<string>() ?? string.Empty);
        WorldbookVerifiedPackage verified = WorldbookPackageIntegrity.ReadAndVerify(
            manifestPath,
            packageId,
            package["version"]?.Value<string>(),
            package["kind"]?.Value<string>(),
            package["manifestHash"]?.Value<string>(),
            package["contentHash"]?.Value<string>(),
            package["packageHash"]?.Value<string>());
        LastSelectedPackage = verified;
        return new WorldbookActivationState
        {
            PackageId = packageId,
            Version = verified.Manifest["version"]?.Value<string>() ?? string.Empty,
            PackageHash = verified.PackageHash,
            ManifestPath = manifestPath
        };
    }

    private string ResolvePackageManifest(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath)) throw new InvalidOperationException("WB2-PATH-ESCAPE");
        string fullRoot = Path.GetFullPath(_root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string candidate = Path.GetFullPath(Path.Combine(_root, relativePath));
        if (!candidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("WB2-PATH-ESCAPE");
        if (Directory.Exists(candidate)) candidate = Path.Combine(candidate, "manifest.json");
        if (!File.Exists(candidate)) throw new FileNotFoundException("WB2-MANIFEST-MISSING", candidate);
        return candidate;
    }
}
