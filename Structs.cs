using net.novelai.util;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using novelai.util;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace net.novelai.api
{
    [JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    public class Structs
	{
        public static string ENDPOINT = "https://api.novelai.net/";

        #region authentication

        public struct AuthConfig
		{
            [JsonInclude]
			public string Username;
            [JsonInclude]
            public string Password;
            [JsonInclude]
            public string AccessKey;
            [JsonInclude]
            public string AccessToken;
            [JsonInclude]
            public string EncryptionKey;
		}

		public struct NaiKeys
		{
			public byte[] EncryptionKey;
			public string AccessKey;
			public string AccessToken;
			public Dictionary<string, byte[]> keystore;
		}
		#endregion

		#region generate
		public struct NaiGenerateHTTPRespRaw
		{
			public string output;
		}

		public struct NaiGenerateHTTPResp
		{
			public string output;
			public string Error;
			public int StatusCode;
			public string Message;
		}
		public struct BiasParams
		{
			public double bias;
			public ushort[] sequence;
			public bool ensure_sequence_finish;
			public bool generate_once;
		}
		
		public struct NaiGenerateParams
		{
			public string label;
			public string model;
			public string prefix; //module ID
			public string promptFilename;
			public double temperature;
			public uint max_length;
			public uint min_length;
			public uint num_logprobs;
			public BiasParams[] logit_bias_exp;
			public double top_a;
			public uint top_k;
			public double top_p;
			public double typical_p;
			public double tail_free_sampling;
			public string phrase_rep_pen;
			public double repetition_penalty;
			public uint repetition_penalty_range;
			public double repetition_penalty_frequency;
			public double repetition_penalty_presence;
			public double repetition_penalty_slope;
			public ushort[][] bad_words_ids;
			public ushort[][] stop_sequences;
			public ushort[] repetition_penalty_whitelist;
			public ushort[] order;
			public bool BanBrackets;
			public bool use_cache;
			public bool use_string;
			public bool return_full_text;
			public bool generate_until_sentence;
		}

		public struct PermutationsSpec
		{
			public string[] Model;
			public string[] Prefix;
			public string[] PromptFilename;
			public double[] Temperature;
			public uint[] MaxLength;
			public uint[] MinLength;
			public uint[] TopK;
			public double[] TopP;
			public double[] TailFreeSampling;
			public double[] RepetitionPenalty;
			public uint[] RepetitionPenaltyRange;
			public double[] RepetitionPenaltySlope;
		}

		public struct NaiGenerateResp
		{
			public string EncodedRequest;
			public string EncodedResponse;
			public string Response;
			public Exception Error;
		}

		public struct NaiGenerateMsg
		{
			public string input;
			public string model;
			public NaiGenerateParams parameters;
		}
		
		public struct NaiGenerateVoice
		{
			public string text;		// The text to speak. Limited to 2000 characters.
			public string seed;     // Seed is used when voice is set to -1
            /*
			 * Use the following seeds for the predefined voices:
			 * Voice	Type    Seed to use
			 * -------- ------- -----------
			 * Aini     Female  Aini
			 * Orea     Female  Orea
			 * Claea    Female  Claea
			 * Lim      Female  Lim
			 * Aurae    Female  Aurae
			 * Naia     Female  Naia
			 * -------- ------- -----------
			 * Aulon    Male    Aulon
			 * Elei     Male    Elei
			 * Ogma     Male    Ogma
			 * Raid     Male    Raid
			 * Pega     Male    Pega
			 * Lam      Male    Lam
			 * -------- ------- -----------
			 * Ligeia   Unisex  Anananan
			 */
            public int voice;       // Voice is used by v1, use -1 to use seed
            /*
			 * Use the following values for the predefined voice
			 * Voice	Type    Voice to use
			 * -------- ------- ------------
			 * Cyllene  Female  17
			 * Leucosia Female  95
			 * Crina    Female  44
			 * Hespe    Female  80
			 * Ida      Female  106
			 * -------- ------- ------------
			 * Alseid   Male    6
			 * Daphnis  Male    10
			 * Echo     Male    16
			 * Thel     Male    41
			 * Nomios   Male    77
			 * -------- ------- ------------
			 * Custom   Seed    -1
			 */
            public bool opus;
			public string version;	// Version should be either v1 or v2
		}
		
		public struct NaiByteArrayResponse
		{
			public byte[] output;
			public string ContentType;
			public string Error;
			public int StatusCode;
			public string Message;
		}

		#endregion

		#region adventure
		public struct ScenarioSettings
		{
			//public NaiGenerateParams Parameters;
			public bool TrimResponses;
			public bool BanBrackets;
			public OutputTrimType TrimType;
		}

		public struct LorebookEntry
		{
			public int LoreId;
			public string Text;
			public ContextConfig ContextCfg;
			public int LastUpdatedAt;
			public string DisplayName;
			public string[] Keys;
			public int SearchRange;
			public bool Enabled;
			public bool ForceActivation;
			public bool KeyRelative;
			public bool NonStoryActivatable;
			public ushort[] Tokens;
			public Regex[] KeysRegex;

			public static LorebookEntry FromEditable(LorebookEntryEditable from)
			{
				gpt_bpe.GPTEncoder encoder = gpt_bpe.NewEncoder();
				ushort[] tokens = encoder.Encode(from.Text);
				Regex[] regexKeys = new Regex[from.Keys.Length];
				for (int i = 0; i < from.Keys.Length; i++)
				{
					string key = from.Keys[i];
					regexKeys[i] = new Regex(string.Format(@"(?i)(^|\W)({0})($|\W)", key));
				}
				return new LorebookEntry
				{
					LoreId = from.LoreId,
					DisplayName = from.Keys[0],
					Text = from.Text,
					ContextCfg = from.ContextCfg,
					Keys = from.Keys,
					SearchRange = from.SearchRange,
					Enabled = from.Enabled,
					ForceActivation = from.ForceActivation,
					KeysRegex = regexKeys,
					Tokens = tokens,
				};
			}
		}

		public struct LorebookEntryEditable
		{
			public int LoreId;
			public string Text;
			public ContextConfig ContextCfg;
			public string[] Keys;
			public int SearchRange;
			public bool Enabled;
			public bool ForceActivation;

			public static LorebookEntryEditable FromLore(LorebookEntry from)
			{
				return new LorebookEntryEditable
				{
					LoreId = from.LoreId,
					Text = from.Text,
					ContextCfg = from.ContextCfg,
					Keys = from.Keys,
					SearchRange = from.SearchRange,
					Enabled = from.Enabled,
					ForceActivation = from.ForceActivation,
				};
			}
		}

		public struct ContextConfig
		{
			public string Prefix;
			public string Suffix;
			public int TokenBudget;
			public int ReservedTokens;
			public int BudgetPriority;
			public TrimDirection TrimDirection;
			public InsertionType InsertionType;
			public MaxTrimType MaximumTrimType;
			public int InsertionPosition;
		}

		public struct ContextEntry
		{
			public string Text;
			public ContextConfig ContextCfg;
			public ushort[] Tokens;
			public string Label;
			//MatchIndexes []map[string][][]int
			public Dictionary<string, int[][]>[] MatchIndexes;
			public uint Index;
			
			public ushort[] ResolveTrim(ITokenizer tokenizer, int budget)
			{
				ushort[] trimmedTokens;

				int trimSize = 0;
				int numTokens = Tokens.Length;
				int projected = budget - numTokens + ContextCfg.ReservedTokens;
				if (projected > ContextCfg.TokenBudget)
				{
					trimSize = ContextCfg.TokenBudget;
				}
				else if (projected >= 0)
				{
					// We have enough to fit this into the budget.
					trimSize = numTokens;
				}
				else
				{
					if (numTokens * 0.3 <= budget)
					{
						trimSize = budget;
					}
					else
					{
						trimSize = 0;
					}
				}
				trimmedTokens = tokenizer.TrimNewlines(Tokens, ContextCfg.TrimDirection, trimSize);
				if (trimmedTokens.Length == 0 && ContextCfg.MaximumTrimType >= MaxTrimType.SENTENCES)
				{
					trimmedTokens = tokenizer.TrimSentences(Tokens, ContextCfg.TrimDirection, trimSize);
				}
				if (trimmedTokens.Length == 0 && ContextCfg.MaximumTrimType == MaxTrimType.TOKENS)
				{
					switch (ContextCfg.TrimDirection)
					{
						case TrimDirection.TOP:
							trimmedTokens = new ushort[trimSize];
							Array.Copy(Tokens, numTokens - trimSize, trimmedTokens, 0, trimSize);
							break;
						case TrimDirection.BOTTOM:
							trimmedTokens = new ushort[trimSize];
							Array.Copy(Tokens, 0, trimmedTokens, 0, trimSize);
							break;
						default:
							trimmedTokens = Tokens;
							break;
					}
				}
				return trimmedTokens;
			}
		}

        [JsonObject]
        public struct RemoteStoryMeta
		{
			[JsonProperty("id")]
			public string storyID;
			public string type;
			public string metaID;
			public StoryMeta meta;
		}

        [JsonObject]
        public struct StoryMeta
		{
			public string id;
			public string remoteId;
			public string remoteStoryId;
			public string title;
			public string description;
			public string textPreview;
			public bool favorite;
			public string[] tags;
            [JsonProperty("createdAt")]
            public long created;
			public long lastUpdatedAt;
		}
		#endregion
	}
}
