using System;
using ArchiveMaster.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.IO;

namespace ArchiveMaster.Configs
{
    public partial class PhotoTagGeneratorConfig : ConfigBase
    {
        [ObservableProperty]
        private int descriptionLength = 30;

        [ObservableProperty]
        private string dir;

        [ObservableProperty]
        private FileFilterRule filter = FileHelper.NoRawImageFileFilterRule;

        [ObservableProperty]
        private int resizingTargetResolutionIn10k = 800;

        [ObservableProperty]
        private string tagFile;

        [ObservableProperty]
        private int targetTagCount = 5;

        /// <summary>
        /// 最大并行数量
        /// </summary>
        [ObservableProperty]
        private int maxDegreeOfParallelism = 1;

        /// <summary>
        /// 自动保存间隔（张图片）
        /// </summary>
        [ObservableProperty]
        private int autoSaveInterval = 10;

        /// <summary>
        /// 错误重试次数
        /// </summary>
        /// <returns></returns>
        [ObservableProperty]
        private int retryCount = 3;

        public override void Check()
        {
            CheckDir(Dir, "目录");
            CheckEmpty(TagFile, "标签文件");
            CheckRange(MaxDegreeOfParallelism, 1, 8, "最大并行数量");
            CheckRange(AutoSaveInterval, 0, 100, "自动保存间隔");
            CheckRange(RetryCount, 0, 10, "错误重试次数");
        }
    }
}