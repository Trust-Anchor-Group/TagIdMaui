using Waher.Events;

namespace NeuroAccessMaui.Services
{
	public static class ServiceHelper
	{
		public static T GetService<T>()
			where T : class
		{
			T? Service;

			try
			{
				Service = MauiProgram.Current?.Services?.GetService<T>();
			}
			catch (Exception ex)
			{
#if DEBUG
				ex = Log.UnnestException(ex);
				App.SendAlertAsync(ex.Message, "text/plain").Wait();
#endif
				throw new ArgumentException("Service not found: " + typeof(T).FullName);
			}

			if (Service is not null)
				return Service;
			else
				throw new ArgumentException("Service not found: " + typeof(T).FullName);
		}

		public static object GetService(Type ServiceType)
		{
			object? Service = MauiProgram.Current?.Services?.GetService(ServiceType);

			if (Service is not null)
				return Service;
			else
				throw new ArgumentException("Service not found: " + ServiceType);
		}
	}
}
