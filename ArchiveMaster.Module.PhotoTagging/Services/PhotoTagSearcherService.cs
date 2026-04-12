using System.Collections.Frozen;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Helpers;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;

namespace ArchiveMaster.Services
{
    public class PhotoTagSearcherService(AppConfig appConfig)
        : TwoStepServiceBase<PhotoTagSearcherConfig>(appConfig)
    {
        public List<TaggingPhotoFileInfo> AllFiles { get; private set; }

        public List<TagAndCount> AllTags { get; private set; }
        public List<TagAndCount> ColorTags { get; private set; }
        public List<TagAndCount> MoodTags { get; private set; }
        public List<TagAndCount> ObjectTags { get; private set; }
        public List<TagAndCount> SceneTags { get; private set; } 
        public List<TagAndCount> TechniqueTags { get; private set; }

        public override async Task ExecuteAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(Config.TagFile))
            {
                throw new Exception("标签文件未指定");
            }

            if (!File.Exists(Config.TagFile))
            {
                throw new FileNotFoundException($"标签文件不存在: {Config.TagFile}");
            }

            NotifyMessage("正在读取标签文件");
            AllFiles = await TagFileHelper.ReadPhotoTaggingFileInfosAsync(Config.TagFile, Config.RootDir, false, ct);

            void UpdateCounts(IEnumerable<string> tags, Dictionary<string, int> targetDict)
            {
                foreach (var tag in tags)
                {
                    if (!targetDict.TryAdd(tag, 1))
                    {
                        targetDict[tag]++;
                    }
                }
            }
            Dictionary<string, int> allTags = new();
            Dictionary<string, int> objectTags = new();
            Dictionary<string, int> sceneTags = new();
            Dictionary<string, int> moodTags = new();
            Dictionary<string, int> colorTags = new();
            Dictionary<string, int> techniqueTags = new();
            foreach (var file in AllFiles)
            {
                UpdateCounts(file.Tags.GetAllTags(), allTags);
                UpdateCounts(file.Tags.ObjectTags, objectTags);
                UpdateCounts(file.Tags.SceneTags, sceneTags);
                UpdateCounts(file.Tags.MoodTags, moodTags);
                UpdateCounts(file.Tags.ColorTags, colorTags);
                UpdateCounts(file.Tags.TechniqueTags, techniqueTags);
            }
            
            List<TagAndCount> ToSortedList(Dictionary<string, int> dict)
            {
                return dict
                    .OrderByDescending(p => p.Value)
                    .Select(p => new TagAndCount { Tag = p.Key, Count = p.Value })
                    .ToList();
            }

            AllTags = ToSortedList(allTags);
            ObjectTags = ToSortedList(objectTags);
            SceneTags = ToSortedList(sceneTags);
            MoodTags = ToSortedList(moodTags);
            ColorTags = ToSortedList(colorTags);
            TechniqueTags = ToSortedList(techniqueTags);
        }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles() => [];

        public override Task InitializeAsync(CancellationToken ct = default) => throw new InvalidOperationException();

        public async Task<List<TaggingPhotoFileInfo>> SearchAsync(TagType type, string keyword, bool partial)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return [];
            }

            var keywords = keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (keywords.Length == 0)
            {
                 return [];
            }

            return await Task.Run(() =>
            {
                return AllFiles.Where(p => 
                    keywords.All(k => partial 
                        ? p.Tags.Matches(k, type) 
                        : p.Tags.ContainsTag(k, type))
                ).ToList();
            });
        }
    }
}