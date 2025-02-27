using System;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Waher.Content;
using Waher.Runtime.IO;
using Waher.Security;

namespace NeuroAccess.Nfc.Extensions
{
	/// <summary>
	/// Contains NFC Extensions for Basic Access Control.
	/// 
	/// Reference:
	/// §4.3, https://www.icao.int/publications/Documents/9303_p11_cons_en.pdf
	/// </summary>
	public static class BasicAccessControl
	{
		/// <summary>
		/// Derives Basic Access Control Keys from the second row of the 
		/// Machine-Readable string in passport (MRZ).
		/// </summary>
		/// <param name="MRZ">Machine-Readable text.</param>
		/// <param name="Info">Parsed Document Information.</param>
		/// <returns>If the string could be parsed.</returns>
		public static bool ParseMrz(string MRZ, out DocumentInformation? Info)
		{
			Match M = td2_mrz_nr9charsplus.Match(MRZ);
			if (M.Success)
			{
				Info = AssembleInfo2(M);
				return Info is not null;
			}

			M = td2_mrz_nr9chars.Match(MRZ);
			if (M.Success)
			{
				Info = AssembleInfo1(M);
				return Info is not null;
			}

			M = td1_mrz_nr9charsplus.Match(MRZ);
			if (M.Success)
			{
				Info = AssembleInfo2(M);
				return Info is not null;
			}

			M = td1_mrz_nr9chars.Match(MRZ);
			if (M.Success)
			{
				Info = AssembleInfo1(M);
				return Info is not null;
			}

			// TODO: Checks

			Info = null;
			return false;
		}

		private static DocumentInformation? AssembleInfo2(Match M)
		{
			DocumentInformation Result = AssembleInfo(M);
			Result.DocumentNumber = M.Groups["Nr1"].Value + M.Groups["Nr2"].Value;

			return CalcMrzInfo(Result, M) ? Result : null;
		}

		private static DocumentInformation? AssembleInfo1(Match M)
		{
			DocumentInformation Result = AssembleInfo(M);
			Result.DocumentNumber = M.Groups["Nr"].Value;

			return CalcMrzInfo(Result, M) ? Result : null;
		}

		private static bool CalcMrzInfo(DocumentInformation Info, Match M)
		{
			if (Info.DocumentNumber is null || Info.DateOfBirth is null || Info.ExpiryDate is null)
				return false;

			string NrCheck = M.Groups["NrCheck"].Value;
			if (NrCheck != CalcCheckDigit(Info.DocumentNumber))
				return false;

			string BirthCheck = M.Groups["BirthCheck"].Value;
			if (BirthCheck != CalcCheckDigit(Info.DateOfBirth))
				return false;

			string ExpiryCheck = M.Groups["ExpiryCheck"].Value;
			if (ExpiryCheck != CalcCheckDigit(Info.ExpiryDate))
				return false;

			if (!string.IsNullOrEmpty(Info.OptionalData))
			{
				string s = Info.OptionalData!.Replace("<", string.Empty);

				if (!string.IsNullOrEmpty(s))
				{
					string OptionalCheck = M.Groups["OptionalCheck"].Value;
					if (OptionalCheck != CalcCheckDigit(Info.OptionalData))
						return false;
				}

				Info.OptionalData = s;
			}

			// TODO: Check OverallCheck

			Info.MRZ_Information = Info.DocumentNumber + NrCheck +
				Info.DateOfBirth + BirthCheck + Info.ExpiryDate + ExpiryCheck;

			Info.DocumentNumber = Info.DocumentNumber.Replace("<", string.Empty);

			return true;
		}

		private static string CalcCheckDigit(string Value)
		{
			// §4.9, ISO/IEC 9303, Part 3: https://www.icao.int/publications/Documents/9303_p3_cons_en.pdf

			int Sum = 0;
			int i = 0;
			int j;

			foreach (char ch in Value)
			{
				if (ch >= '0' && ch <= '9')
					j = ch - '0';
				else if (ch >= 'A' && ch <= 'Z')
					j = ch - 'A' + 10;
				else if (ch >= 'a' && ch <= 'z')
					j = ch - 'a' + 10;
				else if (ch == '<')
					j = 0;
				else
					return string.Empty;

				j *= weights[i++];
				Sum += j;
				i %= 3;
			}

			return new string((char)('0' + Sum % 10), 1);
		}

		private static readonly int[] weights = [7, 3, 1];

		private static DocumentInformation AssembleInfo(Match M)
		{
			return new DocumentInformation()
			{
				DocumentType = M.Groups["DocType"].Value,
				IssuingState = M.Groups["Issuer"].Value,
				Nationality = M.Groups["Nationality"].Value,
				PrimaryIdentifier = M.Groups["PID"].Value.Split('<'),
				SecondaryIdentifier = M.Groups["SID"].Value.Split('<'),
				Gender = M.Groups["Gender"].Value,
				DocumentNumber = M.Groups["Nr"].Value,
				DateOfBirth = M.Groups["Birth"].Value,
				ExpiryDate = M.Groups["Expires"].Value,
				OptionalData = M.Groups["Optional"].Value
			};
		}

		// TD2, ref: ICAO 9303-5, §B: https://www.icao.int/publications/Documents/9303_p5_cons_en.pdf
		private static readonly Regex td2_mrz_nr9charsplus = new(@"^(?'DocType'.{1,2})<(?'Issuer'\w{3})(?'PID'[^<]+(<[^<]+)*)<<(?'SID'[^<]+(<[^<]+)*)<*\n(?'Nr1'[^<]{9})<(?'Nationality'\w{3})(?'Birth'[^<]*)(?'BirthCheck'\d)(?'Gender'[MF])(?'Expires'[^<]{6})(?'ExpiryCheck'\d)(?'Nr2'[^<]*)(?'NrCheck'\d)((?'Optional'.*)(?'OptionalCheck'\d))?<*(?'OverallCheck'\d)$", RegexOptions.Multiline);
		private static readonly Regex td2_mrz_nr9chars = new(@"^(?'DocType'.{1,2})<(?'Issuer'\w{3})(?'PID'[^<]+(<[^<]+)*)<<(?'SID'[^<]+(<[^<]+)*)<*\n(?'Nr'.{9})(?'NrCheck'\d)(?'Nationality'\w{3})(?'Birth'[^<]{6})(?'BirthCheck'\d)(?'Gender'[MF])(?'Expires'[^<]{6})(?'ExpiryCheck'\d)((?'Optional'.*)(?'OptionalCheck'\d))?<*(?'OverallCheck'\d)$", RegexOptions.Multiline);

		// TD1, ref: ICAO 9303-5, §B: https://www.icao.int/publications/Documents/9303_p5_cons_en.pdf
		private static readonly Regex td1_mrz_nr9charsplus = new(@"^(?'DocType'.{1,2})<(?'Issuer'\w{3})(?'Nr1'[^<]{9})<(?'Nr2'.{3})(?'NrCheck'\d)((?'Optional'.*)(?'OptionalCheck'\d))?<*\n(?'Birth'[^<]{6})(?'BirthCheck'\d)(?'Gender'[MF])(?'Expires'[^<]{6})(?'ExpiryCheck'\d)(?'Nationality'\w{3})<*(?'OverallCheck'\d)\n(?'PID'[^<]+(<[^<]+)*)<<(?'SID'[^<]+(<[^<]+)*).*$", RegexOptions.Multiline);
		private static readonly Regex td1_mrz_nr9chars = new(@"^(?'DocType'.{1,2})<(?'Issuer'\w{3})(?'Nr'.{9})(?'NrCheck'.)((?'Optional'.*)(?'OptionalCheck'\d))?<*\n(?'Birth'[^<]{6})(?'BirthCheck'\d)(?'Gender'[MF])(?'Expires'[^<]{6})(?'ExpiryCheck'\d)(?'Nationality'\w{3})<*(?'OverallCheck'\d)\n(?'PID'[^<]+(<[^<]+)*)<<(?'SID'[^<]+(<[^<]+)*).*$", RegexOptions.Multiline);

		/// <summary>
		/// Seed for computing cryptographic keys (§D.2)
		/// </summary>
		/// <param name="Info">Document Information</param>
		public static byte[] KSeed(this DocumentInformation Info)
		{
			byte[] Data = InternetContent.ISO_8859_1.GetBytes(Info.MRZ_Information);
			byte[] H = Hashes.ComputeSHA1Hash(Data);
			Array.Resize<byte>(ref H, 16);
			return H;
		}

		/// <summary>
		/// 3DES Encryption Key (§D.1)
		/// </summary>
		public static byte[] KEnc(this DocumentInformation Info)
		{
			return CalcKey(Info, 1);
		}

		/// <summary>
		/// DES MAC Key (§D.1)
		/// </summary>
		public static byte[] KMac(this DocumentInformation Info)
		{
			return CalcKey(Info, 2);
		}

		private static byte[] CalcKey(this DocumentInformation Info, int Counter)
		{
			byte[] KSeed = Info.KSeed();
			byte[] D = new byte[20];
			Array.Copy(KSeed, 0, D, 0, 16);
			int i;

			for (i = 19; i >= 16; i--)
			{
				D[i] = (byte)(Counter);
				Counter >>= 8;
			}

			byte[] H = Hashes.ComputeSHA1Hash(D);
			Array.Resize<byte>(ref H, 16);
			OddParity(H);

			return H;
		}

		private static void OddParity(byte[] H)
		{
			int i, j, c = H.Length;
			byte b;

			for (i = 0; i < c; i++)
			{
				b = H[i];
				j = 0;

				while (b != 0)
				{
					j += (b & 1);
					b >>= 1;
				}

				if ((j & 1) == 0)
					H[i] ^= 1;
			}
		}

		/// <summary>
		/// Concatenates a series of byte arrays.
		/// </summary>
		/// <param name="Bytes">First byte array</param>
		/// <param name="MoreBytes">following bytes arrays.</param>
		/// <returns>Concatenated byte array.</returns>
		public static byte[] CONCAT(this byte[] Bytes, params byte[][] MoreBytes)
		{
			int c = Bytes.Length;
			int i = c;

			foreach (byte[] A in MoreBytes)
				c += A.Length;

			byte[] Result = new byte[c];

			Array.Copy(Bytes, 0, Result, 0, i);

			foreach (byte[] A in MoreBytes)
			{
				Array.Copy(A, 0, Result, i, c = A.Length);
				i += c;
			}

			return Result;
		}

		/// <summary>
		/// Calculates a response to a challenge.
		/// </summary>
		/// <param name="Challenge">Challenge</param>
		/// <param name="Rnd1">Random number 1</param>
		/// <param name="Rnd2">Random number 2</param>
		/// <param name="KEnc">Encryption Key</param>
		/// <param name="KMac">MAC Key</param>
		/// <returns>Response</returns>
		public static byte[] CalcChallengeResponse(byte[] Challenge, byte[] Rnd1, byte[] Rnd2,
			byte[] KEnc, byte[] KMac)
		{
			byte[] S = Rnd1.CONCAT(Challenge, Rnd2);
			byte[] EIFD;
			byte[] MIFD;

			using (TripleDES Cipher = TripleDES.Create())
			{
				Cipher.Mode = CipherMode.CBC;
				Cipher.Padding = PaddingMode.None;

				using ICryptoTransform Encryptor = Cipher.CreateEncryptor(KEnc, new byte[8]);
				EIFD = Encryptor.TransformFinalBlock(S, 0, 32);
			}

			// MAC Algorithm described in ISO/IEC 9797-1
			// Ref: https://en.wikipedia.org/wiki/ISO/IEC_9797-1

			using (DES Cipher = DES.Create())
			{
				Cipher.Mode = CipherMode.CBC;
				Cipher.Padding = PaddingMode.None;

				int i = 0;
				int c = EIFD.Length;
				int j;

				byte[] Data = new byte[c + 8];
				Array.Copy(EIFD, 0, Data, 0, c);
				Data[c] = 0x80;   // Padding method 2, append 80 00 00 00 00 00 00 00

				byte[] Ka = new byte[8];
				byte[] Kb = new byte[8];

				Array.Copy(KMac, 0, Ka, 0, 8);
				Array.Copy(KMac, 8, Kb, 0, 8);

				byte[] Block = new byte[8];
				byte[]? H = null;

				c += 8;
				using (ICryptoTransform Encryptor2 = Cipher.CreateEncryptor(Ka, new byte[8]))
				{
					while (i < c)
					{
						Array.Copy(Data, i, Block, 0, 8);
						i += 8;

						if (H is not null)
						{
							for (j = 0; j < 8; j++)
								Block[j] ^= H[j];
						}

						H = Encryptor2.TransformFinalBlock(Block, 0, 8);
					}

					using (ICryptoTransform FinalDecryptor = Cipher.CreateDecryptor(Kb, new byte[8]))
					{
						H = FinalDecryptor.TransformFinalBlock(H, 0, 8);
					}

					H = Encryptor2.TransformFinalBlock(H, 0, 8);
				}

				MIFD = H;
			}

			return EIFD.CONCAT(MIFD);
		}

		/// <summary>
		/// Calculates a response to a challenge.
		/// </summary>
		/// <param name="Info">Document Information</param>
		/// <param name="Challenge">Challenge</param>
		/// <returns>Response</returns>
		public static byte[] CalcChallengeResponse(this DocumentInformation Info, byte[] Challenge)
		{
			byte[] Rnd1 = new byte[8];
			byte[] Rnd2 = new byte[16];

			using (RandomNumberGenerator Rnd = RandomNumberGenerator.Create())
			{
				Rnd.GetBytes(Rnd1);
				Rnd.GetBytes(Rnd2);
			}

			return CalcChallengeResponse(Challenge, Rnd1, Rnd2, Info.KEnc(), Info.KMac());
		}

		/// <summary>
		/// Get Challenge (§7.1.5.4, §D.3)
		/// </summary>
		/// <param name="TagInterface">NFC interface to tag.</param>
		/// <returns>Challenge</returns>
		public static async Task<byte[]?> GetChallenge(this IIsoDepInterface TagInterface)
		{
			byte[] Command =
			[
				0x00,	// CLA
				0x84,	// INS
				0x00,	// P1
				0x00,	// P2
				0x08	// Le
			];

			byte[] Response = await TagInterface.ExecuteCommand(Command);
			if (Response.Length != 10 || Response[8] != 0x90 || Response[9] != 0x00)
				return null;

			byte[] Challenge = new byte[8];
			Array.Copy(Response, 0, Challenge, 0, 8);

			return Response;
		}

		/// <summary>
		/// Send Response to challenge (§7.1.5.4, §D.3)
		/// </summary>
		/// <param name="TagInterface">NFC interface to tag.</param>
		/// <param name="ChallengeResponse">ChallengeResponse.</param>
		/// <returns>Challenge</returns>
		public static async Task<byte[]?> ExternalAuthenticate(this IIsoDepInterface TagInterface,
			byte[] ChallengeResponse)
		{
			byte Lc = (byte)ChallengeResponse.Length;
			byte[] Command = new byte[]
			{
				0x00,	// CLA
				0x82,	// INS
				0x00,	// P1
				0x00,	// P2
				Lc
			}.CONCAT(ChallengeResponse,
			[
				0x28	// Le
			]);

			byte[] Response = await TagInterface.ExecuteCommand(Command);
			if (Response.Length != 10 || Response[8] != 0x90 || Response[9] != 0x00)
				return null;

			byte[] Challenge = new byte[8];
			Array.Copy(Response, 0, Challenge, 0, 8);

			return Response;
		}
	}
}
