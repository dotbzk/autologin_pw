using System;

namespace Pw.Hub.Infrastructure;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class LuaApiFunctionAttribute : Attribute
{
	public string? Name { get; init; }

	public string Version { get; init; } = "v1";

	public string Category { get; init; } = string.Empty;

	public string Signature { get; init; } = string.Empty;

	public string Description { get; init; } = string.Empty;

	public string Snippet { get; init; } = string.Empty;
}
