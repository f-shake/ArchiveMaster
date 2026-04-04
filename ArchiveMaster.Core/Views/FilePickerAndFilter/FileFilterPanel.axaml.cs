using ArchiveMaster.Configs;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FzLib.IO;
using Mapster;

namespace ArchiveMaster.Views;

public partial class FileFilterPanel : UserControl
{
    public static readonly StyledProperty<FileFilterRule> FilterProperty =
        AvaloniaProperty.Register<FileFilterControl, FileFilterRule>(
            nameof(Filter), defaultBindingMode: BindingMode.TwoWay);


    public FileFilterPanel()
    {
        InitializeComponent();
    }

   
    public FileFilterRule Filter
    {
        get => GetValue(FilterProperty);
        set => SetValue(FilterProperty, value);
    }

    private void ResetButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (Filter == null)
        {
            Filter = new FileFilterRule();
            return;
        }
        
        var newObj = new FileFilterRule();
        newObj.UseRegex = Filter.UseRegex;
        newObj.Adapt(Filter);
    }
}