using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.Configs;

public partial class DocFileConfig  :ObservableObject
{
    [ObservableProperty]
    private string file = "";
        
    [ObservableProperty]
    private SecurePassword  password = new SecurePassword();
}