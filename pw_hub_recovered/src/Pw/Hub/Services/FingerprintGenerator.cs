using System;
using System.Collections.Generic;
using Pw.Hub.Models;

namespace Pw.Hub.Services;

public static class FingerprintGenerator
{
	private static readonly string[] UaPool = new string[5] { "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36" };

	private static readonly string[] Platforms = new string[2] { "Win32", "Windows" };

	private static readonly string[] Vendors = new string[2] { "Google Inc.", "Google LLC" };

	private static readonly (string vendor, string renderer)[] GpuPairs = new(string, string)[4]
	{
		("Google Inc. (Intel)", "ANGLE (Intel, Intel(R) UHD Graphics 620 Direct3D11 vs_5_0 ps_5_0)"),
		("Google Inc. (NVIDIA)", "ANGLE (NVIDIA, NVIDIA GeForce GTX 1660 Direct3D11 vs_5_0 ps_5_0)"),
		("Google Inc. (AMD)", "ANGLE (AMD, Radeon RX 570 Series Direct3D11 vs_5_0 ps_5_0)"),
		("Google Inc. (Microsoft)", "ANGLE (Microsoft, Basic Render Driver Direct3D11 vs_5_0 ps_5_0)")
	};

	private static readonly string[][] LanguageSets = new string[3][]
	{
		new string[4] { "ru-RU", "ru", "en-US", "en" },
		new string[3] { "ru", "ru-RU", "en-US" },
		new string[3] { "en-US", "en", "ru-RU" }
	};

	private static readonly int[] ColorDepths = new int[2] { 24, 30 };

	private static readonly Random Rng = new Random();

	public static FingerprintProfile Generate()
	{
		string userAgent = Pick(UaPool);
		string platform = Pick(Platforms);
		string vendor = Pick(Vendors);
		(string vendor, string renderer) tuple = Pick(GpuPairs);
		string item = tuple.vendor;
		string item2 = tuple.renderer;
		List<string> list = new List<string>(Pick(LanguageSets));
		int num = Pick(new int[10] { 180, 120, 60, 180, 180, 180, 180, 180, 180, 180 });
		(int, int) tuple2 = Pick(new(int, int)[5]
		{
			(1920, 1080),
			(1366, 768),
			(1536, 864),
			(1600, 900),
			(2560, 1440)
		});
		int item3 = tuple2.Item1;
		int item4 = tuple2.Item2;
		int[] arr = new int[5] { 4, 6, 8, 12, 16 };
		double[] arr2 = new double[5] { 4.0, 6.0, 8.0, 12.0, 16.0 };
		return new FingerprintProfile
		{
			UserAgent = userAgent,
			Platform = platform,
			Vendor = vendor,
			Languages = list,
			AcceptLanguageHeader = string.Join(",", list),
			TimezoneOffsetMinutes = -num,
			HardwareConcurrency = Pick(arr),
			DeviceMemory = Pick(arr2),
			ScreenWidth = item3,
			ScreenHeight = item4,
			ColorDepth = Pick(ColorDepths),
			WebglEnabled = true,
			WebglVendor = item,
			WebglRenderer = item2,
			CanvasNoise = Math.Round(Rng.NextDouble() * 0.6 + 0.2, 2)
		};
	}

	private static T Pick<T>(IList<T> arr)
	{
		return arr[Rng.Next(arr.Count)];
	}
}
