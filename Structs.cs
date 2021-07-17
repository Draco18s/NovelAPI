using System;

namespace net.novelai.api {
	public class Structs {
		#region authentication
		public struct AuthConfig {
			public string Username;
			public string Password;
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
	}
}
