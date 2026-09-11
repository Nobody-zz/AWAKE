namespace MarcusAwakeFramework.Api;

public sealed class GeneratedAssetResult
{
	public AssetHandle Handle { get; }

	public string ResolvedModel { get; }

	public GeneratedAssetResult(AssetHandle handle, string resolvedModel)
	{
		Handle = handle;
		ResolvedModel = resolvedModel ?? string.Empty;
	}
}
