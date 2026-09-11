using System;
using System.Collections.Generic;

namespace MarcusAwakeFramework.Api;

public sealed class AssetListPage
{
	public IReadOnlyList<AssetMetadata> Items { get; }

	public string NextCursor { get; }

	public AssetListPage(IReadOnlyList<AssetMetadata> items, string nextCursor)
	{
		Items = items ?? Array.Empty<AssetMetadata>();
		NextCursor = nextCursor ?? string.Empty;
	}
}
