using System;
using System.Collections.Generic;

namespace MarcusAwakeFramework.Api;

public sealed class SqlQueryResult
{
	public IReadOnlyList<string> Columns { get; }

	public IReadOnlyList<IReadOnlyList<string>> Rows { get; }

	public bool Truncated { get; }

	public SqlQueryResult(IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<string>> rows, bool truncated)
	{
		Columns = columns ?? Array.Empty<string>();
		Rows = rows ?? Array.Empty<IReadOnlyList<string>>();
		Truncated = truncated;
	}
}
