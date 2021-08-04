using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Konscious.Security.Cryptography;
using RestSharp;
using static net.novelai.api.Structs;

namespace net.novelai.generation {
	public struct AIModule {
		//this is silly
		public static readonly string[] defaultModules = new string[]{
			"vanilla",
			"theme_19thcenturyromance",
			"theme_actionarcheology",
			"theme_airships",
			"theme_ai",
			"theme_darkfantasy",
			"theme_dragons",
			"theme_egypt",
			"theme_generalfantasy",
			"theme_huntergatherer",
			"theme_magicacademy",
			"theme_libraries",
			"theme_mars",
			"theme_medieval",
			"theme_militaryscifi",
			"theme_naval",
			"theme_pirates",
			"theme_postapocalyptic",
			"theme_rats",
			"theme_romanceofthreekingdoms",
			"theme_superheroes",
			"style_arthurconandoyle",
			"style_edgarallanpoe",
			"style_hplovecraft",
			"style_shridanlefanu",
			"style_julesverne",
			"inspiration_crabsnailandmonkey",
			"inspiration_mercantilewolfgirlromance",
			"inspiration_nervegear",
			"inspiration_thronewars",
			"inspiration_witchatlevelcap",
		};
		public uint Version;
		public string EncodedData;
		public byte[] EncryptedData;
		public string PrefixID;
		public string Hash;
		public string Name;
		public string Description;
		public string Model;
		public uint Steps;

		public string ToPrefix() {
			return string.Format("{0}:{1}:{2}", Model, PrefixID, Hash);
		}

		public static AIModule AIModuleFromArgs(string id, string name, string description)  {
			string[] idSplit = id.Split(':');
			return new AIModule {
				Model = idSplit[0],
				PrefixID = idSplit[1],
				Hash = idSplit[2],
				Name = name,
				Description = description,
			};
		}

		public static AIModule Unpack(JsonObject json, NaiKeys keys) {
			json.TryGetValue("data", out object v);
			string dat = (string)v;
			json.TryGetValue("data", out v);
			string meta = (string)v;
			byte[] bytes = Convert.FromBase64String(dat);
			byte[] nonce = bytes.Take(24).ToArray();
			byte[] sdata = bytes.Skip(24).ToArray();

			byte[] unsealed = Sodium.SecretBox.Open(sdata, nonce, keys.keystore[meta]);

			string json2 = Encoding.Default.GetString(unsealed);
			Dictionary<string, object> raw3 = SimpleJson.DeserializeObject<Dictionary<string, object>>(json2);
			/*if(raw3.ContainsKey("keys")) {

			}*/

			throw new NotImplementedException();
		}
	}
}
