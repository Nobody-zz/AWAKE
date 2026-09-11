using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace MarcusAwakeRuntimeService;

[SupportedOSPlatform("windows")]
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            RuntimeServiceLog.Enable();
            ServiceArguments.Validate(args);
            return await new RuntimeServiceHost().RunAsync().ConfigureAwait(false);
        }
        catch (ServiceStartupException exception)
        {
            Console.Error.WriteLine(exception.Code);
            return exception.ExitCode;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("service_failed:" + exception.GetType().Name + ":correlation=unavailable");
            return 1;
        }
    }
}

internal static class ServiceArguments
{
    internal static void Validate(string[] args)
    {
        if (args == null || args.Length != 0)
        {
            throw new ServiceStartupException("stdin_bootstrap_only", 2);
        }
    }
}

internal sealed class ServiceStartupException : Exception
{
    internal ServiceStartupException(string code, int exitCode) : base(code)
    {
        Code = code;
        ExitCode = exitCode;
    }

    internal string Code { get; }
    internal int ExitCode { get; }
}
