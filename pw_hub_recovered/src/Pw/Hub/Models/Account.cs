using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace Pw.Hub.Models;

public class Account : INotifyPropertyChanged
{
	private string _name = string.Empty;

	private string _siteId = string.Empty;

	private string _imageSource;

	private DateTime _lastVisit = DateTime.MinValue;

	private int _orderIndex;

	private List<AccountServer> _servers;

	private string _promoDo;

	private List<string> _promoCartItems;

	private string _promoAccInfo;

	private DateTime? _promoLastSubmittedAt;

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

	public string SiteId
	{
		get
		{
			return _siteId;
		}
		set
		{
			SetField(ref _siteId, value, "SiteId");
		}
	}

	public string ImageSource
	{
		get
		{
			return _imageSource;
		}
		set
		{
			if (SetField(ref _imageSource, value, "ImageSource"))
			{
				OnPropertyChanged("ImageUri");
			}
		}
	}

	public DateTime LastVisit
	{
		get
		{
			return _lastVisit;
		}
		set
		{
			SetField(ref _lastVisit, value, "LastVisit");
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

	public List<AccountServer> Servers
	{
		get
		{
			return _servers;
		}
		set
		{
			SetField(ref _servers, value, "Servers");
		}
	}

	public Uri ImageUri
	{
		get
		{
			if (!string.IsNullOrEmpty(ImageSource))
			{
				return new Uri(ImageSource);
			}
			return null;
		}
	}

	public string SquadId { get; set; }

	public Squad Squad { get; set; }

	[NotMapped]
	public string PromoDo
	{
		get
		{
			return _promoDo;
		}
		set
		{
			SetField(ref _promoDo, value, "PromoDo");
		}
	}

	[NotMapped]
	public List<string> PromoCartItems
	{
		get
		{
			return _promoCartItems;
		}
		set
		{
			SetField(ref _promoCartItems, value, "PromoCartItems");
		}
	}

	public string PromoAccInfo
	{
		get
		{
			return _promoAccInfo;
		}
		set
		{
			SetField(ref _promoAccInfo, value, "PromoAccInfo");
		}
	}

	public DateTime? PromoLastSubmittedAt
	{
		get
		{
			return _promoLastSubmittedAt;
		}
		set
		{
			SetField(ref _promoLastSubmittedAt, value, "PromoLastSubmittedAt");
		}
	}

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
