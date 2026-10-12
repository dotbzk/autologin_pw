using System.Threading.Tasks;
using Microsoft.Web.WebView2.Wpf;

namespace Pw.Hub.Abstractions;

public interface IWebViewHost
{
	WebView2 Current { get; }

	Task ReplaceAsync(WebView2 newControl);

	Task PreloadAsync(WebView2 newControl);

	Task FinalizeSwapAsync(WebView2 newControl);
}
