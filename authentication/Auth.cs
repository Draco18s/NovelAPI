using Konscious.Security.Cryptography;
using net.novelai.api;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static net.novelai.api.Structs;

namespace net.novelai.authentication {
	public static class Auth {
		public static string GetAccessToken(string access_key) {
			//https://api.novelai.net/user/login
			RestClient client = new RestClient("https://api.novelai.net/");
			RestRequest request = new RestRequest("user/login");
			Dictionary<string, string> parms = new Dictionary<string, string>();
			parms.Add("key", access_key);
			string json = SimpleJson.SerializeObject(parms);
			request.AddJsonBody(json, "application/json");
			request.AddHeader("Content-Type", "application/json");
			IRestResponse response = client.Post(request);
			if(response.IsSuccessful) {
				Console.WriteLine("Loging successful");
				Dictionary<string, string> resp_decoded = SimpleJson.DeserializeObject<Dictionary<string, string>>(response.Content);
				return resp_decoded["accessToken"];
			}
			else {
				Console.WriteLine("Loging failed:");
				Console.WriteLine(response.StatusCode);
				Console.WriteLine(response.ErrorException);
				Console.WriteLine(response.ErrorMessage);
				return string.Empty;
			}
		}

		public static string ByteArrayToString(byte[] ba) {
			return BitConverter.ToString(ba).Replace("-", "");
		}

		public static byte[] NaiHashArgon(int size, string plaintext, string secret, string domain) {
			HMACBlake2B encoder = new HMACBlake2B(null, 16*8);//param is bits
			var salt = encoder.ComputeHash(Encoding.UTF8.GetBytes(secret + domain));
			var argon2 = new Argon2id(Encoding.UTF8.GetBytes(plaintext)) {
				Salt = salt,
				DegreeOfParallelism = 1,
				MemorySize = 2000000/1024,
				Iterations = 2
			};
			return argon2.GetBytes(size);
		}

		public static NaiKeys NaiGenerateKeys(string email, string password) {
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
			while(encryption_string.EndsWith("=")) // TODO: test robustness
				encryption_string = encryption_string.Substring(0, encryption_string.Length - 1);

			HMACBlake2B encoder = new HMACBlake2B(null, 32 * 8);//param is bits
			encryption_key = encoder.ComputeHash(Encoding.UTF8.GetBytes(encryption_string));
			return new NaiKeys {
				EncryptionKey = encryption_key,
				AccessKey = access_string,
			};
		}

		public static string[] GenerateUsernames(string email) {
			string[] usernames;
			string titleCase = email.ToUpper()[0] + email.ToLower().Substring(1);
			if(email.ToLower() != email) {
				usernames = new string[] { email, email.ToLower(), titleCase };
			}
			else {
				usernames = new string[] { email, titleCase };
			}
			return usernames;
		}

		public static NaiKeys AuthKeys(string email, string password) {
			string[] usernames = GenerateUsernames(email);
			NaiKeys keys = new NaiKeys();
			foreach(string username in usernames) {
				keys = NaiGenerateKeys(username, password);
				keys.AccessToken = GetAccessToken(keys.AccessKey);
				if(!string.IsNullOrEmpty(keys.AccessToken)) {
					break;
				}
			}
			if(string.IsNullOrEmpty(keys.AccessToken)) {
				Console.WriteLine("Failed to authenticate with NovelAI!");
			}
			return keys;
		}

		public static NaiKeys AuthEnv() {
			if(!Directory.Exists("./config")) {
				Directory.CreateDirectory("./config");
			}
			if(File.Exists("./config/auth.json")) {
				string json = File.ReadAllText("./config/auth.json");
				Dictionary<string, string> authCfg = SimpleJson.DeserializeObject<Dictionary<string, string>>(json);
				if(authCfg.ContainsKey("AccessKey")) { //Fallback override
					string tok = GetAccessToken(authCfg["AccessKey"]);
					if(tok.Length != 0)
						return new NaiKeys {
							AccessToken = tok,
							EncryptionKey = Convert.FromBase64String(authCfg["EncryptionKey"]),
						};
				}

				NaiKeys auth = AuthKeys(authCfg["Username"], authCfg["Password"]);
				if(auth.AccessToken.Length == 0) {
					Console.WriteLine("auth: failed to obtain AccessToken!");
				}
				else {
					AuthConfig upAuth = new AuthConfig {
						Username = authCfg["Username"],
						Password = authCfg["Password"],
						AccessKey = auth.AccessKey,
						EncryptionKey = Convert.ToBase64String(auth.EncryptionKey)
					};
					File.WriteAllText("./config/auth.json", SimpleJson.SerializeObject(upAuth));
				}
				return auth;
			}
			else {
				AuthConfig newAuth = new AuthConfig {
					Username = "<empty>",
					Password = "<empty>",
				};
				File.WriteAllText("./config/auth.json", SimpleJson.SerializeObject(newAuth));
				return AuthKeys(newAuth.Username, newAuth.Password);
			}
		}

		public static Dictionary<string, byte[]> GetKeystore(NaiKeys keys) {
			Dictionary<string, byte[]> store = new Dictionary<string, byte[]>();
			RestClient client = new RestClient("https://api.novelai.net/");
			RestRequest request = new RestRequest("user/keystore");
			request.AddHeader("Content-Type", "application/json");
			request.AddHeader("Authorization", "Bearer " + keys.AccessToken);
			IRestResponse response = client.Get(request);
			if(!response.IsSuccessful) {
				Console.WriteLine("Could not fetch keystore:");
				Console.WriteLine(response.StatusCode);
				Console.WriteLine(response.ErrorException);
				Console.WriteLine(response.ErrorMessage);
				return store;
			}
			Dictionary<string, object> raw = SimpleJson.DeserializeObject<Dictionary<string, object>>(response.Content);
			if(!raw.ContainsKey("keystore")) {
				Console.WriteLine("Keystore was not present");
				return store;
			}
			byte[] bytes = Convert.FromBase64String((string)raw["keystore"]);
			string str = Encoding.Default.GetString(bytes);
			Dictionary<string, object> raw2 = SimpleJson.DeserializeObject<Dictionary<string, object>>(str);
			if(!(raw2.ContainsKey("nonce") && raw2.ContainsKey("sdata"))) {
				Console.WriteLine("nonce or sdata was not present");
				return store;
			}
			object[] nonceo = (object[])raw2["nonce"];
			object[] sdatao = (object[])raw2["sdata"];
						
			byte[] nonce = new byte[nonceo.Length];
			byte[] sdata = new byte[sdatao.Length];
			for(int i = 0; i < nonceo.Length; i++) {
				nonce[i] = Convert.ToByte(nonceo[i]);
			}
			for(int i = 0; i < sdatao.Length; i++) {
				sdata[i] = Convert.ToByte(sdatao[i]);
			}
			Dictionary<string, object> raw3 = SimpleJson.DeserializeObject<Dictionary<string, object>>(Encoding.Default.GetString(Sodium.SecretBox.Open(sdata, nonce, keys.EncryptionKey)));
			if(!raw3.ContainsKey("keys")) {
				Console.WriteLine("Unsealed keystore did not contain any keys");
				return store;
			}
			JsonObject keyJson = (JsonObject)raw3["keys"];
			foreach(string key in keyJson.Keys) {
				JsonArray jsonarr = (JsonArray)keyJson[key];
				byte[] vals = new byte[jsonarr.Count];
				for(int i = 0; i < jsonarr.Count; i++) {
					vals[i] = Convert.ToByte(jsonarr[i]);
				}
				store.Add(key, vals);
			}

			return store;
		}
	}
}
