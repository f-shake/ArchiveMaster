﻿using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.Configs
{
    public partial class TimeClassifyConfig: ConfigBase
    {
        [ObservableProperty]
        private string dir;

        [ObservableProperty]
        private TimeSpan minTimeInterval = TimeSpan.FromMinutes(60);
        
        public override void Check()
        {
            CheckDir(Dir,"目录");
        }
    }
}
