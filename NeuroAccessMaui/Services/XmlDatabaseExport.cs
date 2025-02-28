﻿using System.Globalization;
using System.Text;
using System.Xml;
using Waher.Content;
using Waher.Content.Xml;
using Waher.Events;
using Waher.Persistence;
using Waher.Persistence.Serialization;

namespace NeuroAccessMaui.Services
{
	/// <summary>
	/// Exports database contents to XML.
	/// </summary>
	/// <param name="Output">XML output is directed to this XML writer.</param>
	/// <param name="BinaryDataSizeLimit">Size limit of binary data fields. If larger, only a byte count will be presented.</param>
	public class XmlDatabaseExport(XmlWriter Output, int BinaryDataSizeLimit) : IDatabaseExportFilter, IDisposable
	{
		private readonly XmlWriter output = Output;
		private readonly int binaryDataSizeLimit = BinaryDataSizeLimit;
		private bool disposeWriter;

		/// <summary>
		/// Exports database contents to XML.
		/// </summary>
		/// <param name="Output">XML output is directed to this XML writer.</param>
		/// <param name="Indent">If output should be indented (true), or written without unnecessary whitespace (false).</param>
		/// <param name="BinaryDataSizeLimit">Size limit of binary data fields. If larger, only a byte count will be presented.</param>
		public XmlDatabaseExport(StringBuilder Output, bool Indent, int BinaryDataSizeLimit)
			: this(XmlWriter.Create(Output, XML.WriterSettings(Indent, true)), BinaryDataSizeLimit)
		{
			this.disposeWriter = true;
		}

		/// <summary>
		/// Is called when export of database is started.
		/// </summary>
		/// <returns>If export can continue.</returns>
		public Task<bool> StartDatabase()
		{
			this.output.WriteStartElement("Database");
			return Task.FromResult(true);
		}

		/// <summary>
		/// Is called when export of database is finished.
		/// </summary>
		/// <returns>If export can continue.</returns>
		public Task<bool> EndDatabase()
		{
			this.output.WriteEndElement();
			return Task.FromResult(true);
		}

		/// <summary>
		/// If a collection can be exported.
		/// </summary>
		/// <param name="CollectionName">Name of collection</param>
		/// <returns>If the collection can be exported.</returns>
		public bool CanExportCollection(string CollectionName)
		{
			return true;
		}

		/// <summary>
		/// Is called when a collection is started.
		/// </summary>
		/// <param name="CollectionName">Name of collection</param>
		/// <returns>If export can continue.</returns>
		public Task<bool> StartCollection(string CollectionName)
		{
			this.output.WriteStartElement("Collection");
			this.output.WriteAttributeString("name", CollectionName);
			return Task.FromResult(true);
		}

		/// <summary>
		/// Is called when a collection is finished.
		/// </summary>
		/// <returns>If export can continue.</returns>
		public Task<bool> EndCollection()
		{
			this.output.WriteEndElement();
			return Task.FromResult(true);
		}

		/// <summary>
		/// Is called when an index in a collection is started.
		/// </summary>
		/// <returns>If export can continue.</returns>
		public Task<bool> StartIndex()
		{
			this.output.WriteStartElement("Index");
			return Task.FromResult(true);
		}

		/// <summary>
		/// Is called when a field in an index is reported.
		/// </summary>
		/// <param name="FieldName">Name of field.</param>
		/// <param name="Ascending">If the field is sorted using ascending sort order.</param>
		/// <returns>If export can continue.</returns>
		public Task<bool> ReportIndexField(string FieldName, bool Ascending)
		{
			this.output.WriteStartElement("Index");
			this.output.WriteAttributeString("field", FieldName);
			this.output.WriteAttributeString("asc", CommonTypes.Encode(Ascending));
			this.output.WriteEndElement();
			return Task.FromResult(true);
		}

		/// <summary>
		/// Is called when an index in a collection is finished.
		/// </summary>
		/// <returns>If export can continue.</returns>
		public Task<bool> EndIndex()
		{
			this.output.WriteEndElement();
			return Task.FromResult(true);
		}

		/// <summary>
		/// If an object can be exported.
		/// </summary>
		/// <param name="Object">Object to be exported.</param>
		/// <returns>If the object can be exported.</returns>
		public bool CanExportObject(GenericObject Object)
		{
			if (Object.CollectionName != "Settings")
				return true;

			if (!Object.TryGetFieldValue("Key", out object Value) || Value is not string Key)
				return true;

			if (Key.StartsWith(ServiceRef.XmppService.ContractsClient.ContractKeySettingsPrefix, StringComparison.Ordinal) ||
				Key.StartsWith(ServiceRef.XmppService.ContractsClient.KeySettingsPrefix, StringComparison.Ordinal))
			{
				return false;
			}

			return true;
		}

		/// <summary>
		/// Is called when an object is started.
		/// </summary>
		/// <param name="ObjectId">ID of object.</param>
		/// <param name="TypeName">Type name of object.</param>
		/// <returns>Object ID of object, after optional mapping. null means export cannot continue</returns>
		public Task<string> StartObject(string ObjectId, string TypeName)
		{
			this.output.WriteStartElement("Obj");
			this.output.WriteAttributeString("id", ObjectId);
			this.output.WriteAttributeString("type", TypeName);
			return Task.FromResult(ObjectId);
		}

		/// <summary>
		/// Is called when a property is reported.
		/// </summary>
		/// <param name="PropertyName">Property name.</param>
		/// <param name="PropertyValue">Property value.</param>
		/// <returns>If export can continue.</returns>
		public async Task<bool> ReportProperty(string? PropertyName, object? PropertyValue)
		{
			if (PropertyValue is null)
			{
				this.output.WriteStartElement("Null");

				if (PropertyName is not null)
					this.output.WriteAttributeString("n", PropertyName);

				this.output.WriteEndElement();
			}
			else if (PropertyValue is Enum)
			{
				this.output.WriteStartElement("En");

				if (PropertyName is not null)
					this.output.WriteAttributeString("n", PropertyName);

				this.output.WriteAttributeString("v", string.Empty, PropertyValue.ToString());
				this.output.WriteEndElement();
			}
			else
			{
				switch (Type.GetTypeCode(PropertyValue.GetType()))
				{
					case TypeCode.Boolean:
						this.output.WriteStartElement("Bl");

						if (PropertyName is not null)
							this.output.WriteAttributeString("n", PropertyName);

						this.output.WriteAttributeString("v", string.Empty, CommonTypes.Encode((bool)PropertyValue));
						this.output.WriteEndElement();
						break;

					case TypeCode.Byte:
						this.output.WriteStartElement("B");

						if (PropertyName is not null)
							this.output.WriteAttributeString("n", PropertyName);

						this.output.WriteAttributeString("v", string.Empty, PropertyValue.ToString());
						this.output.WriteEndElement();
						break;

					case TypeCode.Char:
						this.output.WriteStartElement("Ch");

						if (PropertyName is not null)
							this.output.WriteAttributeString("n", PropertyName);

						this.output.WriteAttributeString("v", string.Empty, PropertyValue.ToString());
						this.output.WriteEndElement();
						break;

					case TypeCode.DateTime:
						this.output.WriteStartElement("DT");

						if (PropertyName is not null)
							this.output.WriteAttributeString("n", PropertyName);

						this.output.WriteAttributeString("v", string.Empty, XML.Encode((DateTime)PropertyValue));
						this.output.WriteEndElement();
						break;

					case TypeCode.Decimal:
						this.output.WriteStartElement("Dc");

						if (PropertyName is not null)
							this.output.WriteAttributeString("n", PropertyName);

						this.output.WriteAttributeString("v", string.Empty, CommonTypes.Encode((decimal)PropertyValue));
						this.output.WriteEndElement();
						break;

					case TypeCode.Double:
						this.output.WriteStartElement("Db");

						if (PropertyName is not null)
							this.output.WriteAttributeString("n", PropertyName);

						this.output.WriteAttributeString("v", string.Empty, CommonTypes.Encode((double)PropertyValue));
						this.output.WriteEndElement();
						break;

					case TypeCode.Int16:
						this.output.WriteStartElement("I2");

						if (PropertyName is not null)
							this.output.WriteAttributeString("n", PropertyName);

						this.output.WriteAttributeString("v", string.Empty, PropertyValue.ToString());
						this.output.WriteEndElement();
						break;

					case TypeCode.Int32:
						this.output.WriteStartElement("I4");

						if (PropertyName is not null)
							this.output.WriteAttributeString("n", PropertyName);

						this.output.WriteAttributeString("v", string.Empty, PropertyValue.ToString());
						this.output.WriteEndElement();
						break;

					case TypeCode.Int64:
						this.output.WriteStartElement("I8");

						if (PropertyName is not null)
							this.output.WriteAttributeString("n", PropertyName);

						this.output.WriteAttributeString("v", string.Empty, PropertyValue.ToString());
						this.output.WriteEndElement();
						break;

					case TypeCode.SByte:
						this.output.WriteStartElement("I1");

						if (PropertyName is not null)
							this.output.WriteAttributeString("n", PropertyName);

						this.output.WriteAttributeString("v", string.Empty, PropertyValue.ToString());
						this.output.WriteEndElement();
						break;

					case TypeCode.Single:
						this.output.WriteStartElement("Fl");

						if (PropertyName is not null)
							this.output.WriteAttributeString("n", PropertyName);

						this.output.WriteAttributeString("v", string.Empty, CommonTypes.Encode((float)PropertyValue));
						this.output.WriteEndElement();
						break;

					case TypeCode.String:
						string s = PropertyValue?.ToString() ?? string.Empty;
						try
						{
							XmlConvert.VerifyXmlChars(s);
							this.output.WriteStartElement("S");

							if (PropertyName is not null)
								this.output.WriteAttributeString("n", PropertyName);

							this.output.WriteAttributeString("v", string.Empty, s);
							this.output.WriteEndElement();
						}
						catch (XmlException)
						{
							byte[] Bin = Encoding.UTF8.GetBytes(s);
							s = Convert.ToBase64String(Bin);
							this.output.WriteStartElement("S64");

							if (PropertyName is not null)
								this.output.WriteAttributeString("n", PropertyName);

							this.output.WriteAttributeString("v", string.Empty, s);
							this.output.WriteEndElement();
						}
						break;

					case TypeCode.UInt16:
						this.output.WriteStartElement("U2");

						if (PropertyName is not null)
							this.output.WriteAttributeString("n", PropertyName);

						this.output.WriteAttributeString("v", string.Empty, PropertyValue.ToString());
						this.output.WriteEndElement();
						break;

					case TypeCode.UInt32:
						this.output.WriteStartElement("U4");

						if (PropertyName is not null)
							this.output.WriteAttributeString("n", PropertyName);

						this.output.WriteAttributeString("v", string.Empty, PropertyValue.ToString());
						this.output.WriteEndElement();
						break;

					case TypeCode.UInt64:
						this.output.WriteStartElement("U8");

						if (PropertyName is not null)
							this.output.WriteAttributeString("n", PropertyName);

						this.output.WriteAttributeString("v", string.Empty, PropertyValue.ToString());
						this.output.WriteEndElement();
						break;

					case TypeCode.DBNull:
					case TypeCode.Empty:
						this.output.WriteStartElement("Null");

						if (PropertyName is not null)
							this.output.WriteAttributeString("n", PropertyName);

						this.output.WriteEndElement();
						break;

					case TypeCode.Object:
						if (PropertyValue is TimeSpan)
						{
							this.output.WriteStartElement("TS");

							if (PropertyName is not null)
								this.output.WriteAttributeString("n", PropertyName);

							this.output.WriteAttributeString("v", string.Empty, PropertyValue.ToString());
							this.output.WriteEndElement();
						}
						else if (PropertyValue is DateTimeOffset DTO)
						{
							this.output.WriteStartElement("DTO");

							if (PropertyName is not null)
								this.output.WriteAttributeString("n", PropertyName);

							this.output.WriteAttributeString("v", string.Empty, XML.Encode(DTO));
							this.output.WriteEndElement();
						}
						else if (PropertyValue is CaseInsensitiveString Cis)
						{
							s = Cis.Value;
							try
							{
								XmlConvert.VerifyXmlChars(s);
								this.output.WriteStartElement("CIS");

								if (PropertyName is not null)
									this.output.WriteAttributeString("n", PropertyName);

								this.output.WriteAttributeString("v", string.Empty, s);
								this.output.WriteEndElement();
							}
							catch (XmlException)
							{
								byte[] Bin = Encoding.UTF8.GetBytes(s);
								s = Convert.ToBase64String(Bin);
								this.output.WriteStartElement("CIS64");

								if (PropertyName is not null)
									this.output.WriteAttributeString("n", PropertyName);

								this.output.WriteAttributeString("v", string.Empty, s);
								this.output.WriteEndElement();
							}
						}
						else if (PropertyValue is byte[] Bin)
						{
							this.output.WriteStartElement("Bin");

							if (PropertyName is not null)
								this.output.WriteAttributeString("n", PropertyName);

							long c = Bin.Length;

							if (c <= this.binaryDataSizeLimit)
							{
								if (c <= 1024)
									this.output.WriteAttributeString("v", Convert.ToBase64String(Bin));
								else
								{
									byte[]? Buf = null;
									long i = 0;
									long d;
									int j;

									while (i < c)
									{
										d = c - i;

										if (d > 49152)
											j = 49152;
										else
											j = (int)d;

										if (Buf is null)
										{
											if (i == 0 && j == c)
												Buf = Bin;
											else
												Buf = new byte[j];
										}

										if (Buf != Bin)
											Array.Copy(Bin, i, Buf, 0, j);

										this.output.WriteElementString("Chunk", Convert.ToBase64String(Buf, 0, j, Base64FormattingOptions.None));
										i += j;
									}
								}
							}
							else
								this.output.WriteAttributeString("bytes", c.ToString(CultureInfo.InvariantCulture));

							this.output.WriteEndElement();
						}
						else if (PropertyValue is Guid)
						{
							this.output.WriteStartElement("ID");

							if (PropertyName is not null)
								this.output.WriteAttributeString("n", PropertyName);

							this.output.WriteAttributeString("v", string.Empty, PropertyValue.ToString());
							this.output.WriteEndElement();
						}
						else if (PropertyValue is Array A)
						{
							this.output.WriteStartElement("Array");

							if (PropertyName is not null)
								this.output.WriteAttributeString("n", PropertyName);

							this.output.WriteAttributeString("elementType", string.Empty,
								PropertyValue.GetType().GetElementType()?.FullName);

							foreach (object Obj in A)
								await this.ReportProperty(null, Obj);

							this.output.WriteEndElement();
						}
						else if (PropertyValue is GenericObject Obj)
						{
							this.output.WriteStartElement("Obj");

							if (PropertyName is not null)
								this.output.WriteAttributeString("n", PropertyName);

							this.output.WriteAttributeString("type", string.Empty, Obj.TypeName);

							foreach (KeyValuePair<string, object?> P in Obj)
								await this.ReportProperty(P.Key, P.Value);

							this.output.WriteEndElement();
						}
						else
							throw new Exception("Unhandled property value type: " + PropertyValue.GetType().FullName);

						break;

					default:
						throw new Exception("Unhandled property value type: " + PropertyValue.GetType().FullName);
				}
			}

			return true;
		}

		/// <summary>
		/// Is called when an object is finished.
		/// </summary>
		/// <returns>If export can continue.</returns>
		public Task<bool> EndObject()
		{
			this.output.WriteEndElement();
			return Task.FromResult(true);
		}

		/// <summary>
		/// Is called when an error is reported.
		/// </summary>
		/// <param name="Message">Error message.</param>
		/// <returns>If export can continue.</returns>
		public Task<bool> ReportError(string Message)
		{
			this.output.WriteElementString("Error", Message);
			return Task.FromResult(true);
		}

		/// <summary>
		/// Is called when an exception has occurred.
		/// </summary>
		/// <param name="Exception">Exception object.</param>
		/// <returns>If export can continue.</returns>
		public async Task<bool> ReportException(Exception Exception)
		{
			this.output.WriteStartElement("Exception");
			this.output.WriteAttributeString("message", Exception.Message);
			this.output.WriteElementString("StackTrace", Log.CleanStackTrace(Exception.StackTrace));

			if (Exception is AggregateException AggregateException)
			{
				foreach (Exception ex in AggregateException.InnerExceptions)
					await this.ReportException(ex);
			}
			else if (Exception.InnerException is not null)
				await this.ReportException(Exception.InnerException);

			this.output.WriteEndElement();

			return true;
		}

		/// <summary>
		/// <see cref="IDisposable.Dispose"/>
		/// </summary>
		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// <see cref="IDisposable.Dispose"/>
		/// </summary>
		protected virtual void Dispose(bool disposing)
		{
			if (!this.disposeWriter)
				return;

			if (disposing)
			{
				this.output.Flush();
				this.output.Dispose();
			}

			this.disposeWriter = false;
		}
	}
}
