using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels;

public partial class TextSource : ObservableObject
{
    [ObservableProperty]
    private bool fromFile = true;
    
    [ObservableProperty]
    public string text;
    
    [ObservableProperty]
    public ObservableCollection<DocFileConfig> files = new ObservableCollection<DocFileConfig>();
}