using System.Diagnostics;
using System.Text.Json.Serialization;
using ArchiveMaster.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels.FileSystem
{
    public partial class TimeAssVideoFileInfo : SimpleFileInfo
    {
        [ObservableProperty]
        private TimeSpan length = TimeSpan.Zero;

        [ObservableProperty]
        private DateTime? startTime = null;

        [ObservableProperty]
        private double ratio = 1;

        [JsonIgnore]
        public DateTime? EndTime => StartTime.HasValue ? StartTime + Length * Ratio : null;
    }
}