using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroAccessMaui.Services;
using NeuroAccessMaui.UI.Pages.Main.XmppForm.Model;
using System.Collections.ObjectModel;
using Waher.Networking.XMPP.DataForms;
using Layout = Waher.Networking.XMPP.DataForms.Layout;
using Waher.Networking.XMPP.DataForms.FieldTypes;

namespace NeuroAccessMaui.UI.Pages.Main.XmppForm
{
	/// <summary>
	/// The view model to bind to for when displaying the calculator.
	/// </summary>
	public partial class XmppFormViewModel : XmppViewModel
	{
		private readonly DataForm? form;
		private bool responseSent;

		/// <summary>
		/// Creates an instance of the <see cref="XmppFormViewModel"/> class.
		/// </summary>
		/// <param name="Args">Navigation arguments.</param>
		public XmppFormViewModel(XmppFormNavigationArgs? Args)
			: base()
		{
			this.Pages = [];

			if (Args is not null)
			{
				this.form = Args.Form;
				this.responseSent = false;

				// TODO: Post-back fields.

				this.Title = this.form?.Title ?? string.Empty;
				this.Instructions = this.form?.Instructions ?? [];

				if (this.form?.HasPages ?? false)
				{
					foreach (Layout.Page P in this.form.Pages)
					{
							this.Pages.Add(new PageModel(this, P));
					}
					this.MultiplePages = this.form.Pages.Length > 1;
				}
				else
				{
					List<Layout.LayoutElement> Elements = [];

					foreach (Field F in this.form?.Fields ?? [])
					{
						if (F is HiddenField)
							continue;

						Elements.Add(new Layout.FieldReference(this.form, F.Var));
					}

					this.Pages.Add(new PageModel(this, new Layout.Page(this.form, string.Empty, Elements.ToArray())));

					this.MultiplePages = false;
				}

				this.ValidateForm();
			}
		}

		/// <inheritdoc/>
		protected override async Task OnDispose()
		{
			if (this.form is not null && this.form.CanCancel && !this.responseSent)
			{
				await this.form.Cancel();
				this.responseSent = true;
			}

			await base.OnDispose();
		}

		#region Properties

		/// <summary>
		/// Title of form
		/// </summary>
		[ObservableProperty]
		private string? title;

		/// <summary>
		/// Instructions for form
		/// </summary>
		[ObservableProperty]
		private string[]? instructions;

		/// <summary>
		/// Holds the pages of the form
		/// </summary>
		public ObservableCollection<PageModel> Pages { get; }

		/// <summary>
		/// IsFormOk of form
		/// </summary>
		[ObservableProperty]
		private bool isFormOk;

		/// <summary>
		/// MultiplePages of form
		/// </summary>
		[ObservableProperty]
		private bool multiplePages;

		#endregion

		#region Commands

		/// <summary>
		/// Command executed when user wants to submit the form.
		/// </summary>
		[RelayCommand]
		private async Task Submit()
		{
			if (!this.IsFormOk)
				return;

			try
			{
				if (this.form is not null && this.form.CanSubmit && !this.responseSent)
				{
					await this.form.Submit();
					this.responseSent = true;

					await this.GoBack();
				}
			}
			catch (Exception ex)
			{
				await ServiceRef.UiService.DisplayException(ex);
			}
		}

		internal void ValidateForm()
		{
			try
			{
				foreach (Field F in this.form?.Fields ?? [])
				{
					if (F.HasError)
					{
						this.IsFormOk = false;
						return;
					}
				}

				this.IsFormOk = true;
			}
			catch (Exception)
			{
				this.IsFormOk = false;
			}
		}

		#endregion
	}
}
