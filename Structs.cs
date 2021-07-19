using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace net.novelai.api {
	public class Structs {
		#region authentication
		public struct AuthConfig {
			public string Username;
			public string Password;
			public string AccessKey;
			public string AccessToken;
			public string EncryptionKey;
		}

		public struct NaiKeys {
			public byte[] EncryptionKey;
			public string AccessKey;
			public string AccessToken;
		}
		#endregion

		#region generate
		public struct NaiGenerateHTTPRespRaw {
			public string output;
		}

		public struct NaiGenerateHTTPResp {
			public string output;
			public string Error;
			public int StatusCode;
			public string Message;
		}

		public struct NaiGenerateParams {
			public string label;
			public string model;
			public string prefix;
			public string promptFilename;
			public double temperature;
			public uint max_length;
			public uint min_length;
			public uint top_k;
			public double top_p;
			public double tail_free_sampling;
			public double repetition_penalty;
			public uint repetition_penalty_range;
			public double repetition_penalty_slope;
			public ushort[][] bad_words_ids;
			public bool BanBrackets;
			public bool use_cache;
			public bool use_string;
			public bool return_full_text;
		}

		public struct PermutationsSpec {
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

		public struct NaiGenerateResp {
			public string EncodedRequest;
			public string EncodedResponse;
			public string Response;
			public Exception Error;
		}

		public struct NaiGenerateMsg {
			public string input;
			public string model;
			public NaiGenerateParams parameters;
		}
		#endregion

		#region adventure
		public struct ScenarioSettings {
			public NaiGenerateParams Parameters;
			public bool TrimResponses;
			public bool BanBrackets;
		}

		public struct LorebookEntry {
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
		}

		public struct ContextConfig {
			public string Prefix;
			public string Suffix;
			public int TokenBudget;
			public int ReservedTokens;
			public int BudgetPriority;
			public gpt_bpe.TrimDirection TrimDirection;
			public gpt_bpe.InsertionType InsertionType;
			public gpt_bpe.MaxTrimType MaximumTrimType;
			public int InsertionPosition;
		}

		public struct ContextEntry {
			public string Text;
			public ContextConfig ContextCfg;
			public ushort[] Tokens;
			public string Label;
			//MatchIndexes []map[string][][]int
			public Dictionary<string, int[][]>[] MatchIndexes;
			public uint Index;
			
			public ushort[] ResolveTrim(gpt_bpe.GPTEncoder tokenizer, int budget) {
				ushort[] trimmedTokens;

				int trimSize = 0;
				int numTokens = Tokens.Length;
				int projected = budget - numTokens + ContextCfg.ReservedTokens;
				if(projected > ContextCfg.TokenBudget) {
					trimSize = ContextCfg.TokenBudget;
				}
				else if(projected >= 0) {
					// We have enough to fit this into the budget.
					trimSize = numTokens;
				}
				else {
					if(numTokens * 0.3 <= budget) {
						trimSize = budget;
					}
					else {
						trimSize = 0;
					}
				}
				trimmedTokens = tokenizer.TrimNewlines(Tokens, ContextCfg.TrimDirection, trimSize);
				if(trimmedTokens.Length == 0 && ContextCfg.MaximumTrimType >= gpt_bpe.MaxTrimType.SENTENCES) {
					trimmedTokens = tokenizer.TrimSentences(Tokens, ContextCfg.TrimDirection, trimSize);
				}
				if(trimmedTokens.Length == 0 && ContextCfg.MaximumTrimType == gpt_bpe.MaxTrimType.TOKENS) {
					switch(ContextCfg.TrimDirection) {
						case gpt_bpe.TrimDirection.TOP:
							trimmedTokens = new ushort[trimSize];
							Array.Copy(Tokens, numTokens - trimSize, trimmedTokens, 0, trimSize);
							break;
						case gpt_bpe.TrimDirection.BOTTOM:
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
		#endregion
	}
}
