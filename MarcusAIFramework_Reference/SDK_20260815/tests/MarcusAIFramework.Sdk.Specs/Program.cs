using System;
using MarcusAIFramework.Api;
using MarcusAIFramework.Sdk.FakeHost;
using MarcusAIFramework.Sdk.TestKit;

namespace MarcusAIFramework.Sdk.Specs
{
    internal static class Program
    {
        private static int Main()
        {
            FakeClock clock = new FakeClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            RequestContext context = clock.Context("sdk.specs", correlationId: "sdk-tool-correlation");
            FakePermissionMatrix permissions = new FakePermissionMatrix();
            FakeToolCandidateValidator tools = new FakeToolCandidateValidator(permissions);
            ToolDescriptor safe = new ToolDescriptor(
                "sdk.specs.tool.safe", "1", new ExtensionId("sdk.specs"),
                new SchemaRef("sdk.specs.tool.arguments", 1, 0), string.Empty, CommandRiskTier.R0Query);
            tools.Register(safe);
            ToolCandidate candidate = new ToolCandidate("candidate-1", safe.QualifiedId, "{}", 0, "fake-model");

            MafAssertions.Failed(tools.Validate(candidate, new[] { safe.QualifiedId }, context, 0), FrameworkErrorCategory.Denied, "tool.permission_denied");
            permissions.Grant("tool.invoke:" + safe.QualifiedId);
            MafAssertions.Succeeded(tools.Validate(candidate, new[] { safe.QualifiedId }, context, 0));
            MafAssertions.Failed(tools.Validate(candidate, new[] { "sdk.specs.tool.other@1" }, context, 0), FrameworkErrorCategory.Denied, "tool.not_allowlisted");
            MafAssertions.Failed(tools.Validate(new ToolCandidate("candidate-2", safe.QualifiedId, "[]", 0, "fake-model"), new[] { safe.QualifiedId }, context, 0), FrameworkErrorCategory.InvalidRequest, "tool.candidate_invalid");
            Console.WriteLine("PASS SDK FakeHost tool candidate, permission, allowlist, and error-contract specs");
            return 0;
        }
    }
}
