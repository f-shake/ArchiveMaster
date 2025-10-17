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
            //自动滚动到最底部
            this.GetObservable(ResultProperty).Subscribe(r => { result.ScrollToLine(result.GetLineCount()-1); });
        }

        public static readonly StyledProperty<string> MessageProperty =
            AvaloniaProperty.Register<ResultTextBox, string>(
                nameof(Message));

        public static readonly StyledProperty<string> ResultProperty = AvaloniaProperty.Register<ResultTextBox, string>(
            nameof(Result));

        public string Message
        {
            get => GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        public string Result
        {
            get => GetValue(ResultProperty);
            set => SetValue(ResultProperty, value);
        }
    }
}