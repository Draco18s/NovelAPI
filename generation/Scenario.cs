using net.novelai.api;
using net.novelai.util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using static net.novelai.api.Structs;

namespace net.novelai.generation
{
	public class Scenario
	{
		public int ScenarioVersion;
		public string Title;
		public string Author;
		public string Description;
		public string Prompt;
		public string[] Tags;
		public string inputPrefix;
		public ScenarioSettings Settings;
		public ContextEntry[] Context;
		public NaiGenerateParams Parameters;
		public List<LorebookEntry> Lorebook;
		ITokenizer Tokenizer;

		public static Scenario EmptyScenario = FromSpec("", "", "");

		protected Scenario()
		{
			Title = "";
			Author = "";
			Description = "";
			Prompt = "";
			inputPrefix = "";
			Tags = Array.Empty<string>();
			Context = Array.Empty<ContextEntry>();
			Lorebook = new List<LorebookEntry>();
		}

		public List<ContextEntry> ResolveLorebook(List<ContextEntry> contexts)
		{
			List<ContextEntry> entries = new List<ContextEntry>();
			int beginIdx = contexts.Count;
			foreach (LorebookEntry lorebookEntry in Lorebook)
			{
				if (!lorebookEntry.Enabled)
				{
					continue;
				}
				string[] keys = lorebookEntry.Keys;
				Regex[] keysRegex = lorebookEntry.KeysRegex;
				List<Dictionary<string, int[][]>> indexes = new List<Dictionary<string, int[][]>>();
				int searchRange = lorebookEntry.SearchRange;
				for (int keyIdx = 0; keyIdx < keysRegex.Length; keyIdx++)
				{
					Regex keyRegex = keysRegex[keyIdx];
					foreach (ContextEntry contextEntry in contexts)
					{
						string searchText = contextEntry.Text;
						int searchLen = searchText.Length - searchRange;
						if (searchLen > 0)
						{
							searchText = searchText.Substring(searchLen);
						}
						int[][] ctxMatches = keyRegex.FindAllStringIndex(searchText, 0);
						Dictionary<string, int[][]> keyMatches = new Dictionary<string, int[][]>();
						if (searchLen > 0)
						{
							for (int ctxMatchIdx = 0; ctxMatchIdx < ctxMatches.Length; ctxMatchIdx++)
							{
								ctxMatches[ctxMatchIdx][0] = ctxMatches[ctxMatchIdx][0] + searchLen;
								ctxMatches[ctxMatchIdx][1] = ctxMatches[ctxMatchIdx][1] + searchLen;
							}
						}
						if (ctxMatches.Length > 0)
						{
							if (keyMatches.ContainsKey(keys[keyIdx]))
							{
								int[][] existingMatches = keyMatches[keys[keyIdx]];
								int array1OriginalLength = existingMatches.Length;
								Array.Resize(ref existingMatches, array1OriginalLength + ctxMatches.Length);
								Array.Copy(ctxMatches, 0, existingMatches, array1OriginalLength, ctxMatches.Length);
								keyMatches[keys[keyIdx]] = existingMatches;
							}
							else
							{
								keyMatches.Add(keys[keyIdx], ctxMatches);
							}
						}
						if (keyMatches.Count > 0)
						{
							indexes.Add(keyMatches);
						}
					}
				}
				if (indexes.Count > 0 || lorebookEntry.ForceActivation)
				{
					entries.Add(new ContextEntry
					{
						Text = lorebookEntry.Text,
						ContextCfg = lorebookEntry.ContextCfg,
						Tokens = lorebookEntry.Tokens,
						Label = lorebookEntry.DisplayName,
						MatchIndexes = indexes.ToArray(),
						Index = (uint)(beginIdx + Lorebook.IndexOf(lorebookEntry)),
					});
				}
			}
			return entries;
		}

		/**
		Create context list
		Add story to context list
		Add story context array to list
		Add active lorebook entries to the context list
		Add active ephemeral entries to the context list
		Add cascading lorebook entries to the context list
		Determine token lengths of each entry
		Determine reserved tokens for each entry
		Sort context list by insertion order
		For each entry in the context list
			trim entry
			insert entry
			reduce reserved tokens
		**/
		public ContextEntry createStoryContext(string story)
		{
			return new ContextEntry
			{
				Text = story,
				ContextCfg = new ContextConfig
				{
					Prefix = "",
					Suffix = "",
					ReservedTokens = 512,
					InsertionPosition = -1,
					TokenBudget = 2048,
					BudgetPriority = 0,
					TrimDirection = TrimDirection.TOP,
					InsertionType = InsertionType.NEWLINE,
					MaximumTrimType = MaxTrimType.SENTENCES,
				},
				Tokens = Tokenizer.Encode(story),
				Label = "Story",
				Index = 0,
			};
		}

		public List<ContextEntry> getReservedContexts(List<ContextEntry> ctxts)
		{
			List<ContextEntry> reserved = new List<ContextEntry>();
			foreach (ContextEntry ctx in ctxts)
			{
				if (ctx.ContextCfg.ReservedTokens > 0)
				{
					reserved.Add(ctx);
				}
			}
			reserved.Sort((x, y) => x.ContextCfg.BudgetPriority.CompareTo(y.ContextCfg.BudgetPriority));
			return reserved;
		}

		public string GenerateContext(string story, int budget)
		{
			ContextEntry storyEntry = createStoryContext(story);
			List<ContextEntry> contexts = new List<ContextEntry>();
			contexts.Add(storyEntry);
			List<ContextEntry> lorebookContexts = ResolveLorebook(contexts);
			contexts.AddRange(Context);
			contexts.AddRange(lorebookContexts);
			budget -= (int)Parameters.max_length;
			int reservations = 0;
			List<ContextEntry> reservedContexts = getReservedContexts(contexts);
			foreach (ContextEntry ctx in reservedContexts)
			{
				int amt = Math.Min(ctx.ContextCfg.ReservedTokens, ctx.Tokens.Length);
				budget -= amt;
				reservations += amt;
			}
			contexts.Sort((x, y) => x.ContextCfg.BudgetPriority.CompareTo(y.ContextCfg.BudgetPriority));
			List<string> newContexts = new List<string>();
			if (Parameters.prefix != "vanilla")
			{
				budget -= 20;
			}
			foreach (ContextEntry ctx in contexts)
			{
				int reserved = ctx.ContextCfg.ReservedTokens > 0 ? Math.Min(ctx.ContextCfg.ReservedTokens, ctx.Tokens.Length) : ctx.Tokens.Length;
				ushort[] trimmedTokens = ctx.ResolveTrim(Tokenizer, budget + reserved);
				budget -= trimmedTokens.Length - reserved;
				reservations -= reserved;
				string[] contextText = Tokenizer.Decode(trimmedTokens).Split('\n');
				int ctxInsertion = ctx.ContextCfg.InsertionPosition;
				string[] before;
				string[] after;
				if (ctxInsertion < 0)
				{
					ctxInsertion += 1;
					if (newContexts.Count + ctxInsertion >= 0)
					{
						before = new string[newContexts.Count + ctxInsertion];
						Array.Copy(newContexts.ToArray(), 0, before, 0, newContexts.Count + ctxInsertion);
						//before = newContexts[0 : newContexts.Count + ctxInsertion];
						after = new string[Math.Abs(ctxInsertion)];
						Array.Copy(newContexts.ToArray(), newContexts.Count + ctxInsertion, after, 0, Math.Abs(ctxInsertion));
						//after = newContexts[newContexts.Count + ctxInsertion:];
					}
					else
					{
						before = new string[0];
						after = newContexts.ToArray();
					}
				}
				else
				{
					before = new string[ctxInsertion];
					Array.Copy(newContexts.ToArray(), 0, before, 0, ctxInsertion);
					//before = newContexts[0:ctxInsertion];
					after = new string[newContexts.Count - ctxInsertion];
					Array.Copy(newContexts.ToArray(), ctxInsertion, after, 0, newContexts.Count - ctxInsertion);
					//after = newContexts[ctxInsertion:];
				}
				newContexts.Clear();
				newContexts.AddRange(before);
				newContexts.AddRange(contextText);
				newContexts.AddRange(after);
			}
			return string.Join("\n", newContexts).Trim();
		}

		public string TrimResponse(string response)
		{
			if (!Settings.TrimResponses || Settings.TrimType == OutputTrimType.NONE)
				return response;
			List<string> sentences = gpt_bpe.GPTEncoder.SplitIntoSentences(response.Trim());
			string last = sentences.Last();
			int len = 0;
			if (Settings.TrimType == OutputTrimType.FIRST_LINE)
			{
				List<string> snip = sentences.TakeWhile(sen => {
					len++;
					return !sen.Contains("\n");// || len < 50;
				}).ToList();
				snip.AddRange(sentences.SkipWhile(sen => !sen.Contains("\n")).Take(1).ToList());
				return string.Join(" ", snip);
			}
			if (last.Length - last.LastIndexOfAny(new char[] { '.', '!', '?', '"' }) > 2)
			{
				sentences.Remove(last);
			}
			return string.Join(" ", sentences);
		}

		public static Scenario FromDatabase(string prompt, string memory, string authorsNote, string prefix, OutputTrimType trimType, NaiGenerateParams param, List<LorebookEntry> lore)
		{
			gpt_bpe.GPTEncoder encoder = gpt_bpe.NewEncoder();
			return new Scenario
			{
				Tokenizer = encoder,
				Prompt = prompt,
				Parameters = param,
				Lorebook = lore,
				inputPrefix = prefix,
				Settings = new ScenarioSettings
				{
					TrimResponses = true,
					TrimType = trimType,
				},
				Context = new ContextEntry[] {
					new ContextEntry {
						Text = memory,
						ContextCfg = new ContextConfig{
							Prefix = "",
							Suffix = "\n",
							TokenBudget = 2048,
							ReservedTokens = 0,
							BudgetPriority = 800,
							TrimDirection = TrimDirection.BOTTOM,
							InsertionType = InsertionType.NEWLINE,
							InsertionPosition = 0,
						},
						Tokens = encoder.Encode(memory),
						Label = "Memory",
						Index = 1
					},
					new ContextEntry {
						Text = authorsNote,
						ContextCfg = new ContextConfig{
							Prefix = "",
							Suffix = "\n",
							TokenBudget = 2048,
							ReservedTokens = 2048,
							BudgetPriority = -400,
							TrimDirection = TrimDirection.BOTTOM,
							InsertionType = InsertionType.NEWLINE,
							InsertionPosition = -4,
						},
						Tokens = encoder.Encode(authorsNote),
						Label = "A/N",
						Index = 2
					}
				}
			};
		}

		public static Scenario FromSpec(string prompt, string memory, string authorsNote)
		{
			gpt_bpe.GPTEncoder encoder = gpt_bpe.NewEncoder();
			return new Scenario
			{
				Tokenizer = encoder,
				Prompt = prompt,
				Parameters = NovelAPI.defaultParams,
				Settings = new ScenarioSettings
				{
					TrimResponses = true,
				},
				Context = new ContextEntry[] {
					new ContextEntry {
						Text = memory,
						ContextCfg = new ContextConfig {
							Prefix = "",
							Suffix = "\n",
							TokenBudget = 2048,
							ReservedTokens = 0,
							BudgetPriority = 800,
							TrimDirection = TrimDirection.BOTTOM,
							InsertionType = InsertionType.NEWLINE,
							InsertionPosition = 0,
						},
						Tokens = encoder.Encode(memory),
						Label = "Memory",
						Index = 1
					},
					new ContextEntry {
						Text = authorsNote,
						ContextCfg = new ContextConfig {
							Prefix = "",
							Suffix = "\n",
							TokenBudget = 2048,
							ReservedTokens = 2048,
							BudgetPriority = -400,
							TrimDirection = TrimDirection.BOTTOM,
							InsertionType = InsertionType.NEWLINE,
							InsertionPosition = -4,
						},
						Tokens = encoder.Encode(authorsNote),
						Label = "A/N",
						Index = 2
					}
				},
				Lorebook = new List<LorebookEntry>()
			};
		}
	}
}
