using Microsoft.Xaml.Behaviors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Monitorian.Core.Views.Behaviors
{
	public interface IItemBehavior
	{
		bool IsByKey { get; set; }
		ItemBehaviorContainer Container { get; }
	}

	public class ItemBehaviorContainer
	{
		public bool Value { get; set; }
		public HashSet<IItemBehavior> Behaviors { get; } = new HashSet<IItemBehavior>();
	}

	public abstract class ItemBehavior<T> : Behavior<T>, IItemBehavior where T : DependencyObject
	{
		public bool IsByKey
		{
			get { return (bool)GetValue(IsByKeyProperty); }
			set { SetValue(IsByKeyProperty, value); }
		}
		public static readonly DependencyProperty IsByKeyProperty =
			DependencyProperty.Register(
				"IsByKey",
				typeof(bool),
				typeof(ItemBehavior<T>),
				new PropertyMetadata(
					false,
					(d, e) => ((ItemBehavior<T>)d).Conform((bool)e.NewValue)));

		public ItemBehaviorContainer Container { get; private set; }

		protected virtual bool Subscribe(Type targetType)
		{
			var target = this.AssociatedObject.GetSelfAndAncestors()
				.FirstOrDefault(x =>
				{
					var type = x.GetType();
					return (type == targetType) || type.IsSubclassOf(targetType);
				});
			if (target is null)
				return false;

			var behavior = Interaction.GetBehaviors(target).OfType<IItemBehavior>().FirstOrDefault();
			if (behavior is null)
				return false;

			Container = behavior.Container ?? new ItemBehaviorContainer();
			return Container.Behaviors.Add(this);
		}

		protected virtual void Unsubscribe()
		{
			Container?.Behaviors.Remove(this);
		}

		protected virtual void Conform(bool value)
		{
			if ((Container is null) || (Container.Value == value))
				return;

			Container.Value = value;

			foreach (var behavior in Container.Behaviors.Where(x => !ReferenceEquals(x, this)))
				behavior.IsByKey = value;
		}
	}
}