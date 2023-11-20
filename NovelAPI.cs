using net.novelai.authentication;
using net.novelai.generation;
using net.novelai.util;
using RestSharp;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
using System.Text;
//using System.Text.Json;
//using System.Text.Json.Nodes;
using Newtonsoft;
using Newtonsoft.Json;
//using System.Threading.Tasks;
using static net.novelai.api.Structs;
using System.Text.Json.Nodes;
using novelai.util;

namespace net.novelai.api
{
	public class NovelAPI
	{
		public static string CONFIG_PATH = "./config";
		public const string NAME = "novelapi";
		public const string VERSION = "0.1";
		public const string IDENT = NAME + "/" + VERSION;
		public const string LANG = "C# .NET";
		public static readonly string AGENT = IDENT + " (" + Environment.OSVersion + "," + LANG + " " + Environment.Version + ")";
		public NaiKeys keys;
		public RestClient client;
		public KayraEncoder encoder;
		public static NaiGenerateParams defaultParams = NewGenerateParams();
		public NaiGenerateParams currentParams;
		private static bool fetchedModules = false;
		private static readonly List<AIModule> _customUserModules = new List<AIModule>();
		public static IReadOnlyList<AIModule> customUserModules
		{
			get
			{
				if (fetchedModules) return _customUserModules;
				return null!;
			}
		}

		public async Task<int> GetRemainingActions()
		{
			//https://api.novelai.net/user/priority
			RestRequest request = new RestRequest("user/priority");
			request.Method = Method.Post;
			request.AddHeader("User-Agent", AGENT);
			request.AddHeader("Content-Type", "application/json");
			request.AddHeader("Authorization", "Bearer " + keys.AccessToken);

			RestResponse response = await client.ExecutePostAsync(request);
			if (!response.IsSuccessful || response.Content == null)
			{
				return 0;
			}
			Dictionary<string, object> raw = JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Content) ?? throw new Exception("GetRemainingActions Failure");
			if (raw.ContainsKey("maxPriorityActions"))
			{
				return (int)raw["maxPriorityActions"];
			}
			return 0;
		}

		public async Task<string[]> GetModules()
		{
			string[] defaultModules = new string[] { "`Default:`", "vanilla" }
			.Concat(new string[] { "\n`Themes:`" })
			.Concat(AIModule.themeModules)
			.Concat(new string[] { "\n`Styles:`" })
			.Concat(AIModule.styleModules)
			.Concat(new string[] { "\n`Inspiration:`" })
			.Concat(AIModule.inspireModules).ToArray();
			//https://api.novelai.net/user/objects/aimodules
			RestRequest request = new RestRequest("user/objects/aimodules");
			request.Method = Method.Get;
			request.AddHeader("User-Agent", AGENT);
			request.AddHeader("Content-Type", "application/json");
			request.AddHeader("Authorization", "Bearer " + keys.AccessToken);

			RestResponse response = await client.ExecuteAsync(request);
			if (!response.IsSuccessful || response.Content == null)
			{
				return defaultModules;
			}
			Dictionary<string, object> raw = JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Content) ?? throw new Exception("GetModules Failure");
			if (!raw.ContainsKey("objects"))
			{
				return defaultModules;
			}
			object[] rawModules = (object[])raw["objects"];
			List<string> otherModules = new List<string>();
			foreach (object o in rawModules)
			{
				
				JsonObject j = (JsonObject)o;
				AIModule m = AIModule.Unpack(j, keys);

				otherModules.Add(m.Name);
				if (!_customUserModules.Contains(m))
				{
					_customUserModules.Add(m);
				}
			}
			fetchedModules = true;
			if (otherModules.Count > 0)
			{
				otherModules.Sort();
				return defaultModules.Concat(new string[] { "\n`Custom:`" }).Concat(otherModules).ToArray();
			}
			return defaultModules;
		}

		public async Task<List<RemoteStoryMeta>> GetStories()
		{
			List<RemoteStoryMeta> stories = new List<RemoteStoryMeta>();
			RestRequest request = new RestRequest("user/objects/stories");
			request.Method = Method.Get;
			//https://api.novelai.net/user/objects/stories
			request.AddHeader("User-Agent", AGENT);
			request.AddHeader("Content-Type", "application/json");
			request.AddHeader("Authorization", "Bearer " + keys.AccessToken);
			RestResponse response = await client.ExecuteAsync(request);
			if (!response.IsSuccessful || response.Content == null)
			{
				return stories;
			}
			Dictionary<string, object> raw = JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Content) ?? throw new Exception("GetStories Failure");
			if (!raw.ContainsKey("objects")) return stories;
			object objs = raw["objects"];
			foreach (object o in (object[])objs)
			{
				JsonObject json = (JsonObject)o;
				string meta = (string)json["meta"];
				keys.keystore.TryGetValue(meta, out byte[] sk);

				byte[] data = Convert.FromBase64String((string)json["data"]);
				string storyjson = Encoding.Default.GetString(Sodium.SecretBox.Open(data.Skip(24).ToArray(), data.Take(24).ToArray(), sk));
				Dictionary<string, object> rawMeta = JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Content) ?? throw new Exception("GetStories Failure");
				stories.Add(new RemoteStoryMeta
				{
					storyID = (string)json["id"],
					type = (string)json["type"],
					metaID = meta,
					meta = new StoryMeta
					{
						id = (string)rawMeta["id"],
						remoteId = (string)rawMeta["remoteId"],
						remoteStoryId = (string)rawMeta["remoteStoryId"],
						title = (string)rawMeta["title"],
						description = (string)rawMeta["description"],
						textPreview = (string)rawMeta["textPreview"],
						favorite = (bool)rawMeta["favorite"],
						tags = (string[])rawMeta["tags"],
						created = (long)rawMeta["created"],
						lastUpdatedAt = (long)json["lastUpdatedAt"],
					}
				});
			}

			return stories;
		}

		public async Task<int> GetCurrentPriority()
		{
			RestRequest request = new RestRequest("user/priority");
			request.Method = Method.Post;

			RestResponse response = await client.ExecutePostAsync(request);
			if (!response.IsSuccessful || response.Content == null)
			{
				return 0;
			}
			Dictionary<string, object> raw = JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Content) ?? throw new Exception("GetCurrentPriority Failure");
			if (raw?.ContainsKey("taskPriority") ?? false)
			{
				return (int)raw["taskPriority"];
			}
			return 0;
		}

		public async Task<string> GenerateAsync(string content)
		{
			NaiGenerateResp resp = await GenerateWithParamsAsync(content, currentParams);
			return resp.Response;
		}

		public async Task<NaiGenerateResp> GenerateWithParamsAsync(string content, NaiGenerateParams parms)
		{
			ushort[] encoded = encoder.Encode(content);
			byte[] encodedBytes = ToBin(encoded);
			string encodedBytes64 = Convert.ToBase64String(encodedBytes);
			NaiGenerateResp resp = new NaiGenerateResp();
			resp.EncodedRequest = encodedBytes64;
			NaiGenerateMsg msg = NewGenerateMsg(encodedBytes64);
			msg.parameters = parms;
			NaiGenerateHTTPResp apiResp = await NaiApiGenerateAsync(keys, msg, client);
			byte[] binTokens = Convert.FromBase64String(apiResp.output);
			resp.EncodedResponse = apiResp.output;
			resp.Response = encoder.Decode(FromBin(binTokens).ToList());
			
			return resp;
		}

		public static byte[] ToBin(ushort[] tokens)
		{
			ReadWriteBuffer buf = new ReadWriteBuffer(tokens.Length * BitConverter.GetBytes(tokens[0]).Length);
			foreach (ushort b in tokens)
			{
				buf.Write(BitConverter.GetBytes(b));
			}
			return buf.Bytes.ToArray();
		}

		public static ushort[] FromBin(byte[] bytes)
		{
			ushort[] tokens = new ushort[bytes.Length / 2];
			ReadWriteBuffer buf = new ReadWriteBuffer(bytes);
			int i = 0;
			while (buf.Count > 0)
			{
				ushort token = BitConverter.ToUInt16(buf.Read(2), 0);
				tokens[i] = token;
				i++;
			}
			return tokens;
		}

		public static ushort[][] BannedBrackets()
		{
			return new ushort[][]{ new ushort[] { 3 }, new ushort[] { 49356 }, new ushort[] { 1431 }, new ushort[] { 31715 }, new ushort[] { 34387 }, new ushort[] { 20765 },
				new ushort[] { 30702 }, new ushort[] { 10691 }, new ushort[] { 49333 }, new ushort[] { 1266 }, new ushort[] { 26523 }, new ushort[] { 41471 },
				new ushort[] { 2936 }, new ushort[] { 85, 85 }, new ushort[] { 49332 }, new ushort[] { 7286 }, new ushort[] { 1115 } };
		}

		public static NaiGenerateParams NewGenerateParams()
		{
			return new NaiGenerateParams
			{
				model = "kayra-v1",
				prefix = "special_instruct",
				logit_bias_exp = new BiasParams[]
				{
					new BiasParams()
					{
						bias = -0.08,
						ensure_sequence_finish = false,
						generate_once = false,
						sequence = new ushort[]{23}
					},
					new BiasParams()
					{
						bias = -0.08,
						ensure_sequence_finish = false,
						generate_once = false,
						sequence = new ushort[]{21}
					}
				},
				temperature = 1.35,
				max_length = 40,
				min_length = 40,
				top_a = 0.1,
				top_k = 15,
				top_p = 0.85,
				num_logprobs = 0,
				order = new ushort[] { 2, 3, 0, 4, 1 },
				phrase_rep_pen = "aggressive",
				tail_free_sampling = 0.915,
				repetition_penalty = 2.8,
				repetition_penalty_range = 2048,
				repetition_penalty_slope = 0.02,
				repetition_penalty_frequency = 0.02,
				repetition_penalty_presence = 0,
				bad_words_ids = Array.Empty<ushort[]>(),
				stop_sequences = new ushort[][] { new ushort[] { 43145 }, new ushort[] { 19438 } },
				BanBrackets = true,
				use_cache = false,
				use_string = false,
				return_full_text = false,
				generate_until_sentence = true,
				repetition_penalty_whitelist = new ushort[] { 49256, 49264, 49231, 49230, 49287, 85, 49255, 49399, 49262, 336,
					333, 432, 363, 468, 492, 745, 401, 426, 623, 794, 1096, 2919, 2072, 7379, 1259, 2110, 620, 526, 487, 16562,
					603, 805, 761, 2681, 942, 8917, 653, 3513, 506, 5301, 562, 5010, 614, 10942, 539, 2976, 462, 5189, 567, 2032,
					123, 124, 125, 126, 127, 128, 129, 130, 131, 132, 588, 803, 1040, 49209, 4, 5, 6, 7, 8, 9, 10, 11, 12 }
			};
		}

		public static NaiGenerateMsg NewGenerateMsg(string input)
		{
			NaiGenerateParams parms = NewGenerateParams();
			return new NaiGenerateMsg
			{
				input = input,
				model = parms.model,
				parameters = parms,
			};
		}

		public static async Task<NaiGenerateHTTPResp> NaiApiGenerateAsync(NaiKeys keys, NaiGenerateMsg parms, RestClient client)
		{
			parms.model = parms.parameters.model;
			const float oldRange = 1 - 8.0f;
			const float newRange = 1 - 1.525f;
			/*if (parms.model != "2.7B")
			{
				parms.parameters.repetition_penalty = ((parms.parameters.repetition_penalty - 1) * newRange) / oldRange + 1;
			}*/
			if (parms.parameters.BanBrackets)
			{
				List<ushort[]> concat = new List<ushort[]>(parms.parameters.bad_words_ids);
				concat.AddRange(BannedBrackets());
				parms.parameters.bad_words_ids = concat.ToArray();
			}
			
			string json = JsonConvert.SerializeObject(parms);
			RestRequest request = new RestRequest("ai/generate");
			request.Method = Method.Post;
			request.AddJsonBody(json);
			request.AddHeader("User-Agent", AGENT);
			request.AddHeader("Content-Type", "application/json");
			request.AddHeader("Authorization", "Bearer " + keys.AccessToken);
			RestResponse response = await client.ExecutePostAsync(request);
			if (!response.IsSuccessful || response.Content == null)
			{
				//Console.WriteLine("Failed to fetch AI response!");
				Console.WriteLine(response.Content);
				throw new Exception(response.Content);
			}
			Dictionary<string, object> raw = JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Content) ?? throw new Exception("NaiApiGenerateAsync Failure");
			if (raw.ContainsKey("output"))
			{
				return new NaiGenerateHTTPResp
				{
					output = (string)raw["output"],
					StatusCode = 200,
					Error = "",
					Message = ""
				};
			}
			return new NaiGenerateHTTPResp
			{
				output = (string)raw["message"],
				StatusCode = (int)raw["statusCode"],
				Error = "",
				Message = ""
			};
		}

		/*
		Additional endpoints:
		https://api.novelai.net/docs/
		https://api.novelai.net/user/objects/stories
		https://api.novelai.net/user/objects/storycontent/{???}
		*/

		public static NovelAPI? NewNovelAiAPI()
		{
			try
			{
				NaiKeys k = Auth.AuthEnv();
				try
				{
					k.keystore = Auth.GetKeystore(k);
				}
				catch (Exception bex)
				{
					Console.WriteLine(bex.ToString());
				}
				return new NovelAPI
				{
					keys = k,
					client = new RestClient("https://api.novelai.net/"),
					encoder = KayraEncoder.Create(),
					currentParams = defaultParams,
				};
			}
			catch (Exception ex)
			{
				Console.WriteLine("Error creating NovelAPI");
				Console.WriteLine(ex.ToString());
				return null;
			}
		}

		public string[] GetTokens(string input)
		{
			ushort[] tok = encoder.Encode(input);
			return new string[] {encoder.Decode(tok.ToList())}; //.DecodeToTokens(tok);
		}
	}
}
