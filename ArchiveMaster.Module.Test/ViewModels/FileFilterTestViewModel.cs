using System.Collections.ObjectModel;
using ArchiveMaster.Configs;
using ArchiveMaster.Helpers;
using ArchiveMaster.ViewModels.FileSystem;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FzLib.Avalonia.Dialogs;
using FzLib.IO;

namespace ArchiveMaster.ViewModels;

public partial class FileFilterTestViewModel(ViewModelServices services) : ViewModelBase(services)
{
    [ObservableProperty]
    private FileFilterRule filter = new FileFilterRule();

    [ObservableProperty]
    private string dir;

    [ObservableProperty]
    private ObservableCollection<SimpleFileInfo> files;

    partial void OnDirChanged(string value)
    {
        if (Directory.Exists(value))
        {
            Files = new ObservableCollection<SimpleFileInfo>(new DirectoryInfo(value)
                .EnumerateFiles("*", FileEnumerateExtension.GetEnumerationOptions())
                .Select(p => new SimpleFileInfo(p, value)));
            UpdateStatus();
        }
        else
        {
            Files = null;
        }
    }

    [RelayCommand]
    private void UpdateStatus()
    {
        if (Files == null)
        {
            return;
        }

        FileFilterHelper filter = new FileFilterHelper(Filter);
        foreach (var file in Files)
        {
            file.IsChecked = filter.IsMatched(file);
        }

        Files = new ObservableCollection<SimpleFileInfo>(Files.OrderByDescending(p => p.IsChecked));
    }
}