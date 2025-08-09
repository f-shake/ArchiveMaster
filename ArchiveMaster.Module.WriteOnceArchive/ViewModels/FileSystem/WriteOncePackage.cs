using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels.FileSystem
{
    public partial class WriteOncePackage : ObservableObject
    {
        [ObservableProperty]
        private int index;

        [ObservableProperty]
        private List<WriteOnceFile> files = new List<WriteOnceFile>();

        [ObservableProperty]
        private long totalLength;

        [ObservableProperty]
        private bool isChecked;
    }
}