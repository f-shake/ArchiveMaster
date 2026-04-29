using System.Diagnostics;
using System.Text.Json.Serialization;
using ArchiveMaster.Enums;
using ArchiveMaster.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels.FileSystem
{
    public partial class VideoInfoFileInfo(FileInfo file, string topDir) : SimpleFileInfo(file, topDir)
    {
        [ObservableProperty]
        VideoInfo videoInfo;
    }
}