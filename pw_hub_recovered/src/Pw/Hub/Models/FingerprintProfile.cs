using System.Collections.Generic;

namespace Pw.Hub.Models;

public class FingerprintProfile
{
	public string UserAgent { get; set; }

	public string Platform { get; set; }

	public string Vendor { get; set; }

	public string VendorSub { get; set; } = string.Empty;

	public string Product { get; set; } = "Gecko";

	public string ProductSub { get; set; } = "20030107";

	public List<string> Languages { get; set; } = new List<string>();

	public string AcceptLanguageHeader { get; set; }

	public int TimezoneOffsetMinutes { get; set; }

	public int HardwareConcurrency { get; set; } = 8;

	public double DeviceMemory { get; set; } = 8.0;

	public int ScreenWidth { get; set; } = 1920;

	public int ScreenHeight { get; set; } = 1080;

	public int ColorDepth { get; set; } = 24;

	public bool WebglEnabled { get; set; } = true;

	public string WebglVendor { get; set; }

	public string WebglRenderer { get; set; }

	public double CanvasNoise { get; set; } = 0.33;
}
