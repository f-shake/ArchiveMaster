using System;
using ArchiveMaster.Helpers;
using ArchiveMaster.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.IO;

namespace ArchiveMaster.Configs
{
    public partial class SmartDocSearchConfig : ConfigBase
    {
        [ObservableProperty]
        private TextSource source = new TextSource();

        [ObservableProperty]
        private int contextLength = 200;

        [ObservableProperty]
        private bool useRegex = false;

        [ObservableProperty]
        private bool useAiConclude = true;

        [ObservableProperty]
        private List<string> keywords = new List<string>();
        
        [ObservableProperty]
        private int expectedAiConcludeLength = 300;

        [ObservableProperty]
        private string extraAiPrompt;

        public override void Check()
        {
            if (Source.FromFile)
            {
                if (Source.Files.Count == 0)
                {
                    throw new ArgumentException("请添加至少一个文件");
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(Source.Text))
                {
                    throw new ArgumentException("请输入文本");
                }
            }
            
            CheckEmpty(Keywords, "关键词");
        }
    }
}