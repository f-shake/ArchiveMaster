using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels.FileSystem
{
    public partial class WriteOncePackage : ObservableObject
    {
        [ObservableProperty]
        private List<WriteOnceFile> files = new List<WriteOnceFile>();

        [ObservableProperty]
        private int index;

        [ObservableProperty]
        private bool isChecked;

        [ObservableProperty]
        private long totalLength;
    }
}