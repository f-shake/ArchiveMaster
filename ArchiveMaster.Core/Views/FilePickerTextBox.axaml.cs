using ArchiveMaster.Configs;
using ArchiveMaster.Helpers;
using ArchiveMaster.Messages;
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
using System.Threading.Tasks;

namespace ArchiveMaster.Views;

public partial class FilePickerTextBox : UserControl
{
    public static readonly StyledProperty<bool> AllowMultipleProperty =
        AvaloniaProperty.Register<FilePickerTextBox, bool>(nameof(AllowMultiple));

    public static readonly StyledProperty<object> ButtonContentProperty =
        AvaloniaProperty.Register<FilePickerTextBox, object>(nameof(ButtonContent), "浏览..");

    public static readonly StyledProperty<string> FileNamesProperty =
        AvaloniaProperty.Register<FilePickerTextBox, string>(nameof(FileNames), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<FileFilterRule> FilterProperty =
        AvaloniaProperty.Register<FilePickerTextBox, FileFilterRule>(nameof(Filter));

    public static readonly StyledProperty<bool> IsFilterBarVisibleProperty =
        AvaloniaProperty.Register<FilePickerTextBox, bool>(nameof(IsFilterBarVisible));

    public static readonly DirectProperty<FilePickerTextBox, string> SaveFileDefaultExtensionProperty =
        AvaloniaProperty.RegisterDirect<FilePickerTextBox, string>(nameof(SaveFileDefaultExtension),
            o => o.SaveFileDefaultExtension,
            (o, v) => o.SaveFileDefaultExtension = v);

    public static readonly DirectProperty<FilePickerTextBox, string> SaveFileSuggestedFileNameProperty =
        AvaloniaProperty.RegisterDirect<FilePickerTextBox, string>(nameof(SaveFileSuggestedFileName),
            o => o.SaveFileSuggestedFileName,
            (o, v) => o.SaveFileSuggestedFileName = v);

    public static readonly DirectProperty<FilePickerTextBox, string> SuggestedStartLocationProperty =
        AvaloniaProperty.RegisterDirect<FilePickerTextBox, string>(nameof(SuggestedStartLocation),
            o => o.SuggestedStartLocation,
            (o, v) => o.SuggestedStartLocation = v);

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<FilePickerTextBox, string>(nameof(Title));

    private string saveFileDefaultExtension = default;
    private string saveFileSuggestedFileName = default;
    private string suggestedStartLocation = default;
    public FilePickerTextBox()
    {
        InitializeComponent();
        txt.AddHandler(DragDrop.DragEnterEvent, DragEnter);
        txt.AddHandler(DragDrop.DropEvent, Drop);
    }

    public enum PickerType
    {
        OpenFile,
        OpenFolder,
        SaveFile
    }

    public static string AndroidExternalFilesDir { get; set; }
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
    public string SaveFileDefaultExtension
    {
        get => saveFileDefaultExtension;
        set => SetAndRaise(SaveFileDefaultExtensionProperty, ref saveFileDefaultExtension, value);
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

    public string Title
    {
        get => this.GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public PickerType Type { get; set; } = PickerType.OpenFile;

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
            var files = e.Data.GetFiles()?.Select(p => p.TryGetLocalPath()).ToList();
            if (files is null or { Count: 0 })
            {
                return;
            }

            FileNames = AllowMultiple ? string.Join(Environment.NewLine, files) : files.First();
        }
    }

    private async void Button_Click(object sender, RoutedEventArgs e)
    {
        var storageProvider = TopLevel.GetTopLevel(this).StorageProvider;
        string suggestedStartLocation = SuggestedStartLocation;
        if (suggestedStartLocation == null && !string.IsNullOrWhiteSpace(FileNames))
        {
            var file = FileNames.Split(Environment.NewLine)[0];
            if (Type is PickerType.OpenFile or PickerType.SaveFile && File.Exists(file))
            {
                suggestedStartLocation = Path.GetDirectoryName(file);
            }
            else if (Type is PickerType.OpenFolder && Directory.Exists(file))
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

        switch (Type)
        {
            case PickerType.OpenFile:
                var openFiles = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
                {
                    AllowMultiple = AllowMultiple,
                    FileTypeFilter = FileTypeFilter,
                    Title = Title,
                    SuggestedStartLocation = suggestedStartLocationUri
                });
                if (openFiles != null && openFiles.Count > 0)
                {
                    FileNames = string.Join(Environment.NewLine, openFiles.Select(p => GetPath(p)));
                    var a = openFiles[0].TryGetLocalPath();
                }

                break;
            case PickerType.OpenFolder:
                var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
                {
                    Title = Title,
                    AllowMultiple = AllowMultiple,
                    SuggestedStartLocation = suggestedStartLocationUri
                });
                if (folders != null && folders.Count > 0)
                {
                    FileNames = string.Join(Environment.NewLine, folders.Select(p => GetPath(p)));
                }

                break;
            case PickerType.SaveFile:
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
                    FileNames = GetPath(saveFiles);
                }

                break;
        }
    }

    private bool CanDrop(DragEventArgs e)
    {
        if (e.Data.GetDataFormats().Contains(DataFormats.Files))
        {
            var fileAttributes = e.Data.GetFiles()
                .Select(p => p.TryGetLocalPath())
                .Select(p => File.GetAttributes(p))
                .ToList();
            if (Type == PickerType.SaveFile && fileAttributes.Count > 1)
            {
                return false;
            }

            var isAllDir = fileAttributes.All(p => p.HasFlag(FileAttributes.Directory));
            var isAllFile = fileAttributes.All(p => !p.HasFlag(FileAttributes.Directory));
            switch (Type)
            {
                case PickerType.OpenFile:
                case PickerType.SaveFile:
                    if (AllowMultiple && isAllFile)
                    {
                        return true;
                    }
                    else if (!AllowMultiple && fileAttributes.Count == 1 && isAllFile)
                    {
                        return true;
                    }

                    break;
                case PickerType.OpenFolder:
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

    private void FileFilterPopup_Closed(object sender, EventArgs e)
    {
        var binding = BindingOperations.GetBindingExpressionBase(tbkFilterDescription, TextBlock.TextProperty);
        binding?.UpdateTarget();
    }

    private string GetPath(IStorageItem file)
    {
        if (OperatingSystem.IsAndroid())
        {
            if (AndroidExternalFilesDir == null)
            {
                throw new ArgumentException(
                    "在Android中使用时，应当设置AndroidExternalFilesDir。" +
                    "值可以从Android项目中使用GetExternalFilesDir(string.Empty)" +
                    ".AbsolutePath.Split([\"Android\"], StringSplitOptions.None)[0]赋值");
            }

            var path = file.Path.LocalPath;
            return Path.Combine(AndroidExternalFilesDir, path.Split(':')[^1]);
        }

        return file.TryGetLocalPath();
    }

    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = HostServices.GetRequiredService<IDialogService>();
        WeakReferenceMessenger.Default.Send(new LoadingMessage(true));
        try
        {
            if (Type is not PickerType.OpenFolder)
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

            var results = new List<FileInfo>();
            var filter = Filter;
            await Task.Run(() =>
            {
                foreach (var dir in dirs)
                {
                    results.AddRange(new DirectoryInfo(dir)
                    .EnumerateFiles("*", FileEnumerateExtension.GetEnumerationOptions())
                    .ApplyFilter(default, filter));
                }
            });

            var fileNames = string.Join(Environment.NewLine, results.Select(p => p.FullName).Take(1000));
            if (results.Count > 1000)
            {
                fileNames = "仅显示前1000个：" + Environment.NewLine + fileNames;
            }
            await dialog.ShowOkDialogAsync("筛选测试", $"共筛选到{results.Count}个文件", fileNames);
        }
        catch (Exception ex)
        {
            await dialog.ShowErrorDialogAsync("测试失败", ex);
        }
        finally
        {
            WeakReferenceMessenger.Default.Send(new LoadingMessage(false));
        }
    }
}