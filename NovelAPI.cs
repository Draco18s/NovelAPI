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
using System.IO;
using System.IO.Compression;
using System.Linq;
using net.novelai.api.msgpackr;
using Newtonsoft.Json.Linq;
using net.novelai.generation;

namespace net.novelai.api
{
	public class NovelAPI
	{
		#region Properties and Constants
		public static string CONFIG_PATH = "./config";
		public const string NAME = "novelapi";
		public const string VERSION = "0.4";
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

		private NovelAPI() { }

		/// <summary>
		/// Static API method to retrieve the endpoint for: /
		/// </summary>
		/// <returns>true if the endpoint returns "OK", otherwise false</returns>
		public static async Task<bool> GetEndpointStatus(string urlEndpoint = null)
		{
			try
			{
				var client = new RestClient(string.IsNullOrWhiteSpace(urlEndpoint) ? API_ENDPOINT : urlEndpoint);
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

		#region Module Methods

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
			RestRequest request = BuildNewRestRequest("user/objects/aimodules", Method.Get);
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

		#endregion

		#region Story Methods
		/// <summary>
		/// API method to retrieve the endpoint for: /user/objects/stories
		/// </summary>
		/// <returns>An initialized list of RemoteStoryMeta objects</returns>
		/// <exception cref="Exception"></exception>
		public async Task<List<RemoteStoryMeta>> GetStories()
		{
			List<RemoteStoryMeta> stories = new List<RemoteStoryMeta>();
			RestRequest request = BuildNewRestRequest("user/objects/stories", Method.Get);
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
			//https://api.novelai.net/user/objects/stories/{id}

			RestRequest request = BuildNewRestRequest("user/objects/stories/" + storyId, Method.Get);
			RestResponse response = await client.ExecuteAsync(request);
			if (!response.IsSuccessful || response.Content == null)
			{
				return null;
			}
			RemoteStoryMeta remoteStoryMeta = ParseRemoteStoryJson(response.Content) ?? throw new Exception("GetStory Failure");

			return remoteStoryMeta.meta;
		}

		public async Task<IEnumerable<JToken>> GetStoryContent()
		{
			var data = new JArray();
			var response = await GetUserObjects(UserObjectType.StoryContent);

			foreach (var content in response.Objects)
			{
				var decodedString = DecodeData(content.Meta, content.Data);
				if (!string.IsNullOrWhiteSpace(decodedString))
				{
					var jData = JToken.Parse(decodedString ?? "");

					// Document data is encoded as MessagePack data. See spec: https://msgpack.org/
					string document = data.SelectToken("$.document")?.ToString();
					// Todo: Decode document data.

					data.Add(jData);
				}
			}

			return data;
		}

		public async Task<JToken> GetStoryContent(string storyId)
		{
			JToken data = null;
			var content = await GetUserObject(UserObjectType.StoryContent, storyId);

			var decodedString = DecodeData(content.Meta, content.Data);
			if (!string.IsNullOrWhiteSpace(decodedString))
			{
				data = JToken.Parse(decodedString ?? "");

				// Document data is encoded as MessagePack data. See spec: https://msgpack.org/
				// Specifically, NovelAi uses the MIT licensed Javascript library provided by Kris Zyp
				// to decode the data. (https://github.com/kriszyp/msgpackr) 
				// MsgPackerUnpack is a C# port of Kris Zyp's Javascript code.
				// NovelAiMsgUnpacker is an extended version with NovelAi specific extension handlers
				try
				{
					string document = data.SelectToken("$.document")?.ToString();
					byte[] documentData = Convert.FromBase64String(document ?? "");
					var reader = new NovelAiMsgUnpacker(new MsgUnpackerOptions() { BundleStrings = true, MoreTypes = true, StructuredClone = false });
					var o = reader.Unpack(documentData, new MsgUnpackerOptions() { MapsAsObjects = false });
					if (o is JToken token)
					{
						data["packedDocument"] = document;
						data["document"]?.Replace(token);
					}
				}
				catch
				{
					// Do nothing
				}
			}

			return data ?? new JObject();
		}

		#endregion

		#region Text Generation Endpoints

		public async Task<string[]> GetModels()
		{
			string[] defaultModels = new string[] { "" };
			RestRequest request = BuildNewRestRequest("oa/v1/models", Method.Get);
			RestResponse response = await client.ExecuteAsync(request);
			if (!response.IsSuccessful || response.Content == null)
			{
				return defaultModels;
			}
			Dictionary<string, object> raw = JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Content) ?? throw new Exception("GetModels Failure");
			if (!raw.ContainsKey("data"))
			{
				return defaultModels;
			}
			object[] rawModels = (object[])raw["data"];

			List<NaiModel> models = new List<NaiModel>();
			foreach (object o in rawModels)
			{
				NaiModel m = JsonConvert.DeserializeObject<NaiModel>(o.ToString());
				models.Add(m);
			}

			return models.Select(m => m.id).ToArray();
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
			NaiGenerateResp resp = new NaiGenerateResp();
			string json = string.Empty;

			if (parms.model == "null")
			{
				parms.model = "glm-4-6";
			}

			string hook = string.Empty;
			if (parms.model == "kayra-v1")
			{
				hook = "ai/generate";
				uint[] encoded = encoder.Encode(content.Trim());
				byte[] encodedBytes = ToBin(encoded.Select(n => (uint)n).ToArray());
				string encodedBytes64 = Convert.ToBase64String(encodedBytes);
				resp.EncodedRequest = encodedBytes64;
				NaiGenerateKayra kayra = NewKayraMsg(encodedBytes64);
				kayra.parameters = parms;
				kayra.model = kayra.parameters.model;
				if (kayra.parameters.bracket_ban)
				{
					List<uint[]> concat = new List<uint[]>(kayra.parameters.bad_words_ids);
					concat.AddRange(BannedBrackets());
					kayra.parameters.bad_words_ids = concat.ToArray();
				}

				json = JsonConvert.SerializeObject(kayra);
			}
			else if(parms.model == "glm-4-6")
			{
				hook = "oa/v1/completions";
				uint[] encoded = encoder.Encode(content.Trim());

				NaiGenerateGLM glm = NewGlmMsg(encoded, parms);
				json = JsonConvert.SerializeObject(glm);
				Console.WriteLine($"\n\n{json}\n\n");
			}

			RestRequest request = BuildNewRestRequest(hook, Method.Post);
			request.AddJsonBody(json);
			request.AddHeader("x-correlation-id", "DRAC18");
			RestResponse response = await client.ExecutePostAsync(request);
			if (!response.IsSuccessful || response.Content == null)
			{
				Console.WriteLine(response.Content);
				throw new Exception(response.Content);
			}

			Dictionary<string, object> raw = JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Content) ?? throw new Exception("NaiApiGenerateAsync Failure");
			
			if (raw.ContainsKey("choices"))
			{
				var choices = JsonConvert.DeserializeObject<object[]>(raw["choices"].ToString()) ?? throw new Exception("NaiApiGenerateAsync Failure");
				var c1 = choices[0];
				var m = JsonConvert.DeserializeObject<Dictionary<string, object>>(c1.ToString()) ?? throw new Exception("NaiApiGenerateAsync Failure");
				if (m.ContainsKey("text"))
				{
					resp.Response = m["text"].ToString();
				}
			}
			else
			{
				string output = raw.ContainsKey("output") ? (string)raw["output"] : (string)raw["message"];
				byte[] binTokens = Convert.FromBase64String(output);
				resp.Response = encoder.Decode(FromBinTouint(binTokens).ToArray());

				resp.EncodedResponse = output;
			}
			
			return resp;
		}

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
		/// <summary>
		/// API method to access the endpoint for: /ai/generate-stream
		/// </summary>
		/// <returns></returns>
		/// <exception cref="NotImplementedException"></exception>
		public async Task<object> GenerateStreamAsync(object inputParams)
		{
			throw new NotImplementedException();
		}
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

		#endregion


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
		https://api.novelai.net/user/delete
		https://api.novelai.net/user/deletion/request
		https://api.novelai.net/user/deletion/delete
		https://api.novelai.net/user/giftkeys
		https://api.novelai.net/user/information
		https://api.novelai.net/user/keystore
		https://api.novelai.net/user/login
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
		https://api.novelai.net/user/submission
		https://api.novelai.net/user/submission/{???}
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
		/// <param name="errorCallback">Callback for exception handling. If null, errors are printed to the console.<br/>Useful if the console is unavailable.</param>
		/// <returns>An initialized NovelAPI object authenticated using the credentials given</returns>
		public static NovelAPI NewNovelAiAPI(string username, string password, Action<Exception> errorCallback = null)
		{
			return NewNovelAiAPI(new AuthConfig() { Username = username, Password = password }, null, null, errorCallback);
		}
		/// <summary>
		/// Factory constructor to create a NovelAPI object initialized with API token
		/// </summary>
		/// <param name="apikey">The NovelAi.net username in plain text</param>
		/// <param name="errorCallback">Callback for exception handling. If null, errors are printed to the console.<br/>Useful if the console is unavailable.</param>
		/// <returns>An initialized NovelAPI object authenticated using the credentials given</returns>
		public static NovelAPI NewNovelAiAPI(string apikey, Action<Exception> errorCallback = null)
		{
			return NewNovelAiAPI(new AuthConfig() { APIKey = apikey }, null, null, errorCallback);
		}

		/// <summary>
		/// Factory constructor to create a NovelAPI object initialized with the credentials 
		/// provided in the authConfig parameter. 
		/// </summary>
		/// <param name="authConfig">
		/// Authorization parameters to initialize the API object with. If the parameters are not set, 
		/// or authConfig is null, then the values will be loaded from the auth.json file found in the config path.
		/// </param>
		/// <param name="generationParams">Parameters used to override the default generation params</param>
		/// <param name="urlEndpoint">API endpoint to use (<see cref="net.novelai.api.Structs"/>); default https://text.novelai.net/</param>
		/// <param name="errorCallback">Callback for exception handling. If null, errors are printed to the console.<br/>Useful if the console is unavailable.</param>
		/// <returns></returns>
		public static NovelAPI NewNovelAiAPI(AuthConfig? authConfig = null, NaiGenerateParams? generationParams = null, string urlEndpoint = null, Action<Exception> errorCallback = null)
		{
			try
			{
				NaiKeys? keys = null;
				var prms = generationParams ?? defaultParams;
				if (prms.model == "null")
					prms.model = "glm-4-6";

				if (!string.IsNullOrWhiteSpace(authConfig?.APIKey))
				{
					keys = Auth.AuthKeys(authConfig?.APIKey);
					return new NovelAPI
					{
						keys = keys.Value,
						client = new RestClient(string.IsNullOrWhiteSpace(urlEndpoint) ? TEXT_ENDPOINT : urlEndpoint, options => options.UserAgent = AGENT),
						currentParams = prms,
						encoder = KayraEncoder.Create(prms.model),
					};
				}

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
					if (errorCallback != null)
						errorCallback(bex);
					else
						Console.WriteLine(bex.ToString());
				}

				return new NovelAPI
				{
					keys = k,
					client = new RestClient(string.IsNullOrWhiteSpace(urlEndpoint) ? TEXT_ENDPOINT : urlEndpoint),
					currentParams = prms,
					encoder = KayraEncoder.Create(prms.model),
				};
			}
			catch (Exception ex)
			{
				if (errorCallback != null)
					errorCallback(ex);
				else
					Console.WriteLine($"Error creating NovelAPI!\n{ex}");
				return null;
			}
		}
		#endregion


#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
		/// <summary>
		/// API method to access the endpoint for: /ai/classify
		/// </summary>
		/// <returns></returns>
		/// <exception cref="NotImplementedException"></exception>
		public async Task<object> ClassifyAsync()
		{
			throw new NotImplementedException();
		}
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

		#region Image Generation Endpoints

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
		/// <summary>
		/// API method to access the endpoint for: /ai/annotate-image
		/// </summary>
		/// <returns></returns>
		/// <exception cref="NotImplementedException"></exception>
		public async Task<object> AnnotateImageAsync(object inputParams)
		{
			throw new NotImplementedException();
		}
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

		/// <summary>
		/// API method to access the endpoint for: /ai/generate-image, and extract a single image
		/// </summary>
		/// <returns></returns>
		/// <exception cref="NotImplementedException"></exception>
		public async Task<NaiByteArrayResponse> GenerateImageAsync(NaiImageGenerationRequest imgRequest)
		{
			try
			{
				imgRequest.Parameters.NumberOfImagesToGenerate = 1;
				var response = await GenerateImageArchiveAsync(imgRequest);
				byte[] archiveBytes = response.output;
				response.output = ExtractFileFromByteArchive(archiveBytes, "image_0.png");
				return response;
			}
			catch (Exception ex)
			{
				NaiByteArrayResponse data = new NaiByteArrayResponse();
				data.ContentType = "";
				data.StatusCode = -1;
				data.Error = "An unknown error has occurred";
				data.Message = ex.Message;
				return data;
			}
		}

		public async Task<NaiByteArrayResponse> GenerateImageArchiveAsync(NaiImageGenerationRequest imgRequest)
		{
			NaiByteArrayResponse data = new NaiByteArrayResponse();
			try
			{
				var request = BuildNewRestRequest("ai/generate-image", Method.Post);
				request.AddJsonBody(JsonConvert.SerializeObject(imgRequest));
				RestResponse response = await client.ExecuteAsync(request);
				data.ContentType = response.ContentType;
				data.StatusCode = (int)response.StatusCode;
				if (response.IsSuccessStatusCode)
				{
					data.output = response.RawBytes ?? new byte[] { };
				}
				else
				{
					var err = JsonConvert.DeserializeObject<NaiApiError>(response.Content ?? "{}");
					data.StatusCode = err.StatusCode;
					data.Message = err.Message;
				}
			}
			catch (Exception ex)
			{
				data.ContentType = "";
				data.StatusCode = -1;
				data.Error = "An unknown error has occurred";
				data.Message = ex.Message;
			}
			return data;
		}

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
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
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

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
			RestRequest request = BuildNewRestRequest("ai/generate-voice");
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

		/// <summary>
		/// API method to retrieve the endpoint for: /user/priority
		/// </summary>
		/// <returns>The number of remaining priority actions if successful, otherwise 0</returns>
		/// <exception cref="Exception"></exception>
		public async Task<int> GetCurrentPriority()
		{
			RestRequest request = BuildNewRestRequest("user/priority", Method.Post);

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
		/// API method to retrieve the endpoint for: /user/priority
		/// </summary>
		/// <returns>The number of remaining priority actions if successful, otherwise 0</returns>
		/// <exception cref="Exception"></exception>
		public async Task<int> GetRemainingActions()
		{
			//https://api.novelai.net/user/priority
			RestRequest request = BuildNewRestRequest("user/priority", Method.Post);

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
		/// Retrieves a current copy of the keystore array from the server
		/// </summary>
		/// <returns></returns>
		public IEnumerable<KeyValuePair<string, byte[]>> GetKeystore() => Auth.GetKeystore(keys);

		public async Task<JArray> GetUserShelf(string id = null)
		{
			JArray result = new JArray();

			try
			{
				if (string.IsNullOrWhiteSpace(id))
				{
					var response = await GetUserObjects(UserObjectType.Shelf);
					// Todo: Add error handling
					foreach (var obj in response.Objects)
					{
						var data = DecodeData(obj.Meta, obj.Data);
						JToken jData = JToken.FromObject(obj);
						jData["decodedData"] = JToken.Parse(data);
						result.Add(jData);
					}
				}
				else
				{
					var response = await GetUserObject(UserObjectType.Shelf, id);
					// Todo: Add error handling
					var data = DecodeData(response.Meta, response.Data);
					JToken jData = JToken.FromObject(response);
					jData["decodedData"] = JToken.Parse(data);
					result.Add(jData);
				}
			}
			catch
			{
				// do nothing
				result = null;
			}

			return result;
		}

		public async Task<NaiObjectResponse> GetUserObjects(UserObjectType type) => await RetrieveNaiApiResponse<NaiObjectResponse>($"user/objects/{(JsonConvert.SerializeObject(type) ?? "").Replace("\"", "")}");

		public async Task<NaiUserData> GetUserObject(UserObjectType type, string id) => await RetrieveNaiApiResponse<NaiUserData>($"user/objects/{(JsonConvert.SerializeObject(type) ?? "").Replace("\"", "")}/{id}");

		public async Task<NaiAccountInformationResponse> GetUserAccountInformationAsync() => await GetNaiApiResponse<NaiAccountInformationResponse>("user/information");

		public async Task<NaiUserAccountDataResponse> GetUserDataAsync() => await GetNaiApiResponse<NaiUserAccountDataResponse>("user/data");

		public async Task<NaiPriorityResponse> GetUserPriorityAsync() => await GetNaiApiResponse<NaiPriorityResponse>("user/priority");

		public async Task<NaiSubscriptionResponse> GetUserSubscriptionAsync() => await GetNaiApiResponse<NaiSubscriptionResponse>("user/subscription");

		#endregion

		#region Helper Methods

		public async Task<T> RetrieveNaiApiResponse<T>(string endpoint, object data = null, Method requestMethod = Method.Get) where T : class, INaiApiError, new()
		{
			try
			{
				var request = BuildNewRestRequest(endpoint, requestMethod);

				if (data != null)
					request.AddJsonBody(JsonConvert.SerializeObject(data));

				RestResponse response = await client.ExecuteAsync(request);
				if (response.Content != null)
					return JsonConvert.DeserializeObject<T>(response.Content);
			}
			catch
			{
				// Do nothing
			}
			return new T();
		}

		public async Task<T> GetNaiApiResponse<T>(string endpoint) where T : class, INaiApiError, new() => await RetrieveNaiApiResponse<T>(endpoint);

		public RestRequest BuildNewRestRequest(string endpoint, Method requestMethod = Method.Get)
		{
			RestRequest newRequest = new RestRequest(endpoint);
			newRequest.Method = requestMethod;
			newRequest.AddHeader("User-Agent", AGENT);
			newRequest.AddHeader("Content-Type", "application/json");
			string key = keys.APIKey ?? keys.AccessToken;
			newRequest.AddHeader("Authorization", $"Bearer {key.Trim()}");

			return newRequest;
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

		public static readonly byte[] CompressionHeader = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01 };

		public string DecodeData(string meta, string dataIn)
		{
			try
			{
				keys.keystore.TryGetValue(meta, out byte[] sk);
				byte[] data = Convert.FromBase64String(dataIn);

				// Is data compressed?
				if (data.Length > 16)
				{
					if (CompressionHeader.SequenceEqual(data.Take(16)))
					{
						data = data.Skip(16).ToArray();

						// Is data encrypted?
						if (sk != null)
						{
							// Decrypt data
							data = Sodium.SecretBox.Open(data.Skip(24).ToArray(), data.Take(24).ToArray(), sk);
						}

						// Data is compressed. need to decompress
						using (var outputStream = new MemoryStream())
						{
							using (var dataStream = new MemoryStream(data))
							{
								dataStream.Seek(0, SeekOrigin.Begin);
								using (var deflateStream = new DeflateStream(dataStream, CompressionMode.Decompress))
								{
									deflateStream.CopyTo(outputStream);
								}
							}
							data = outputStream.ToArray();
						}
						return Encoding.UTF8.GetString(data);
					}

				}

				if (sk != null)
				{
					return Encoding.Default.GetString(Sodium.SecretBox.Open(data.Skip(24).ToArray(), data.Take(24).ToArray(), sk));
				}

				return DecodeBase64(dataIn);
			}
			catch
			{
				// Do nothing
			}

			return null;
		}



		public static string DecodeBase64(string dataIn)
		{
			byte[] data = Convert.FromBase64String(dataIn);
			return Encoding.UTF8.GetString(data);
		}

		/// <summary>
		/// Static API method to convert an array of tokens into a byte array
		/// </summary>
		/// <param name="tokens">an array of encoded tokens</param>
		/// <returns>an initialized byte array</returns>
		public static byte[] ToBin(uint[] tokens)
		{
			ReadWriteBuffer buf = new ReadWriteBuffer(tokens.Length * BitConverter.GetBytes(tokens[0]).Length);
			foreach (uint b in tokens)
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
		public static uint[] FromBinTouint(byte[] bytes)
		{
			uint[] tokens = new uint[bytes.Length / 2];
			ReadWriteBuffer buf = new ReadWriteBuffer(bytes);
			int i = 0;
			while (buf.Count > 0)
			{
				uint token = BitConverter.ToUInt16(buf.Read(2), 0);
				tokens[i] = token;
				i++;
			}
			return tokens;
		}

		/// <summary>
		/// Static API method to convert a byte array into an array of tokens
		/// </summary>
		/// <param name="bytes">a byte array with token data</param>
		/// <returns>an initialized array of encoded token data</returns>
		public static uint[] FromBinToUint(byte[] bytes)
		{
			uint[] tokens = new uint[bytes.Length / 4];
			ReadWriteBuffer buf = new ReadWriteBuffer(bytes);
			int i = 0;
			while (buf.Count > 0)
			{
				uint token = BitConverter.ToUInt32(buf.Read(4), 0);
				tokens[i] = token;
				i++;
			}
			return tokens;
		}

		/// <summary>
		/// Static API method to create a default array of Banned Bracket tokens
		/// </summary>
		/// <returns></returns>
		public static uint[][] BannedBrackets()
		{
			return new uint[][]{ new uint[] { 3 }, new uint[] { 49356 }, new uint[] { 1431 }, new uint[] { 31715 }, new uint[] { 34387 }, new uint[] { 20765 },
				new uint[] { 30702 }, new uint[] { 10691 }, new uint[] { 49333 }, new uint[] { 1266 }, new uint[] { 26523 }, new uint[] { 41471 },
				new uint[] { 2936 }, new uint[] { 85, 85 }, new uint[] { 49332 }, new uint[] { 7286 }, new uint[] { 1115 } };
		}

		/// <summary>
		/// Static API method to create a default NaiGenerateParams object
		/// </summary>
		/// <returns>An initalized object with default parameters set</returns>
		public static NaiGenerateParams NewGenerateParams()
		{
			return new NaiGenerateParams
			{
				model = "null",
				prefix = "special_openings",
				logit_bias_exp = new BiasParams[]
				{
					new BiasParams()
					{
						bias = -0.08,
						ensure_sequence_finish = false,
						generate_once = false,
						sequence = new uint[]{23}
					},
					new BiasParams()
					{
						bias = -0.08,
						ensure_sequence_finish = false,
						generate_once = false,
						sequence = new uint[]{21}
					}
				},
				temperature = 1.35,
				max_length = 40,
				min_length = 40,
				top_a = 0.1,
				top_k = 15,
				top_p = 0.85,
				num_logprobs = 0,
				order = new uint[] { 2, 3, 0, 4, 1 },
				phrase_rep_pen = "aggressive",
				tail_free_sampling = 0.915,
				repetition_penalty = 2.8,
				repetition_penalty_range = 2048,
				repetition_penalty_slope = 0.02,
				repetition_penalty_frequency = 0.02,
				repetition_penalty_presence = 0,
				bad_words_ids = Array.Empty<uint[]>(),
				stop_sequences = new uint[][] { new uint[] { 85 } },
				bracket_ban = true,
				use_cache = false,
				use_string = false,
				return_full_text = false,
				generate_until_sentence = true,
				repetition_penalty_whitelist = new uint[] { 49256, 49264, 49231, 49230, 49287, 85, 49255, 49399, 49262, 336,
					333, 432, 363, 468, 492, 745, 401, 426, 623, 794, 1096, 2919, 2072, 7379, 1259, 2110, 620, 526, 487, 16562,
					603, 805, 761, 2681, 942, 8917, 653, 3513, 506, 5301, 562, 5010, 614, 10942, 539, 2976, 462, 5189, 567, 2032,
					123, 124, 125, 126, 127, 128, 129, 130, 131, 132, 588, 803, 1040, 49209, 4, 5, 6, 7, 8, 9, 10, 11, 12 },
			};
		}

		/// <summary>
		/// Static API method to create a default NaiGenerateMsg object
		/// </summary>
		/// <param name="input">The input prompt to use for the message</param>
		/// <returns>An initialized object with default parameters set</returns>
		public static NaiGenerateKayra NewKayraMsg(string input)
		{
			NaiGenerateParams parms = NewGenerateParams();
			return new NaiGenerateKayra
			{
				input = input,
				model = "kayra-v1",
				parameters = parms,
			};
		}

		/// <summary>
		/// Static API method to create a default NaiGenerateMsg object
		/// </summary>
		/// <param name="input">The input prompt to use for the message</param>
		/// <returns>An initialized object with default parameters set</returns>
		public static NaiGenerateGLM NewGlmMsg(uint[] input, NaiGenerateParams parms)
		{
			//NaiGenerateParams parms = NewGenerateParams();
			return new NaiGenerateGLM
			{
				prompt = input,
				model = "glm-4-6",
				max_tokens = parms.max_length,
				temperature = 1, //parms.temperature,
				top_p = 0.95, //parms.top_p,
				top_k = 40,// parms.top_k,
				frequency_penalty = 0,//parms.repetition_penalty_frequency,
				presence_penalty = parms.repetition_penalty_presence,
				unified_linear = 0,//parms.repetition_penalty_slope,
				stop = [],
				logit_bias = new Dictionary<int, double>()
				{  // taken directly from NAI's own invocation
					{ 42, -1.25 },  { 47, -0.43125007 },  { 467, -0.85 },  { 479, -0.9 },  { 521, -0.29375002 },  { 619, -1.15 },  { 663, -3.2 },  { 730, -1.65 },  { 806, -2.2250001 },
					{ 882, -4.1 },  { 883, -1.13125 },  { 949, -0.61875 },  { 1084, -0.85 },  { 1101, -0.4375 },  { 1148, -0.35625 },  { 1156, -2.3750002 },  { 1185, -0.35 },
					{ 1212, -0.58750004 },  { 1424, -2.04375 },  { 1644, -2.4 },  { 1739, -0.2 },  { 2160, -2.6 },  { 2784, -0.3125 },  { 3108, -0.94375 },  { 3347, -1.175 },
					{ 3685, -0.6625 },  { 3720, -1.5937499 },  { 3984, -1.3 },  { 4104, -0.75 },  { 4445, -1.3 },  { 4680, -0.9625001 },  { 4746, -0.93125 },  { 5112, -1.3 },
					{ 5306, -0.3625 },  { 5530, -0.625 },  { 5662, -2.875 },  { 6285, -0.36875 },  { 6414, -1.1 },  { 6476, -0.48749998 },  { 6821, -2.23125 },  { 6911, -0.68125004 },
					{ 6961, -0.675 },  { 7027, -1.0124999 },  { 7071, -0.25 },  { 7726, -0.35625 },  { 7737, -0.41875 },  { 7743, -3.075 },  { 8050, -1.8 },  { 8244, -0.7 },  { 8412, -0.5 },
					{ 8457, -0.625 },  { 9055, -0.475 },  { 9252, -0.47499996 },  { 10136, -4.19375 },  { 10482, -0.34999996 },  { 10616, -1 },  { 10775, -2.05625 },  { 10860, -0.45 },
					{ 11047, -1.1062499 },  { 11103, -0.85 },  { 11169, -0.9375 },  { 12039, -1.275 },  { 12238, -2.06875 },  { 12449, -0.29375 },  { 12457, -1.00625 },  { 12865, -2.5187502 },
					{ 12877, -0.925 },  { 13047, -0.55625004 },  { 13692, -0.39375 },  { 13929, -0.88125 },  { 13986, -0.15 },  { 14585, -2.3 },  { 14985, -1.4 },  { 15940, -0.9 },
					{ 16754, -1.1125 },  { 16910, -0.6125 },  { 16952, -0.35 },  { 17212, -0.63125 },  { 17574, -0.5 },  { 17833, -0.84375 },  { 18254, -2.35 },  { 18444, -3.2 },  { 19015, -0.3 },
					{ 19423, -0.21874999 },  { 20403, -1.25 },  { 21463, -0.71875 },  { 22013, -0.73125 }, { 22816, -0.3 },  { 22955, -1.1 },  { 23133, -1.725 },  { 23392, -1.1562499 },
					{ 23762, -1 },  { 24103, -0.40624997 },  { 24778, -0.99375004 },  { 25023, -0.4625 },  { 27494, -2.4 },  { 27871, -0.61875 },  { 28083, -0.225 },  { 28248, -0.55 },
					{ 29832, -1.35 },  { 30435, -0.20625001 },  { 31771, -1.65 },  { 32670, -3.7 },  { 33897, -1.45 },  { 34342, -1.26875 },  { 34682, -0.59999996 },  { 35221, -0.41250002 },
					{ 35505, -0.65 },  { 36117, -0.2875 },  { 36557, -1.7 },  { 36952, -0.45000005 },  { 37383, -0.23750001 },  { 37601, -1.2 },  { 37610, -0.8 },  { 40144, -0.675 },
					{ 40420, -0.99375004 },  { 50019, -1.55 },  { 50225, -1.55 },  { 54779, -3 },  { 55672, -0.425 },  { 56879, -0.3 },  { 59782, -1.65 },  { 60510, -0.40625 },  { 63588, -0.3 },
					{ 63887, -1.55 },  { 68076, -1.8 }, { 68316, -1.75 },  { 74522, -0.8375 },  { 75829, -2.2 },  { 80405, -1.85 },  { 84799, -0.5 },  { 84950, -1.45 },  { 84991, -0.062500015 },
					{ 87681, -0.61875 },{ 87874, -0.59375 },  { 88689, -1.25 },  { 90889, -1 },  { 91843, -0.70000005 },  { 94524, -1.95 },  { 96163, -1.55 },  { 151331, -100 },
					{ 151350, -100 },  { 151351, -100 },  { 151360, -100 }
				},
				logprobs = 1,//parms.num_logprobs,
				unified_quadratic = 0,
				unified_increase_linear_with_entropy = 0,
				unified_cubic = 0,
				stream = false,
			};
		}

		public static byte[] ExtractFileFromByteArchive(byte[] archiveBytes, string filename)
		{
			byte[] data = null;
			try
			{
				// Wrap byte array in memory stream for use with ZipArchive
				using (var byteStream = new MemoryStream(archiveBytes))
				{
					// Open byteStream as a Zip Archive
					using (var archive = new ZipArchive(byteStream, ZipArchiveMode.Read, false))
					{
						// Get the archive entry for the filename (if it exists)
						var entry = archive.GetEntry(filename);
						// Open the entry if it exists
						using (var entryStream = entry?.Open())
						{
							// If entry was opened, then continue
							if (entryStream != null)
							{
								// create a new MemoryStream to read the bytes of the entry into
								using (var memoryStream = new MemoryStream())
								{
									// copy entry into memoryStream
									entryStream.CopyTo(memoryStream);
									// Copy stream out to a byte array
									data = memoryStream.ToArray();
								}
							}
						}

					}
				}
			}
			catch
			{
				// Do nothing
			}
			return data ?? new byte[] { };
		}


		public string[] GetTokens(string input)
		{
			uint[] tok = encoder.Encode(input);
			return new string[] { encoder.Decode(tok.ToArray()) };
		}


		#endregion
	}
}