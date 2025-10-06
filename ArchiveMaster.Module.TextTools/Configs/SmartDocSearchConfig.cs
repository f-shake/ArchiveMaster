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
        private int contextLength = 1000;
        
        [ObservableProperty]
        private bool useRegex = false;
        
        [ObservableProperty]
        private List<string> keywords = new List<string>();

        public override void Check()
        {
            
        }
    }
}