using net.novelai.api;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static net.novelai.api.Structs;

namespace net.novelai.api {
	public class Scenario {
		public int ScenarioVersion;
		public string Title;
		public string Author;
		public string Description;
		public string Prompt;
		public string[] Tags;
		//public ContextEntry[] Context;
		public NaiGenerateParams Parameters;
		public List<LorebookEntry> Lorebook;
		gpt_bpe.GPTEncoder Tokenizer;

		public List<ContextEntry> ResolveLorebook(List<ContextEntry> contexts) {
			return contexts;
		}
	}
}
