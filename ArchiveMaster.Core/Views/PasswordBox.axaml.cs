using ArchiveMaster.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace ArchiveMaster.Views;

public partial class PasswordBox : UserControl
{
    public PasswordBox()
    {
        InitializeComponent();
    }

    public static readonly StyledProperty<SecurePassword> PasswordProperty =
        AvaloniaProperty.Register<PasswordBox, SecurePassword>(
            nameof(Password));

    public SecurePassword Password
    {
        get => GetValue(PasswordProperty);
        set => SetValue(PasswordProperty, value);
    }

    public static readonly StyledProperty<string> WatermarkProperty = TextBox.WatermarkProperty.AddOwner<PasswordBox>();

    public string Watermark
    {
        get => GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }
}