using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeFramework.Api
{
    internal interface ICompatibilityDiagnosticsService
    {
        HealthSnapshot GetHealth();
        IReadOnlyList<CapabilityDescriptor> GetCompatibilityReport();
        IReadOnlyList<ExtensionManifest> GetExtensions();
    }

    internal interface ICompatibilityGameDataService
    {
        Task<OperationResult<PlayerSnapshotDto>> GetCurrentPlayerAsync(RequestContext context, CancellationToken cancellationToken);
    }

    internal interface ICompatibilityCommandService
    {
        Task<OperationResult<CommandPreflight>> PreflightAsync(CommandRequest request, RequestContext context, CancellationToken cancellationToken);
        Task<OperationResult<CommandReceipt>> SubmitAsync(CommandRequest request, RequestContext context, CancellationToken cancellationToken);
    }

    public static class ApiCompatibilityExtensions
    {
        public static HealthSnapshot GetHealth(this IDiagnosticsService diagnostics)
        {
            ICompatibilityDiagnosticsService compatible = diagnostics as ICompatibilityDiagnosticsService;
            return compatible?.GetHealth() ?? new HealthSnapshot(DateTimeOffset.UtcNow, new[]
            {
                new HealthComponent("diagnostics", HealthLevel.Unknown, "diagnostics_unavailable", "Diagnostics service does not expose the legacy health projection.")
            });
        }

        public static IReadOnlyList<CapabilityDescriptor> GetCompatibilityReport(this IDiagnosticsService diagnostics)
        {
            ICompatibilityDiagnosticsService compatible = diagnostics as ICompatibilityDiagnosticsService;
            return compatible?.GetCompatibilityReport() ?? new CapabilityDescriptor[0];
        }

        public static IReadOnlyList<ExtensionManifest> GetExtensions(this IDiagnosticsService diagnostics)
        {
            ICompatibilityDiagnosticsService compatible = diagnostics as ICompatibilityDiagnosticsService;
            return compatible?.GetExtensions() ?? new ExtensionManifest[0];
        }

        public static Task<OperationResult<PlayerSnapshotDto>> GetCurrentPlayerAsync(this IGameDataService gameData, RequestContext context, CancellationToken cancellationToken)
        {
            ICompatibilityGameDataService compatible = gameData as ICompatibilityGameDataService;
            return compatible?.GetCurrentPlayerAsync(context, cancellationToken)
                ?? Task.FromResult(OperationResult<PlayerSnapshotDto>.Failed(FrameworkErrors.Create(
                    "game_data.unavailable",
                    FrameworkErrorCategory.Unavailable,
                    "Current player data is not available.",
                    context?.CorrelationId ?? "game-data",
                    retryable: true)));
        }

        public static Task<OperationResult<CommandPreflight>> PreflightAsync(this ICommandService commands, CommandRequest request, RequestContext context, CancellationToken cancellationToken)
        {
            ICompatibilityCommandService compatible = commands as ICompatibilityCommandService;
            return compatible?.PreflightAsync(request, context, cancellationToken)
                ?? Task.FromResult(OperationResult<CommandPreflight>.Failed(FrameworkErrors.Create(
                    "command.unavailable",
                    FrameworkErrorCategory.Unavailable,
                    "Command service is not available.",
                    context?.CorrelationId ?? "command-preflight",
                    retryable: true)));
        }

        public static Task<OperationResult<CommandReceipt>> SubmitAsync(this ICommandService commands, CommandRequest request, RequestContext context, CancellationToken cancellationToken)
        {
            ICompatibilityCommandService compatible = commands as ICompatibilityCommandService;
            return compatible?.SubmitAsync(request, context, cancellationToken)
                ?? Task.FromResult(OperationResult<CommandReceipt>.Failed(FrameworkErrors.Create(
                    "command.unavailable",
                    FrameworkErrorCategory.Unavailable,
                    "Command service is not available.",
                    context?.CorrelationId ?? "command-submit",
                    retryable: true)));
        }
    }
}