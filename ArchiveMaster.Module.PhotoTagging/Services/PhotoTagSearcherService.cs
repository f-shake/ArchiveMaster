using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Helpers;
using ArchiveMaster.ViewModels.FileSystem;

namespace ArchiveMaster.Services
{
    public class PhotoTagSearcherService(AppConfig appConfig)
        : TwoStepServiceBase<PhotoTagSearcherConfig>(appConfig)
    {
        public List<TaggingPhotoFileInfo> AllFiles { get; private set; }

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
        }

        public async Task<List<TaggingPhotoFileInfo>> SearchAsync(TagType type, string keyword, bool partial)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return [];
            }

            return await Task.Run(() =>
            {
                return partial
                    ? AllFiles.Where(p => p.Tags.Matches(keyword, type)).ToList()
                    : AllFiles.Where(p => p.Tags.Contains(keyword, type)).ToList();
            });
        }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles() => [];

        public override Task InitializeAsync(CancellationToken ct = default) => throw new InvalidOperationException();
    }
}