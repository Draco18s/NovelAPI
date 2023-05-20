using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Konscious.Security.Cryptography;
using RestSharp;
using static net.novelai.api.Structs;

namespace net.novelai.generation
{
	public struct AIModule
	{
		//this is silly
		public static readonly string[] themeModules = new string[]{
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
			"theme_textadventure",
		};
		public static readonly string[] styleModules = new string[]{
			"style_arthurconandoyle",
			"style_edgarallanpoe",
			"style_hplovecraft",
			"style_shridanlefanu",
			"style_julesverne",
		};
		public static readonly string[] inspireModules = new string[]{
			"inspiration_crabsnailandmonkey",
			"inspiration_mercantilewolfgirlromance",
			"inspiration_nervegear",
			"inspiration_thronewars",
			"inspiration_witchatlevelcap",
		};
		public static readonly string[] defaultModules = new string[] { "vanilla" }.Concat(themeModules).Concat(styleModules).Concat(inspireModules).ToArray();
		public uint Version;
		public string EncodedData;
		public byte[] EncryptedData;
		public string PrefixID;
		public string Hash;
		public string Name;
		public string Description;
		public string Model;
		public uint Steps;

		public string ToPrefix()
		{
			return string.Format("{0}:{1}:{2}", Model, PrefixID, Hash);
		}

		public override bool Equals(object? obj)
		{
			if (obj is AIModule other)
			{
				return this.PrefixID == other.PrefixID && this.Version == other.Version;
			}
			return base.Equals(obj);
		}

		public override int GetHashCode()
		{
			return (PrefixID + ":" + Version).GetHashCode();
		}

		public static AIModule AIModuleFromArgs(string id, string name, string description)
		{
			string[] idSplit = id.Split(':');
			return new AIModule
			{
				Model = idSplit[0],
				PrefixID = idSplit[1],
				Hash = idSplit[2],
				Name = name,
				Description = description,
			};
		}

		public static AIModule Unpack(JsonObject json, NaiKeys keys)
		{
			JsonNode? v;
			if(!json.TryGetPropertyValue("data", out v) || v == null) throw new Exception("AIModule parser failure");
			string dat = v.ToJsonString();
			if (!json.TryGetPropertyValue("meta", out v) || v == null) throw new Exception("AIModule parser failure");
			string meta = v.ToJsonString();
			byte[] bytes = Convert.FromBase64String(dat);
			byte[] nonce = bytes.Take(24).ToArray();
			byte[] sdata = bytes.Skip(24).ToArray();

			byte[] unsealed = Sodium.SecretBox.Open(sdata, nonce, keys.keystore[meta]);

			string json2 = Encoding.Default.GetString(unsealed);
			Dictionary<string, object> raw3 = JsonSerializer.Deserialize<Dictionary<string, object>>(json2) ?? throw new Exception("AIModule parser failure");
			string id = (string)raw3["id"];
			string name = (string)raw3["name"];
			string description = (string)raw3["description"];
			string remoteId = (string)raw3["remoteId"];

			return AIModuleFromArgs(id, name, description);
		}
	}
}
