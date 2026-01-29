using System.Diagnostics;
using ArchiveMaster.Helpers;
using Avalonia.Controls;
using ArchiveMaster.ViewModels;
using Avalonia;
using Avalonia.Input;
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

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyModifiers == KeyModifiers.Shift && e.Key == Key.Enter)
            {
                var textBox = sender as TextBox;
                if (textBox == null)
                {
                    return;
                }

                // 获取光标位置
                int cursorPosition = textBox.CaretIndex;
                string currentText = textBox.Text ?? "";

                // 如果有选中的文本，先删除选中的文本
                if (textBox.SelectedText.Length > 0)
                {
                    currentText = currentText.Remove(textBox.SelectionStart, textBox.SelectedText.Length);
                }

                // 在光标前插入换行符，光标后也插入换行符
                string beforeCursor = currentText.Substring(0, cursorPosition);
                string afterCursor = currentText.Substring(cursorPosition);

                // 将光标前后的文本拼接在一起，并在光标位置前后加上换行符
                textBox.Text = beforeCursor + Environment.NewLine + afterCursor;

                // 移动光标到换行符后面
                textBox.CaretIndex = cursorPosition + Environment.NewLine.Length;

                e.Handled = true;
            }
        }
    }
}