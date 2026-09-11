using System;

namespace MarcusAwakeFramework.Api;

public sealed class AssetContent
{
	private readonly byte[] _content;

	public AssetHandle Handle { get; }

	public int ByteLength => _content.Length;

	public AssetContent(AssetHandle handle, byte[] content)
	{
		Handle = handle ?? throw new ArgumentNullException("handle");
		if (content == null)
		{
			throw new ArgumentNullException("content");
		}
		_content = (byte[])content.Clone();
	}

	public byte[] GetContentCopy()
	{
		return (byte[])_content.Clone();
	}
}
