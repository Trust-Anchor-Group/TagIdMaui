namespace NeuroAccessMaui.UI.Pages.Wallet.TokenEvents
{
	/// <summary>
	/// Data Template Selector, based on Event Type.
	/// </summary>
	public class EventTypeTemplateSelector : DataTemplateSelector
	{
		/// <summary>
		/// Template to use for creation events.
		/// </summary>
		public DataTemplate? CreatedTemplate { get; set; }

		/// <summary>
		/// Template to use for destruction events.
		/// </summary>
		public DataTemplate? DestroyedTemplate { get; set; }

		/// <summary>
		/// Template to use for transfer events.
		/// </summary>
		public DataTemplate? TransferredTemplate { get; set; }

		/// <summary>
		/// Template to use for donation events.
		/// </summary>
		public DataTemplate? DonatedTemplate { get; set; }

		/// <summary>
		/// Template to use for text note events.
		/// </summary>
		public DataTemplate? NoteTextTemplate { get; set; }

		/// <summary>
		/// Template to use for XML note events.
		/// </summary>
		public DataTemplate? NoteXmlTemplate { get; set; }

		/// <summary>
		/// Template to use for external text note events.
		/// </summary>
		public DataTemplate? ExternalNoteTextTemplate { get; set; }

		/// <summary>
		/// Template to use for external XML note events.
		/// </summary>
		public DataTemplate? ExternalNoteXmlTemplate { get; set; }

		/// <summary>
		/// Template to use for other items.
		/// </summary>
		public DataTemplate? DefaultTemplate { get; set; }

		/// <inheritdoc/>
		protected override DataTemplate? OnSelectTemplate(object item, BindableObject container)
		{
			if (item is EventItem Event)
			{
				return Event.Type switch
				{
					EventType.Created => this.CreatedTemplate ?? this.DefaultTemplate,
					EventType.Destroyed => this.DestroyedTemplate ?? this.DefaultTemplate,
					EventType.Transferred => this.TransferredTemplate ?? this.DefaultTemplate,
					EventType.Donated => this.DonatedTemplate ?? this.DefaultTemplate,
					EventType.NoteText => this.NoteTextTemplate ?? this.DefaultTemplate,
					EventType.NoteXml => this.NoteXmlTemplate ?? this.DefaultTemplate,
					EventType.ExternalNoteText => this.ExternalNoteTextTemplate ?? this.DefaultTemplate,
					EventType.ExternalNoteXml => this.ExternalNoteXmlTemplate ?? this.DefaultTemplate,
					_ => this.DefaultTemplate,
				};
			}

			return this.DefaultTemplate;
		}
	}
}
