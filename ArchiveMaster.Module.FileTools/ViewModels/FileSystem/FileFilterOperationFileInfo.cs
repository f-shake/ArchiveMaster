using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels.FileSystem;

public partial class FileFilterOperationFileInfo(FileSystemInfo file, string topDir) : SimpleFileInfo(file, topDir)
{
    [ObservableProperty]
    private string targetPath;
}
