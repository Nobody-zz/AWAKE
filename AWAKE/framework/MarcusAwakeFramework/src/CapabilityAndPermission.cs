using System;
using System.Collections.Generic;

namespace MarcusAwakeFramework.Api
{
    public sealed class ExtensionDependency
    {
        public ExtensionDependency(ExtensionId extensionId, ApiVersion minimumApiVersion)
        {
            ExtensionId = extensionId ?? throw new ArgumentNullException(nameof(extensionId));
            MinimumApiVersion = minimumApiVersion ?? throw new ArgumentNullException(nameof(minimumApiVersion));
        }

        public ExtensionId ExtensionId { get; }
        public ApiVersion MinimumApiVersion { get; }
    }

    public sealed class ExtensionManifest
    {
        private readonly IReadOnlyList<string> routeIds;

        public ExtensionManifest(ExtensionId extensionId, ApiVersion apiVersion, IReadOnlyList<CapabilityId> capabilities, IReadOnlyList<string> requestedPermissions, IReadOnlyList<ExtensionDependency> dependencies)
        {
            ExtensionId = extensionId ?? throw new ArgumentNullException(nameof(extensionId));
            ApiVersion = apiVersion ?? throw new ArgumentNullException(nameof(apiVersion));
            Capabilities = capabilities ?? new CapabilityId[0];
            RequestedPermissions = requestedPermissions ?? new string[0];
            Dependencies = dependencies ?? new ExtensionDependency[0];
            DisplayNameTextId = extensionId.Value;
            Version = apiVersion.ToString();
            MinimumPublicApiMajor = apiVersion.Major;
            MaximumPublicApiMajorExclusive = apiVersion.Major + 1;
            BannerlordApiAllowlist = new string[0];
            RequiredCapabilities = Capabilities;
            OptionalCapabilities = new CapabilityId[0];
            routeIds = new string[0];
        }

        public ExtensionManifest(ExtensionId extensionId, string displayNameTextId, string version, int minimumPublicApiMajor, int maximumPublicApiMajorExclusive, IReadOnlyList<string> bannerlordApiAllowlist, IReadOnlyList<string> requestedPermissions, IReadOnlyList<CapabilityId> requiredCapabilities = null, IReadOnlyList<CapabilityId> optionalCapabilities = null)
            : this(extensionId, displayNameTextId, version, minimumPublicApiMajor, maximumPublicApiMajorExclusive, bannerlordApiAllowlist, requestedPermissions, requiredCapabilities, optionalCapabilities, new string[0])
        {
        }

        public ExtensionManifest(ExtensionId extensionId, string displayNameTextId, string version, int minimumPublicApiMajor, int maximumPublicApiMajorExclusive, IReadOnlyList<string> bannerlordApiAllowlist, IReadOnlyList<string> requestedPermissions, IReadOnlyList<CapabilityId> requiredCapabilities, IReadOnlyList<CapabilityId> optionalCapabilities, IReadOnlyList<string> routeIds)
        {
            ExtensionId = extensionId ?? throw new ArgumentNullException(nameof(extensionId));
            DisplayNameTextId = displayNameTextId ?? string.Empty;
            Version = string.IsNullOrWhiteSpace(version) ? "0.0.0" : version;
            MinimumPublicApiMajor = minimumPublicApiMajor;
            MaximumPublicApiMajorExclusive = maximumPublicApiMajorExclusive;
            BannerlordApiAllowlist = bannerlordApiAllowlist ?? new string[0];
            RequestedPermissions = requestedPermissions ?? new string[0];
            RequiredCapabilities = requiredCapabilities ?? new CapabilityId[0];
            OptionalCapabilities = optionalCapabilities ?? new CapabilityId[0];
            this.routeIds = routeIds ?? new string[0];
            ApiVersion = new ApiVersion(Math.Max(0, minimumPublicApiMajor), 0);
            Capabilities = RequiredCapabilities;
            Dependencies = new ExtensionDependency[0];
        }

        public ExtensionId ExtensionId { get; }
        public ApiVersion ApiVersion { get; }
        public IReadOnlyList<CapabilityId> Capabilities { get; }
        public IReadOnlyList<string> RequestedPermissions { get; }
        public IReadOnlyList<ExtensionDependency> Dependencies { get; }
        public string DisplayNameTextId { get; }
        public string Version { get; }
        public int MinimumPublicApiMajor { get; }
        public int MaximumPublicApiMajorExclusive { get; }
        public IReadOnlyList<string> BannerlordApiAllowlist { get; }
        public IReadOnlyList<CapabilityId> RequiredCapabilities { get; }
        public IReadOnlyList<CapabilityId> OptionalCapabilities { get; }
        public IReadOnlyList<string> RouteIds => routeIds;
    }

    public sealed class CapabilityDescriptor
    {
        public CapabilityDescriptor(CapabilityId id, ExtensionId owner, ApiVersion version, string kind, IReadOnlyList<CapabilityId> dependencies)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Version = version ?? throw new ArgumentNullException(nameof(version));
            Kind = ContractGuard.Id(kind, nameof(kind));
            Dependencies = dependencies ?? new CapabilityId[0];
            Visibility = CapabilityVisibility.Public;
            Maturity = CapabilityMaturity.Preview;
            Availability = CapabilityAvailability.Available;
            ThreadAffinity = "any";
            InputSchema = null;
            OutputSchema = null;
        }

        public CapabilityDescriptor(CapabilityId id, ExtensionId owner, string kind, SchemaRef inputSchema, SchemaRef outputSchema, CapabilityVisibility visibility, CapabilityMaturity maturity, CapabilityAvailability availability, string threadAffinity, string unavailableReason = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Kind = kind ?? string.Empty;
            InputSchema = inputSchema;
            OutputSchema = outputSchema;
            Visibility = visibility;
            Maturity = maturity;
            Availability = availability;
            ThreadAffinity = threadAffinity ?? "any";
            UnavailableReason = unavailableReason;
            Version = inputSchema == null ? new ApiVersion(1, 0) : new ApiVersion(inputSchema.Major, inputSchema.Minor);
            Dependencies = new CapabilityId[0];
        }

        public CapabilityId Id { get; }
        public ExtensionId Owner { get; }
        public ApiVersion Version { get; }
        public string Kind { get; }
        public IReadOnlyList<CapabilityId> Dependencies { get; }
        public SchemaRef InputSchema { get; }
        public SchemaRef OutputSchema { get; }
        public CapabilityVisibility Visibility { get; }
        public CapabilityMaturity Maturity { get; }
        public CapabilityAvailability Availability { get; }
        public string ThreadAffinity { get; }
        public string UnavailableReason { get; }
    }

    public interface ICapabilityBroker
    {
        OperationResult<CapabilityDescriptor> Register(ExtensionManifest manifest, CapabilityDescriptor descriptor);
        OperationResult<CapabilityDescriptor> Resolve(CapabilityId id);
        IReadOnlyList<CapabilityDescriptor> Discover();
    }

    public static class CapabilityGraphValidator
    {
        public static OperationResult<bool> Validate(IReadOnlyList<CapabilityDescriptor> descriptors, string correlationId = "capability-graph")
        {
            if (descriptors == null) return OperationResult<bool>.Failed(FrameworkErrors.Create("capability.graph_invalid", FrameworkErrorCategory.InvalidRequest, "Capability descriptors are required.", correlationId));
            var byId = new Dictionary<string, CapabilityDescriptor>(StringComparer.Ordinal);
            for (var index = 0; index < descriptors.Count; index++)
            {
                var descriptor = descriptors[index];
                if (descriptor == null) return OperationResult<bool>.Failed(FrameworkErrors.Create("capability.graph_invalid", FrameworkErrorCategory.InvalidRequest, "Capability descriptors cannot contain null.", correlationId));
                if (byId.ContainsKey(descriptor.Id.Value)) return OperationResult<bool>.Failed(FrameworkErrors.Create("capability.duplicate", FrameworkErrorCategory.Conflict, "Capability IDs must be unique.", correlationId));
                byId.Add(descriptor.Id.Value, descriptor);
            }
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (var descriptor in descriptors)
            {
                var missing = FindCycleOrMissing(descriptor, byId, visiting, visited);
                if (missing != null) return OperationResult<bool>.Failed(FrameworkErrors.Create(missing.StartsWith("missing:", StringComparison.Ordinal) ? "capability.missing_dependency" : "capability.cycle", FrameworkErrorCategory.Conflict, missing, correlationId));
            }
            return OperationResult<bool>.Succeeded(true);
        }

        private static string FindCycleOrMissing(CapabilityDescriptor descriptor, Dictionary<string, CapabilityDescriptor> byId, HashSet<string> visiting, HashSet<string> visited)
        {
            if (visited.Contains(descriptor.Id.Value)) return null;
            if (!visiting.Add(descriptor.Id.Value)) return "cycle:" + descriptor.Id.Value;
            for (var index = 0; index < descriptor.Dependencies.Count; index++)
            {
                var dependency = descriptor.Dependencies[index];
                CapabilityDescriptor dependencyDescriptor;
                if (!byId.TryGetValue(dependency.Value, out dependencyDescriptor)) return "missing:" + dependency.Value;
                var failure = FindCycleOrMissing(dependencyDescriptor, byId, visiting, visited);
                if (failure != null) return failure;
            }
            visiting.Remove(descriptor.Id.Value);
            visited.Add(descriptor.Id.Value);
            return null;
        }
    }

    public sealed class PermissionCatalog
    {
        private readonly HashSet<string> known = new HashSet<string>(StringComparer.Ordinal);
        public void Register(string permission) { known.Add(ContractGuard.Id(permission, nameof(permission))); }
        public bool Contains(string permission) => permission != null && known.Contains(permission);
    }

    public interface IPermissionGate
    {
        OperationResult<bool> Evaluate(string permission, RequestContext context);
        void Grant(string permission);
        void Revoke(string permission);
    }

    public sealed class PermissionGate : IPermissionGate
    {
        private readonly PermissionCatalog catalog;
        private readonly HashSet<string> grants = new HashSet<string>(StringComparer.Ordinal);
        public PermissionGate(PermissionCatalog catalog) { this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog)); }
        public void Grant(string permission) { if (!catalog.Contains(permission)) throw new InvalidOperationException("Unknown permissions cannot be granted."); grants.Add(permission); }
        public void Revoke(string permission) { grants.Remove(permission ?? string.Empty); }
        public OperationResult<bool> Evaluate(string permission, RequestContext context)
        {
            var correlation = context?.CorrelationId ?? "permission-evaluate";
            if (context == null) return OperationResult<bool>.Failed(FrameworkErrors.Create("permission.context_missing", FrameworkErrorCategory.InvalidRequest, "A request context is required.", correlation));
            if (!catalog.Contains(permission)) return OperationResult<bool>.Failed(FrameworkErrors.Create("permission.unknown", FrameworkErrorCategory.Denied, "Unknown permissions fail closed.", correlation));
            if (!grants.Contains(permission)) return OperationResult<bool>.Failed(FrameworkErrors.Create("permission.denied", FrameworkErrorCategory.Denied, "Permission is not granted.", correlation));
            return OperationResult<bool>.Succeeded(true);
        }
    }
}