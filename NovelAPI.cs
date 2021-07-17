using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using static net.novelai.api.Structs;

namespace net.novelai.api {
	public class NovelAPI {
		public NaiKeys keys;
		public RestClient client;
		public gpt_bpe.GPTEncoder encoder;

		public async Task<string> GenerateAsync(string content) {
			NaiGenerateParams defaultParams = NewGenerateParams();
			NaiGenerateResp resp = await GenerateWithParamsAsync(content, defaultParams);
			return resp.Response;
		}

		public async Task<NaiGenerateResp> GenerateWithParamsAsync(string content, NaiGenerateParams parms) {
			ushort[] encoded = encoder.Encode(content);
			byte[] encodedBytes = ToBin(encoded);

			byte[] actual = Convert.FromBase64String("iDxvCAAA");
			ushort[] shorts = FromBin(actual);

			string encodedBytes64 = Convert.ToBase64String(encodedBytes);
			NaiGenerateResp resp = new NaiGenerateResp();
			resp.EncodedRequest = encodedBytes64;
			NaiGenerateMsg msg = NewGenerateMsg(encodedBytes64);
			msg.parameters = parms;
			NaiGenerateHTTPResp apiResp = await NaiApiGenerateAsync(keys, msg);
			byte[] binTokens = Convert.FromBase64String(apiResp.output);
			resp.EncodedResponse = apiResp.output;
			resp.Response = encoder.Decode(FromBin(binTokens));
			return resp;
		}

		public static byte[] ToBin(ushort[] tokens) {
			ReadWriteBuffer buf = new ReadWriteBuffer(tokens.Length * BitConverter.GetBytes(tokens[0]).Length);
			foreach(ushort b in tokens) {
				buf.Write(BitConverter.GetBytes(b));
			}
			return buf.Bytes.ToArray();
		}

		public static ushort[] FromBin (byte[] bytes) {
			ushort[] tokens = new ushort[bytes.Length/2];
			ReadWriteBuffer buf = new ReadWriteBuffer(bytes);
			int i = 0;
			while(buf.Count > 0) {
				ushort token = BitConverter.ToUInt16(buf.Read(2),0);
				tokens[i] = token;
				i++;
			}
			return tokens;
		}

		public static ushort[][] BannedBrackets() {
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

		public static NaiGenerateParams NewGenerateParams() {
			return new NaiGenerateParams {
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
				bad_words_ids = BannedBrackets(),
				BanBrackets = true,
				use_cache = false,
				use_string = false,
				return_full_text = false,
			};
		}

		public static NaiGenerateMsg NewGenerateMsg(string input) {
			NaiGenerateParams parms = NewGenerateParams();
			return new NaiGenerateMsg{
				input = input,
				model = parms.model,
				parameters = parms,
			};
		}

		public static async Task<NaiGenerateHTTPResp> NaiApiGenerateAsync(NaiKeys keys, NaiGenerateMsg parms) {
			parms.model = parms.parameters.model;
			//const oldRange = 1 - 8.0
			//const newRange = 1 - 1.525
			if(parms.model != "2.7B"){
				//parms.Parameters.RepetitionPenalty = ((parms.Parameters.RepetitionPenalty - 1) * newRange) / oldRange + 1;
			}
			if(parms.parameters.BanBrackets){
				//parms.Parameters.BadWordsIds = parms.Parameters.BadWordsIds.Concat(BannedBrackets());
			}

			string json = SimpleJson.SerializeObject(parms);
			//json = "{\"input\":\"iDxvCAAA\",\"model\":\"6B-v3\",\"parameters\":{\"temperature\":0.55,\"max_length\":40,\"min_length\":40,\"top_k\":140,\"top_p\":0.9,\"tail_free_sampling\":1,\"repetition_penalty\":1.1875,\"repetition_penalty_range\":1024,\"repetition_penalty_slope\":6.57,\"bad_words_ids\":[[58],[60],[90],[92],[685],[1391],[1782],[2361],[3693],[4083],[4357],[4895],[5512],[5974],[7131],[8183],[8351],[8762],[8964],[8973],[9063],[11208],[11709],[11907],[11919],[12878],[12962],[13018],[13412],[14631],[14692],[14980],[15090],[15437],[16151],[16410],[16589],[17241],[17414],[17635],[17816],[17912],[18083],[18161],[18477],[19629],[19779],[19953],[20520],[20598],[20662],[20740],[21476],[21737],[22133],[22241],[22345],[22935],[23330],[23785],[23834],[23884],[25295],[25597],[25719],[25787],[25915],[26076],[26358],[26398],[26894],[26933],[27007],[27422],[28013],[29164],[29225],[29342],[29565],[29795],[30072],[30109],[30138],[30866],[31161],[31478],[32092],[32239],[32509],[33116],[33250],[33761],[34171],[34758],[34949],[35944],[36338],[36463],[36563],[36786],[36796],[36937],[37250],[37913],[37981],[38165],[38362],[38381],[38430],[38892],[39850],[39893],[41832],[41888],[42535],[42669],[42785],[42924],[43839],[44438],[44587],[44926],[45144],[45297],[46110],[46570],[46581],[46956],[47175],[47182],[47527],[47715],[48600],[48683],[48688],[48874],[48999],[49074],[49082],[49146],[49946],[10221],[4841],[1427],[2602,834],[29343],[37405],[35780],[2602],[17202],[8162]],\"use_cache\":false,\"use_string\":false,\"return_full_text\":false,\"prefix\":\"vanilla\"}}";
			//SimpleJson.DeserializeObject(json);

			RestClient client = new RestClient("https://api.novelai.net/");
			// client.Authenticator = new HttpBasicAuthenticator(username, password);
			RestRequest request = new RestRequest("ai/generate");
			request.Method = Method.POST;
			request.AddJsonBody(json);
			request.AddHeader("User-Agent", "nrt/0.1 (" + Environment.OSVersion + ")");
			request.AddHeader("Content-Type", "application/json");
			request.AddHeader("Authorization", "Bearer " + keys.AccessToken);
			CancellationToken cancellationToken = new CancellationToken { };
			IRestResponse response = await client.ExecutePostAsync(request);
			if(!response.IsSuccessful) {
				Console.WriteLine("Failed to fetch AI response!");
				Console.WriteLine(response.ErrorMessage);
			}
			Dictionary<string, string> raw = SimpleJson.DeserializeObject<Dictionary<string,string>>(response.Content);
			return new NaiGenerateHTTPResp {
				output = raw["output"],
				StatusCode = 200,
				Error = "",
				Message = ""
			};
		}

		public static NovelAPI NewNovelAiAPI()  {
			try {
				return new NovelAPI {
					keys = Auth.AuthEnv(),
					client = new RestClient(),
					encoder = gpt_bpe.NewEncoder(),
				};
			}
			catch(Exception ex) {
				Console.WriteLine("Error creating NovelAPI");
				Console.WriteLine(ex.ToString());
				return null;
			}
		}
	}
}
