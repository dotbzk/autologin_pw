using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Pw.Hub.Models;
using Pw.Hub.Services;
using Pw.Hub.ViewModels;

namespace Pw.Hub.Infrastructure.Behaviors;

public static class TreeViewReorderBehavior
{
	private class State
	{
		public Point DragStartPoint;

		public Account DraggedAccount;

		public Squad DraggedSquad;

		public TreeViewItem AdornerItem;

		public InsertionAdorner InsertionAdorner;

		public Account DropTargetAccount;

		public bool InsertAbove;

		public Squad DropTargetSquad;

		public bool InsertAboveSquad;
	}

	public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(TreeViewReorderBehavior), new PropertyMetadata(false, OnEnabledChanged));

	public static void SetEnabled(DependencyObject element, bool value)
	{
		element.SetValue(EnabledProperty, value);
	}

	public static bool GetEnabled(DependencyObject element)
	{
		return (bool)element.GetValue(EnabledProperty);
	}

	private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (!(d is TreeView treeView))
		{
			return;
		}
		if ((bool)e.NewValue)
		{
			State tag = new State();
			treeView.Tag = tag;
			treeView.AllowDrop = true;
			treeView.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
			treeView.PreviewMouseMove += OnPreviewMouseMove;
			treeView.DragOver += OnDragOver;
			treeView.Drop += OnDrop;
			treeView.DragLeave += OnDragLeave;
			treeView.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
			return;
		}
		treeView.AllowDrop = false;
		treeView.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
		treeView.PreviewMouseMove -= OnPreviewMouseMove;
		treeView.DragOver -= OnDragOver;
		treeView.Drop -= OnDrop;
		treeView.DragLeave -= OnDragLeave;
		treeView.PreviewMouseLeftButtonUp -= OnPreviewMouseLeftButtonUp;
		if (treeView.Tag is State st)
		{
			ClearInsertionAdorner(treeView, st);
			treeView.Tag = null;
		}
	}

	private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		try
		{
			TreeView treeView = (TreeView)sender;
			State state = (State)treeView.Tag;
			state.DragStartPoint = e.GetPosition(treeView);
			state.DraggedAccount = null;
			state.DraggedSquad = null;
			TreeViewItem treeViewItem = FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject);
			if (treeViewItem?.DataContext is Account draggedAccount)
			{
				state.DraggedAccount = draggedAccount;
			}
			else if (treeViewItem?.DataContext is Squad draggedSquad)
			{
				state.DraggedSquad = draggedSquad;
			}
		}
		catch
		{
		}
	}

	private static void OnPreviewMouseMove(object sender, MouseEventArgs e)
	{
		try
		{
			TreeView treeView = (TreeView)sender;
			State state = (State)treeView.Tag;
			if (e.LeftButton != MouseButtonState.Pressed || (state.DraggedAccount == null && state.DraggedSquad == null))
			{
				return;
			}
			Point position = e.GetPosition(treeView);
			Vector vector = new Vector(Math.Abs(position.X - state.DragStartPoint.X), Math.Abs(position.Y - state.DragStartPoint.Y));
			if (!(vector.X < SystemParameters.MinimumHorizontalDragDistance) || !(vector.Y < SystemParameters.MinimumVerticalDragDistance))
			{
				if (state.DraggedAccount != null)
				{
					DragDrop.DoDragDrop(treeView, new DataObject(typeof(Account), state.DraggedAccount), DragDropEffects.Move);
				}
				else if (state.DraggedSquad != null)
				{
					DragDrop.DoDragDrop(treeView, new DataObject(typeof(Squad), state.DraggedSquad), DragDropEffects.Move);
				}
			}
		}
		catch
		{
		}
	}

	private static void OnDragOver(object sender, DragEventArgs e)
	{
		try
		{
			TreeView treeView = (TreeView)sender;
			State state = (State)treeView.Tag;
			if (e.Data.GetDataPresent(typeof(Account)))
			{
				if ((Account)e.Data.GetData(typeof(Account)) == null)
				{
					e.Effects = DragDropEffects.None;
					e.Handled = true;
					ClearInsertionAdorner(treeView, state);
					return;
				}
				(Squad squad, Account account) target = GetAccountOrSquadUnderMouse(treeView, e.OriginalSource as DependencyObject);
				if (target.squad == null)
				{
					e.Effects = DragDropEffects.None;
					e.Handled = true;
					ClearInsertionAdorner(treeView, state);
					return;
				}
				e.Effects = DragDropEffects.Move;
				if (target.account != null)
				{
					TreeViewItem treeViewItem = (TreeViewItem)treeView.ItemContainerGenerator.ContainerFromItem(target.squad);
					TreeViewItem treeViewItem2 = null;
					if (treeViewItem != null)
					{
						treeViewItem2 = (TreeViewItem)treeViewItem.ItemContainerGenerator.ContainerFromItem(target.account);
					}
					if (treeViewItem2 != null)
					{
						bool flag = e.GetPosition(treeViewItem2).Y < treeViewItem2.ActualHeight / 2.0;
						state.DropTargetAccount = target.account;
						state.InsertAbove = flag;
						UpdateInsertionAdorner(treeView, state, treeViewItem2, flag);
					}
					else
					{
						ClearInsertionAdorner(treeView, state);
					}
				}
				else
				{
					Squad squad = (treeView.DataContext as MainViewModel)?.Squads.FirstOrDefault((Squad s) => s.Id == target.squad.Id);
					if (squad != null && squad.Accounts.Count > 0)
					{
						Account account = squad.Accounts.Last();
						TreeViewItem treeViewItem3 = (TreeViewItem)(((TreeViewItem)treeView.ItemContainerGenerator.ContainerFromItem(target.squad))?.ItemContainerGenerator.ContainerFromItem(account));
						if (treeViewItem3 != null)
						{
							state.DropTargetAccount = account;
							state.InsertAbove = false;
							UpdateInsertionAdorner(treeView, state, treeViewItem3, above: false);
						}
						else
						{
							ClearInsertionAdorner(treeView, state);
						}
					}
					else
					{
						ClearInsertionAdorner(treeView, state);
					}
				}
				e.Handled = true;
			}
			else if (e.Data.GetDataPresent(typeof(Squad)))
			{
				TreeViewItem treeViewItem4 = FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject);
				Squad squad2 = treeViewItem4?.DataContext as Squad;
				e.Effects = DragDropEffects.Move;
				if (treeViewItem4 != null && squad2 != null)
				{
					bool flag2 = e.GetPosition(treeViewItem4).Y < treeViewItem4.ActualHeight / 2.0;
					state.DropTargetSquad = squad2;
					state.InsertAboveSquad = flag2;
					UpdateInsertionAdorner(treeView, state, treeViewItem4, flag2);
				}
				else if (treeView.DataContext is MainViewModel mainViewModel && mainViewModel.Squads.Count > 0)
				{
					Squad squad3 = mainViewModel.Squads.Last();
					TreeViewItem treeViewItem5 = (TreeViewItem)treeView.ItemContainerGenerator.ContainerFromItem(squad3);
					if (treeViewItem5 != null)
					{
						state.DropTargetSquad = squad3;
						state.InsertAboveSquad = false;
						UpdateInsertionAdorner(treeView, state, treeViewItem5, above: false);
					}
					else
					{
						ClearInsertionAdorner(treeView, state);
					}
				}
				else
				{
					ClearInsertionAdorner(treeView, state);
				}
				e.Handled = true;
			}
			else
			{
				e.Effects = DragDropEffects.None;
				e.Handled = true;
				ClearInsertionAdorner(treeView, state);
			}
		}
		catch
		{
		}
	}

	private static void OnDrop(object sender, DragEventArgs e)
	{
		TreeView treeView = (TreeView)sender;
		State st = (State)treeView.Tag;
		try
		{
			if (e.Data.GetDataPresent(typeof(Account)))
			{
				Account dragged = (Account)e.Data.GetData(typeof(Account));
				if (dragged == null)
				{
					ClearInsertionAdorner(treeView, st);
					return;
				}
				(Squad squad, Account account) target = GetAccountOrSquadUnderMouse(treeView, e.OriginalSource as DependencyObject);
				if (target.squad == null)
				{
					ClearInsertionAdorner(treeView, st);
					return;
				}
				MainViewModel obj = treeView.DataContext as MainViewModel;
				Squad squad = obj?.Squads.FirstOrDefault((Squad s) => s.Accounts.Any((Account a) => a.Id == dragged.Id));
				Squad squad2 = obj?.Squads.FirstOrDefault((Squad s) => s.Id == (target.account?.SquadId ?? target.squad.Id));
				if (squad == null || squad2 == null)
				{
					ClearInsertionAdorner(treeView, st);
					return;
				}
				ObservableCollection<Account> accounts = squad2.Accounts;
				int num;
				if (st.DropTargetAccount == null)
				{
					num = ((target.account == null) ? (accounts.Count - 1) : accounts.IndexOf(accounts.First((Account a) => a.Id == target.account.Id)));
				}
				else
				{
					Account account = accounts.FirstOrDefault((Account a) => a.Id == st.DropTargetAccount.Id);
					num = ((account != null) ? accounts.IndexOf(account) : (accounts.Count - 1));
				}
				int num2 = (st.InsertAbove ? num : (num + 1));
				if (num2 < 0)
				{
					num2 = 0;
				}
				if (num2 > accounts.Count)
				{
					num2 = accounts.Count;
				}
				ObservableCollection<Account> accounts2 = squad.Accounts;
				int num3 = accounts2.IndexOf(accounts2.First((Account a) => a.Id == dragged.Id));
				if (num3 < 0)
				{
					ClearInsertionAdorner(treeView, st);
					return;
				}
				if (accounts2 == accounts && num3 < num2)
				{
					num2--;
				}
				accounts2.RemoveAt(num3);
				num2 = Math.Max(0, Math.Min(accounts.Count, num2));
				accounts.Insert(num2, dragged);
				if (squad.Id != squad2.Id)
				{
					dragged.SquadId = squad2.Id;
				}
				((App.Services.GetService(typeof(IOrderingService)) as IOrderingService) ?? new OrderingService()).ReorderAccountAsync(targetAccount: st.DropTargetAccount ?? target.account, insertAfter: !st.InsertAbove, moved: dragged, targetSquad: squad2);
				e.Handled = true;
			}
			else
			{
				if (!e.Data.GetDataPresent(typeof(Squad)))
				{
					return;
				}
				Squad draggedSquad = (Squad)e.Data.GetData(typeof(Squad));
				if (draggedSquad == null)
				{
					ClearInsertionAdorner(treeView, st);
					return;
				}
				ObservableCollection<Squad> observableCollection = (treeView.DataContext as MainViewModel)?.Squads;
				if (observableCollection == null)
				{
					ClearInsertionAdorner(treeView, st);
					return;
				}
				int num4 = observableCollection.IndexOf(observableCollection.First((Squad s) => s.Id == draggedSquad.Id));
				if (num4 < 0)
				{
					ClearInsertionAdorner(treeView, st);
					return;
				}
				int num5 = -1;
				if (st.DropTargetSquad != null)
				{
					Squad squad3 = observableCollection.FirstOrDefault((Squad s) => s.Id == st.DropTargetSquad.Id);
					num5 = ((squad3 != null) ? observableCollection.IndexOf(squad3) : (observableCollection.Count - 1));
				}
				else
				{
					object obj2 = FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject)?.DataContext;
					Squad sq = obj2 as Squad;
					num5 = ((sq == null) ? (observableCollection.Count - 1) : observableCollection.IndexOf(observableCollection.First((Squad s) => s.Id == sq.Id)));
				}
				int num6 = (st.InsertAboveSquad ? num5 : (num5 + 1));
				if (num4 < num6)
				{
					num6--;
				}
				num6 = Math.Max(0, Math.Min(observableCollection.Count - 1, num6));
				if (num6 != num4)
				{
					observableCollection.Move(num4, num6);
				}
				IOrderingService orderingService = (App.Services.GetService(typeof(IOrderingService)) as IOrderingService) ?? new OrderingService();
				if (st.DropTargetSquad != null)
				{
					bool insertAfter = !st.InsertAboveSquad;
					orderingService.ReorderSquadAsync(draggedSquad, st.DropTargetSquad, insertAfter);
				}
				else if (observableCollection.Count > 0)
				{
					Squad squad4 = observableCollection[Math.Min(num6, observableCollection.Count - 1)];
					bool insertAfter2 = num6 > observableCollection.IndexOf(squad4);
					orderingService.ReorderSquadAsync(draggedSquad, squad4, insertAfter2);
				}
				e.Handled = true;
			}
		}
		catch
		{
		}
		finally
		{
			ClearInsertionAdorner(treeView, st);
		}
	}

	private static void OnDragLeave(object sender, DragEventArgs e)
	{
		try
		{
			TreeView obj = (TreeView)sender;
			State st = (State)obj.Tag;
			ClearInsertionAdorner(obj, st);
		}
		catch
		{
		}
	}

	private static void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
	}

	private static (Squad squad, Account account) GetAccountOrSquadUnderMouse(TreeView tree, DependencyObject element)
	{
		try
		{
			TreeViewItem treeViewItem = FindAncestor<TreeViewItem>(element);
			if (treeViewItem != null)
			{
				object dataContext = treeViewItem.DataContext;
				Account acc = dataContext as Account;
				if (acc != null)
				{
					return (squad: (tree.DataContext as MainViewModel)?.Squads.FirstOrDefault((Squad s) => s.Id == acc.SquadId), account: acc);
				}
				if (treeViewItem.DataContext is Squad item)
				{
					return (squad: item, account: null);
				}
			}
		}
		catch
		{
		}
		return (squad: null, account: null);
	}

	private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
	{
		while (current != null && !(current is T))
		{
			current = VisualTreeHelper.GetParent(current);
		}
		return current as T;
	}

	private static void UpdateInsertionAdorner(TreeView tree, State st, TreeViewItem item, bool above)
	{
		try
		{
			if (item == null)
			{
				ClearInsertionAdorner(tree, st);
			}
			else if (st.AdornerItem != item || st.InsertionAdorner == null)
			{
				ClearInsertionAdorner(tree, st);
				st.AdornerItem = item;
				AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(item);
				if (adornerLayer != null)
				{
					st.InsertionAdorner = new InsertionAdorner(item)
					{
						PositionAbove = above
					};
					adornerLayer.Add(st.InsertionAdorner);
				}
			}
			else
			{
				st.InsertionAdorner.PositionAbove = above;
				st.InsertionAdorner.InvalidateVisual();
			}
		}
		catch
		{
		}
	}

	private static void ClearInsertionAdorner(ItemsControl owner, State st)
	{
		try
		{
			if (st.AdornerItem != null && st.InsertionAdorner != null)
			{
				AdornerLayer.GetAdornerLayer(st.AdornerItem)?.Remove(st.InsertionAdorner);
			}
		}
		catch
		{
		}
		finally
		{
			st.InsertionAdorner = null;
			st.AdornerItem = null;
			st.DropTargetAccount = null;
			st.DropTargetSquad = null;
			st.InsertAboveSquad = false;
		}
	}
}
