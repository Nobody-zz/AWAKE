using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeFramework.Api;

public interface IPromptRegistry
{
	Task<OperationResult<bool>> RegisterAsync(PromptDefinition definition, RequestContext context, CancellationToken cancellationToken);

	Task<OperationResult<PromptCompilation>> CompileAsync(PromptCompileRequest request, RequestContext context, CancellationToken cancellationToken);
}
