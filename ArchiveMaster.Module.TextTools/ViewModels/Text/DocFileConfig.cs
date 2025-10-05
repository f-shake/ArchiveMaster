using ArchiveMaster.Configs;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels;

public partial class DocFileConfig  :ObservableObject
{
    public DocFileConfig()
    {
        
    }

    public DocFileConfig(string file)
    {
        File = file;
    }
    
    [ObservableProperty]
    private string file = "";
        
    [ObservableProperty]
    private SecurePassword  password = new SecurePassword();
}