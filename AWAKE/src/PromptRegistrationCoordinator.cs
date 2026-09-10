using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;

namespace Awake;

internal static class PromptRegistrationCoordinator
{
    private static readonly object Gate = new object();
    private static readonly Dictionary<string, Task<OperationResult<bool>>> InFlight = new Dictionary<string, Task<OperationResult<bool>>>(StringComparer.Ordinal);
    private static readonly HashSet<string> Usable = new HashSet<string>(StringComparer.Ordinal);

    internal static async Task<OperationResult<bool>> EnsureAsync(
        string key,
        Func<CancellationToken, Task<OperationResult<bool>>> registerAsync,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key) || registerAsync == null)
        {
            return OperationResult<bool>.Failed(FrameworkErrors.Create(
                "awake.prompt_registration.invalid_request",
                FrameworkErrorCategory.InvalidRequest,
                "Prompt registration requires a key and callback.",
                null,
                owner: AwakeConstants.OwnerValue));
        }

        Task<OperationResult<bool>> registrationTask;
        lock (Gate)
        {
            if (Usable.Contains(key)) return OperationResult<bool>.Succeeded(true);
            if (!InFlight.TryGetValue(key, out registrationTask))
            {
                registrationTask = RegisterCoreAsync(registerAsync, cancellationToken);
                InFlight[key] = registrationTask;
            }
        }

        OperationResult<bool> result;
        try
        {
            result = await registrationTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            result = OperationResult<bool>.Failed(FrameworkErrors.Create(
                "awake.prompt_registration.cancelled",
                FrameworkErrorCategory.Cancelled,
                "Prompt registration was cancelled.",
                null,
                owner: AwakeConstants.OwnerValue));
        }
        catch (Exception ex)
        {
            result = OperationResult<bool>.Failed(FrameworkErrors.Create(
                "awake.prompt_registration.error",
                FrameworkErrorCategory.InternalFailure,
                ex.Message,
                null,
                owner: AwakeConstants.OwnerValue));
        }

        lock (Gate)
        {
            if (AiTaskConstants.IsPromptRegistrationUsable(result))
            {
                Usable.Add(key);
            }
            Task<OperationResult<bool>> current;
            if (InFlight.TryGetValue(key, out current) && ReferenceEquals(current, registrationTask))
            {
                InFlight.Remove(key);
            }
        }
        return result;
    }

    private static async Task<OperationResult<bool>> RegisterCoreAsync(
        Func<CancellationToken, Task<OperationResult<bool>>> registerAsync,
        CancellationToken cancellationToken)
    {
        return await registerAsync(cancellationToken).ConfigureAwait(false);
    }
}