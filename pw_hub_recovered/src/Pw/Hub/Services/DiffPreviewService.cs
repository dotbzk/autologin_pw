using System;
using System.Collections.Generic;

namespace Pw.Hub.Services;

public sealed class DiffPreviewService : IDiffPreviewService
{
	public IList<string> BuildUnifiedDiffGit(string oldText, string newText, int context = 3)
	{
		string[] array = (oldText ?? string.Empty).Replace("\r\n", "\n").Split('\n');
		string[] array2 = (newText ?? string.Empty).Replace("\r\n", "\n").Split('\n');
		List<string> list = new List<string>(array.Length + array2.Length + 8) { "--- Current file", "+++ AI suggestion" };
		int num = array.Length;
		int num2 = array2.Length;
		int[,] array3 = new int[num + 1, num2 + 1];
		for (int num3 = num - 1; num3 >= 0; num3--)
		{
			for (int num4 = num2 - 1; num4 >= 0; num4--)
			{
				if (array[num3] == array2[num4])
				{
					array3[num3, num4] = array3[num3 + 1, num4 + 1] + 1;
				}
				else
				{
					array3[num3, num4] = Math.Max(array3[num3 + 1, num4], array3[num3, num4 + 1]);
				}
			}
		}
		List<(char, string, int, int)> list2 = new List<(char, string, int, int)>();
		int i = 0;
		int j = 0;
		while (i < num && j < num2)
		{
			if (array[i] == array2[j])
			{
				list2.Add((' ', array[i], i, j));
				i++;
				j++;
			}
			else if (array3[i + 1, j] >= array3[i, j + 1])
			{
				list2.Add(('-', array[i], i, j));
				i++;
			}
			else
			{
				list2.Add(('+', array2[j], i, j));
				j++;
			}
		}
		for (; i < num; i++)
		{
			list2.Add(('-', array[i], i, j));
		}
		for (; j < num2; j++)
		{
			list2.Add(('+', array2[j], i, j));
		}
		int k = 0;
		while (k < list2.Count)
		{
			for (; k < list2.Count && list2[k].Item1 == ' '; k++)
			{
			}
			if (k >= list2.Count)
			{
				break;
			}
			int num5 = Math.Max(0, k - context);
			int l = k;
			int num6 = k;
			for (; l < list2.Count; l++)
			{
				if (list2[l].Item1 != ' ')
				{
					num6 = l;
				}
				if (list2[l].Item1 == ' ' && l - num6 > context)
				{
					break;
				}
			}
			int num7 = Math.Min(list2.Count, num6 + context + 1);
			int num8 = 0;
			int num9 = 0;
			int num10 = 0;
			int num11 = 0;
			int num12 = 1;
			int num13 = 1;
			for (int m = 0; m < num5; m++)
			{
				if (list2[m].Item1 != '+')
				{
					num12++;
				}
				if (list2[m].Item1 != '-')
				{
					num13++;
				}
			}
			num8 = num12;
			num9 = num13;
			for (int n = num5; n < num7; n++)
			{
				if (list2[n].Item1 != '+')
				{
					num10++;
				}
				if (list2[n].Item1 != '-')
				{
					num11++;
				}
			}
			list.Add($"@@ -{num8},{num10} +{num9},{num11} @@");
			for (int num14 = num5; num14 < num7; num14++)
			{
				char item = list2[num14].Item1;
				string item2 = list2[num14].Item2;
				switch (item)
				{
				case ' ':
					list.Add(" " + item2);
					break;
				case '+':
					list.Add("+" + item2);
					break;
				case '-':
					list.Add("-" + item2);
					break;
				}
			}
			k = num7;
		}
		if (list.Count <= 2)
		{
			list.Add("Изменений нет (код идентичен текущему).");
		}
		return list;
	}
}
