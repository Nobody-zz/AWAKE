using System;

namespace MarcusAwakeFramework.Api
{
    public sealed class FrameworkIdentity
    {
        public FrameworkIdentity(string productId, string assemblyName, ApiVersion apiVersion, string frameworkVersion, string bannerlordApi)
        {
            ProductId = ContractGuard.Id(productId, nameof(productId));
            AssemblyName = ContractGuard.Id(assemblyName, nameof(assemblyName));
            ApiVersion = apiVersion ?? throw new ArgumentNullException(nameof(apiVersion));
            FrameworkVersion = ContractGuard.Id(frameworkVersion, nameof(frameworkVersion));
            BannerlordApi = ContractGuard.Id(bannerlordApi, nameof(bannerlordApi));
        }

        public string ProductId { get; }
        public string AssemblyName { get; }
        public ApiVersion ApiVersion { get; }
        public string FrameworkVersion { get; }
        public string BannerlordApi { get; }

        public static FrameworkIdentity Current(string bannerlordApi = "1.3.15") =>
            new FrameworkIdentity("awake.framework", "MarcusAwakeFramework", new ApiVersion(2, 0), "0.1.0", bannerlordApi);
    }
}
