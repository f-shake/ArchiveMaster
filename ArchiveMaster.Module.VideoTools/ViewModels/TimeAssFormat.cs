using System.Drawing;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels;

public partial class TimeAssFormat : ObservableObject
{
    [ObservableProperty]
    private int horizontalAlignment = 2;
    
    [ObservableProperty]
    private int verticalAlignment = 0;

    [ObservableProperty]
    private bool bold;

    [ObservableProperty]
    private Color borderColor = Color.Black;

    [ObservableProperty]
    private int borderWidth = 3;

    [ObservableProperty]
    private string font = "Microsoft YaHei UI";

    [ObservableProperty]
    private Color textColor = Color.White;

    [ObservableProperty]
    private string timeFormat = "HH:mm:ss";

    [ObservableProperty]
    private int interval = 1000;

    [ObservableProperty]
    private bool italic;

    [ObservableProperty]
    private int verticalMargin = 20;

    [ObservableProperty]
    private int horizontalMargin = 20;

    [ObservableProperty]
    private int size = 32;

    [ObservableProperty]
    private bool underline;
}