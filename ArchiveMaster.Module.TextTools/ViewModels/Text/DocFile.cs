using ArchiveMaster.Configs;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels;

public partial class DocFile  :ObservableObject
{
    public DocFile()
    {
        
    }

    public DocFile(string file)
    {
        File = file;
    }
    
    [ObservableProperty]
    private string file = "";
        
    [ObservableProperty]
    private SecurePassword  password = new SecurePassword();
}