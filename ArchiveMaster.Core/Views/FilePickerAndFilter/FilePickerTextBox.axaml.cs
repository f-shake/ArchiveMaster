using ArchiveMaster.Configs;
using ArchiveMaster.Helpers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Messaging;
using FzLib.Avalonia.Converters;
using FzLib.Avalonia.Dialogs;
using FzLib.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.Avalonia.Controls;

namespace ArchiveMaster.Views;

public partial class FilePickerTextBox : UserControl
{
    public static readonly StyledProperty<bool> AllowMultipleProperty =
        AvaloniaProperty.Register<FilePickerTextBox, bool>(nameof(AllowMultiple));

    public static readonly StyledProperty<object> ButtonContentProperty =
        AvaloniaProperty.Register<FilePickerTextBox, object>(nameof(ButtonContent), "浏览..");

    public static readonly StyledProperty<string> FileNamesProperty =
        AvaloniaProperty.Register<FilePickerTextBox, string>(nameof(FileNames), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<ICommand> FileSelectedCommandProperty =
        AvaloniaProperty.Register<FilePickerTextBox, ICommand>(
            nameof(FileSelectedCommand));

    public static readonly StyledProperty<FileFilterRule> FilterProperty =
        FileFilterBar.FilterProperty.AddOwner<FilePickerTextBox>();

    public static readonly StyledProperty<bool> IsFilterBarVisibleProperty =
        AvaloniaProperty.Register<FilePickerTextBox, bool>(nameof(IsFilterBarVisible));

    public static readonly StyledProperty<string> OpenFileMenuItemHeaderProperty =
        AvaloniaProperty.Register<FilePickerTextBox, string>(
            nameof(OpenFileMenuItemHeader), "打开文件");

    //AvaloniaProperty.Register<FilePickerTextBox, FileFilterRule>(nameof(Filter));
    public static readonly DirectProperty<FilePickerTextBox, string> SaveFileDefaultExtensionProperty =
        AvaloniaProperty.RegisterDirect<FilePickerTextBox, string>(nameof(SaveFileDefaultExtension),
            o => o.SaveFileDefaultExtension,
            (o, v) => o.SaveFileDefaultExtension = v);

    public static readonly StyledProperty<string> SaveFileMenuItemHeaderProperty =
        AvaloniaProperty.Register<FilePickerTextBox, string>(
            nameof(SaveFileMenuItemHeader), "保存文件");

    public static readonly DirectProperty<FilePickerTextBox, string> SaveFileSuggestedFileNameProperty =
        AvaloniaProperty.RegisterDirect<FilePickerTextBox, string>(nameof(SaveFileSuggestedFileName),
            o => o.SaveFileSuggestedFileName,
            (o, v) => o.SaveFileSuggestedFileName = v);

    public static readonly DirectProperty<FilePickerTextBox, string> SuggestedStartLocationProperty =
        AvaloniaProperty.RegisterDirect<FilePickerTextBox, string>(nameof(SuggestedStartLocation),
            o => o.SuggestedStartLocation,
            (o, v) => o.SuggestedStartLocation = v);

    public static readonly StyledProperty<ICommand> TextChangedCommandProperty =
        AvaloniaProperty.Register<FilePickerTextBox, ICommand>(
            nameof(TextChangedCommand));

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<FilePickerTextBox, string>(nameof(Title));

    public static readonly DirectProperty<FilePickerTextBox, FilePickerType> typeProperty =
        AvaloniaProperty.RegisterDirect<FilePickerTextBox, FilePickerType>(
            nameof(Type), o => o.Type, (o, v) => o.Type = v);

    public static readonly StyledProperty<string> WatermarkProperty =
            TextBox.WatermarkProperty.AddOwner<FilePickerTextBox>();

    private string saveFileDefaultExtension = default;

    private string saveFileSuggestedFileName = default;

    private string suggestedStartLocation = default;

    private FilePickerType type;

    public FilePickerTextBox()
    {
        InitializeComponent();
        txt.AddHandler(DragDrop.DragEnterEvent, DragEnter);
        txt.AddHandler(DragDrop.DropEvent, Drop);
    }


    public bool AllowMultiple
    {
        get => GetValue(AllowMultipleProperty);
        set => SetValue(AllowMultipleProperty, value);
    }

    public object ButtonContent
    {
        get => GetValue(ButtonContentProperty);
        set => SetValue(ButtonContentProperty, value);
    }

    public string FileNames
    {
        get => GetValue(FileNamesProperty);
        set => SetValue(FileNamesProperty, value);
    }

    public ICommand FileSelectedCommand
    {
        get => GetValue(FileSelectedCommandProperty);
        set => SetValue(FileSelectedCommandProperty, value);
    }

    public List<FilePickerFileType> FileTypeFilter { get; set; }

    public FileFilterRule Filter
    {
        get => GetValue(FilterProperty);
        set => SetValue(FilterProperty, value);
    }

    public bool IsFilterBarVisible
    {
        get => GetValue(IsFilterBarVisibleProperty);
        set => SetValue(IsFilterBarVisibleProperty, value);
    }

    public string OpenFileMenuItemHeader
    {
        get => GetValue(OpenFileMenuItemHeaderProperty);
        set => SetValue(OpenFileMenuItemHeaderProperty, value);
    }

    public string SaveFileDefaultExtension
    {
        get => saveFileDefaultExtension;
        set => SetAndRaise(SaveFileDefaultExtensionProperty, ref saveFileDefaultExtension, value);
    }

    public string SaveFileMenuItemHeader
    {
        get => GetValue(SaveFileMenuItemHeaderProperty);
        set => SetValue(SaveFileMenuItemHeaderProperty, value);
    }

    public string SaveFileSuggestedFileName
    {
        get => saveFileSuggestedFileName;
        set => SetAndRaise(SaveFileSuggestedFileNameProperty, ref saveFileSuggestedFileName, value);
    }

    public bool? ShowOverwritePrompt { get; set; }

    public string StringFileTypeFilter
    {
        set => FileTypeFilter = FilePickerFilterConverter.String2FilterList(value);
    }

    public string SuggestedStartLocation
    {
        get => suggestedStartLocation;
        set => SetAndRaise(SuggestedStartLocationProperty, ref suggestedStartLocation, value);
    }

    public ICommand TextChangedCommand
    {
        get => GetValue(TextChangedCommandProperty);
        set => SetValue(TextChangedCommandProperty, value);
    }

    public string Title
    {
        get => this.GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
    public FilePickerType Type
    {
        get => type;
        set => SetAndRaise(typeProperty, ref type, value);
    }

    public string Watermark
    {
        get => GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }

    public void DragEnter(object sender, DragEventArgs e)
    {
        if (CanDrop(e))
        {
            e.DragEffects = DragDropEffects.Link;
        }
    }

    public void Drop(object sender, DragEventArgs e)
    {
        if (CanDrop(e))
        {
            var files = e.DataTransfer.TryGetFiles()?.Select(p => p.TryGetLocalPath()).ToList();
            if (files is null or { Count: 0 })
            {
                return;
            }

            FileNames = AllowMultiple ? string.Join(Environment.NewLine, files) : files.First();
        }
    }

    private async void Button_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Type == FilePickerType.OpenOrSaveFile)
            {
                return;
            }

            await OpenPickerDialogAsync(Type);
        }
        catch
        {
            Debug.Assert(false);
        }
    }

    private bool CanDrop(DragEventArgs e)
    {
        if (e.DataTransfer.Formats.Contains(DataFormat.File))
        {
            var fileAttributes = e.DataTransfer.TryGetFiles()?
                .Select(p => p.TryGetLocalPath())
                .Select(File.GetAttributes)
                .ToList();
            if (Type == FilePickerType.SaveFile && fileAttributes.Count > 1)
            {
                return false;
            }

            var isAllDir = fileAttributes.All(p => p.HasFlag(FileAttributes.Directory));
            var isAllFile = fileAttributes.All(p => !p.HasFlag(FileAttributes.Directory));
            switch (Type)
            {
                case FilePickerType.OpenFile:
                case FilePickerType.SaveFile:
                case FilePickerType.OpenOrSaveFile:
                    if (AllowMultiple && isAllFile)
                    {
                        return true;
                    }
                    else if (!AllowMultiple && fileAttributes.Count == 1 && isAllFile)
                    {
                        return true;
                    }

                    break;
                case FilePickerType.OpenFolder:
                    if (AllowMultiple && isAllDir)
                    {
                        return true;
                    }
                    else if (!AllowMultiple && fileAttributes.Count == 1 && isAllDir)
                    {
                        return true;
                    }

                    break;
            }

            return false;
        }

        return false;
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (TextChangedCommand?.CanExecute(FileNames) == true)
        {
            TextChangedCommand.Execute(FileNames);
        }
    }

    private async void OpenOrSaveFileMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var typeStr = (sender as Control).Tag as string;
            switch (typeStr)
            {
                case "open":
                    await OpenPickerDialogAsync(FilePickerType.OpenFile);
                    break;
                case "save":
                    await OpenPickerDialogAsync(FilePickerType.SaveFile);
                    break;
                default:
                    throw new InvalidOperationException($"未知的菜单项：{typeStr}");
            }
        }
        catch (Exception ex)
        {
            Debug.Assert(false);
        }
    }

    private async Task OpenPickerDialogAsync(FilePickerType browseType)
    {
        var storageProvider = TopLevel.GetTopLevel(this).StorageProvider;
        string suggestedStartLocation = SuggestedStartLocation;
        if (suggestedStartLocation == null && !string.IsNullOrWhiteSpace(FileNames))
        {
            var file = FileNames.Split(Environment.NewLine)[0];
            if (Type is FilePickerType.OpenFile or FilePickerType.SaveFile && File.Exists(file))
            {
                suggestedStartLocation = Path.GetDirectoryName(file);
            }
            else if (Type is FilePickerType.OpenFolder && Directory.Exists(file))
            {
                suggestedStartLocation = file;
            }
        }

        IStorageFolder suggestedStartLocationUri = null;
        try
        {
            suggestedStartLocationUri = suggestedStartLocation == null
                ? null
                : await storageProvider.TryGetFolderFromPathAsync(suggestedStartLocation);
        }
        catch
        {
        }

        switch (browseType)
        {
            case FilePickerType.OpenFile:
                var openFiles = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
                {
                    AllowMultiple = AllowMultiple,
                    FileTypeFilter = FileTypeFilter,
                    Title = Title,
                    SuggestedStartLocation = suggestedStartLocationUri
                });
                if (openFiles != null && openFiles.Count > 0)
                {
                    FileNames = string.Join(Environment.NewLine, openFiles.Select(p => p.GetPath()));
                    if (FileSelectedCommand?.CanExecute(FileNames) == true)
                    {
                        FileSelectedCommand.Execute(FileNames);
                    }
                }

                break;
            case FilePickerType.OpenFolder:
                var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
                {
                    Title = Title,
                    AllowMultiple = AllowMultiple,
                    SuggestedStartLocation = suggestedStartLocationUri
                });
                if (folders != null && folders.Count > 0)
                {
                    FileNames = string.Join(Environment.NewLine, folders.Select(p => p.GetPath()));
                    if (FileSelectedCommand?.CanExecute(FileNames) == true)
                    {
                        FileSelectedCommand.Execute(FileNames);
                    }
                }

                break;
            case FilePickerType.SaveFile:
                var saveFiles = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions()
                {
                    Title = Title,
                    FileTypeChoices = FileTypeFilter,
                    DefaultExtension = SaveFileDefaultExtension,
                    ShowOverwritePrompt = ShowOverwritePrompt,
                    SuggestedFileName = SaveFileSuggestedFileName,
                    SuggestedStartLocation = suggestedStartLocationUri
                });
                if (saveFiles != null)
                {
                    FileNames = saveFiles.GetPath();
                    if (FileSelectedCommand?.CanExecute(FileNames) == true)
                    {
                        FileSelectedCommand.Execute(FileNames);
                    }
                }

                break;
            default:
                throw new NotSupportedException($"不支持的文件选择类型：{browseType}");
        }
    }
    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        var progress = HostServices.GetRequiredService<IProgressOverlayService>();
        var dialogService = HostServices.GetRequiredService<IDialogService>();
        var results = new List<SimpleFileInfo>();
        long totalLength = 0;
        bool ok = false;

        async Task MainTask(CancellationToken ct)
        {
            if (Type is not FilePickerType.OpenFolder)
            {
                throw new Exception("只有目录可供筛选测试");
            }

            if (Filter == null)
            {
                throw new Exception("筛选器为空");
            }

            string[] dirs = null;
            if (AllowMultiple)
            {
                dirs = FileNameHelper.GetDirNames(FileNames);
            }
            else
            {
                dirs = [FileNames];
            }

            foreach (var dir in dirs)
            {
                if (!Directory.Exists(dir))
                {
                    throw new DirectoryNotFoundException(dir);
                }
            }

            var filter = Filter;

            await Task.Run(() =>
            {
                foreach (var dir in dirs)
                {
                    results.AddRange(new DirectoryInfo(dir)
                        .EnumerateFiles("*", FileEnumerateExtension.GetEnumerationOptions())
                        .ApplyFilter(ct, filter)
                        .Select(p => new SimpleFileInfo(p, dir)));
                }

                totalLength = results.Sum(p => p.Length);
            }, ct);
            ok = true;
        }

        await progress.WithOverlayAsync(MainTask,
            onError: async ex =>
            {
                await HostServices.GetRequiredService<IDialogService>().ShowErrorDialogAsync("测试失败", ex);
            },
            initialMessage: "正在筛选指定目录中的文件");

        if (ok)
        {
            var dialog = new FilterTestResultDialog(results.Count, totalLength, results);
            await dialog.ShowDialog(dialogService.ContainerType, TopLevel.GetTopLevel(this));
        }
    }
}