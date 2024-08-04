﻿using System.Reflection;
using ArchiveMaster.Enums;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;
using ArchiveMaster.Utilities;

namespace ArchiveMaster.Views
{
    /// <summary>
    /// RebuildPanel.xaml 的交互逻辑
    /// </summary>
    public partial class Step2Panel : OfflineSyncPanelBase
    {
        public Step2Panel()
        {
            ViewModel = new Step2ViewModel();
            DataContext = ViewModel;
            InitializeComponent();
        }

        public Step2ViewModel ViewModel { get; }

        

    }
}