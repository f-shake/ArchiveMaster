using System.Diagnostics;
using System.Text.Json.Serialization;
using ArchiveMaster.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels.FileSystem
{
    public partial class TimeAssVideoFileInfo : SimpleFileInfo
    {
        [ObservableProperty]
        private TimeSpan? videoLength;

        [ObservableProperty]
        private DateTime? startTime = null;

        [ObservableProperty]
        private double ratio = 1;

        [JsonIgnore]
        public DateTime? EndTime =>
            StartTime.HasValue && VideoLength.HasValue ? StartTime + VideoLength.Value * Ratio : null;

        public TimeAssVideoFileInfo()
        {
        }

        public TimeAssVideoFileInfo(string file) : base(new FileInfo(file), null)
        {
        }
    }
}