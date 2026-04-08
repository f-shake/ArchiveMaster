using FzLib;
using ArchiveMaster.Configs;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ArchiveMaster.Enums;
using ArchiveMaster.Helpers;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;
using ImageMagick;

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
            AllFiles = await TagFileHelper.GetPhotoTaggingFileInfosAsync(Config.TagFile, Config.RootDir, ct);
        }

        public Task<List<TaggingPhotoFileInfo>> SearchAsync(TagType type, string keyword, bool partial)
        {
            return Task.Run(() =>
            {
                return partial
                    ? AllFiles.Where(p => p.Tags.Matches(keyword,type)).ToList()
                    : AllFiles.Where(p => p.Tags.Contains(keyword,type)).ToList();
            });
        }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            return [];
        }

        public override Task InitializeAsync(CancellationToken ct = default)
        {
            throw new InvalidOperationException();
        }
    }
}