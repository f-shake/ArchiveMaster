using System.Diagnostics;
using System.Text.Json.Serialization;
using ArchiveMaster.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels.FileSystem
{
    public partial class TimeAssVideoFileInfo : SimpleFileInfo
    {
        [NotifyPropertyChangedFor(nameof(EndTime))]
        [ObservableProperty]
        private TimeSpan? videoLength;

        [NotifyPropertyChangedFor(nameof(EndTime))]
        [ObservableProperty]
        private DateTime? startTime = null;

        [NotifyPropertyChangedFor(nameof(EndTime))]
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