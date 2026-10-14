using System;

namespace Pw.Hub.Services;

public static class PwNavigationPolicy
{
	public static bool IsAllowedUrl(string? value)
	{
		if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
			|| (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
		{
			return false;
		}
		string host = uri.IdnHost.ToLowerInvariant();
		return host == "pwonline.ru" || host.EndsWith(".pwonline.ru", StringComparison.Ordinal)
			|| host == "pw.mail.ru" || host.EndsWith(".pw.mail.ru", StringComparison.Ordinal);
	}

	public static string? BuildAllowedUrl(string? input)
	{
		if (string.IsNullOrWhiteSpace(input))
		{
			return null;
		}
		string value = input.Trim();
		if (value.StartsWith("/", StringComparison.Ordinal))
		{
			value = "https://pwonline.ru" + value;
		}
		else if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
			&& !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
		{
			value = "https://" + value;
		}
		return IsAllowedUrl(value) ? value : null;
	}
}
