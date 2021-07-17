using Konscious.Security.Cryptography;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using static net.novelai.api.Structs;

namespace net.novelai.api {
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
				Dictionary<string, string> resp_decoded = SimpleJson.DeserializeObject<Dictionary<string, string>>(response.Content);
				return resp_decoded["accessToken"];
			}
			else {
				Console.WriteLine(response.StatusCode);
				Console.WriteLine(response.ErrorException);
				Console.WriteLine(response.ErrorMessage);
				return string.Empty;
			}
		}

		private static byte[] CreateSalt() {
			var buffer = new byte[16];
			var rng = new RNGCryptoServiceProvider();
			rng.GetBytes(buffer);
			return buffer;
		}

		public static byte[] NaiHashArgon(int size, string plaintext, string secret, string domain) {
			var argon2 = new Argon2id(Encoding.UTF8.GetBytes(plaintext));
			argon2.Salt = CreateSalt();
			argon2.DegreeOfParallelism = 8;
			argon2.Iterations = 4;
			argon2.MemorySize = 1024 * 1024;

			return argon2.GetBytes(16);
		}

		public static NaiKeys NaiGenerateKeys(string email, string password) {
			string pw_email_secret = password.Substring(0,6) + email;
			byte[] encryption_key = NaiHashArgon(128,
				password,
				pw_email_secret,
				"novelai_data_encryption_key");
			byte[] access_key = NaiHashArgon(64,
				password,
				pw_email_secret,
				"novelai_data_access_key");

			string asccess_string = Convert.ToBase64String(access_key);
			if(asccess_string.Length > 64)
				asccess_string = asccess_string.Substring(0, 64);
			asccess_string = asccess_string.Replace("/", "_");
			asccess_string = asccess_string.Replace("+", "-");
			return new NaiKeys {
				EncryptionKey = encryption_key,
				AccessKey = asccess_string,
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
				//
			}
			return keys;
		}

		public static NaiKeys AuthEnv() {
			if(!Directory.Exists("./config")) {
				Directory.CreateDirectory("./config");
			}
			if(File.Exists("./config/auth.json")) {
				string json = File.ReadAllText("./config/auth.json");
				Dictionary<string, string> authCfg = SimpleJson.DeserializeObject<Dictionary<string,string>>(json);
				NaiKeys auth = AuthKeys(authCfg["Username"], authCfg["Password"]);
				if(auth.AccessToken.Length == 0) {
					Console.WriteLine("auth: failed to obtain AccessToken!");
				}
				return auth;
			}
			else {
				AuthConfig newAuth = new AuthConfig {
					Username = "<empty>",
					Password = "<empty>"
				};
				File.WriteAllText("./config/auth.json", SimpleJson.SerializeObject(newAuth));
				return AuthKeys(newAuth.Username, newAuth.Password);
			}
		}
	}
}
