using System;
using MarcusAwakeFramework.Api;
using MarcusAwakeFramework.Tests.TestDoubles;

namespace MarcusAwakeFramework.Tests
{
    internal sealed class RuntimeServiceFixture : IDisposable
    {
        internal RuntimeServiceFixture(string suffix)
        {
            Identity = FrameworkIdentity.Current();
            Caller = new ExtensionId("awake.runtime.vertical");
            Routes = new FixtureRouteProfileResolver();
            Gateway = new FixtureAiGateway(Routes);
            Prompts = new FixturePromptService();
            Storage = new FixtureStorageService();
            Rag = new FixtureRagService();
            Egress = new FixtureEgressPolicy();
            Runtime = new RuntimeService("marcus-awake.runtime-service", new ApiVersion(1, 0), new[] { "ai.gateway", "prompt", "storage", "rag", "diagnostics" }, Gateway, () => FixtureLifecycleDriver.FixtureNow);
            Sessions = new SessionCoordinator(() => FixtureLifecycleDriver.FixtureNow);
            Reference = new SessionRef("campaign-runtime-" + suffix, "timeline-runtime-" + suffix, "session-runtime-" + suffix);
            Context = null;
            Host = new FrameworkHost(Identity, new InMemoryCapabilityBroker(), new InMemoryGameDataService(FixtureLifecycleDriver.FixtureDeadline), new ContextPlanner(), new InMemoryCommandService(), Sessions, new InMemorySaveAnchorStore(), new DiagnosticsService(Identity, Sessions, () => FixtureLifecycleDriver.FixtureNow), Runtime);
            AssertEx.True(Host.Register().IsSuccess, "Runtime fixture host registration failed.");
            AssertEx.True(Host.Ready().IsSuccess, "Runtime fixture host ready transition failed.");
            AssertEx.True(FrameworkHostLocator.Register(Host).IsSuccess, "Runtime fixture host locator registration failed.");
            Lease = Sessions.BeginSession(Reference).Value;
            Context = new RequestContext(Caller, Lease, "corr-runtime-" + suffix, FixtureLifecycleDriver.FixtureDeadline);
        }

        internal FrameworkIdentity Identity { get; }
        internal ExtensionId Caller { get; }
        internal SessionRef Reference { get; }
        internal SessionCoordinator Sessions { get; }
        internal SessionLease Lease { get; }
        internal RequestContext Context { get; }
        internal FrameworkHost Host { get; }
        internal RuntimeService Runtime { get; }
        internal FixtureRouteProfileResolver Routes { get; }
        internal FixtureAiGateway Gateway { get; }
        internal FixturePromptService Prompts { get; }
        internal FixtureStorageService Storage { get; }
        internal FixtureRagService Rag { get; }
        internal FixtureEgressPolicy Egress { get; }

        internal OperationResult<RuntimeServiceStatus> StartRuntime()
        {
            return Runtime.Start(new RuntimeServiceStartRequest("marcus-awake.runtime-service", new ApiVersion(1, 0), 65536), Context);
        }

        internal AiTaskRequest CreateTask(string taskId, string messageId, string providerId = "openai-compatible", string inputJson = "{\"topic\":\"world\"}", string idempotencyKey = null, RuntimeResourceBudget budget = null, DateTimeOffset? deadline = null)
        {
            return new AiTaskRequest(taskId, messageId, "dialogue", providerId, "default", inputJson, new SchemaRef("npc.reply", new ApiVersion(1, 0)), "public", deadline ?? FixtureLifecycleDriver.FixtureDeadline, false, budget ?? new RuntimeResourceBudget(1024, 4096, 512, 16), idempotencyKey ?? taskId);
        }

        public void Dispose()
        {
            if (Runtime.Status.State == RuntimeServiceState.Ready) Runtime.BeginDrain(Context);
            if (Runtime.Status.State == RuntimeServiceState.Draining)
            {
                for (var index = 0; index < RuntimeService.MaximumDrainSteps && Runtime.Status.State == RuntimeServiceState.Draining; index++) Runtime.CompleteDrain(Context);
            }
            if (Lease.State == SessionState.Ready)
            {
                var closing = Sessions.BeginClosing(Reference);
                if (closing.IsSuccess) Sessions.CompleteDrain(Reference, closing.Value.Generation);
            }
            if (Host.State == HostState.Ready) Host.BeginClosing();
            if (Host.State == HostState.Closing) Host.CompleteDrain();
            FrameworkHostLocator.Clear(Host);
        }
    }

    internal static class RuntimeServiceComposition
    {
        internal static RuntimeServiceFixture CreateFixture(string suffix) => new RuntimeServiceFixture(suffix);
    }
}
