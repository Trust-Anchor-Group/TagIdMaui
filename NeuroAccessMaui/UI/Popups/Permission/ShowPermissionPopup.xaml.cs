using CommunityToolkit.Mvvm.Input;
using Mopups.Services;
using NeuroAccessMaui.Services;

namespace NeuroAccessMaui.UI.Popups.Permission
{
	public partial class ShowPermissionPopup
	{
		public ShowPermissionPopup(ShowPermissionViewModel Vm)
		{
			this.InitializeComponent();
			this.BindingContext = Vm;

		}


	}
}
