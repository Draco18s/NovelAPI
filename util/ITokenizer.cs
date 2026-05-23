using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace net.novelai.util
{
	public interface ITokenizer
	{
		uint[] TrimNewlines(uint[] tokens, TrimDirection direction, int limit, int min = 0);
		uint[] TrimSentences(uint[] tokens, TrimDirection direction, int limit, int min = 0);
		uint[] Encode(string text);
		string Decode(uint[] tokens);
	}
}
