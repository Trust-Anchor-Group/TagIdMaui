using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Waher.Content;
using Waher.Runtime.Inventory;
using Waher.Runtime.Temporary;

namespace NeuroAccessMaui.Services.Content
{
	/// <summary>
	/// Gets content previously encrypted using AES-256.
	/// </summary>
	public class Aes256Getter : IContentGetter
	{
		/// <summary>
		/// Gets content previously encrypted using AES-256.
		/// </summary>
		public Aes256Getter()
		{
		}

		/// <summary>
		/// URI Schemes handled.
		/// </summary>
		public string[] UriSchemes => [Constants.UriSchemes.Aes256];

		/// <summary>
		/// How well the URI can be managed.
		/// </summary>
		/// <param name="Uri">URI</param>
		/// <param name="Grade">How well the getter can get content specified by the URI.</param>
		/// <returns>If an URI can be used to get encrypted content using the getter.</returns>
		public bool CanGet(Uri Uri, out Grade Grade)
		{
			Grade = Grade.NotAtAll;

			return TryParse(Uri, out _, out _, out _, out Uri? EncryptedUri) &&
				InternetContent.CanGet(EncryptedUri, out Grade, out _);
		}

		/// <summary>
		/// Try to parce the AES256 URI
		/// </summary>
		public static bool TryParse(Uri Uri, [NotNullWhen(true)] out byte[]? Key, [NotNullWhen(true)] out byte[]? IV,
			[NotNullWhen(true)] out string? ContentType, [NotNullWhen(true)] out Uri? EncryptedUri)
		{
			Key = null;
			IV = null;
			ContentType = null;
			EncryptedUri = null;

			string s = Uri.OriginalString;
			int i = s.IndexOf(':');
			if (i < 0)
				return false;

			string s2 = s[..i];
			if (!string.Equals(Constants.UriSchemes.Aes256, s2, StringComparison.OrdinalIgnoreCase))
				return false;

			int j = s.IndexOf(':', i + 1);
			if (j < 0)
				return false;

			try
			{
				s2 = s.Substring(i + 1, j - i - 1);
				Key = Convert.FromBase64String(s2);

				i = s.IndexOf(':', j + 1);
				if (i < 0)
					return false;

				s2 = s.Substring(j + 1, i - j - 1);
				IV = Convert.FromBase64String(s2);

				j = s.IndexOf(':', i + 1);
				if (j < 0)
					return false;

				ContentType = s.Substring(i + 1, j - i - 1);
				EncryptedUri = new Uri(s[(j + 1)..]);

				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}

		/// <summary>
		/// Gets and decrypts content from the Internet.
		/// </summary>
		/// <param name="Uri">Full URI</param>
		/// <param name="Certificate">Optional client certificate.</param>
		/// <param name="RemoteCertificateValidator">Optional callback method for validating remote certificates.</param>
		/// <param name="Headers">Additional headers</param>
		/// <returns>Decrypted and decoded content.</returns>
		/// <exception cref="ArgumentException">If URI cannot be parsed.</exception>
		public Task<ContentResponse> GetAsync(Uri Uri, X509Certificate Certificate, RemoteCertificateEventHandler RemoteCertificateValidator,
			params KeyValuePair<string, string>[] Headers)
		{
			return this.GetAsync(Uri, Certificate, RemoteCertificateValidator, 60000, Headers);
		}

		/// <summary>
		/// Gets and decrypts content from the Internet.
		/// </summary>
		/// <param name="Uri">Full URI</param>
		/// <param name="Certificate">Optional client certificate.</param>
		/// <param name="RemoteCertificateValidator">Optional callback method for validating remote certificates.</param>
		/// <param name="TimeoutMs">Timeout, in milliseconds.</param>
		/// <param name="Headers">Additional headers</param>
		/// <returns>Decrypted and decoded content.</returns>
		/// <exception cref="ArgumentException">If URI cannot be parsed.</exception>
		public async Task<ContentResponse> GetAsync(Uri Uri, X509Certificate Certificate, RemoteCertificateEventHandler RemoteCertificateValidator,
			int TimeoutMs, params KeyValuePair<string, string>[] Headers)
		{
			(string ContentType, byte[] Bin, Uri EncryptedUri) = await GetAndDecrypt(Uri, Certificate, RemoteCertificateValidator, TimeoutMs, Headers);

			return await InternetContent.DecodeAsync(ContentType, Bin, EncryptedUri);
		}

		private static async Task<(string, byte[], Uri)> GetAndDecrypt(Uri Uri, X509Certificate Certificate,
			RemoteCertificateEventHandler RemoteCertificateValidator, int TimeoutMs, params KeyValuePair<string, string>[] Headers)
		{
			if (!TryParse(Uri, out byte[]? Key, out byte[]? IV, out string? ContentType, out Uri? EncryptedUri))
				throw new ArgumentException("URI not supported.", nameof(Uri));

			ContentResponse Response = await InternetContent.GetAsync(EncryptedUri, Certificate, RemoteCertificateValidator, TimeoutMs, AcceptBinary(Headers));
			Response.AssertOk();

			byte[] Bin = Response.Decoded as byte[]
				?? throw new IOException("Expected binary response.");

			Aes Aes = Aes.Create();
			Aes.BlockSize = 128;
			Aes.KeySize = 256;
			Aes.Mode = CipherMode.CBC;
			Aes.Padding = PaddingMode.PKCS7;

			using ICryptoTransform Transform = Aes.CreateDecryptor(Key, IV);
			Bin = Transform.TransformFinalBlock(Bin, 0, Bin.Length);

			return (ContentType, Bin, EncryptedUri);
		}

		private static KeyValuePair<string, string>[] AcceptBinary(KeyValuePair<string, string>[] Headers)
		{
			List<KeyValuePair<string, string>> Result = [];

			if (Headers is not null)
			{
				foreach (KeyValuePair<string, string> P in Headers)
				{
					if (P.Key != "Accept")
						Result.Add(P);
				}
			}

			Result.Add(new KeyValuePair<string, string>("Accept", "application/octet-stream"));

			return [.. Result];
		}

		/// <summary>
		/// Gets a (possibly big) resource, using a Uniform Resource Identifier (or Locator).
		/// </summary>
		/// <param name="Uri">URI</param>
		/// <param name="Certificate">Optional client certificate to use in a Mutual TLS session.</param>
		/// <param name="RemoteCertificateValidator">Optional callback method for validating remote certificates.</param>
		/// <param name="Headers">Optional headers. Interpreted in accordance with the corresponding URI scheme.</param>
		/// <returns>Content-Type, together with a Temporary file, if resource has been downloaded, or null if resource is data-less.</returns>
		public Task<ContentStreamResponse> GetTempStreamAsync(Uri Uri, X509Certificate Certificate,
			RemoteCertificateEventHandler RemoteCertificateValidator, params KeyValuePair<string, string>[] Headers)
		{
			return this.GetTempStreamAsync(Uri, Certificate, RemoteCertificateValidator, 60000, Headers);
		}

		/// <summary>
		/// Gets a (possibly big) resource, using a Uniform Resource Identifier (or Locator).
		/// </summary>
		/// <param name="Uri">URI</param>
		/// <param name="Certificate">Optional Client certificate to use in a Mutual TLS session.</param>
		/// <param name="RemoteCertificateValidator">Optional validator of remote certificates.</param>
		/// <param name="Destination">Optional destination. Content will be output to this stream. If not provided, a new temporary stream will be created.</param>
		/// <param name="Headers">Optional headers. Interpreted in accordance with the corresponding URI scheme.</param>
		/// <returns>Content-Type, together with a Temporary file, if resource has been downloaded, or null if resource is data-less.</returns>
		public Task<ContentStreamResponse> GetTempStreamAsync(Uri Uri, X509Certificate Certificate,
			RemoteCertificateEventHandler RemoteCertificateValidator, TemporaryStream Destination, params KeyValuePair<string, string>[] Headers)
		{
			return this.GetTempStreamAsync(Uri, Certificate, RemoteCertificateValidator, 60000, Destination, Headers);
		}

		/// <summary>
		/// Gets a (possibly big) resource, using a Uniform Resource Identifier (or Locator).
		/// </summary>
		/// <param name="Uri">URI</param>
		/// <param name="Certificate">Optional client certificate to use in a Mutual TLS session.</param>
		/// <param name="RemoteCertificateValidator">Optional callback method for validating remote certificates.</param>
		/// <param name="TimeoutMs">Timeout, in milliseconds. (Default=60000)</param>
		/// <param name="Headers">Optional headers. Interpreted in accordance with the corresponding URI scheme.</param>
		/// <returns>Content-Type, together with a Temporary file, if resource has been downloaded, or null if resource is data-less.</returns>
		public Task<ContentStreamResponse> GetTempStreamAsync(Uri Uri, X509Certificate Certificate,
			RemoteCertificateEventHandler RemoteCertificateValidator, int TimeoutMs, params KeyValuePair<string, string>[] Headers)
		{
			return this.GetTempStreamAsync(Uri, Certificate, RemoteCertificateValidator, TimeoutMs, null, Headers);
		}

		/// <summary>
		/// Gets a (possibly big) resource, using a Uniform Resource Identifier (or Locator).
		/// </summary>
		/// <param name="Uri">URI</param>
		/// <param name="Certificate">Optional Client certificate to use in a Mutual TLS session.</param>
		/// <param name="RemoteCertificateValidator">Optional validator of remote certificates.</param>
		/// <param name="TimeoutMs">Timeout, in milliseconds. (Default=60000)</param>
		/// <param name="Destination">Optional destination. Content will be output to this stream. If not provided, a new temporary stream will be created.</param>
		/// <param name="Headers">Optional headers. Interpreted in accordance with the corresponding URI scheme.</param>
		/// <returns>Content-Type, together with a Temporary file, if resource has been downloaded, or null if resource is data-less.</returns>
		public async Task<ContentStreamResponse> GetTempStreamAsync(Uri Uri, X509Certificate Certificate,
			RemoteCertificateEventHandler RemoteCertificateValidator, int TimeoutMs, TemporaryStream? Destination,
			params KeyValuePair<string, string>[] Headers)
		{
			(string ContentType, byte[] Bin, _) = await GetAndDecrypt(Uri, Certificate, RemoteCertificateValidator, TimeoutMs, Headers);

			Destination ??= new TemporaryStream();
			await Destination.WriteAsync(Bin, 0, Bin.Length);
			Destination.Position = 0;

			return new ContentStreamResponse(ContentType, Destination);
		}

	}
}
