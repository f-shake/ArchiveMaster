using System.Diagnostics;
using ArchiveMaster.Enums;
using ArchiveMaster.Helpers;
using ArchiveMaster.Services;
using Avalonia.Controls;
using ArchiveMaster.ViewModels;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DocumentFormat.OpenXml.Bibliography;
using FluentIcons.Avalonia;
using FluentIcons.Common;
using FzLib.Avalonia.Controls;
using FzLib.Avalonia.Dialogs;
using FzLib.Avalonia.Services;
using FzLib.Collections;
using Serilog;

namespace ArchiveMaster.Views
{
    public partial class AiChatPanel : UserControl
    {
        public static readonly DirectProperty<AiChatPanel, bool> AllowAttachmentsProperty =
            AvaloniaProperty.RegisterDirect<AiChatPanel, bool>(
                nameof(AllowAttachments), o => o.AllowAttachments, (o, v) => o.AllowAttachments = v);

        public static readonly DirectProperty<AiChatPanel, bool> CanSendProperty =
            AvaloniaProperty.RegisterDirect<AiChatPanel, bool>(
                nameof(CanSend), o => o.CanSend, (o, v) => o.CanSend = v);

        public static readonly StyledProperty<AiConversation> ConversationProperty =
                    AvaloniaProperty.Register<AiChatPanel, AiConversation>(
                nameof(Conversation));

        private bool allowAttachments = true;

        private bool canSend;

        public AiChatPanel()
        {
            InitializeComponent();

            this.GetObservable(ConversationProperty).Subscribe(OnConversationChanged);
        }

        public bool AllowAttachments
        {
            get => allowAttachments;
            set => SetAndRaise(AllowAttachmentsProperty, ref allowAttachments, value);
        }

        public bool CanSend
        {
            get => canSend;
            private set => SetAndRaise(CanSendProperty, ref canSend, value);
        }

        public AiConversation Conversation
        {
            get => GetValue(ConversationProperty);
            set => SetValue(ConversationProperty, value);
        }

        public static async Task TestFileAsync(DocFile file)
        {
            DocFile[] sources = [file];
            string result = null;
            bool canceled = false;
            await HostServices.GetRequiredService<IProgressOverlayService>()
                .WithOverlayAsync(
                    ct => Task.Run(async () =>
                        result = (await sources.GetPlainTextAsync(TextSourceReadUnit.Combined, ct)
                            .FirstOrDefaultAsync()).Text, ct),
                    () =>
                    {
                        canceled = true;
                        return Task.CompletedTask;
                    },
                    async ex =>
                    {
                        await HostServices.GetRequiredService<IDialogService>()
                            .ShowErrorDialogAsync("打开文件失败", $"无法打开文件：{file.File}", ex.ToString());
                        canceled = true;
                    },
                    "正在打开文件");
            if (canceled)
            {
                return;
            }

            if (string.IsNullOrEmpty(result))
            {
                await HostServices.GetRequiredService<IDialogService>()
                    .ShowWarningDialogAsync("文件为空", $"成功打开文件：{file.File}，但文件为空");
            }
            else
            {
                await HostServices.GetRequiredService<IDialogService>()
                    .ShowOkDialogAsync("打开文件成功", $"成功打开文件：{file.File}", result);
            }
        }

        private async void AddAttachmentButton_Click(object sender, RoutedEventArgs e)
        {
            var storage = HostServices.GetRequiredService<IStorageProviderService>();
            var files = await storage.CreatePickerBuilder()
                .AddFilter("支持的格式", "txt", /* "doc",*/ "docx", "xlsx", "md", "pdf", "txt")
                .AddFilter("Word文档", /*"doc",*/ "docx")
                .AddFilter("Excel表格", "xlsx")
                .AddFilter("PDF文档", /*"doc",*/ "pdf")
                .AddFilter("Markdown文档", "md")
                .AddFilter("纯文本", "txt")
                .AddFilter("所有文件（作为纯文本读取）", "*")
                .AllowMultiple()
                .OpenFilePickerAsync();
            if (files is { Count: > 0 })
            {
                foreach (var file in files)
                {
                    Conversation.Attachments.Add(new DocFile(file.GetPath()));
                }
            }
        }

        private async void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var message = (AiChatMessage)((Control)sender).DataContext;
                if (message == null)
                {
                    return;
                }

                var tag = (string)((Control)sender).Tag;
                var text = tag switch
                {
                    "PlainText" => ((AiAssistantChatMessage)message).PlainText,
                    "RawText" => message.FullText,
                    "ThinkText" => ((AiAssistantChatMessage)message).ThinkText,
                    _ => message.FullText
                };
                var task = TopLevel.GetTopLevel(this)?.Clipboard?.SetTextAsync(text);
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

        private void ExpandCollapseButton_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null)
            {
                Debug.Assert(false);
                return;
            }

            SelectableTextBlock tbk = (btn.Parent as DockPanel)?.Children?.OfType<SelectableTextBlock>()
                ?.FirstOrDefault();
            if (tbk == null)
            {
                Debug.Assert(false);
                return;
            }

            if (tbk.MaxLines == 3) //当前已折叠，需要展开
            {
                tbk.MaxLines = int.MaxValue;
                btn.Content = new FluentIcon { Icon = Icon.ChevronUp };
            }
            else
            {
                tbk.MaxLines = 3;
                btn.Content = new FluentIcon { Icon = Icon.ChevronDown };
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

        private void OnConversationChanged(AiConversation c)
        {
            if (c == null)
            {
                return;
            }

            c.MessageAppended += (s, e) => { Dispatcher.UIThread.Invoke(() => { scr.ScrollToEnd(); }); };
            c.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(AiConversation.InputText))
                {
                    UpdateCanSend();
                }
            };
            c.Attachments.CollectionChanged += (s, e) => { UpdateCanSend(); };
        }

        private async void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is DocFile file)
            {
                await TestFileAsync(file);
            }
        }

        private void RemoveFileButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is DocFile file)
            {
                Conversation.Attachments.Remove(file);
            }
        }

        private void UpdateCanSend()
        {
            if (Conversation == null)
            {
                return;
            }

            CanSend = Conversation.InputText is { Length: > 0 } || Conversation.Attachments.Count > 0;
        }
    }
}