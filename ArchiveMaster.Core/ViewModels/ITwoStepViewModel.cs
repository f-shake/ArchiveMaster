using ArchiveMaster.Services;
using ArchiveMaster.ViewModels.FileSystem;

namespace ArchiveMaster.ViewModels;

public interface ITwoStepViewModel
{
    public bool CanCancel { get; }
    public bool CanInitialize { get; }
    public bool CanExecute { get; }
    public bool CanReset { get; }
    public ITwoStepService TwoStepService { get; }
    public SimpleFileInfo SelectedFile { get; set; }
}