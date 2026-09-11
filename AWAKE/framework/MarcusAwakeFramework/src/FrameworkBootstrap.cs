using System;
using System.Collections.Generic;

namespace MarcusAwakeFramework.Api
{
    internal enum BootstrapFaultPoint
    {
        None,
        AfterHostRegister,
        AfterHostReady,
        AfterLocatorRegister,
        AfterSessionBegin
    }

    internal sealed class FrameworkBootstrapResult
    {
        internal FrameworkBootstrapResult(FrameworkHost host, SessionLease session)
        {
            Host = host;
            Session = session;
        }

        internal FrameworkHost Host { get; }
        internal SessionLease Session { get; }
    }

    internal sealed class FrameworkBootstrap
    {
        private readonly Func<FrameworkHost> hostFactory;

        internal FrameworkBootstrap(Func<FrameworkHost> hostFactory)
        {
            this.hostFactory = hostFactory ?? throw new ArgumentNullException(nameof(hostFactory));
        }

        internal OperationResult<FrameworkBootstrapResult> Start(SessionRef reference, BootstrapFaultPoint faultPoint = BootstrapFaultPoint.None)
        {
            if (reference == null) return Failure("bootstrap.reference_missing", "A session reference is required.", "bootstrap-start");
            var host = hostFactory();
            var registered = host.Register();
            if (!registered.IsSuccess) return Rollback(host, null, registered.Error);
            if (faultPoint == BootstrapFaultPoint.AfterHostRegister) return Rollback(host, null, Injected("Host.Register"));

            var ready = host.Ready();
            if (!ready.IsSuccess) return Rollback(host, null, ready.Error);
            if (faultPoint == BootstrapFaultPoint.AfterHostReady) return Rollback(host, null, Injected("Host.Ready"));

            var located = FrameworkHostLocator.Register(host);
            if (!located.IsSuccess) return Rollback(host, null, located.Error);
            if (faultPoint == BootstrapFaultPoint.AfterLocatorRegister) return Rollback(host, null, Injected("Locator.Register"));

            var session = host.Sessions.BeginSession(reference);
            if (!session.IsSuccess) return Rollback(host, null, session.Error);
            if (faultPoint == BootstrapFaultPoint.AfterSessionBegin) return Rollback(host, session.Value, Injected("Session.BeginSession"));
            return OperationResult<FrameworkBootstrapResult>.Succeeded(new FrameworkBootstrapResult(host, session.Value));
        }

        private static OperationResult<FrameworkBootstrapResult> Rollback(FrameworkHost host, SessionLease session, FrameworkError failure)
        {
            if (session != null)
            {
                var closing = host.Sessions.BeginClosing(session.Reference);
                if (closing.IsSuccess) host.Sessions.CompleteDrain(session.Reference, closing.Value.Generation);
            }
            FrameworkHostLocator.Clear(host);
            host.AbortRegistration();
            return OperationResult<FrameworkBootstrapResult>.Failed(failure);
        }

        private static FrameworkError Injected(string stage)
        {
            return FrameworkErrors.Create("bootstrap.injected_failure", FrameworkErrorCategory.Conflict, "The fixture injected a bootstrap failure.", "bootstrap-" + stage, details: new Dictionary<string, string> { { "stage", stage } });
        }

        private static OperationResult<FrameworkBootstrapResult> Failure(string code, string fallback, string correlationId)
        {
            return OperationResult<FrameworkBootstrapResult>.Failed(FrameworkErrors.Create(code, FrameworkErrorCategory.InvalidRequest, fallback, correlationId));
        }
    }
}
