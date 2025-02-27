using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NeuroAccessMaui.Services.AppPermissions;
using NeuroAccessMaui.Services.Settings;
using Waher.Runtime.Inventory;

namespace NeuroAccessMaui.Services.AppPermissions
{
	/// <summary>
	/// Handles checking and requesting permissions for the app.
	/// </summary>
	[DefaultImplementation(typeof(PermissionService))]
	public interface IPermissionService
	{
		Task<bool> CheckCameraPermissionAsync();
	}
}
