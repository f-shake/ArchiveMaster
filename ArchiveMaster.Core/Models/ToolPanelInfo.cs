using ArchiveMaster.ViewModels;
using ArchiveMaster.Views;

namespace ArchiveMaster.Models;

public class ToolPanelGroupInfo
{
    public string GroupName { get; set; }
    public string GroupDescription { get; set; }
    public List<ModuleMenuItemInfo> MenuItems { get; } = new List<ModuleMenuItemInfo>();
    public List<ToolPanelInfo> Panels { get; } = new List<ToolPanelInfo>();
}

public class ToolPanelInfo
{
    public ToolPanelInfo(Type viewType, Type viewModelType, string title, string description, string iconUri = null)
    {
        ViewType = viewType;
        ViewModelType = viewModelType;
        Title = title;
        Description = description;
        IconUri = iconUri;
    }

    private ToolPanelInfo()
    {
    }
    public string Description { get; private set; }

    public string IconUri { get; private set; }

    public PanelBase PanelInstance { get; set; }

    public string Title { get; private set; }
    public Type ViewModelType { get; private set; }
    public Type ViewType { get; private set; }
}