using System.Diagnostics;
using ArchiveMaster.Helpers;
using ArchiveMaster.Services;
using Avalonia.Controls;
using ArchiveMaster.ViewModels;
using Avalonia;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FzLib.Avalonia.Controls;
using FzLib.Avalonia.Dialogs;
using FzLib.Avalonia.Services;
using FzLib.Collections;
using Serilog;

namespace ArchiveMaster.Views
{
    public partial class TextSourceInput : UserControl
    {
        public static readonly StyledProperty<TextSource> SourceProperty =
            AvaloniaProperty.Register<TextSourceInput, TextSource>(
                nameof(Source));

        public TextSourceInput()
        {
            InitializeComponent();
        }

        public TextSource Source
        {
            get => GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        private async void AddFilesButton_Click(object sender, RoutedEventArgs e)
        {
            var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storageProvider == null)
            {
                return;
            }

            var pickerOptions = FilePickerOptionsBuilder.Create()
                .AddFilter("支持的格式", "txt", /* "doc",*/ "docx", "xlsx", "md", "pdf", "txt")
                .AddFilter("Word文档", /*"doc",*/ "docx")
                .AddFilter("Excel表格", "xlsx")
                .AddFilter("PDF文档", /*"doc",*/ "pdf")
                .AddFilter("Markdown文档", "md")
                .AddFilter("纯文本", "txt")
                .AddFilter("所有文件（作为纯文本读取）", "*")
                .AllowMultiple()
                .BuildOpenOptions();
            var files = await storageProvider.OpenFilePickerAsync(pickerOptions);
            if (files.Count > 0)
            {
                foreach (var file in files)
                {
                    Source.Files.Add(new DocFile(file.GetPath()));
                }
            }
        }

        private void ClearFilesButton_Click(object sender, RoutedEventArgs e)
        {
            Source.Files.Clear();
        }

        private async void PasteButton_Click(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
            {
                return;
            }

            var text = await (topLevel.Clipboard?.TryGetTextAsync() ?? Task.FromResult(""));
            if (!string.IsNullOrWhiteSpace(text))
            {
                Source.Text = text;
            }
        }

        private async void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is DocFile file)
            {
                var sources = new TextSource() { Files = { file } };
                string result = null;
                await HostServices.GetRequiredService<IProgressOverlayService>()
                    .WithOverlayAsync(
                        () => Task.Run(async () =>
                            result = (await sources.GetPlainTextAsync().FirstOrDefaultAsync()).Text),
                        ex => HostServices.GetRequiredService<IDialogService>()
                            .ShowErrorDialogAsync("打开文件失败", $"无法打开文件：{file.File}", ex.ToString()), "正在打开文件");
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
        }

        private void RemoveFileButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is DocFile file)
            {
                Source.Files.Remove(file);
            }
        }
    }
}