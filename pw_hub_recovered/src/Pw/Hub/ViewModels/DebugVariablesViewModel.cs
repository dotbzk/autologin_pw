using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Pw.Hub.Infrastructure;

namespace Pw.Hub.ViewModels;

public class DebugVariablesViewModel : INotifyPropertyChanged
{
	private string _title = "Variables";

	public string Title
	{
		get
		{
			return _title;
		}
		set
		{
			_title = value;
			OnPropertyChanged("Title");
		}
	}

	public ObservableCollection<DebugVarNode> Locals { get; } = new ObservableCollection<DebugVarNode>();

	public ObservableCollection<DebugVarNode> Globals { get; } = new ObservableCollection<DebugVarNode>();

	public ICommand CloseCommand { get; }

	public event Action RequestClose;

	public event PropertyChangedEventHandler PropertyChanged;

	public DebugVariablesViewModel()
	{
		CloseCommand = new RelayCommand(delegate
		{
			this.RequestClose?.Invoke();
		});
	}

	public void SetData(int line, IDictionary<string, object> locals, IDictionary<string, object> globals)
	{
		Title = $"Paused at line {line}";
		Locals.Clear();
		Globals.Clear();
		foreach (DebugVarNode item in BuildNodes(locals))
		{
			Locals.Add(item);
		}
		foreach (DebugVarNode item2 in BuildNodes(globals))
		{
			Globals.Add(item2);
		}
	}

	private static IEnumerable<DebugVarNode> BuildNodes(IDictionary<string, object> dict, int depth = 0)
	{
		if (dict == null)
		{
			yield break;
		}
		foreach (KeyValuePair<string, object> item in dict.OrderBy((KeyValuePair<string, object> k) => k.Key))
		{
			yield return CreateNode(item.Key, item.Value, depth);
		}
	}

	private static DebugVarNode CreateNode(string name, object value, int depth)
	{
		DebugVarNode debugVarNode = new DebugVarNode
		{
			Name = name
		};
		if (value is IDictionary<string, object> dictionary)
		{
			debugVarNode.TypeName = "table";
			debugVarNode.DisplayValue = $"table[{dictionary.Count}]";
			foreach (DebugVarNode item in BuildNodes(dictionary, depth + 1))
			{
				debugVarNode.Children.Add(item);
			}
		}
		else if (value is string text)
		{
			debugVarNode.TypeName = "string";
			debugVarNode.DisplayValue = "\"" + text + "\"";
		}
		else if (value is bool flag)
		{
			debugVarNode.TypeName = "boolean";
			debugVarNode.DisplayValue = (flag ? "true" : "false");
		}
		else if (value == null)
		{
			debugVarNode.TypeName = "nil";
			debugVarNode.DisplayValue = "nil";
		}
		else
		{
			debugVarNode.TypeName = value.GetType().Name;
			try
			{
				debugVarNode.DisplayValue = value.ToString();
			}
			catch
			{
				debugVarNode.DisplayValue = debugVarNode.TypeName;
			}
		}
		return debugVarNode;
	}

	private void OnPropertyChanged([CallerMemberName] string propertyName = null)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
