using System;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeFramework.Api
{
    /// <summary>
    /// 公开的玩家数据出口。独立编译的扩展实现本接口后交给框架，
    /// 由框架内部把它适配成兼容层所需的 ICompatibilityGameDataService。
    /// </summary>
    public interface IPlayerSnapshotProvider
    {
        Task<OperationResult<PlayerSnapshotDto>> GetCurrentPlayerAsync(RequestContext context, CancellationToken cancellationToken);
    }

    /// <summary>
    /// 把公开的 IPlayerSnapshotProvider 适配成宿主服务面。
    /// 该类型是框架内部实现，扩展只需提供 provider 实例。
    /// </summary>
    internal sealed class ProviderBackedGameDataService : IGameDataService, ICompatibilityGameDataService
    {
        private readonly IPlayerSnapshotProvider provider;

        public ProviderBackedGameDataService(IPlayerSnapshotProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public OperationResult<Page<DynamicEntityDto>> Query(GameDataQuery query, RequestContext context)
        {
            return HostUnavailable.Failure<Page<DynamicEntityDto>>("game_data", "query", context);
        }

        public Task<OperationResult<PlayerSnapshotDto>> GetCurrentPlayerAsync(RequestContext context, CancellationToken cancellationToken)
        {
            return provider.GetCurrentPlayerAsync(context, cancellationToken);
        }
    }

    /// <summary>
    /// 可选的服务覆盖包。任何属性为 null 即表示「未提供」，框架保持对应的 Unavailable 存根，
    /// 不做任何上游探测、优先级推断或降级放行。
    /// </summary>
    public sealed class FrameworkServiceOverrides
    {
        public IPermissionService Permissions { get; set; }

        public IPromptRegistry Prompts { get; set; }

        public IStorageService Storage { get; set; }

        public IRagService Rag { get; set; }

        public IPlayerSnapshotProvider GameData { get; set; }
    }
}
