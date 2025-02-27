namespace NeuroAccessMaui.UI
{
	/// <summary>
	/// Static class that gives access to app-specific themed colors
	/// </summary>
	public static class AppColors
	{
		private static Color? primaryForegroundDark;
		private static Color? primaryForegroundLight;
		private static Color? primaryBackgroundDark;
		private static Color? primaryBackgroundLight;
		private static Color? secondaryForegroundDark;
		private static Color? secondaryForegroundLight;
		private static Color? secondaryBackgroundDark;
		private static Color? secondaryBackgroundLight;
		private static Color? accentForegroundDark;
		private static Color? accentForegroundLight;
		private static Color? normalForegroundDark;
		private static Color? normalForegroundLight;
		private static Color? normalBackgroundDark;
		private static Color? normalBackgroundLight;
		private static Color? selectedForegroundDark;
		private static Color? selectedForegroundLight;
		private static Color? selectedBackgroundDark;
		private static Color? selectedBackgroundLight;
		private static Color? enabledFilledButtonForegroundDark;
		private static Color? enabledFilledButtonForegroundLight;
		private static Color? enabledFilledButtonBackgroundDark;
		private static Color? enabledFilledButtonBackgroundLight;
		private static Color? disabledFilledButtonForegroundDark;
		private static Color? disabledFilledButtonForegroundLight;
		private static Color? disabledFilledButtonBackgroundDark;
		private static Color? disabledFilledButtonBackgroundLight;
		private static Color? enabledOutlinedButtonForegroundDark;
		private static Color? enabledOutlinedButtonForegroundLight;
		private static Color? enabledOutlinedButtonBackgroundDark;
		private static Color? enabledOutlinedButtonBackgroundLight;
		private static Color? disabledOutlinedButtonForegroundDark;
		private static Color? disabledOutlinedButtonForegroundLight;
		private static Color? disabledOutlinedButtonBackgroundDark;
		private static Color? disabledOutlinedButtonBackgroundLight;
		private static Color? normalEditForegroundDark;
		private static Color? normalEditForegroundLight;
		private static Color? normalEditBackgroundDark;
		private static Color? normalEditBackgroundLight;
		private static Color? normalEditPlaceholderDark;
		private static Color? normalEditPlaceholderLight;
		private static Color? alertDark;
		private static Color? alertLight;
		private static Color? errorBackgroundDark;
		private static Color? errorBackgroundLight;
		private static Color? clickableDark;
		private static Color? clickableLight;
		private static Color? weakPasswordForeground;
		private static Color? mediumPasswordForeground;
		private static Color? strongPasswordForeground;
		private static Color? blueLink;
		private static Color? insertedBorder;
		private static Color? deletedBorder;
		private static Color? purple15Light;
		private static Color? purple15Dark;
		private static Color? purpleLight;
		private static Color? purpleDark;
		private static Color? blue20AffirmLight;
		private static Color? blue20AffirmDark;
		private static Color? blueLight;
		private static Color? blueDark;

		/// <summary>
		/// Primary foreground color.
		/// </summary>
		public static Color PrimaryForeground
		{
			get
			{
				if (Application.Current?.RequestedTheme == AppTheme.Dark)
				{
					primaryForegroundDark ??= AppStyles.TryGetResource<Color>("PrimaryForegroundDark");
					return primaryForegroundDark!;
				}
				else
				{
					primaryForegroundLight ??= AppStyles.TryGetResource<Color>("PrimaryForegroundLight");
					return primaryForegroundLight!;
				}
			}
		}

		/// <summary>
		/// Primary background color.
		/// </summary>
		public static Color PrimaryBackground
		{
			get
			{
				if (Application.Current?.RequestedTheme == AppTheme.Dark)
				{
					primaryBackgroundDark ??= AppStyles.TryGetResource<Color>("PrimaryBackgroundDark");
					return primaryBackgroundDark!;
				}
				else
				{
					primaryBackgroundLight ??= AppStyles.TryGetResource<Color>("PrimaryBackgroundLight");
					return primaryBackgroundLight!;
				}
			}
		}

		/// <summary>
		/// Secondary foreground color.
		/// </summary>
		public static Color SecondaryForeground
		{
			get
			{
				if (Application.Current?.RequestedTheme == AppTheme.Dark)
				{
					secondaryForegroundDark ??= AppStyles.TryGetResource<Color>("SecondaryForegroundDark");
					return secondaryForegroundDark!;
				}
				else
				{
					secondaryForegroundLight ??= AppStyles.TryGetResource<Color>("SecondaryForegroundLight");
					return secondaryForegroundLight!;
				}
			}
		}

		/// <summary>
		/// Secondary background color.
		/// </summary>
		public static Color SecondaryBackground
		{
			get
			{
				if (Application.Current?.RequestedTheme == AppTheme.Dark)
				{
					secondaryBackgroundDark ??= AppStyles.TryGetResource<Color>("SecondaryBackgroundDark");
					return secondaryBackgroundDark!;
				}
				else
				{
					secondaryBackgroundLight ??= AppStyles.TryGetResource<Color>("SecondaryBackgroundLight");
					return secondaryBackgroundLight!;
				}
			}
		}

		/// <summary>
		/// Accent foreground color.
		/// </summary>
		public static Color AccentForeground
		{
			get
			{
				if (Application.Current?.RequestedTheme == AppTheme.Dark)
				{
					accentForegroundDark ??= AppStyles.TryGetResource<Color>("AccentForegroundDark");
					return accentForegroundDark!;
				}
				else
				{
					accentForegroundLight ??= AppStyles.TryGetResource<Color>("AccentForegroundLight");
					return accentForegroundLight!;
				}
			}
		}

		/// <summary>
		/// Normal foreground color.
		/// </summary>
		public static Color NormalForeground
		{
			get
			{
				if (Application.Current?.RequestedTheme == AppTheme.Dark)
				{
					normalForegroundDark ??= AppStyles.TryGetResource<Color>("NormalForegroundDark");
					return normalForegroundDark!;
				}
				else
				{
					normalForegroundLight ??= AppStyles.TryGetResource<Color>("NormalForegroundLight");
					return normalForegroundLight!;
				}
			}
		}

		/// <summary>
		/// Normal background color.
		/// </summary>
		public static Color NormalBackground
		{
			get
			{
				if (Application.Current?.RequestedTheme == AppTheme.Dark)
				{
					normalBackgroundDark ??= AppStyles.TryGetResource<Color>("NormalBackgroundDark");
					return normalBackgroundDark!;
				}
				else
				{
					normalBackgroundLight ??= AppStyles.TryGetResource<Color>("NormalBackgroundLight");
					return normalBackgroundLight!;
				}
			}
		}

		/// <summary>
		/// Selected foreground color.
		/// </summary>
		public static Color SelectedForeground
		{
			get
			{
				if (Application.Current?.RequestedTheme == AppTheme.Dark)
				{
					selectedForegroundDark ??= AppStyles.TryGetResource<Color>("SelectedForegroundDark");
					return selectedForegroundDark!;
				}
				else
				{
					selectedForegroundLight ??= AppStyles.TryGetResource<Color>("SelectedForegroundLight");
					return selectedForegroundLight!;
				}
			}
		}

		/// <summary>
		/// Selected background color.
		/// </summary>
		public static Color SelectedBackground
		{
			get
			{
				if (Application.Current?.RequestedTheme == AppTheme.Dark)
				{
					selectedBackgroundDark ??= AppStyles.TryGetResource<Color>("SelectedBackgroundDark");
					return selectedBackgroundDark!;
				}
				else
				{
					selectedBackgroundLight ??= AppStyles.TryGetResource<Color>("SelectedBackgroundLight");
					return selectedBackgroundLight!;
				}
			}
		}

		/// <summary>
		/// EnabledFilledButton foreground color.
		/// </summary>
		public static Color EnabledFilledButtonForeground
		{
			get
			{
				if (Application.Current?.RequestedTheme == AppTheme.Dark)
				{
					enabledFilledButtonForegroundDark ??= AppStyles.TryGetResource<Color>("EnabledFilledButtonForegroundDark");
					return enabledFilledButtonForegroundDark!;
				}
				else
				{
					enabledFilledButtonForegroundLight ??= AppStyles.TryGetResource<Color>("EnabledFilledButtonForegroundLight");
					return enabledFilledButtonForegroundLight!;
				}
			}
		}

		/// <summary>
		/// EnabledFilledButton background color.
		/// </summary>
		public static Color EnabledFilledButtonBackground
		{
			get
			{
				if (Application.Current?.RequestedTheme == AppTheme.Dark)
				{
					enabledFilledButtonBackgroundDark ??= AppStyles.TryGetResource<Color>("EnabledFilledButtonBackgroundDark");
					return enabledFilledButtonBackgroundDark!;
				}
				else
				{
					enabledFilledButtonBackgroundLight ??= AppStyles.TryGetResource<Color>("EnabledFilledButtonBackgroundLight");
					return enabledFilledButtonBackgroundLight!;
				}
			}
		}

		/// <summary>
		/// DisabledFilledButton foreground color.
		/// </summary>
		public static Color DisabledFilledButtonForeground
		{
			get
			{
				if (Application.Current?.RequestedTheme == AppTheme.Dark)
				{
					disabledFilledButtonForegroundDark ??= AppStyles.TryGetResource<Color>("DisabledFilledButtonForegroundDark");
					return disabledFilledButtonForegroundDark!;
				}
				else
				{
					disabledFilledButtonForegroundLight ??= AppStyles.TryGetResource<Color>("DisabledFilledButtonForegroundLight");
					return disabledFilledButtonForegroundLight!;
				}
			}
		}

		/// <summary>
		/// DisabledFilledButton background color.
		/// </summary>
		public static Color DisabledFilledButtonBackground
		{
			get
			{
				if (Application.Current?.RequestedTheme == AppTheme.Dark)
				{
					disabledFilledButtonBackgroundDark ??= AppStyles.TryGetResource<Color>("DisabledFilledButtonBackgroundDark");
					return disabledFilledButtonBackgroundDark!;
				}
				else
				{
					disabledFilledButtonBackgroundLight ??= AppStyles.TryGetResource<Color>("DisabledFilledButtonBackgroundLight");
					return disabledFilledButtonBackgroundLight!;
				}
			}
		}

		/// <summary>
		/// EnabledOutlinedButton foreground color.
		/// </summary>
		public static Color EnabledOutlinedButtonForeground
		{
			get
			{
				if (Application.Current?.RequestedTheme == AppTheme.Dark)
				{
					enabledOutlinedButtonForegroundDark ??= AppStyles.TryGetResource<Color>("EnabledOutlinedButtonForegroundDark");
					return enabledOutlinedButtonForegroundDark!;
				}
				else
				{
					enabledOutlinedButtonForegroundLight ??= AppStyles.TryGetResource<Color>("EnabledOutlinedButtonForegroundLight");
					return enabledOutlinedButtonForegroundLight!;
				}
			}
		}

		/// <summary>
		/// EnabledOutlinedButton background color.
		/// </summary>
		public static Color EnabledOutlinedButtonBackground
		{
			get
			{
				if (Application.Current?.RequestedTheme == AppTheme.Dark)
				{
					enabledOutlinedButtonBackgroundDark ??= AppStyles.TryGetResource<Color>("EnabledOutlinedButtonBackgroundDark");
					return enabledOutlinedButtonBackgroundDark!;
				}
				else
				{
					enabledOutlinedButtonBackgroundLight ??= AppStyles.TryGetResource<Color>("EnabledOutlinedButtonBackgroundLight");
					return enabledOutlinedButtonBackgroundLight!;
				}
			}
		}

		/// <summary>
		/// DisabledOutlinedButton foreground color.
		/// </summary>
		public static Color DisabledOutlinedButtonForeground
		{
			get
			{
				if (Application.Current?.RequestedTheme == AppTheme.Dark)
				{
					disabledOutlinedButtonForegroundDark ??= AppStyles.TryGetResource<Color>("DisabledOutlinedButtonForegroundDark");
					return disabledOutlinedButtonForegroundDark!;
				}
				else
				{
					disabledOutlinedButtonForegroundLight ??= AppStyles.TryGetResource<Color>("DisabledOutlinedButtonForegroundLight");
					return disabledOutlinedButtonForegroundLight!;
				}
			}
		}

		/// <summary>
		/// DisabledOutlinedButton background color.
		/// </summary>
		public static Color DisabledOutlinedButtonBackground
		{
			get
			{
				if (Application.Current?.RequestedTheme == AppTheme.Dark)
				{
					disabledOutlinedButtonBackgroundDark ??= AppStyles.TryGetResource<Color>("DisabledOutlinedButtonBackgroundDark");
					return disabledOutlinedButtonBackgroundDark!;
				}
				else
				{
					disabledOutlinedButtonBackgroundLight ??= AppStyles.TryGetResource<Color>("DisabledOutlinedButtonBackgroundLight");
					return disabledOutlinedButtonBackgroundLight!;
				}
			}
		}

		/// <summary>
		/// NormalEdit foreground color.
		/// </summary>
		public static Color NormalEditForeground
		{
			get
			{
				if (Application.Current?.RequestedTheme == AppTheme.Dark)
				{
					normalEditForegroundDark ??= AppStyles.TryGetResource<Color>("NormalEditForegroundDark");
					return normalEditForegroundDark!;
				}
				else
				{
					normalEditForegroundLight ??= AppStyles.TryGetResource<Color>("NormalEditForegroundLight");
					return normalEditForegroundLight!;
				}
			}
		}

		/// <summary>
		/// NormalEdit background color.
		/// </summary>
		public static Color NormalEditBackground
		{
			get
			{
				if (Application.Current?.RequestedTheme == AppTheme.Dark)
				{
					normalEditBackgroundDark ??= AppStyles.TryGetResource<Color>("NormalEditBackgroundDark");
					return normalEditBackgroundDark!;
				}
				else
				{
					normalEditBackgroundLight ??= AppStyles.TryGetResource<Color>("NormalEditBackgroundLight");
					return normalEditBackgroundLight!;
				}
			}
		}

		/// <summary>
		/// NormalEdit placeholder color.
		/// </summary>
		public static Color NormalEditPlaceholder
		{
			get
			{
				if (Application.Current?.RequestedTheme == AppTheme.Dark)
				{
					normalEditBackgroundDark ??= AppStyles.TryGetResource<Color>("NormalEditPlaceholderDark");
					return normalEditPlaceholderDark!;
				}
				else
				{
					normalEditBackgroundLight ??= AppStyles.TryGetResource<Color>("NormalEditPlaceholderLight");
					return normalEditPlaceholderLight!;
				}
			}
		}

		/// <summary>
		/// Alert color.
		/// </summary>
		public static Color Alert
		{
			get
			{
				if (Application.Current?.RequestedTheme == AppTheme.Dark)
				{
					alertDark ??= AppStyles.TryGetResource<Color>("AlertDark");
					return alertDark!;
				}
				else
				{
					alertLight ??= AppStyles.TryGetResource<Color>("AlertLight");
					return alertLight!;
				}
			}
		}

		/// <summary>
		/// Error Background Color.
		/// </summary>
		public static Color ErrorBackground
		{
			get
			{
				if (Application.Current?.RequestedTheme == AppTheme.Dark)
				{
					errorBackgroundDark ??= AppStyles.TryGetResource<Color>("ErrorBackgroundDark");
					return errorBackgroundDark!;
				}
				else
				{
					errorBackgroundLight ??= AppStyles.TryGetResource<Color>("ErrorBackgroundLight");
					return errorBackgroundLight!;
				}
			}
		}

		/// <summary>
		/// Clickable color.
		/// </summary>
		public static Color Clickable
		{
			get
			{
				if (Application.Current?.RequestedTheme == AppTheme.Dark)
				{
					clickableDark ??= AppStyles.TryGetResource<Color>("ClickableDark");
					return clickableDark!;
				}
				else
				{
					clickableLight ??= AppStyles.TryGetResource<Color>("ClickableLight");
					return clickableLight!;
				}
			}
		}

		/// <summary>
		/// Weak password foreground color.
		/// </summary>
		public static Color WeakPasswordForeground
		{
			get
			{
				if (Application.Current?.RequestedTheme == AppTheme.Dark)
				{
					weakPasswordForeground ??= AppStyles.TryGetResource<Color>("WeakPasswordBarForegroundDark");
					return weakPasswordForeground!;
				}
				else
				{
					weakPasswordForeground ??= AppStyles.TryGetResource<Color>("WeakPasswordBarForegroundLight");
					return weakPasswordForeground!;
				}
			}
		}

		/// <summary>
		/// Medium password foreground color.
		/// </summary>
		public static Color MediumPasswordForeground
		{
			get
			{
				if (Application.Current?.RequestedTheme == AppTheme.Dark)
				{
					mediumPasswordForeground ??= AppStyles.TryGetResource<Color>("MediumPasswordBarForegroundDark");
					return mediumPasswordForeground!;
				}
				else
				{
					mediumPasswordForeground ??= AppStyles.TryGetResource<Color>("MediumPasswordBarForegroundLight");
					return mediumPasswordForeground!;
				}
			}
		}

		/// <summary>
		/// Strong password foreground color.
		/// </summary>
		public static Color StrongPasswordForeground
		{
			get
			{
				if (Application.Current?.RequestedTheme == AppTheme.Dark)
				{
					strongPasswordForeground ??= AppStyles.TryGetResource<Color>("StrongPasswordBarForegroundDark");
					return strongPasswordForeground!;
				}
				else
				{
					strongPasswordForeground ??= AppStyles.TryGetResource<Color>("StrongPasswordBarForegroundLight");
					return strongPasswordForeground!;
				}
			}
		}

		/// <summary>
		/// Blue link color
		/// </summary>
		public static Color BlueLink
		{
			get
			{
				if (Application.Current?.RequestedTheme == AppTheme.Dark)
				{
					blueLink ??= AppStyles.TryGetResource<Color>("BlueLinkDark");
					return blueLink!;
				}
				else
				{
					blueLink ??= AppStyles.TryGetResource<Color>("BlueLink");
					return blueLink!;
				}
			}
		}

		/// Purple color with 15% transparency.
		/// </summary>
		public static Color Purple15
		{
			get
			{
				if (Application.Current?.RequestedTheme == AppTheme.Dark)
				{
					purple15Dark ??= AppStyles.TryGetResource<Color>("Purple15Dark");
					return purple15Dark!;
				}
				else
				{
					purple15Light ??= AppStyles.TryGetResource<Color>("Purple15Light");
					return purple15Light!;
				}
			}
		}

		/// <summary>
		/// Inserted Border color
		/// </summary>
		public static Color InsertedBorder
		{
			get
			{
				if (Application.Current?.RequestedTheme == AppTheme.Light)
				{
					insertedBorder ??= AppStyles.TryGetResource<Color>("InsertedBorderLight");
					return insertedBorder!;
				}
				else
				{
					insertedBorder ??= AppStyles.TryGetResource<Color>("InsertedBorderDark");
					return insertedBorder!;
				}
			}
		}


		/// Purple color
		/// </summary>
		public static Color Purple
		{
			get
			{
				if (Application.Current?.RequestedTheme == AppTheme.Dark)
				{
					purpleDark ??= AppStyles.TryGetResource<Color>("PurpleDark");
					return purpleDark!;
				}
				else
				{
					purpleLight ??= AppStyles.TryGetResource<Color>("PurpleLight");
					return purpleLight!;
				}
			}
		}

		/// <summary>
		/// Deleted Border color
		/// </summary>
		public static Color DeletedBorder
		{
			get
			{
				if (Application.Current?.RequestedTheme == AppTheme.Light)
				{
					deletedBorder ??= AppStyles.TryGetResource<Color>("DeletedBorderLight");
					return deletedBorder!;
				}
				else
				{
					deletedBorder ??= AppStyles.TryGetResource<Color>("DeletedBorderDark");
					return deletedBorder!;
				}
			}
		}

		/// Blue affirm color with 20% transparency.
		/// </summary>
		public static Color Blue20Affirm
		{
			get
			{
				if (Application.Current?.RequestedTheme == AppTheme.Dark)
				{
					blue20AffirmDark ??= AppStyles.TryGetResource<Color>("Blue20AffirmDark");
					return blue20AffirmDark!;
				}
				else
				{
					blue20AffirmLight ??= AppStyles.TryGetResource<Color>("Blue20AffirmLight");
					return blue20AffirmLight!;
				}
			}
		}

		/// <summary>
		/// Blue color
		/// </summary>
		public static Color Blue
		{
			get
			{
				if (Application.Current?.RequestedTheme == AppTheme.Dark)
				{
					blueDark ??= AppStyles.TryGetResource<Color>("BlueDark");
					return blueDark!;
				}
				else
				{
					blueLight ??= AppStyles.TryGetResource<Color>("BlueLight");
					return blueLight!;
				}
			}
		}
	}
}
