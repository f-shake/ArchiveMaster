using System;
using System.Collections.ObjectModel;
using System.Text;
using ArchiveMaster.Helpers;
using ArchiveMaster.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.IO;
using FzLib.Text;

namespace ArchiveMaster.Configs
{
    public partial class SmartDocSearchConfig : ConfigBase
    {
        [ObservableProperty]
        private int aiConcludeMaxCount = 100;

        [ObservableProperty]
        private int contextLength = 200;

        [ObservableProperty]
        private int expectedAiConcludeLength = 300;

        [ObservableProperty]
        private string extraAiPrompt;

        [ObservableProperty]
        private ObservableStringList keywords = new ObservableStringList();

        [ObservableProperty]
        private TextSource source = new TextSource();

        [ObservableProperty]
        private bool useRegex = false;

        public override void Check()
        {
            if (Source.IsEmpty())
            {
                throw new ArgumentException("文本源为空");
            }

            if (!Keywords.Trimmed.Any())
            {
                throw new ArgumentException("请添加至少一个关键词");
            }
        }
    }
}