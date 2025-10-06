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
using ArchiveMaster.Helpers;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;

namespace ArchiveMaster.Services
{
    public class SmartDocSearchService(AppConfig appConfig)
        : TwoStepServiceBase<SmartDocSearchConfig>(appConfig)
    {
        public List<TextPartResult> SearchResults { get; private set; }

        public override async Task ExecuteAsync(CancellationToken ct)
        {
            List<string> lines = new List<string>();
            await Task.Run(async () =>
            {
                await foreach (var line in Config.Source.GetPlainTextAsync(ct: ct))
                {
                    List<int> indexes = new List<int>();
                    foreach (var keyword in Config.Keywords)
                    {
                        indexes.AddRange(Config.UseRegex?
                            FindAllIndexesRegex(line, keyword):
                            FindAllIndexes(line, keyword, StringComparison.OrdinalIgnoreCase));
                    }
                    var sortedIndexes = indexes.OrderBy(i => i).ToList();
                    //生成SearchResults
                    //合并相邻
                }
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

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            throw new NotSupportedException();
        }

        public override async Task InitializeAsync(CancellationToken ct)
        {
            throw new NotSupportedException();
        }
    }
}