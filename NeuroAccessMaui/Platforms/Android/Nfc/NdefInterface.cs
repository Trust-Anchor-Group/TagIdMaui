using Android.Nfc;
using Android.Nfc.Tech;
using NeuroAccess.Nfc;
using NeuroAccess.Nfc.Records;
using NeuroAccessMaui.AndroidPlatform.Nfc.Records;
using System.Text;
using Waher.Content;
using Waher.Runtime.Inventory;

namespace NeuroAccessMaui.AndroidPlatform.Nfc
{
	/// <summary>
	/// Class handling NDEF Interfaces.
	/// </summary>
	/// <param name="Tag">Underlying Android Tag object.</param>
	/// <param name="Technology">NFC interface.</param>
	public class NdefInterface(Tag Tag, Ndef Technology)
		: NfcInterface(Tag, Technology), INdefInterface
	{
		private readonly Ndef ndef = Technology;

		/// <summary>
		/// If the TAG can be made read-only
		/// </summary>
		public Task<bool> CanMakeReadOnly()
		{
			return Task.FromResult(this.ndef.CanMakeReadOnly());
		}

		/// <summary>
		/// If the TAG is writable
		/// </summary>
		public Task<bool> IsWritable()
		{
			return Task.FromResult(this.ndef.IsWritable);
		}

		/// <summary>
		/// Gets the message (with records) of the NDEF tag.
		/// </summary>
		public Task<INdefRecord[]> GetMessage()
		{
			NdefMessage? Message = this.ndef.NdefMessage ?? throw UnableToReadDataFromDevice();
			NdefRecord[]? Records = Message.GetRecords() ?? throw UnableToReadDataFromDevice();
			List<INdefRecord> Result = [];

			foreach (NdefRecord Record in Records)
			{
				switch (Record.Tnf)
				{
					case NdefRecord.TnfAbsoluteUri:
						Result.Add(new UriRecord(Record));
						break;

					case NdefRecord.TnfMimeMedia:
						Result.Add(new MimeTypeRecord(Record));
						break;

					case NdefRecord.TnfWellKnown:
						byte[] Bin = Record.GetTypeInfo() ?? throw UnableToReadDataFromDevice();
						string TypeInfo = Encoding.UTF8.GetString(Bin);

						if (TypeInfo == "U")
							Result.Add(new UriRecord(Record));
						else
							Result.Add(new WellKnownTypeRecord(Record));
						break;

					case NdefRecord.TnfExternalType:
						Result.Add(new ExternalTypeRecord(Record));
						break;

					case NdefRecord.TnfEmpty:
					case NdefRecord.TnfUnchanged:
					case NdefRecord.TnfUnknown:
					default:
						break;
				}
			}

			return Task.FromResult<INdefRecord[]>([.. Result]);
		}

		/// <summary>
		/// Sets the message (with records) on the NDEF tag.
		/// </summary>
		/// <param name="Items">Items to encode</param>
		/// <returns>If the items could be encoded and written to the tag.</returns>
		public async Task<bool> SetMessage(params object[] Items)
		{
			try
			{
				NdefMessage Message = await CreateMessage(Items);
				await this.ndef.WriteNdefMessageAsync(Message);

				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}

		/// <summary>
		/// Creates an NDEF Message.
		/// </summary>
		/// <param name="Items">Items to encode.</param>
		/// <returns>Message object.</returns>
		/// <exception cref="ArgumentException">If an item could not be encoded.</exception>
		public static async Task<NdefMessage> CreateMessage(params object[] Items)
		{
			List<NdefRecord> Records = [];

			foreach (object Item in Items)
			{
				if (Item is null)
					continue;

				if (Item is Uri Uri)
				{
					if (Uri.IsAbsoluteUri)
						Records.Add(NdefRecord.CreateUri(Uri.AbsoluteUri) ?? throw new ArgumentException("Unable to create URI record.", nameof(Items)));
					else
						throw new ArgumentException("URI not absolute.", nameof(Items));
				}
				else if (Item is string s)
					Records.Add(NdefRecord.CreateTextRecord(null, s) ?? throw new ArgumentException("Unable to create text record.", nameof(Items)));
				else
				{
					if (Item is KeyValuePair<byte[], string> Mime)
						Records.Add(NdefRecord.CreateMime(Mime.Value, Mime.Key) ?? throw new ArgumentException("Unable to create MIME record.", nameof(Items)));
					else
					{
						if (!InternetContent.Encodes(Item, out Grade _, out IContentEncoder Encoder))
							throw new ArgumentException("Unable to encode objects of type " + Item.GetType().FullName + ".", nameof(Items));

						ContentResponse Encoded = await Encoder.EncodeAsync(Item, Encoding.UTF8, null);
						Encoded.AssertOk();

						Records.Add(NdefRecord.CreateMime(Encoded.ContentType, Encoded.Encoded)
							?? throw new ArgumentException("Unable to create MIME record.", nameof(Items)));
					}
				}
			}

			return new(Records.ToArray());
		}

	}
}
