using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels;

public partial class TextSource : ObservableObject
{
    [ObservableProperty]
    private bool fromFile = true;
    
    [ObservableProperty]
    private string text;
    
    [ObservableProperty]
    private ObservableCollection<DocFile> files = new ObservableCollection<DocFile>();
}