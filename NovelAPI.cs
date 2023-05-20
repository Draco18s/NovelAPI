using net.novelai.authentication;
using net.novelai.generation;
using net.novelai.util;
using RestSharp;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
//using System.Threading.Tasks;
using static net.novelai.api.Structs;

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
		public RestClient? client;
		public gpt_bpe.GPTEncoder encoder;
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
			Dictionary<string, object> raw = JsonSerializer.Deserialize<Dictionary<string, object>>(response.Content) ?? throw new Exception("GetRemainingActions Failure");
			if (raw != null && raw.ContainsKey("maxPriorityActions"))
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
			Dictionary<string, object> raw = JsonSerializer.Deserialize<Dictionary<string, object>>(response.Content) ?? throw new Exception("GetModules Failure");
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
			Dictionary<string, object> raw = JsonSerializer.Deserialize<Dictionary<string, object>>(response.Content) ?? throw new Exception("GetStories Failure");
			if (!raw.ContainsKey("objects")) return stories;
			object objs = raw["objects"];
			foreach (object o in (object[])objs)
			{
				JsonObject json = (JsonObject)o;
				string meta = (string)json["meta"];
				keys.keystore.TryGetValue(meta, out byte[] sk);

				byte[] data = Convert.FromBase64String((string)json["data"]);
				string storyjson = Encoding.Default.GetString(Sodium.SecretBox.Open(data.Skip(24).ToArray(), data.Take(24).ToArray(), sk));
				Dictionary<string, object> rawMeta = JsonSerializer.Deserialize<Dictionary<string, object>>(response.Content) ?? throw new Exception("GetStories Failure");
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
			Dictionary<string, object> raw = JsonSerializer.Deserialize<Dictionary<string, object>>(response.Content) ?? throw new Exception("GetCurrentPriority Failure");
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
			resp.Response = encoder.Decode(FromBin(binTokens));
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
			return new ushort[][]{ new ushort[]{58}, new ushort[]{60}, new ushort[]{90}, new ushort[]{92}, new ushort[]{685}, new ushort[]{1391}, new ushort[]{1782},
				new ushort[]{2361}, new ushort[]{3693}, new ushort[]{4083}, new ushort[]{4357}, new ushort[]{4895}, new ushort[]{5512}, new ushort[]{5974}, new ushort[]{7131},
				new ushort[]{8183}, new ushort[]{8351}, new ushort[]{8762}, new ushort[]{8964}, new ushort[]{8973}, new ushort[]{9063}, new ushort[]{11208}, new ushort[]{11709},
				new ushort[]{11907}, new ushort[]{11919}, new ushort[]{12878}, new ushort[]{12962}, new ushort[]{13018}, new ushort[]{13412}, new ushort[]{14631},
				new ushort[]{14692}, new ushort[]{14980}, new ushort[]{15090}, new ushort[]{15437}, new ushort[]{16151}, new ushort[]{16410}, new ushort[]{16589},
				new ushort[]{17241}, new ushort[]{17414}, new ushort[]{17635}, new ushort[]{17816}, new ushort[]{17912}, new ushort[]{18083}, new ushort[]{18161},
				new ushort[]{18477}, new ushort[]{19629}, new ushort[]{19779}, new ushort[]{19953}, new ushort[]{20520}, new ushort[]{20598}, new ushort[]{20662},
				new ushort[]{20740}, new ushort[]{21476}, new ushort[]{21737}, new ushort[]{22133}, new ushort[]{22241}, new ushort[]{22345}, new ushort[]{22935},
				new ushort[]{23330}, new ushort[]{23785}, new ushort[]{23834}, new ushort[]{23884}, new ushort[]{25295}, new ushort[]{25597}, new ushort[]{25719},
				new ushort[]{25787}, new ushort[]{25915}, new ushort[]{26076}, new ushort[]{26358}, new ushort[]{26398}, new ushort[]{26894}, new ushort[]{26933},
				new ushort[]{27007}, new ushort[]{27422}, new ushort[]{28013}, new ushort[]{29164}, new ushort[]{29225}, new ushort[]{29342}, new ushort[]{29565},
				new ushort[]{29795}, new ushort[]{30072}, new ushort[]{30109}, new ushort[]{30138}, new ushort[]{30866}, new ushort[]{31161}, new ushort[]{31478},
				new ushort[]{32092}, new ushort[]{32239}, new ushort[]{32509}, new ushort[]{33116}, new ushort[]{33250}, new ushort[]{33761}, new ushort[]{34171},
				new ushort[]{34758}, new ushort[]{34949}, new ushort[]{35944}, new ushort[]{36338}, new ushort[]{36463}, new ushort[]{36563}, new ushort[]{36786},
				new ushort[]{36796}, new ushort[]{36937}, new ushort[]{37250}, new ushort[]{37913}, new ushort[]{37981}, new ushort[]{38165}, new ushort[]{38362},
				new ushort[]{38381}, new ushort[]{38430}, new ushort[]{38892}, new ushort[]{39850}, new ushort[]{39893}, new ushort[]{41832}, new ushort[]{41888},
				new ushort[]{42535}, new ushort[]{42669}, new ushort[]{42785}, new ushort[]{42924}, new ushort[]{43839}, new ushort[]{44438}, new ushort[]{44587},
				new ushort[]{44926}, new ushort[]{45144}, new ushort[]{45297}, new ushort[]{46110}, new ushort[]{46570}, new ushort[]{46581}, new ushort[]{46956},
				new ushort[]{47175}, new ushort[]{47182}, new ushort[]{47527}, new ushort[]{47715}, new ushort[]{48600}, new ushort[]{48683}, new ushort[]{48688},
				new ushort[]{48874}, new ushort[]{48999}, new ushort[]{49074}, new ushort[]{49082}, new ushort[]{49146}, new ushort[]{49946}, new ushort[]{10221},
				new ushort[]{4841}, new ushort[]{1427}, new ushort[]{2602, 834}, new ushort[]{29343}, new ushort[]{37405}, new ushort[]{35780}, new ushort[]{2602},
				new ushort[]{17202}, new ushort[]{8162} };
		}

		public static NaiGenerateParams NewGenerateParams()
		{
			return new NaiGenerateParams
			{
				model = "6B-v3",
				prefix = "vanilla",
				temperature = 0.55,
				max_length = 40,
				min_length = 40,
				top_k = 140,
				top_p = 0.9,
				tail_free_sampling = 1,
				repetition_penalty = 1.1875,
				repetition_penalty_range = 1024,
				repetition_penalty_slope = 6.57,
				bad_words_ids = new ushort[0][],
				BanBrackets = true,
				use_cache = false,
				use_string = false,
				return_full_text = false,
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

		public static async Task<NaiGenerateHTTPResp> NaiApiGenerateAsync(NaiKeys keys, NaiGenerateMsg parms, RestClient? client)
		{
			parms.model = parms.parameters.model;
			const float oldRange = 1 - 8.0f;
			const float newRange = 1 - 1.525f;
			if (parms.model != "2.7B")
			{
				parms.parameters.repetition_penalty = ((parms.parameters.repetition_penalty - 1) * newRange) / oldRange + 1;
			}
			if (parms.parameters.BanBrackets)
			{
				List<ushort[]> concat = new List<ushort[]>(parms.parameters.bad_words_ids);
				concat.AddRange(BannedBrackets());
				parms.parameters.bad_words_ids = concat.ToArray();
			}

			string json = JsonSerializer.Serialize(parms);
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
				//Console.WriteLine(response.ErrorMessage);
				throw new Exception(response.ErrorMessage);
			}
			Dictionary<string, object> raw = JsonSerializer.Deserialize<Dictionary<string, object>>(response.Content) ?? throw new Exception("NaiApiGenerateAsync Failure");
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
		https://api.novelai.net/user/objects/stories
		https://api.novelai.net/user/objects/storycontent/{???}
		*/

		public static NovelAPI NewNovelAiAPI()
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
					encoder = gpt_bpe.NewEncoder(),
					currentParams = defaultParams,
				};
			}
			catch (Exception ex)
			{
				Console.WriteLine("Error creating NovelAPI");
				Console.WriteLine(ex.ToString());
				return null!;
			}
		}

		public string[] GetTokens(string input)
		{
			ushort[] tok = encoder.Encode(input);
			return encoder.DecodeToTokens(tok);
		}
	}
}
