using System.Diagnostics;
using ArchiveMaster.Helpers;
using Avalonia.Controls;
using ArchiveMaster.ViewModels;
using Avalonia;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FzLib.Avalonia.Dialogs.Pickers;
using Serilog;

namespace ArchiveMaster.Views
{
    public partial class ResultTextBox : UserControl
    {
        public ResultTextBox()
        {
            InitializeComponent();
        }

        public static readonly StyledProperty<string> MessageProperty =
            AvaloniaProperty.Register<ResultTextBox, string>(
                nameof(Message));

        public string Message
        {
            get => GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        public static readonly StyledProperty<string> ResultProperty = AvaloniaProperty.Register<ResultTextBox, string>(
            nameof(Result));

        public string Result
        {
            get => GetValue(ResultProperty);
            set => SetValue(ResultProperty, value);
        }

        private async void CopyButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(Result))
            {
                return;
            }

            try
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard == null)
                {
                    return;
                }
                await clipboard.SetTextAsync(Result);
            }
            catch
            {
                Debug.Assert(false);
            }
        }
    }
}