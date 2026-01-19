using System.Diagnostics;
using ArchiveMaster.Helpers;
using Avalonia.Controls;
using ArchiveMaster.ViewModels;
using Avalonia;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Serilog;

namespace ArchiveMaster.Views
{
    public partial class AiChatPanel : UserControl
    {
        public AiChatPanel()
        {
            InitializeComponent();
            //自动滚动到最底部
            // this.GetObservable(ResultProperty).Subscribe(r => { result.ScrollToLine(result.GetLineCount()-1); });
        }

        public static readonly StyledProperty<AiConversation> ConversationProperty = AvaloniaProperty.Register<AiChatPanel, AiConversation>(
            nameof(Conversation));

        public AiConversation Conversation
        {
            get => GetValue(ConversationProperty);
            set => SetValue(ConversationProperty, value);
        }
    }
}