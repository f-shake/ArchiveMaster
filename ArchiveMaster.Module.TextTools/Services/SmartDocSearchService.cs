using FzLib;
using ArchiveMaster.Configs;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.Events;
using ArchiveMaster.Helpers;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;
using Markdig;

namespace ArchiveMaster.Services
{
    public class SmartDocSearchService(AppConfig appConfig)
        : TwoStepServiceBase<SmartDocSearchConfig>(appConfig)
    {
        public List<TextSearchResult> SearchResults { get; private set; }
        public string AiConclude { get; private set; }

        public override async Task ExecuteAsync(CancellationToken ct)
        {
            List<string> lines = new List<string>();
            await Task.Run(async () =>
            {
                NotifyMessage("正在文档中搜索关键词");
                NotifyProgress(0.1);
                SearchResults = await GetSearchResultAsync(ct);

                NotifyMessage("正在调用AI进行归纳总结");
                NotifyProgress(0.5);
                AiConclude = await GetAiConcludeAsync(ct);
            }, ct);
        }

        private async Task<string> GetAiConcludeAsync(CancellationToken ct)
        {
            AiProviderConfig c = new AiProviderConfig()
            {
                Type = AiProviderConfig.ProviderType.OpenAI,
                Key = "sk-dc566c4f94ff44c08f47c00fbcf6ce84",
                Url = "https://api.deepseek.com",
                Model = "deepseek-chat",
            };
            LlmCallerService s = new LlmCallerService(c);
            string sys = """
                         你是一个归纳总结机器人，用户对一些文段进行了搜索，得到了一系列的结果。
                         你需要根据这些结果，进行归纳总结。
                         """;
            string prompt = $"""
                             搜索关键词：{string.Join(" ", Config.Keywords)}
                             搜索结果：{string.Join("\n", SearchResults.Select(p => p.Context))}
                             期望输出长度（字数）：{Config.ExpectedAiConcludeLength}
                             额外要求：{(string.IsNullOrWhiteSpace(Config.ExtraAiPrompt) ? "无" : Config.ExtraAiPrompt)}
                             """;
            List<string> result = new List<string>();
            await foreach (var part in s.CallStreamAsync(sys, prompt).WithCancellation(ct))
            {
                AitStreamUpdate?.Invoke(this, new ChatStreamUpdateEventArgs(part));
                result.Add(part);
            }

            return Markdown.ToPlainText(string.Concat(result));
        }

        public event EventHandler<ChatStreamUpdateEventArgs> AitStreamUpdate;

        public event EventHandler<SearchUpdateEventArgs> SearchResultsUpdate;

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
                    indexes.AddRange(tempIndexes.Select(p => (keyword, p)));
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
                SearchResultsUpdate?.Invoke(this, new SearchUpdateEventArgs(searchResult));
            }

            return results;
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

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            return [];
        }

        public override async Task InitializeAsync(CancellationToken ct)
        {
            throw new NotSupportedException();
        }
    }
}