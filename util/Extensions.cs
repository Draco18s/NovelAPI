using System.Text.RegularExpressions;

namespace net.novelai.util
{
	public static class Extentions
	{
		public static int[][] FindAllStringIndex(this Regex pattern, string txt, int start)
		{
			if (start < 0 || start >= txt.Length) return new int[0][];
			MatchCollection coll = pattern.Matches(txt, start);
			int[][] list = new int[coll.Count][];
			int i = 0;
			foreach (Match match in coll)
			{
				list[i] = new int[] { match.Index, match.Length };
				i++;
			}
			return list;
		}
	}
}