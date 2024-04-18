using net.novelai.authentication;
using net.novelai.generation;
using net.novelai.util;
using RestSharp;
using System.Text;
using Newtonsoft.Json;
using static net.novelai.api.Structs;
using System.Text.Json.Nodes;
using novelai.util;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace net.novelai.api
{
	public class NovelAPI
	{
		#region Properties and Constants
        public static string CONFIG_PATH = "./config";
        public const string NAME = "novelapi";
		public const string VERSION = "0.3";
		public const string IDENT = NAME + "/" + VERSION;
		public const string LANG = "C# .NET";
		public static readonly string AGENT = IDENT + " (" + Environment.OSVersion + "," + LANG + " " + Environment.Version + ")";
		public NaiKeys keys;
		public RestClient client;
		public ITokenizer encoder;
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
        #endregion

		/// <summary>
		/// Static API method to retrieve the endpoint for: /
		/// </summary>
		/// <returns>true if the endpoint returns "OK", otherwise false</returns>
		public static async Task<bool> GetEndpointStatus(string urlEndpoint = null)
		{
			try
			{
				var client = new RestClient(Structs.ENDPOINT);
                RestRequest request = new RestRequest("");
                request.Method = Method.Get;
                request.AddHeader("User-Agent", AGENT);
                request.AddHeader("accept", "*/*");

                RestResponse response = await client.ExecuteGetAsync(request);
                if (response.IsSuccessful && response.Content == "OK")
                {
                    return true;
                }
            }
            catch
			{
				// Do nothing
			}
			return false;
		}

        #region Story / Module Methods

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

		/// <summary>
		/// API method to retrieve the endpoint for: /user/objects/aimodules
		/// </summary>
		/// <returns>an initialized array of strings with module names</returns>
		/// <exception cref="Exception"></exception>
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

		/// <summary>
		/// API method to retrieve the endpoint for: /user/objects/stories
		/// </summary>
		/// <returns>An initialized list of RemoteStoryMeta objects</returns>
		/// <exception cref="Exception"></exception>
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
            JObject raw = JObject.Parse(response.Content) ?? throw new Exception("GetStories Failure");

            if (!raw.ContainsKey("objects")) return stories;
            JToken objs = raw["objects"];

            foreach (JObject json in objs)
			{
                RemoteStoryMeta remoteStoryMeta = ParseRemoteStoryJObject(json) ?? throw new Exception("GetStories Failure");
				stories.Add(remoteStoryMeta);
            }

            return stories;
		}

		/// <summary>
		/// API method to retrieve the endpoint for: /user/objects/stories/{storyId}
		/// </summary>
		/// <param name="storyId">The Id string for the story to retrieve</param>
		/// <returns>An initialized StoryMeta object if successful, otherwise null</returns>
		/// <exception cref="Exception"></exception>
		public async Task<StoryMeta?> GetStory(string storyId)
		{

            RestRequest request = new RestRequest("user/objects/stories/" + storyId);
            request.Method = Method.Get;
            //https://api.novelai.net/user/objects/stories/{id}
            request.AddHeader("User-Agent", AGENT);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Authorization", "Bearer " + keys.AccessToken);
            RestResponse response = await client.ExecuteAsync(request);
            if (!response.IsSuccessful || response.Content == null)
            {
                return null;
            }
			RemoteStoryMeta remoteStoryMeta = ParseRemoteStoryJson(response.Content) ?? throw new Exception("GetStory Failure");

            return remoteStoryMeta.meta;
		}

		/// <summary>
		/// Parse a JSON string and returns an initialized RemoteStoryMeta object
		/// </summary>
		/// <param name="jsonString">a JSON string from a remote endpoint</param>
		/// <returns>An initialized RemoteStoryMeta object if successful, otherwise null</returns>
        public RemoteStoryMeta? ParseRemoteStoryJson(string jsonString)
        {
            RemoteStoryMeta? remoteStoryMeta = null;

            try
            {
                JObject jsonData = JObject.Parse(jsonString);
                return ParseRemoteStoryJObject(jsonData);
            }
            catch { }

            return remoteStoryMeta;
        }

        /// <summary>
        /// Parse a JObject and returns an initialized RemoteStoryMeta object
        /// </summary>
        /// <param name="jsonData">an initialized JObject with RemoteStoryMeta data</param>
        /// <returns>An initialized RemoteStoryMeta object if successful, otherwise null</returns>
        public RemoteStoryMeta? ParseRemoteStoryJObject(JObject jsonData)
		{
            RemoteStoryMeta? remoteStoryMeta = null;
            
			try
            {
                string meta = jsonData.SelectToken("meta", false)?.ToString();
                keys.keystore.TryGetValue(meta, out byte[] sk);
                if (sk != null)
                {
                    byte[] data = Convert.FromBase64String(jsonData.SelectToken("$.data", false)?.ToString());
                    string storyjson = Encoding.Default.GetString(Sodium.SecretBox.Open(data.Skip(24).ToArray(), data.Take(24).ToArray(), sk));
                    JObject rawMeta = JObject.Parse(storyjson);
                    jsonData["metaId"] = meta;
                    jsonData["meta"] = rawMeta;
                    remoteStoryMeta = jsonData.ToObject<RemoteStoryMeta?>();
                }
            }
            catch { }

            return remoteStoryMeta;
		}

        #endregion

        /// <summary>
        /// API method to retrieve the endpoint for: /user/priority
        /// </summary>
        /// <returns>The number of remaining priority actions if successful, otherwise 0</returns>
        /// <exception cref="Exception"></exception>
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

		/// <summary>
		/// API method to access the endpoint for: /ai/generate
		/// </summary>
		/// <param name="content">The prompt string used to generate text</param>
		/// <returns>The text generated by the API endpoint</returns>
		public async Task<string> GenerateAsync(string content)
		{
			NaiGenerateResp resp = await GenerateWithParamsAsync(content, currentParams);
			return resp.Response;
		}

        /// <summary>
        /// API method to access the endpoint for: /ai/generate
        /// </summary>
		/// <param name="content">The prompt string used to generate text</param>
        /// <param name="parms">Parameters to use when generating the reponse</param>
        /// <returns>An initialized NaiGenerateResp response object</returns>
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
			resp.Response = encoder.Decode(FromBin(binTokens).ToArray());

			return resp;
		}

		/// <summary>
		/// Static API method to convert an array of tokens into a byte array
		/// </summary>
		/// <param name="tokens">an array of encoded tokens</param>
		/// <returns>an initialized byte array</returns>
		public static byte[] ToBin(ushort[] tokens)
		{
			ReadWriteBuffer buf = new ReadWriteBuffer(tokens.Length * BitConverter.GetBytes(tokens[0]).Length);
			foreach (ushort b in tokens)
			{
				buf.Write(BitConverter.GetBytes(b));
			}
			return buf.Bytes.ToArray();
		}

		/// <summary>
		/// Static API method to convert a byte array into an array of tokens
		/// </summary>
		/// <param name="bytes">a byte array with token data</param>
		/// <returns>an initialized array of encoded token data</returns>
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

		/// <summary>
		/// Static API method to create a default array of Banned Bracket tokens
		/// </summary>
		/// <returns></returns>
		public static ushort[][] BannedBrackets()
		{
			return new ushort[][]{ new ushort[] { 3 }, new ushort[] { 49356 }, new ushort[] { 1431 }, new ushort[] { 31715 }, new ushort[] { 34387 }, new ushort[] { 20765 },
				new ushort[] { 30702 }, new ushort[] { 10691 }, new ushort[] { 49333 }, new ushort[] { 1266 }, new ushort[] { 26523 }, new ushort[] { 41471 },
				new ushort[] { 2936 }, new ushort[] { 85, 85 }, new ushort[] { 49332 }, new ushort[] { 7286 }, new ushort[] { 1115 } };
		}

		/// <summary>
		/// Static API method to create a default NaiGenerateParams object
		/// </summary>
		/// <returns>An initalized object with default parameters set</returns>
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

		/// <summary>
		/// Static API method to create a default NaiGenerateMsg object
		/// </summary>
		/// <param name="input">The input prompt to use for the message</param>
		/// <returns>An initialized object with default parameters set</returns>
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

        /// <summary>
        /// Static API method to access the endpoint for: /ai/generate
        /// </summary>
        /// <param name="keys">The keys object with the access token for the request</param>
        /// <param name="parms">The message parameters to send to the endpoint</param>
        /// <param name="client">The RestClient used to send the message</param>
        /// <returns>An initialized NaiGenerateHTTPResp response object</returns>
        /// <exception cref="Exception"></exception>
        public static async Task<NaiGenerateHTTPResp> NaiApiGenerateAsync(NaiKeys keys, NaiGenerateMsg parms, RestClient client)
		{
			parms.model = parms.parameters.model;
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
		https://api.novelai.net/
		https://api.novelai.net/ai/module/{???}
		https://api.novelai.net/ai/module/all
		https://api.novelai.net/ai/module/buy-training-steps
		https://api.novelai.net/ai/module/train
		https://api.novelai.net/ai/upscale
		https://api.novelai.net/docs
		https://api.novelai.net/user/change-access-key
		https://api.novelai.net/user/clientsettings
		https://api.novelai.net/user/create-persistent-token
		https://api.novelai.net/user/data
		https://api.novelai.net/user/delete
		https://api.novelai.net/user/deletion/request
		https://api.novelai.net/user/deletion/delete
		https://api.novelai.net/user/giftkeys
		https://api.novelai.net/user/information
		https://api.novelai.net/user/keystore
		https://api.novelai.net/user/login
		https://api.novelai.net/user/priority
		https://api.novelai.net/user/recovery/recover
		https://api.novelai.net/user/recovery/request
		https://api.novelai.net/user/register
		https://api.novelai.net/user/resend-email-verification
		https://api.novelai.net/user/objects/aimodules
		https://api.novelai.net/user/objects/aimodules/{???}
		https://api.novelai.net/user/objects/presets
		https://api.novelai.net/user/objects/presets/{???}
		https://api.novelai.net/user/objects/shelf
		https://api.novelai.net/user/objects/shelf/{???}
		https://api.novelai.net/user/objects/stories
		https://api.novelai.net/user/objects/stories/{???}
		https://api.novelai.net/user/objects/storycontent
		https://api.novelai.net/user/objects/storycontent/{???}
		https://api.novelai.net/user/submission
		https://api.novelai.net/user/submission/{???}
		https://api.novelai.net/user/subscription
		https://api.novelai.net/user/subscription/bind
		https://api.novelai.net/user/subscription/change
		https://api.novelai.net/user/verify-email
		https://api.novelai.net/user/vote-submission/{???}
		*/

        #region Factory Constructors
        /// <summary>
        /// Factory constructor to create a NovelAPI object initialized with username/password credentials
        /// </summary>
        /// <param name="username">The NovelAi.net username in plain text</param>
        /// <param name="password">The NovelAi.new password in plain text</param>
        /// <returns>An initialized NovelAPI object authenticated using the credentials given</returns>
        public static NovelAPI NewNovelAiAPI(string username, string password)
		{
			return NewNovelAiAPI(new AuthConfig() { Username = username, Password = password });
		}

		/// <summary>
		/// Factory constructor to create a NovelAPI object initialized with the credentials 
		/// provided in the authConfig parameter. 
		/// </summary>
		/// <param name="authConfig">
		/// Authorization parameters to initialize the API object with. If the parameters are not set, 
		/// or authConfig is null, then the values will be loaded from the auth.json file found in the config path.
		/// </param>
		/// <param name="generationParams">Parameters used to override the </param>
		/// <returns></returns>
		public static NovelAPI NewNovelAiAPI(AuthConfig? authConfig = null, NaiGenerateParams? generationParams = null, string urlEndpoint = null)
		{
			try
			{
				NaiKeys? keys = null;

				if (!string.IsNullOrWhiteSpace(authConfig?.EncryptionKey) && !string.IsNullOrWhiteSpace(authConfig?.AccessToken))
				{
					string tok = authConfig?.AccessToken;
					if (tok.Length != 0)
						keys = new NaiKeys
						{
							AccessToken = tok,
							EncryptionKey = Convert.FromBase64String(authConfig?.EncryptionKey),
						};
				}

				if (keys == null && !string.IsNullOrWhiteSpace(authConfig?.EncryptionKey) && !string.IsNullOrWhiteSpace(authConfig?.AccessKey))
				{
					string tok = Auth.GetAccessToken(authConfig?.AccessKey);
					if (tok.Length != 0)
						keys = new NaiKeys
						{
							AccessToken = tok,
							EncryptionKey = Convert.FromBase64String(authConfig?.EncryptionKey),
						};
				}

				if (keys == null && !string.IsNullOrWhiteSpace(authConfig?.Username) && !string.IsNullOrWhiteSpace(authConfig?.Password))
				{
					keys = Auth.AuthKeys(authConfig?.Username, authConfig?.Password);
				}

				keys ??= Auth.AuthEnv();

				NaiKeys k = keys.Value;

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
					client = new RestClient(string.IsNullOrWhiteSpace(urlEndpoint) ? Structs.ENDPOINT : urlEndpoint),
					encoder = KayraEncoder.Create(),
					currentParams = generationParams ?? defaultParams,
				};
			}
			catch (Exception ex)
			{
				Console.WriteLine("Error creating NovelAPI");
				Console.WriteLine(ex.ToString());
				return null;
			}
		}
        #endregion


        /// <summary>
        /// API method to access the endpoint for: /ai/classify
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<object> ClassifyAsync()
        {
            throw new NotImplementedException();
        }

        #region Image Generation Endpoints

        /// <summary>
        /// API method to access the endpoint for: /ai/annotate-image
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<object> AnnotateImageAsync(object inputParams)
		{
			throw new NotImplementedException();
		}

        /// <summary>
        /// API method to access the endpoint for: /ai/generate-image
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<object> GenerateImageAsync(object inputParams)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// API method to access the endpoint for: /ai/generate-image/suggest-tags
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<object> GenerateImageSuggestTagsAsync(object inputParams)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// API method to access the endpoint for: /ai/upscale
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<object> UpscaleImageAsync(object inputParams)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Story/Text Generation Endpoints

        /// <summary>
        /// API method to access the endpoint for: /ai/generate-stream
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<object> GenerateStreamAsync(object inputParams)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Voice Generation Endpoints

        /// <summary>
        /// API method to access the endpoint for: /ai/generate-voice
        /// </summary>
		/// <param name="inputParams"
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<NaiByteArrayResponse> GenerateVoiceAsync(NaiGenerateVoice inputParams)
        {
            //https://api.novelai.net/ai/generate-voice
            RestRequest request = new RestRequest("ai/generate-voice");
            request.Method = Method.Get;
            request.AddHeader("User-Agent", NovelAPI.AGENT);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Authorization", "Bearer " + keys.AccessToken);
            request.AddParameter("text", inputParams.text, true);
            request.AddParameter("voice", inputParams.voice, true);
            request.AddParameter("seed", inputParams.seed, true);
            request.AddParameter("opus", inputParams.opus ? "true" : "false", true);
            request.AddParameter("version", inputParams.version, true);

            RestResponse response = await client.ExecuteAsync(request);
			if (response.IsSuccessStatusCode)
				return new NaiByteArrayResponse()
				{
					ContentType = response.ContentType,
					output = response.RawBytes ?? new byte[] { },
                    StatusCode = (int)response.StatusCode
                };

			return new NaiByteArrayResponse()
			{
				ContentType = response.ContentType,
				output = response.RawBytes ?? new byte[] { },
				StatusCode = (int)response.StatusCode
			};
        }

        #endregion

        #region User Endpoints


        #endregion

        #region Helper Methods

        public string[] GetTokens(string input)
		{
			ushort[] tok = encoder.Encode(input);
			return new string[] { encoder.Decode(tok.ToArray()) };
		}


        #endregion
	}
}