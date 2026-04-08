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
    public class PhotoTagManagerService(AppConfig appConfig)
        : TwoStepServiceBase<PhotoTagManagerConfig>(appConfig)
    {
        public TreeDirInfo Tree { get; private set; }

        public override async Task ExecuteAsync(CancellationToken ct = default)
        {
            NotifyMessage("正在读取标签文件");
            var tags = await TagFileHelper.GetPhotoTagCollectionAsync(Config.TagFile, ct);
            var relativePathToTags = tags.Photos.ToDictionary(p => p.RelativePath, p => p.Tags);

            NotifyMessage("正在枚举目录文件");
            var tree = await TreeDirInfo.BuildTreeAsync(Config.RootDir, Config.Filter, ct);

            NotifyMessage("正在匹配标签");
            foreach (var file in tree.Flatten())
            {
                if (relativePathToTags.TryGetValue(file.RelativePath, out PhotoTags value))
                {
                    file.Tag = value;
                }
            }

            Tree = tree;
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