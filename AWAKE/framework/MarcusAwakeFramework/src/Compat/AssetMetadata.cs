using System;

namespace MarcusAwakeFramework.Api;

public sealed class AssetMetadata
{
	public AssetHandle Handle { get; }

	public DateTimeOffset CreatedUtc { get; }

	public bool Pinned { get; }

	public AssetMetadata(AssetHandle handle, DateTimeOffset createdUtc, bool pinned)
	{
		Handle = handle ?? throw new ArgumentNullException("handle");
		CreatedUtc = createdUtc;
		Pinned = pinned;
	}
}
