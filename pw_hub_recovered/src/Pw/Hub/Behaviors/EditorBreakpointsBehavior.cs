using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;
using Pw.Hub.Windows;

namespace Pw.Hub.Behaviors;

public static class EditorBreakpointsBehavior
{
	private sealed class BehaviorState
	{
		private readonly ObservableCollection<int> _vmCollection;

		private readonly HashSet<int> _set = new HashSet<int>();

		private BreakpointBackgroundRenderer _renderer;

		public BehaviorState(ObservableCollection<int> vmCollection)
		{
			_vmCollection = vmCollection;
			foreach (int item in vmCollection.Distinct())
			{
				_set.Add(item);
			}
		}

		public void Attach(TextEditor editor)
		{
			try
			{
				_renderer = new BreakpointBackgroundRenderer(_set);
				editor.TextArea.TextView.BackgroundRenderers.Add(_renderer);
				editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
			}
			catch
			{
			}
			_vmCollection.CollectionChanged += VmCollectionOnChanged;
			editor.PreviewKeyDown += OnEditorKeyDown;
		}

		public void Detach(TextEditor editor)
		{
			try
			{
				_vmCollection.CollectionChanged -= VmCollectionOnChanged;
				editor.PreviewKeyDown -= OnEditorKeyDown;
				if (_renderer != null)
				{
					editor.TextArea.TextView.BackgroundRenderers.Remove(_renderer);
					editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
				}
			}
			catch
			{
			}
		}

		private void VmCollectionOnChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			try
			{
				if (e.Action == NotifyCollectionChangedAction.Reset)
				{
					_set.Clear();
					return;
				}
				if (e.OldItems != null)
				{
					foreach (int oldItem in e.OldItems)
					{
						_set.Remove(oldItem);
					}
				}
				if (e.NewItems == null)
				{
					return;
				}
				foreach (int newItem in e.NewItems)
				{
					_set.Add(newItem);
				}
			}
			catch
			{
			}
		}

		private void OnEditorKeyDown(object? sender, KeyEventArgs e)
		{
			if (e.Key != Key.F9 || !(sender is TextEditor textEditor))
			{
				return;
			}
			try
			{
				int line = textEditor.TextArea.Caret.Line;
				if (_set.Contains(line))
				{
					_set.Remove(line);
					_vmCollection.Remove(line);
				}
				else
				{
					_set.Add(line);
					if (!_vmCollection.Contains(line))
					{
						_vmCollection.Add(line);
					}
				}
				try
				{
					textEditor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
				}
				catch
				{
				}
			}
			catch
			{
			}
			finally
			{
				e.Handled = true;
			}
		}
	}

	public static readonly DependencyProperty BreakpointsProperty = DependencyProperty.RegisterAttached("Breakpoints", typeof(ObservableCollection<int>), typeof(EditorBreakpointsBehavior), new PropertyMetadata(null, OnBreakpointsChanged));

	private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached("_State", typeof(BehaviorState), typeof(EditorBreakpointsBehavior));

	public static void SetBreakpoints(DependencyObject element, ObservableCollection<int> value)
	{
		element.SetValue(BreakpointsProperty, value);
	}

	public static ObservableCollection<int> GetBreakpoints(DependencyObject element)
	{
		return (ObservableCollection<int>)element.GetValue(BreakpointsProperty);
	}

	private static void OnBreakpointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is TextEditor textEditor)
		{
			((BehaviorState)textEditor.GetValue(StateProperty))?.Detach(textEditor);
			if (!(e.NewValue is ObservableCollection<int> vmCollection))
			{
				textEditor.SetValue(StateProperty, null);
				return;
			}
			BehaviorState behaviorState = new BehaviorState(vmCollection);
			behaviorState.Attach(textEditor);
			textEditor.SetValue(StateProperty, behaviorState);
		}
	}
}
