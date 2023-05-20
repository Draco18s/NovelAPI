using System.Text.Json;
using net.novelai.api;
using System.Text.RegularExpressions;

namespace net.novelai.util
{
	public class gpt_bpe
	{
		public enum TrimDirection
		{
			TOP, BOTTOM, NONE
		}

		public enum MaxTrimType
		{
			SENTENCES, NEWLINES, TOKENS
		}

		public enum OutputTrimType
		{
			NONE, FIRST_LINE, SENTENCES
		}

		public enum InsertionType
		{
			NEWLINE
		}

		public struct GPTEncoder
		{
			public Dictionary<string, int> encoder;
			public Dictionary<int, string> decoder;
			public Dictionary<GPTPair, double> bpe_ranks;
			public Regex pattern;
			public Dictionary<byte, char> byteToRune;
			public Dictionary<char, char> runeToByte;
			public Dictionary<string, string[]> cache;

			public ushort[] Encode(string text)
			{
				string[] words = SplitWords(text);
				List<ushort> encoded = new List<ushort>();
				for (int idx = 0; idx < words.Length; idx++)
				{
					string fragment = toUnicode(words[idx]);
					string[] token = toBPE(fragment);
					encoded.AddRange(encodeTokens(token));
				}
				return encoded.ToArray();
			}

			public string Decode(ushort[] encoded)
			{
				return string.Join("", DecodeToTokens(encoded));
			}

			public string[] DecodeToTokens(ushort[] encoded)
			{
				List<string> bs = new List<string>();
				for (int idx = 0; idx < encoded.Length; idx++)
				{
					if (decoder.ContainsKey(encoded[idx]))
					{
						string v = fromUnicode(decoder[encoded[idx]]);
						bs.Add(v);
					}
				}
				return bs.ToArray();
			}

			public BGERank[] rankPairs(GPTPair[] pairs)
			{
				List<BGERank> rankedPairs = new List<BGERank>();
				for (int idx = 0; idx < pairs.Length; idx++)
				{
					double bpe;
					if (bpe_ranks.ContainsKey(pairs[idx]))
					{
						bpe = bpe_ranks[pairs[idx]];
					}
					else
					{
						bpe = double.PositiveInfinity;
					}
					rankedPairs.Add(new BGERank
					{
						rank = bpe,
						bigram = pairs[idx]
					});
				}
				rankedPairs.Sort((x, y) => x.rank.CompareTo(y.rank));
				return rankedPairs.ToArray();
			}

			public GPTPair minPair(GPTPair[] pairs)
			{
				BGERank[] rankedPairs = rankPairs(pairs);
				if (rankedPairs.Length > 0)
				{
					return rankedPairs[0].bigram;
				}
				return new GPTPair();
			}

			public string toUnicode(string text)
			{
				string result = "";
				foreach (char c in text)
				{
					byte b = (byte)c;
					result += byteToRune[b];
				}
				return result;
			}

			public string fromUnicode(string text)
			{
				string result = "";
				foreach (char c in text)
				{
					result += runeToByte[c];
				}
				return result;
			}

			public ushort[] encodeTokens(string[] tokens)
			{
				List<ushort> encoded = new List<ushort>();
				for (int idx = 0; idx < tokens.Length; idx++)
				{
					if (encoder.ContainsKey(tokens[idx]))
						encoded.Add((ushort)encoder[tokens[idx]]);
				}
				return encoded.ToArray();
			}

			public string[] toBPE(string text)
			{
				if (cache.ContainsKey(text)) return cache[text];
				string[] word = Regex.Split(text, string.Empty);
				GPTPair[] pairs = getPairs(word);
				if (pairs.Length == 0)
				{
					return new string[] { text };
				}
				while (true)
				{
					GPTPair bigram = minPair(pairs);
					if (!bpe_ranks.ContainsKey(bigram))
						break;
					string first = bigram.left;
					string second = bigram.right;
					List<string> newWord = new List<string>();
					for (int i = 0; i < word.Length;)
					{
						int j = pos(word, first, i);
						if (j == -1)
						{
							for (int k = i; k < word.Length; k++)
								newWord.Add(word[k]);
							break;
						}
						for (int k = i; k < j; k++)
							newWord.Add(word[k]);
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
						pairs = getPairs(word);
					}
				}
				cache[text] = word;
				return word;
			}

			public string[] SplitWords(string text)
			{
				int[][] idxes = pattern.FindAllStringIndex(text, 0);
				List<string> words = new List<string>();
				for (int i = 0; i < idxes.Length; i++)
				{
					words.Add(text.Substring(idxes[i][0], idxes[i][1]));
				}
				return words.ToArray();
			}

			public ushort[] TrimNewlines(ushort[] tokens, TrimDirection direction, int limit)
			{
				List<ushort> accTokens = new List<ushort>();
				if (tokens.Length <= limit)
				{
					return tokens;
				}
				else if (direction == TrimDirection.NONE || limit < 0)
				{
					return accTokens.ToArray();
				}
				string[] lines = Decode(tokens).Split('\n');
				int start = 0, end = 0, step = 0, idx;
				switch (direction)
				{
					case TrimDirection.TOP:
						start = lines.Length - 1;
						end = -1;
						step = -1;
						break;
					case TrimDirection.BOTTOM:
						start = 0;
						end = lines.Length;
						step = 1;
						break;
					default:
						return accTokens.ToArray();
				}
				for (idx = start; idx != end; idx += step)
				{
					string line = lines[idx];
					switch (direction)
					{
						case TrimDirection.TOP:
							line = "\n" + line;
							break;
						case TrimDirection.BOTTOM:
							line = line + "\n";
							break;
					}
					var newTokens = Encode(line);
					if (newTokens.Length + accTokens.Count > limit)
					{
						return accTokens.ToArray();
					}
					else
					{
						switch (direction)
						{
							case TrimDirection.TOP:
								List<ushort> n = new List<ushort>();
								n.AddRange(newTokens);
								n.AddRange(accTokens);
								accTokens = n; //{ new, acc }
								break;
							case TrimDirection.BOTTOM:
								accTokens.AddRange(newTokens); //{ acc, new }
								break;
						}
					}
				}
				return accTokens.ToArray();
			}

			public ushort[] TrimSentences(ushort[] tokens, TrimDirection direction, int limit)
			{
				List<ushort> accTokens = new List<ushort>();
				if (tokens.Length <= limit)
				{
					return tokens;
				}
				else if (direction == TrimDirection.NONE || limit < 0)
				{
					return accTokens.ToArray();
				}
				string str = Decode(tokens);
				List<string> sentences = SplitIntoSentences(str);
				int start = 0, end = 0, step = 0, idx;
				switch (direction)
				{
					case TrimDirection.TOP:
						start = sentences.Count - 1;
						end = -1;
						step = -1;
						break;
					case TrimDirection.BOTTOM:
						start = 0;
						end = sentences.Count;
						step = 1;
						break;
					default:
						return accTokens.ToArray();
				}
				for (idx = start; idx != end; idx += step)
				{
					string sentence = sentences[idx];
					switch (direction)
					{
						case TrimDirection.TOP:
							sentence = " " + sentence;
							break;
						case TrimDirection.BOTTOM:
							sentence = sentence + " ";
							break;
					}
					var newTokens = Encode(sentence);
					if (newTokens.Length + accTokens.Count > limit)
					{
						return accTokens.ToArray();
					}
					else
					{
						switch (direction)
						{
							case TrimDirection.TOP:
								List<ushort> n = new List<ushort>();
								n.AddRange(newTokens);
								n.AddRange(accTokens);
								accTokens = n; //{ new, acc }
								break;
							case TrimDirection.BOTTOM:
								accTokens.AddRange(newTokens); //{ acc, new }
								break;
						}
					}
				}
				return accTokens.ToArray();
			}

			public static List<string> SplitIntoSentences(string str)
			{
				char[] seperator = new char[] { '.', '?', '!' };
				List<string> sentences = new List<string>();
				int index = 0;
				bool quotes = false;
				bool newline = false;
				while (index < str.Length)
				{
					var word = str.Skip(index).TakeWhile(ch => {
						index++;
						if (ch == '"') quotes = !quotes;
						if (ch == '\n')
						{
							newline = true;
							return false;
						}
						return quotes || index <= 1 || !seperator.Contains(str[index - 2]);
					});
					string newstring = string.Join("", word).Trim();
					if (newline && !string.IsNullOrEmpty(newstring))
					{
						//newstring += ".";
						if (quotes)
							newstring += "\"";
						newstring += "\n";
					}
					if (!string.IsNullOrEmpty(newstring))
						sentences.Add(newstring);
				}
				sentences[sentences.Count - 1] = sentences[sentences.Count - 1].TrimEnd();
				return sentences;
			}
		}

		public struct GPTPair
		{
			public string left;
			public string right;
		}

		public struct BGERank
		{
			public double rank;
			public GPTPair bigram;
		}

		public static GPTPair[] getPairs(string[] word)
		{
			Dictionary<GPTPair, bool> pairsSet = new Dictionary<GPTPair, bool>();
			List<GPTPair> pairs = new List<GPTPair>();
			int begin = 0;
			string prev = word[0];
			for (int idx = begin; idx < word.Length; idx++)
			{
				string present = word[idx];
				GPTPair pair = new GPTPair
				{
					left = prev,
					right = present
				};
				if (!pairsSet.ContainsKey(pair))
				{
					pairs.Add(pair);
				}
				pairsSet[pair] = true;
				prev = present;
			}
			return pairs.ToArray();
		}

		public static int pos(string[] word, string seek, int i)
		{
			for (int j = i; j < word.Length; j++)
			{
				if (seek == word[j])
					return j;
			}
			return -1;
		}

		public static GPTEncoder NewEncoder()
		{
			string json = File.ReadAllText(NovelAPI.CONFIG_PATH + "/encoder.json");
			
			Dictionary<string, int> encoderTokens = JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? throw new Exception("GPTEncoder failure");
			Dictionary<int, string> tokensEncoder = new Dictionary<int, string>();
			foreach (KeyValuePair<string, int> entry in encoderTokens)
			{
				tokensEncoder.Add(entry.Value, entry.Key);
			}
			Dictionary<GPTPair, double> bpeRanks = new Dictionary<GPTPair, double>();
			bool firstLine = true;
			ushort idx = 0;
			foreach (string line in File.ReadAllLines(NovelAPI.CONFIG_PATH + "/vocab.bpe"))
			{
				if (firstLine)
				{
					firstLine = false;
					continue;
				}
				string[] left_right = line.Split(' ');
				GPTPair p = new GPTPair { left = left_right[0], right = left_right[1] };
				bpeRanks[p] = idx;
				idx++;
			}
			Regex pat = new Regex("'s|'t|'re|'ve|'m|'ll|'d| ?\\p{L}+| ?\\p{N}+| ?[^\\s\\p{L}\\p{N}]+|\\s+(\\S){0}|\\s+");
			List<byte> bs = new List<byte>();
			Dictionary<byte, char> bytesUnicode = new Dictionary<byte, char>();
			Dictionary<char, char> unicodeBytes = new Dictionary<char, char>();
			char gc = 'Ġ';
			ushort gb = (ushort)gc;
			for (byte b = (byte)'!'; b < (byte)'~' + 1; b++)
			{
				bs.Add(b);
				bytesUnicode[b] = (char)b;
				unicodeBytes[(char)b] = (char)b;
			}
			for (byte b = (byte)'¡'; b < (byte)'¬' + 1; b++)
			{
				bs.Add(b);
				bytesUnicode[b] = (char)b;
				unicodeBytes[(char)b] = (char)b;
			}
			for (ushort b = '®'; b < 'ÿ' + 1; b++)
			{
				bs.Add((byte)b);
				bytesUnicode[(byte)b] = (char)b;
				unicodeBytes[(char)b] = (char)b;
			}
			int uct = 0;
			for (ushort b = 0; b < 256; b++)
			{
				byte bb = (byte)b;
				if (!bytesUnicode.ContainsKey(bb))
				{
					bytesUnicode[(byte)b] = (char)(256 + uct);//
					unicodeBytes[(char)(256 + uct)] = (char)b;//
					uct += 1;
				}
			}
			return new GPTEncoder
			{
				encoder = encoderTokens,
				decoder = tokensEncoder,
				bpe_ranks = bpeRanks,
				pattern = pat,
				byteToRune = bytesUnicode,
				runeToByte = unicodeBytes,
				cache = new Dictionary<string, string[]>()
			};
		}
	}
}
