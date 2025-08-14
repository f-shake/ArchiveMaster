using ArchiveMaster.Configs;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels.FileSystem;
public partial class BatchCommandLineFileInfo(FileSystemInfo file, string topDir) : SimpleFileInfo(file, topDir)
{
    [ObservableProperty]
    private string commandLine;

    [ObservableProperty]
    private string autoCreateDir;

    [ObservableProperty]
    private string processOutput;
    
    [ObservableProperty]
    private string processError;
}