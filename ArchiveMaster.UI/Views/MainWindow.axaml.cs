using System;
using ArchiveMaster.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.Messaging;
using FzLib.Avalonia.Controls;

namespace ArchiveMaster.Views;

public partial class MainWindow : ExtendedWindow
{
    public MainWindow()
    {
        throw new Exception("请调用带参数的构造函数");
    }

    public MainWindow(MainViewModel viewModel, MainView view)
    {
        DataContext = viewModel;
        InitializeComponent();
        Content = view;
        if (OperatingSystem.IsWindows() && Environment.OSVersion.Version.Major == 10)
        {
            //SetWin10TitleBar();
        }
#if !DEBUG
        MinWidth = 640;
        MinHeight = 400;
#endif
    }

    private void SetWin10TitleBar()
    {
        Grid grid = new Grid()
        {
            RowDefinitions =
            [
                new RowDefinition(1, GridUnitType.Auto),
                new RowDefinition(1, GridUnitType.Star)
            ],
        };

        // grid.Bind(Grid.BackgroundProperty, Resources.GetResourceObservable("Background0"));

        var content = Content as Control;
        Content = null;
        Grid.SetRow(content, 1);
        grid.Children.Add(content);
        grid.Children.Add(new FzLib.Avalonia.Controls.WindowButtons()
        {
            HorizontalAlignment = HorizontalAlignment.Right
        });

        Content = grid;

        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
    }

    private void LightDarkButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (App.Current.ActualThemeVariant == ThemeVariant.Light)
        {
            App.Current.RequestedThemeVariant = ThemeVariant.Dark;
        }
        else
        {
            App.Current.RequestedThemeVariant = ThemeVariant.Light;
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (ViewModelBase.Current?.IsWorking ?? false)
        {
            e.Cancel = true;
        }
    }
}