using Waher.Events;

namespace NeuroAccessMaui.Helpers
{
	public sealed class StreamWrapper(Stream Wrapped, IDisposable? AdditionalDisposable) : Stream
	{
		private readonly Stream wrapped = Wrapped;
		private IDisposable? additionalDisposable = AdditionalDisposable;

		public event EventHandler? Disposed;

		public StreamWrapper(Stream Wrapped) : this(Wrapped, null)
		{
		}

		public override long Position
		{
			get => this.wrapped.Position;
			set => this.wrapped.Position = value;
		}

		public override long Length => this.wrapped.Length;
		public override bool CanRead => this.wrapped.CanRead;
		public override bool CanSeek => this.wrapped.CanSeek;
		public override bool CanWrite => this.wrapped.CanWrite;

		public override void Flush() => this.wrapped.Flush();
		public override int Read(byte[] Buffer, int Offset, int Count) => this.wrapped.Read(Buffer, Offset, Count);
		public override long Seek(long Offset, SeekOrigin Origin) => this.wrapped.Seek(Offset, Origin);
		public override void SetLength(long Value) => this.wrapped.SetLength(Value);
		public override void Write(byte[] Buffer, int Offset, int Count) => this.wrapped.Write(Buffer, Offset, Count);

		protected override void Dispose(bool Disposing)
		{
			this.wrapped.Dispose();

			Disposed.Raise(this, EventArgs.Empty);

			this.additionalDisposable?.Dispose();
			this.additionalDisposable = null;

			base.Dispose(Disposing);
		}

		public static async Task<Stream?> GetStreamAsync(Uri uri, HttpClient client, CancellationToken cancellationToken)
		{
			HttpResponseMessage response = await client.GetAsync(uri, cancellationToken).ConfigureAwait(false);

			if (!response.IsSuccessStatusCode)
			{
				return null;
			}

			// it needs to be disposed after the calling code is done with the stream
			// otherwise the stream may get disposed before the caller can use it
			return new StreamWrapper(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), response);
		}
	}
}
