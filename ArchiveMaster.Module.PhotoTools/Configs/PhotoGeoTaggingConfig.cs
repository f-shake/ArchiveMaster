using ArchiveMaster.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.IO;

namespace ArchiveMaster.Configs
{
    public partial class PhotoGeoTaggingConfig : ConfigBase
    {
        [ObservableProperty]
        private string dir;

        [ObservableProperty]
        private FileFilterRule filter = FileHelper.NoRawImageFileFilterRule;
        
        [ObservableProperty]
        private string gpxFile;

        partial void OnMaxToleranceChanged(TimeSpan value)
        {
            if (value.TotalSeconds < 1)
            {
                MaxTolerance = TimeSpan.FromSeconds(1);
            }

            if (value.TotalHours > 10)
            {
                MaxTolerance = TimeSpan.FromHours(10);
            }
        }

        [ObservableProperty]
        private TimeSpan maxTolerance = TimeSpan.FromMinutes(1);
        
        [ObservableProperty]
        private TimeSpan timeOffset = TimeSpan.Zero;

        [ObservableProperty]
        private bool inverseTimeOffset = false;

        public override void Check()
        {
            CheckDir(Dir, "目录");
            CheckFile(GpxFile, "GPX文件");
        }
    }
}