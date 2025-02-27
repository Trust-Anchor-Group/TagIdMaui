using CommunityToolkit.Mvvm.Messaging.Messages;

namespace NeuroAccessMaui.UI.Popups.Settings
{
	/// <summary>
	/// Message used to instruct the view to scroll to a language item identified by its name.
	/// </summary>
	public class ScrollToLanguageMessage(string languageName) : ValueChangedMessage<string>(languageName);
}
