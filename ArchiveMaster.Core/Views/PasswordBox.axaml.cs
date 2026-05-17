using ArchiveMaster.Configs;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace ArchiveMaster.Views;

public partial class PasswordBox : UserControl
{
    public static readonly StyledProperty<SecurePassword> PasswordProperty =
        AvaloniaProperty.Register<PasswordBox, SecurePassword>(
            nameof(Password));

    public static readonly StyledProperty<string> PlaceholderTextProperty = TextBox.PlaceholderTextProperty.AddOwner<PasswordBox>();

    public PasswordBox()
    {
        InitializeComponent();
    }

    public SecurePassword Password
    {
        get => GetValue(PasswordProperty);
        set => SetValue(PasswordProperty, value);
    }

    public string PlaceholderText
    {
        get => GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }
}