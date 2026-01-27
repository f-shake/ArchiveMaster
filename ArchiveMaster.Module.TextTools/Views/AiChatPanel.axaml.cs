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
        public static readonly StyledProperty<AiConversation> ConversationProperty =
            AvaloniaProperty.Register<AiChatPanel, AiConversation>(
                nameof(Conversation));

        public AiChatPanel()
        {
            InitializeComponent();
            //自动滚动到最底部
            this.GetObservable(ConversationProperty).Subscribe(c =>
            {
                if (c != null)
                {
                    c.MessageAppended += (s, e) => { scr.ScrollToEnd(); };
                }
            });
        }

        public AiConversation Conversation
        {
            get => GetValue(ConversationProperty);
            set => SetValue(ConversationProperty, value);
        }

        private async void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var message = (AiChatMessage)((Button)sender).DataContext;
                if (message == null)
                {
                    return;
                }

                var task = TopLevel.GetTopLevel(this)?.Clipboard?.SetTextAsync(message.FullText);
                if (task != null)
                {
                    await task;
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "复制AI消息失败");
            }
        }
    }
}