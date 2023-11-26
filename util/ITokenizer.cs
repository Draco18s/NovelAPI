using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace net.novelai.util
{
	public interface ITokenizer
	{
		ushort[] TrimNewlines(ushort[] tokens, TrimDirection direction, int limit, int min = 0);
		ushort[] TrimSentences(ushort[] tokens, TrimDirection direction, int limit, int min = 0);
		ushort[] Encode(string text);
		string Decode(ushort[] tokens);
	}
}
