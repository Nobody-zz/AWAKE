using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeFramework.Api;

public interface IRawSqlSession
{
	Task<OperationResult<int>> ExecuteAsync(string sql, IReadOnlyList<SqlParameterValue> parameters, RequestContext context, CancellationToken cancellationToken);

	Task<OperationResult<SqlQueryResult>> QueryAsync(string sql, IReadOnlyList<SqlParameterValue> parameters, int maximumRows, RequestContext context, CancellationToken cancellationToken);
}
