using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Pw.Hub.Infrastructure;

public static class LuaApiRegistry
{
	public sealed class Entry
	{
		public string Name { get; init; } = string.Empty;

		public string Version { get; init; } = "v1";

		public string Category { get; init; } = string.Empty;

		public string Signature { get; init; } = string.Empty;

		public string Description { get; init; } = string.Empty;

		public string Snippet { get; init; } = string.Empty;
	}

	private static readonly object _lock = new object();

	private static List<Entry> _entries = new List<Entry>();

	private static bool _initializedFromAttributes;

	public static void ReplaceAll(IEnumerable<Entry> entries)
	{
		if (entries == null)
		{
			return;
		}
		lock (_lock)
		{
			_entries = (from e in entries
				where !string.IsNullOrWhiteSpace(e?.Name)
				group e by e.Name into g
				select g.First() into e
				orderby e.Category, e.Name
				select e).ToList();
			_initializedFromAttributes = true;
		}
	}

	private static void EnsureInitialized()
	{
		lock (_lock)
		{
			if (_initializedFromAttributes && _entries.Count > 0)
			{
				return;
			}
			if (_entries.Count == 0)
			{
				try
				{
					List<Entry> list = new List<Entry>();
					Assembly[] array = (from a in AppDomain.CurrentDomain.GetAssemblies()
						where !a.IsDynamic
						where a.GetName().Name?.StartsWith("Pw.") ?? false
						select a).ToArray();
					for (int num = 0; num < array.Length; num++)
					{
						Type[] types = array[num].GetTypes();
						for (int num2 = 0; num2 < types.Length; num2++)
						{
							MethodInfo[] methods = types[num2].GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
							foreach (MethodInfo methodInfo in methods)
							{
								LuaApiFunctionAttribute customAttribute = methodInfo.GetCustomAttribute<LuaApiFunctionAttribute>(inherit: false);
								if (customAttribute != null)
								{
									string name = (string.IsNullOrWhiteSpace(customAttribute.Name) ? methodInfo.Name : customAttribute.Name);
									list.Add(new Entry
									{
										Name = name,
										Version = (customAttribute.Version ?? "v1"),
										Category = (customAttribute.Category ?? string.Empty),
										Signature = (customAttribute.Signature ?? string.Empty),
										Description = (customAttribute.Description ?? string.Empty),
										Snippet = (customAttribute.Snippet ?? string.Empty)
									});
								}
							}
						}
					}
					_entries = (from e in list
						where !string.IsNullOrWhiteSpace(e.Name)
						group e by e.Name into g
						select g.First() into e
						orderby e.Category, e.Name
						select e).ToList();
				}
				catch
				{
				}
			}
			_initializedFromAttributes = true;
		}
	}

	public static IReadOnlyList<Entry> GetAll()
	{
		EnsureInitialized();
		lock (_lock)
		{
			return _entries.ToList();
		}
	}

	public static string ToCheatSheetText(int examplesLimitPerCategory = 3)
	{
		EnsureInitialized();
		List<Entry> list;
		lock (_lock)
		{
			list = _entries.ToList();
		}
		if (list.Count == 0)
		{
			return "No Lua API functions are available (the catalog is empty).";
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("Available Lua API functions (v1 and v2):");
		foreach (IGrouping<string, Entry> item in from e in list
			group e by e.Category)
		{
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder3 = stringBuilder2;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(3, 1, stringBuilder2);
			handler.AppendLiteral("\n[");
			handler.AppendFormatted(item.Key);
			handler.AppendLiteral("]");
			stringBuilder3.AppendLine(ref handler);
			foreach (Entry item2 in item)
			{
				string value = (string.IsNullOrWhiteSpace(item2.Version) ? "" : (" (" + item2.Version + ")"));
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder4 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(7, 4, stringBuilder2);
				handler.AppendLiteral("- ");
				handler.AppendFormatted(item2.Name);
				handler.AppendFormatted(value);
				handler.AppendLiteral(" — ");
				handler.AppendFormatted(item2.Signature);
				handler.AppendLiteral(". ");
				handler.AppendFormatted(item2.Description);
				stringBuilder4.AppendLine(ref handler);
			}
			List<Entry> list2 = item.Take(examplesLimitPerCategory).ToList();
			if (list2.Count <= 0)
			{
				continue;
			}
			stringBuilder.AppendLine("Examples:");
			foreach (Entry item3 in list2)
			{
				if (!string.IsNullOrWhiteSpace(item3.Snippet))
				{
					stringBuilder.AppendLine();
					stringBuilder.AppendLine(item3.Snippet);
				}
			}
		}
		return stringBuilder.ToString();
	}
}
