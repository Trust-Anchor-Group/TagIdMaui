using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NeuroAccessMaui.UI.Pages;

namespace NeuroAccessMaui.UI.Popups
{
	public class BasePopupViewModel : BaseViewModel
	{
		protected readonly TaskCompletionSource popped = new();

		/// <summary>
		/// Completion task that is triggered when the popup is popped from the stack.
		/// </summary>
		public Task Popped => this.popped.Task;

		/// <summary>
		/// Calls OnPop and sets the completion task.
		/// This should not be called externally.
		/// </summary>
		/// <returns></returns>
		public async Task OnPopInternal()
		{
			await this.OnPop();
			this.popped.TrySetResult();
		}

		/// <summary>
		/// Called when the popup is popped from the stack.
		/// This can be used to clean up resources, cancel tasks, etc.
		/// </summary>
		public virtual Task OnPop()
		{
			return Task.CompletedTask;
		}
	}
}
