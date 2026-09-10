using System.Net;

namespace PersonaWorkbench.Web;

internal static class ProviderEndpointGuard
{
    public static async Task<bool> IsSafeResolvedEndpointAsync(
        IProviderEndpointResolver endpointResolver,
        Uri endpoint,
        CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<IPAddress> addresses = await endpointResolver.ResolveAsync(endpoint, cancellationToken).ConfigureAwait(false);
            return ProviderEndpointPolicy.AreResolvedAddressesAllowed(endpoint, addresses);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }
}