using net.novelai.api;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace novelai.util
{
	public struct Config
	{
		public string splitRegex;
	}

	public class GPTPair
	{
		public string Left { get; set; }
		public string Right { get; set; }
	}

	public class BGERank
	{
		public int Rank { get; set; }
		public GPTPair Bigram { get; set; }
	}

	public class SpecialsTreeNode
	{
		public char Char { get; set; }
		public List<SpecialsTreeNode> Children { get; set; }
		public string Value { get; set; }
	}

	public class ByteEncoderDecoder
	{
		public Dictionary<int, string> ByteDecoder { get; set; }
		public Dictionary<string,int> ByteEncoder { get; set; }
	}

	public class TokenInfo
	{
		public string Token { get; set; }
		public int Id { get; set; }
	}

	public class KayraEncoder
	{
		//private readonly string[][] merges;
		private readonly Dictionary<string,int> specials;
		private readonly SpecialsTreeNode specialsTree;
		//private readonly Config config;
		private readonly Regex splitRegex;
		private readonly Dictionary<string, int> bpeRanks;
		//private Dictionary<string, int> tokenMerges;

		private readonly Dictionary<string,int> encoder;
		private readonly Dictionary<int, string> decoder;
		//private readonly Dictionary<string,int> charToByte;
		//private readonly Dictionary<int, string> byteToChar;
		private readonly Dictionary<string,int> bytesEncoder; // Used for sentencepiece
		private readonly Dictionary<string, int[]> cache = new Dictionary<string, int[]>();

		private static int CharCode(char character)
		{
			return char.ConvertToUtf32(character.ToString(), 0);
		}

		private static string Char(int charCode)
		{
			return char.ConvertFromUtf32(charCode);
		}

		public ByteEncoderDecoder BuildByteEncoderDecoder()
		{
			Dictionary<int, string> bytesUnicodeMap = new Dictionary<int, string>();
			Dictionary<string,int> unicodeBytes = new Dictionary<string,int>();

			for (int i = CharCode('!'); i <= CharCode('~'); i++)
			{
				bytesUnicodeMap[i] = Char(i);
				unicodeBytes[Char(i)] = i;
			}

			for (int i = CharCode('¡'); i <= CharCode('¬'); i++)
			{
				bytesUnicodeMap[i] = Char(i);
				unicodeBytes[Char(i)] = i;
			}

			for (int i = CharCode('®'); i <= CharCode('ÿ'); i++)
			{
				bytesUnicodeMap[i] = Char(i);
				unicodeBytes[Char(i)] = i;
			}

			int utc = 0;
			Dictionary<int, string> bytesUnicode = new Dictionary<int, string>();

			for (int i = 0; i < 256; i++)
			{
				if (!bytesUnicodeMap.ContainsKey(i))
				{
					bytesUnicodeMap[i] = Char(256 + utc);
					unicodeBytes[Char(256 + utc)] = i;
					utc++;
				}
				bytesUnicode[i] = bytesUnicodeMap[i];
			}

			return new ByteEncoderDecoder
			{
				ByteDecoder = bytesUnicode,
				ByteEncoder = unicodeBytes
			};
		}

		private struct Tokenizer
		{
			public Dictionary<string, int> vocab;
			public string[][] merges;
			public string[] specialTokens;
			public Config config;
		}

		public static KayraEncoder Create()
		{
			var tokenizerFilePath = NovelAPI.CONFIG_PATH + "/nerdstash_tokenizer.json";
			var tokenizerJson = File.ReadAllText(tokenizerFilePath);
			var tokenizer = JsonSerializer.Deserialize<Dictionary<string, object>>(tokenizerJson);
			var a = JsonSerializer.Deserialize<Dictionary<string, int>>(tokenizer["vocab"].ToString());
			var b = JsonSerializer.Deserialize<string[][]>(tokenizer["merges"].ToString());
			var c = JsonSerializer.Deserialize<string[]>(tokenizer["specialTokens"].ToString());
			var d = JsonSerializer.Deserialize<Dictionary<string, object>>(tokenizer["config"].ToString());
			var e = d["splitRegex"].ToString();
			return new KayraEncoder(a,
				b,
				c,
				new Config()
				{
					splitRegex = e
				});
	}

		public KayraEncoder(Dictionary<string,int> vocab, string[][] merges, string[] specials, Config config)
		{
			//this.merges = merges;
			this.specials = specials
				.Select(special => new { Key = special, Value = vocab[special] })
				.ToDictionary(item => item.Key, item => item.Value);
			//this.config = config;

			//ByteEncoderDecoder byteEncoderDecoder = BuildByteEncoderDecoder();
			//this.byteToChar = byteEncoderDecoder.ByteDecoder;
			//this.charToByte = byteEncoderDecoder.ByteEncoder;

			this.encoder = vocab;
			Dictionary<string, int> byEnc = new Dictionary<string,int>();
			bool hasByteRunes = false;

			this.decoder = new Dictionary<int, string>();
			foreach (KeyValuePair<string, int> item in this.encoder)
			{
				var key = item.Key;
				var value = item.Value;

				if (key.StartsWith("0x"))
				{
					// Byte rune
					hasByteRunes = true;
					var byteValue = int.Parse(key.Substring(2), System.Globalization.NumberStyles.HexNumber);
					byEnc[byteValue.ToString()] = value;
				}

				this.decoder[value] = key;
			}

			if (hasByteRunes)
			{
				this.bytesEncoder = byEnc;
			}

			Dictionary<string, int> bpe_ranks = new Dictionary<string, int>();
			for (int i = 0; i < merges.Length; i++)
			{
				var merge = merges[i];
				var pair = string.Join("", merge);
				bpe_ranks[pair] = i;
			}
			this.bpeRanks = bpe_ranks;

			//Dictionary<string, int> mergeDict = new Dictionary<string, int>();
			//foreach (var pair in bpe_ranks.Keys)
			//{
			//	mergeDict[pair] = this.encoder[pair];
			//}
			//this.tokenMerges = mergeDict;

			List<KeyValuePair<string, int>> specialsSorted = this.specials
				.OrderByDescending(item => item.Key.Length)
				.ToList();

			SpecialsTreeNode specTree = new SpecialsTreeNode { Char = (char)0, Children = new List<SpecialsTreeNode>() };
			foreach (KeyValuePair<string, int> item in specialsSorted)
			{
				var special = item.Key;
				SpecialsTreeNode currentNode = specTree;

				foreach (var c in special)
				{
					var found = false;
					foreach (SpecialsTreeNode child in currentNode.Children)
					{
						if (child.Char == c)
						{
							currentNode = child;
							found = true;
							break;
						}
					}

					if (!found)
					{
						SpecialsTreeNode newNode = new SpecialsTreeNode { Char = c, Children = new List<SpecialsTreeNode>() };
						currentNode.Children.Add(newNode);
						currentNode = newNode;
					}
				}

				currentNode.Value = special;
			}

			this.specialsTree = specTree;

			this.splitRegex = new Regex(config.splitRegex, RegexOptions.Compiled | RegexOptions.Multiline);
		}

		private string[] SplitWords(string text)
		{
			List<string> words = new List<string>();
			SpecialsTreeNode specialRoot = this.specialsTree;
			Regex regex = this.splitRegex;
			string accumulated = string.Empty;
			string accumulatedSpecial = string.Empty;
			SpecialsTreeNode currentSpecialNode = specialRoot;
			SpecialsTreeNode lastFullSpecialNode = null;

			void Split()
			{
				if (!string.IsNullOrEmpty(accumulated))
				{
					MatchCollection matches = regex.Matches(accumulated);
					foreach (Match match in matches)
					{
						words.Add(match.Value);
					}
					accumulated = string.Empty;
				}
				if (!string.IsNullOrEmpty(accumulatedSpecial))
				{
					words.Add(accumulatedSpecial);
					accumulatedSpecial = string.Empty;
					currentSpecialNode = specialRoot;
				}
			}

			int i = 0;
			while (i < text.Length)
			{
				char currentChar = text[i];
				bool specialFound = false;

				foreach (SpecialsTreeNode child in currentSpecialNode.Children)
				{
					if (child.Char == currentChar)
					{
						currentSpecialNode = child;
						specialFound = true;
						break;
					}
				}

				if (specialFound)
				{
					accumulatedSpecial += currentChar;
					i++;
				}
				else
				{
					if (accumulatedSpecial.Length == 0)
					{
						accumulated += currentChar;
						i++;
					}
					else if (lastFullSpecialNode == null)
					{
						accumulated += accumulatedSpecial[0];
						i -= accumulatedSpecial.Length - 1;
						accumulatedSpecial = string.Empty;
						currentSpecialNode = specialRoot;
					}
					else if (lastFullSpecialNode.Value != null)
					{
						string extra = accumulatedSpecial.Substring(lastFullSpecialNode.Value.Length);
						accumulatedSpecial = lastFullSpecialNode.Value;
						i -= extra.Length;
						lastFullSpecialNode = null;
						Split();
					}
				}

				if (currentSpecialNode.Value != null && accumulatedSpecial == currentSpecialNode.Value)
				{
					lastFullSpecialNode = currentSpecialNode;
				}
			}

			if (!string.IsNullOrEmpty(accumulatedSpecial))
			{
				if (lastFullSpecialNode?.Value != null)
				{
					string extra = accumulatedSpecial.Substring(lastFullSpecialNode.Value.Length);
					accumulatedSpecial = lastFullSpecialNode.Value;
					Split();
					accumulated = extra;
				}
				else
				{
					accumulated += accumulatedSpecial;
					accumulatedSpecial = string.Empty;
				}
			}

			Split();

			return words.ToArray();
		}

		private int[] ToBPE(string text)
		{
			if (cache.TryGetValue(text, out var c))
			{
				return c;
			}

			string[] word = text.Select(t => t.ToString()).ToArray();
			List<BGERank> rankedPairs = GetRankedPairs(word);
			if (rankedPairs.Count == 0)
			{
				List<int> tokens = new List<int>();
				if (encoder.TryGetValue(text, out var v))
				{
					tokens.Add(v);
				}
				else
				{
					foreach (byte charValue in EncodeStr(text))
					{
						tokens.Add(bytesEncoder[charValue.ToString()]);
					}
				}

				int[] tokensArray = tokens.ToArray();
				cache[text] = tokensArray;
				return tokensArray;
			}

			while (true)
			{
				GPTPair gptPair = rankedPairs[0].Bigram;
				string first = gptPair.Left;
				string second = gptPair.Right;
				List<string> newWord = new List<string>();
				int i = 0;
				while (i < word.Length)
				{
					int j = Array.IndexOf(word, first, i);
					if (j == -1)
					{
						newWord.AddRange(word.Skip(i));
						break;
					}
					newWord.AddRange(word.Skip(i).Take(j - i));
					i = j;
					if (word[i] == first && i < word.Length - 1 && word[i + 1] == second)
					{
						newWord.Add(first + second);
						i += 2;
					}
					else
					{
						newWord.Add(word[i]);
						i += 1;
					}
				}
				word = newWord.ToArray();
				if (word.Length == 1)
				{
					break;
				}
				else
				{
					rankedPairs = GetRankedPairs(word);
				}
			}

			List<int> finalTokens = new List<int>();
			foreach (string token in word)
			{
				if (encoder.TryGetValue(token, out var v))
				{
					finalTokens.Add(v);
				}
				else if (bytesEncoder != null)
				{
					foreach (byte charValue in EncodeStr(token))
					{
						finalTokens.Add(bytesEncoder[charValue.ToString()]);
					}
				}
			}
			int[] resultTokens = finalTokens.ToArray();
			cache[text] = resultTokens;
			return resultTokens;
		}

		public List<byte> EncodeStr(string str)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(str);
			return bytes.ToList();
		}

		public string DecodeStr(IEnumerable<int> arr)
		{
			var bytes = arr.Select(i => (byte)i).ToArray();
			return Encoding.UTF8.GetString(bytes);
		}

		public ushort[] Encode(string data)
		{
			// Split the data into words.
			string[] words = SplitWords(data);
			List<int> encodedTokens = new List<int>();
			foreach (var word in words)
			{
				// Handle special tokens.
				if (specials.TryGetValue(word, out var s))
				{
					encodedTokens.Add(s);
					continue;
				}
				var fragment = ToUnicode(word);
				encodedTokens.AddRange(ToBPE(fragment));
			}
			return encodedTokens.Select(n => (ushort)n).ToArray();
		}

		public string Decode(List<ushort> tokens)
		{
			string text = "";
			List<int> accumulatedBytes = new List<int>();
			foreach (var token in tokens)
			{
				var str = decoder[token];
				// If it starts with 0x, it's a byte token.
				if (str.StartsWith("0x"))
				{
					// Accumulate bytes.
					accumulatedBytes.Add(int.Parse(str));
				}
				else
				{
					// Decode accumulated bytes.
					if (accumulatedBytes.Count > 0)
					{
						text += DecodeStr(accumulatedBytes);
						accumulatedBytes.Clear();
					}
					// Decode the token.
					text += str;
				}
			}
			// Decode remaining bytes.
			if (accumulatedBytes.Count > 0)
			{
				text += DecodeStr(accumulatedBytes);
			}

			/*if (bytesEncoder == null)
			{
				IEnumerable<byte> converted = text.SelectMany(x =>
				{
					List<byte> encoded = charToByte.ContainsKey(x.ToString()) ? new List<byte> { (byte)charToByte[x.ToString()] } : EncodeStr(x.ToString());
					return encoded;
				});
				return DecodeStr(converted.Select(b => (int)b));
			}*/

			return text;
		}

		private string ToUnicode(string data)
		{
			return data;
			/*if (this.bytesEncoder != null)
			{
				// No transformation needed.
				return data;
			}
			else
			{
				// Transform using byteToChar.
				return string.Join("", EncodeStr(data).Select(byteValue => this.byteToChar[byteValue])); ;
			}*/
		}

		private void InsertSortedNoDups(List<BGERank> data, BGERank item)
		{
			int i = 0;
			while (i < data.Count && data[i].Rank < item.Rank)
			{
				i++;
			}
			if (i < data.Count && data[i].Rank == item.Rank)
			{
				return;
			}
			data.Insert(i, item);
		}

		private List<BGERank> GetRankedPairs(string[] word)
		{
			var rankedPairs = new List<BGERank>();
			string prev = word[0];
			for (int i = 1; i < word.Length; i++)
			{
				string current = word[i];
				string pair = prev + current;
				int? rank = this.bpeRanks.GetValueOrDefault(pair, int.MaxValue);
				this.InsertSortedNoDups(rankedPairs, new BGERank
				{
					Rank = rank.Value,
					Bigram = new GPTPair
					{
						Left = prev,
						Right = current
					}
				});
				prev = current;
			}
			return rankedPairs;
		}

		public List<TokenInfo> TokensContaining(string str)
		{
			var keys = encoder.Keys;
			var arr = new List<TokenInfo>();
			foreach (var key in keys)
			{
				if (key.Contains(str))
				{
					arr.Add(new TokenInfo { Token = key, Id = encoder[key] });
				}
			}
			return arr;
		}

		// Note: This is very slow.
		public List<int> MakeUnitrim()
		{
			var unicodeReq = new List<int>();
			var encoderKeys = encoder.Keys.ToArray();
			for (int i = 0; i < encoderKeys.Length; i++)
			{
				var v = decoder[i];
				int need = 0;
				int minNeed = 0;
				// Turn the string into bytes.
				List<byte> bytes = new List<byte>();
				if (bytesEncoder != null && v.StartsWith("0x"))
				{
					// Byte tokens start with 0x.
					bytes.Add(byte.Parse(v));
				}
				else
				{
					bytes = EncodeStr(v);
				}

				foreach (var c in bytes)
				{
					if ((c & 0b10000000) == 0)
					{
						need = 0;
					}
					else if ((c & 0b11000000) == 0b10000000)
					{
						need -= 1;
					}
					else if ((c & 0b11100000) == 0b11000000)
					{
						need = 1;
					}
					else if ((c & 0b11110000) == 0b11100000)
					{
						need = 2;
					}
					else if ((c & 0b11111000) == 0b11110000)
					{
						need = 3;
					}
					if (need < minNeed)
					{
						minNeed = need;
					}
				}
				if (need == 0)
				{
					need = minNeed;
				}
				unicodeReq.Add(need);
			}

			return unicodeReq;
		}

		public int TotalTokens()
		{
			return encoder.Count;
		}
	}
}
