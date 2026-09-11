using System;

namespace MarcusAwakeFramework.Api;

public sealed class SqlParameterValue
{
	public string Name { get; }

	public string ValueJson { get; }

	public SqlParameterValue(string name, string valueJson)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			throw new ArgumentException("Parameter name is required.", "name");
		}
		Name = name;
		ValueJson = valueJson ?? "null";
	}
}
