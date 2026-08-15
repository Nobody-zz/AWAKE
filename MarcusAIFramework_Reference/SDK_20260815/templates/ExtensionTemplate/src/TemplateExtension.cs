using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcusAIFramework.Api;

namespace YourCompany.YourExtension
{
    internal sealed class TemplateExtension : IFrameworkExtension
    {
        private static readonly ExtensionId Owner = new ExtensionId("YourCompany.YourExtension");
        private static readonly CapabilityId FactsId = new CapabilityId("capability://YourCompany.YourExtension/context/facts/v1");

        public ExtensionManifest Manifest { get; } = new ExtensionManifest(
            Owner,
            "YOUR_EXTENSION_NAME",
            "0.1.0",
            0,
            1,
            new[] { "1.4.8", "1.3.15" },
            new[] { "data.player_known.read", "ai.route.invoke:YourCompany.YourExtension.route.dialogue" },
            requiredCapabilities: null,
            optionalCapabilities: null,
            routeIds: new[] { "YourCompany.YourExtension.route.dialogue" });

        public void Register(IExtensionRegistration registration)
        {
            registration.RegisterCapability(
                new CapabilityDescriptor(
                    FactsId,
                    Owner,
                    "context",
                    new SchemaRef("your.extension.facts.request", 1, 0),
                    new SchemaRef("your.extension.facts.response", 1, 0),
                    CapabilityVisibility.Public,
                    CapabilityMaturity.Preview,
                    CapabilityAvailability.Available,
                    "any"),
                HandleFactsAsync);
        }

        public void OnLifecycle(ExtensionLifecycleStage stage, SessionRef session)
        {
            // Do not access Campaign.Current here. Keep session references owned by the extension.
        }

        private static Task<OperationResult<string>> HandleFactsAsync(
            string payloadJson,
            RequestContext context,
            CancellationToken cancellationToken)
        {
            if (context == null || context.IsExpired)
                return Task.FromResult(OperationResult<string>.Failed(FrameworkErrors.Create(
                    "your_extension.context_expired",
                    FrameworkErrorCategory.Expired,
                    "The request context expired.",
                    context?.CorrelationId,
                    owner: Owner.Value)));
            return Task.FromResult(OperationResult<string>.Succeeded("{\"source\":\"YourCompany.YourExtension\",\"facts\":[]}"));
        }
    }
}
