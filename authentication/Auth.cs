using Konscious.Security.Cryptography;
using net.novelai.api;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;


//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using static net.novelai.api.Structs;

namespace net.novelai.authentication
{
	public static class Auth
	{
		public static string GetAccessToken(string access_key)
		{
			//https://api.novelai.net/user/login
			RestClient client = new RestClient("https://api.novelai.net/");
			RestRequest request = new RestRequest("user/login");
			Dictionary<string, string> parms = new Dictionary<string, string>();
			parms.Add("key", access_key);
			string json = JsonSerializer.Serialize(parms);
			request.AddJsonBody(json, "application/json");
			request.AddHeader("Content-Type", "application/json");
			RestResponse response = client.Post(request);
			if (response.IsSuccessful && response.Content != null)
			{
				Console.WriteLine("Login successful");
				Dictionary<string, string> resp_decoded = JsonSerializer.Deserialize<Dictionary<string, string>>(response.Content) ?? throw new Exception("Login failure");
				return resp_decoded["accessToken"];
			}
			else
			{
				Console.WriteLine("Login failed:");
				Console.WriteLine(response.StatusCode);
				Console.WriteLine(response.ErrorException);
				Console.WriteLine(response.ErrorMessage);
				return string.Empty;
			}
		}

		public static string ByteArrayToString(byte[] ba)
		{
			return BitConverter.ToString(ba).Replace("-", "");
		}

		public static byte[] NaiHashArgon(int size, string plaintext, string secret, string domain)
		{
			HMACBlake2B encoder = new HMACBlake2B(null, 16 * 8);//param is bits
			var salt = encoder.ComputeHash(Encoding.UTF8.GetBytes(secret + domain));
			var argon2 = new Argon2id(Encoding.UTF8.GetBytes(plaintext))
			{
				Salt = salt,
				DegreeOfParallelism = 1,
				MemorySize = 2000000 / 1024,
				Iterations = 2
			};
			return argon2.GetBytes(size);
		}

		public static NaiKeys NaiGenerateKeys(string email, string password)
		{
			string pw_email_secret = password.Substring(0, 6) + email;
			byte[] encryption_key = NaiHashArgon(128,
				password,
				pw_email_secret,
				"novelai_data_encryption_key");
			byte[] access_key = NaiHashArgon(64,
				password,
				pw_email_secret,
				"novelai_data_access_key");

			string access_string = Convert.ToBase64String(access_key).Substring(0, 64);
			access_string = access_string.Replace("/", "_");
			access_string = access_string.Replace("+", "-");
			string encryption_string = Convert.ToBase64String(encryption_key);
			encryption_string = encryption_string.Replace("/", "_");
			encryption_string = encryption_string.Replace("+", "-");
			while (encryption_string.EndsWith("=")) // TODO: test robustness
				encryption_string = encryption_string.Substring(0, encryption_string.Length - 1);

			HMACBlake2B encoder = new HMACBlake2B(null, 32 * 8);//param is bits
			encryption_key = encoder.ComputeHash(Encoding.UTF8.GetBytes(encryption_string));
			return new NaiKeys
			{
				EncryptionKey = encryption_key,
				AccessKey = access_string,
			};
		}

		public static string[] GenerateUsernames(string email)
		{
			string[] usernames;
			string titleCase = email.ToUpper()[0] + email.ToLower().Substring(1);
			if (email.ToLower() != email)
			{
				usernames = new string[] { email, email.ToLower(), titleCase };
			}
			else
			{
				usernames = new string[] { email, titleCase };
			}
			return usernames;
		}

        /// <summary>
        /// Static method to initialize an NaiKeys object using an email/password combination
        /// </summary>
        /// <param name="email"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public static NaiKeys AuthKeys(string email, string password)
		{
			string[] usernames = GenerateUsernames(email);
			NaiKeys keys = new NaiKeys();
			foreach (string username in usernames)
			{
				keys = NaiGenerateKeys(username, password);
				keys.AccessToken = GetAccessToken(keys.AccessKey);
				if (!string.IsNullOrEmpty(keys.AccessToken))
				{
					break;
				}
			}
			if (string.IsNullOrEmpty(keys.AccessToken))
			{
				Console.WriteLine("Failed to authenticate with NovelAI!");
			}
			return keys;
		}

		/// <summary>
		/// Static method to initialize an NaiKeys object from the auth.json file in the config path
		/// </summary>
		/// <returns>an initialized NaiKeys object</returns>
		/// <exception cref="Exception"></exception>
		public static NaiKeys AuthEnv()
		{
			if (!Directory.Exists(NovelAPI.CONFIG_PATH + ""))
			{
				Directory.CreateDirectory(NovelAPI.CONFIG_PATH + "");
			}
			if (File.Exists(NovelAPI.CONFIG_PATH + "/auth.json"))
			{
				string json = File.ReadAllText(NovelAPI.CONFIG_PATH + "/auth.json");
				Dictionary<string, string> authCfg = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? throw new Exception("NaiKeys AuthEnv Failure");
				if (authCfg.ContainsKey("AccessKey") && !string.IsNullOrWhiteSpace(authCfg["AccessKey"]))
				{ //Fallback override
					string tok = GetAccessToken(authCfg["AccessKey"]);
					if (tok.Length != 0)
						return new NaiKeys
						{
							AccessToken = tok,
							EncryptionKey = Convert.FromBase64String(authCfg["EncryptionKey"]),
						};
				}

				NaiKeys auth = AuthKeys(authCfg["Username"], authCfg["Password"]);
				if (auth.AccessToken.Length == 0)
				{
					Console.WriteLine("auth: failed to obtain AccessToken!");
				}
				else
				{
					AuthConfig upAuth = new AuthConfig
					{
						Username = authCfg["Username"],
						Password = authCfg["Password"],
						AccessKey = auth.AccessKey,
						EncryptionKey = Convert.ToBase64String(auth.EncryptionKey)
					};
					File.WriteAllText(NovelAPI.CONFIG_PATH + "/auth.json", JsonSerializer.Serialize(upAuth));
				}
				return auth;
			}
			else
			{
				AuthConfig newAuth = new AuthConfig
				{
					Username = "<empty>",
					Password = "<empty>",
				};
				File.WriteAllText(NovelAPI.CONFIG_PATH + "/auth.json", JsonSerializer.Serialize(newAuth));
				return AuthKeys(newAuth.Username, newAuth.Password);
			}
		}

        /// <summary>
        /// API method to retrieve the endpoint for: /user/keystore
        /// </summary>
        /// <param name="keys"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static Dictionary<string, byte[]> GetKeystore(NaiKeys keys)
		{
			Dictionary<string, byte[]> store = new Dictionary<string, byte[]>();
			RestClient client = new RestClient("https://api.novelai.net/");
			RestRequest request = new RestRequest("user/keystore");
			request.AddHeader("Content-Type", "application/json");
			request.AddHeader("Authorization", "Bearer " + keys.AccessToken);
			RestResponse response = client.Get(request);
			if (!response.IsSuccessful || response.Content == null)
			{
				Console.WriteLine("Could not fetch keystore:");
				Console.WriteLine(response.StatusCode);
				Console.WriteLine(response.ErrorException);
				Console.WriteLine(response.ErrorMessage);
				return store;
			}
			Dictionary<string, object> raw = JsonSerializer.Deserialize<Dictionary<string, object>>(response.Content) ?? throw new Exception("GetKeystore Failure");
			if (!raw.ContainsKey("keystore"))
			{
				Console.WriteLine("Keystore was not present");
				return store;
			}
			byte[] bytes = Convert.FromBase64String(raw["keystore"].ToString()!);
			string str = Encoding.Default.GetString(bytes);
			Dictionary<string, object> raw2 = JsonSerializer.Deserialize<Dictionary<string, object>>(str) ?? throw new Exception("GetKeystore Failure");
			if (!(raw2.ContainsKey("nonce") && raw2.ContainsKey("sdata")))
			{
				Console.WriteLine("nonce or sdata was not present");
				return store;
			}

			;

			raw2.TryGetValue("nonce", out object? obj);
			
			JsonElement nonceo = (JsonElement)raw2["nonce"];
			JsonElement sdatao = (JsonElement)raw2["sdata"];

			byte[] nonce = new byte[nonceo.GetArrayLength()];
			byte[] sdata = new byte[sdatao.GetArrayLength()];
			for (int i = 0; i < nonce.Length; i++)
			{
				JsonElement q = nonceo[i];
				nonce[i] = q.GetByte();
			}
			for (int i = 0; i < sdata.Length; i++)
			{
				JsonElement q = sdatao[i];
				sdata[i] = q.GetByte();
			}
			Dictionary<string, object> raw3 = JsonSerializer.Deserialize<Dictionary<string, object>>(Encoding.Default.GetString(Sodium.SecretBox.Open(sdata, nonce, keys.EncryptionKey))) ?? throw new Exception("GetKeystore Failure");
			if (!raw3.ContainsKey("keys"))
			{
				Console.WriteLine("Unsealed keystore did not contain any keys");
				return store;
			}
			JsonElement keyJson = (JsonElement)raw3["keys"];
			foreach (var kv in keyJson.EnumerateObject())
			{
				string key = kv.Name;
				JsonElement jsonarr = kv.Value;
				
				byte[] vals = new byte[jsonarr.GetArrayLength()];
				for (int i = 0; i < vals.Length; i++)
				{
					vals[i] = jsonarr[i].GetByte();
				}
				store.Add(key, vals);
			}

			return store;
		}
	}
}
