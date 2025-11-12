using FzLib;
using ArchiveMaster.Configs;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.Events;
using ArchiveMaster.Helpers;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;
using Markdig;

namespace ArchiveMaster.Services
{
    public class SmartDocSearchService(AppConfig appConfig)
        : AiTwoStepServiceBase<SmartDocSearchConfig>(appConfig)
    {
        public event GenericEventHandler<LlmOutputItem> AitStreamUpdate;

        public string AiConclude { get; private set; }

        public List<TextSearchResult> SearchResults { get; private set; }

        public static List<T> RandomSelect<T>(List<T> source, int m)
        {
            if (m > source.Count)
            {
                return source;
            }

            var indices = Enumerable.Range(0, source.Count).ToArray();
            Random.Shared.Shuffle(indices);
            var selected = indices.Take(m).OrderBy(i => i);
            return selected.Select(i => source[i]).ToList();
        }

        public override async Task ExecuteAsync(CancellationToken ct)
        {
            await Task.Run(async () =>
            {
                NotifyMessage("正在调用AI进行归纳总结");
                AiConclude = await GetAiConcludeAsync(ct);
            }, ct);
        }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            return null;
        }

        public override async Task InitializeAsync(CancellationToken ct)
        {
            await Task.Run(async () =>
            {
                NotifyMessage("正在文档中搜索关键词");
                SearchResults = await GetSearchResultAsync(ct);
            }, ct);
        }

        private static List<int> FindAllIndexes(string source, string keyword, StringComparison comparison)
        {
            var indexes = new List<int>();
            int index = 0;

            while ((index = source.IndexOf(keyword, index, comparison)) != -1)
            {
                indexes.Add(index);
                index += keyword.Length; // 避免死循环，跳到关键字之后
            }

            return indexes;
        }

        private static List<int> FindAllIndexesRegex(string source, string pattern)
        {
            var indexes = new List<int>();
            foreach (Match match in Regex.Matches(source, pattern))
            {
                indexes.Add(match.Index);
            }

            return indexes;
        }

        private async Task<string> GetAiConcludeAsync(CancellationToken ct)
        {
            LlmCallerService s = new LlmCallerService(AI);
            string sys = $"""
                          你是一个归纳总结机器人。当前，用户以“{string.Join(" ", Config.Keywords.Trimmed)}”为关键词，对一些文段进行了搜索，得到了一系列的结果，这些结果将在下面给出。
                          你需要根据这些结果，进行归纳总结。期望输出长度（字数）：{Config.ExpectedAiConcludeLength}，请严格遵守输出字数要求。
                          回复的时候，你只需要回复结果，不要参杂其他内容。不要使用MarkDown，使用纯文本进行回复，在必要时，可以使用编号或者列表，但需要以纯文本的形式提供。
                          {(string.IsNullOrWhiteSpace(Config.ExtraAiPrompt) ? "" : "用户的额外要求，你需要尽可能满足，除非与上文冲突：" + Config.ExtraAiPrompt)}
                          """;
            var prompt = new StringBuilder();
            foreach (var (item, index) in RandomSelect(SearchResults, Config.AiConcludeMaxCount)
                         .Select((item, index) => (item, index)))
            {
                prompt.Append('第')
                    .Append(index + 1)
                    .Append("条搜索结果");
                if (item.Source != null)
                {
                    prompt.Append('(')
                        .Append("来源：")
                        .Append(Path.GetFileName(item.Source))
                        .Append(')');
                }

                prompt.Append('：')
                    .AppendLine();

                prompt.AppendLine(item.Context);
            }

            List<string> result = new List<string>();
            await foreach (var part in s.CallStreamAsync(sys, prompt.ToString(), null, ct))
            {
                AitStreamUpdate?.Invoke(this, new GenericEventArgs<LlmOutputItem>(part));
                result.Add(part);
            }

            var removeThink = LlmCallerService.RemoveThink(string.Concat(result));
            string plainText = Markdown.ToPlainText(removeThink);
            return plainText;
        }

        private async Task<List<TextSearchResult>> GetSearchResultAsync(CancellationToken ct)
        {
            var results = new List<TextSearchResult>();
            await foreach (var docFileLine in Config.Source.GetPlainTextAsync(ct: ct))
            {
                List<(string keyword, int index)> indexes = new List<(string, int)>();

                //对每个关键词进行搜索
                foreach (var keyword in Config.Keywords)
                {
                    var tempIndexes = Config.UseRegex
                        ? FindAllIndexesRegex(docFileLine.Text, keyword)
                        : FindAllIndexes(docFileLine.Text, keyword, StringComparison.OrdinalIgnoreCase);
                    indexes.AddRange(tempIndexes.Select(p => (keyword.Value, p)));
                }

                //对这个段落的搜索结果，按先后顺序进行排序
                var tempResults = indexes
                    .OrderBy(p => p.index)
                    .Select(p => new TextSearchResult()
                    {
                        Source = docFileLine.Source,
                        Keywords = [p.keyword],
                        Indexes = [p.index],
                        ContextStartIndex = p.index - Config.ContextLength / 2,
                        ContextEndIndex = p.index + Config.ContextLength / 2,
                        SourceParagraph = docFileLine.Text
                    })
                    .ToList();

                //合并交叉的搜索结果
                var paraResults = new List<TextSearchResult>();
                foreach (var current in tempResults)
                {
                    if (paraResults.Count == 0)
                    {
                        paraResults.Add(current);
                        continue;
                    }

                    var lastResult = paraResults[^1];
                    if (lastResult.ContextEndIndex > current.ContextStartIndex)
                    {
                        //合并
                        lastResult.Keywords.Add(current.Keywords.Single()); //一定只有一个
                        lastResult.Indexes.Add(current.Indexes.Single()); //同样只有一个
                        lastResult.ContextEndIndex = current.ContextEndIndex;
                    }
                    else
                    {
                        paraResults.Add(current);
                    }
                }

                results.AddRange(paraResults);
            }

            foreach (var searchResult in results)
            {
                searchResult.GenerateContext();
            }

            return results;
        }
    }
}