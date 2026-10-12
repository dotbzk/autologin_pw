using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Pw.Hub.Models;

public class Squad : INotifyPropertyChanged
{
	private string _name = string.Empty;

	private int _orderIndex;

	public string Id { get; set; } = Guid.NewGuid().ToString();

	public string Name
	{
		get
		{
			return _name;
		}
		set
		{
			SetField(ref _name, value, "Name");
		}
	}

	public int OrderIndex
	{
		get
		{
			return _orderIndex;
		}
		set
		{
			SetField(ref _orderIndex, value, "OrderIndex");
		}
	}

	public ObservableCollection<Account> Accounts { get; set; } = new ObservableCollection<Account>();

	public event PropertyChangedEventHandler PropertyChanged;

	public override string ToString()
	{
		return Name;
	}

	protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
	{
		if (EqualityComparer<T>.Default.Equals(field, value))
		{
			return false;
		}
		field = value;
		OnPropertyChanged(propertyName);
		return true;
	}
}
