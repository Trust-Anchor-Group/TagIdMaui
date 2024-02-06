﻿using System.ComponentModel;

namespace NeuroAccessMaui.UI.Pages.Signatures.ClientSignature
{
    /// <summary>
    /// A page that displays a client signature.
    /// </summary>
    [DesignTimeVisible(true)]
	public partial class ClientSignaturePage
	{
        /// <summary>
        /// Creates a new instance of the <see cref="ClientSignaturePage"/> class.
        /// </summary>
		public ClientSignaturePage()
		{
			this.ContentPageModel = new ClientSignatureViewModel();
			this.InitializeComponent();
		}
    }
}
