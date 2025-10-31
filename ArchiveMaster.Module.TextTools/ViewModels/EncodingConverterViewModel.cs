using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FzLib.Cryptography;
using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.Avalonia.Dialogs;
using Serilog;

namespace ArchiveMaster.ViewModels;

public partial class EncodingConverterViewModel(ViewModelServices services)
    : TwoStepViewModelBase<EncodingConverterService, EncodingConverterConfig>(services)
{
    [ObservableProperty]
    private ObservableCollection<EncodingFileInfo> files;

    public List<string> EncodingNames { get; } = Encoding.GetEncodings().Select(p => p.Name).ToList();

    protected override Task OnInitializedAsync()
    {
        Files = new ObservableCollection<EncodingFileInfo>(Service.Files);
        return base.OnInitializedAsync();
    }

    [RelayCommand]
    private async Task TestEncodingAsync(EncodingFileInfo file)
    {
        if (file.Encoding == null)
        {
            throw new ArgumentException("未识别到合适的编码");
        }

        int bufferLength = 1000;
        int maxLength = 10_000;
        int readLength ;
        char[] buffer = new char[bufferLength];
        StringBuilder str = new StringBuilder(maxLength + 100);
        bool notEnd = false;
        bool success = false;
        await Services.ProgressOverlay.WithOverlayAsync(async ct =>
        {
            using var fs = new StreamReader(file.Path, file.Encoding.Encoding);
            while ((readLength = await fs.ReadAsync(buffer, 0, bufferLength)) > 0)
            {
                ct.ThrowIfCancellationRequested();
                if (str.Length + readLength > maxLength)
                {
                    notEnd = true;
                    str.Append("...");
                    break;
                }

                str.Append(buffer, 0, readLength);
            }

            success = true;
        }, null, async ex =>
        {
            await Services.Dialog.ShowErrorDialogAsync("读取失败", ex);
            Log.Error(ex, "编码测试读取文件失败");
        }, "正在读取文件");

        if (success)
        {
            await Services.Dialog.ShowOkDialogAsync("读取成功",
                notEnd ? "文件较大，仅读取前10000个字符，展开详情查看" : "展开详情查看",
                str.ToString());
        }
    }

    protected override void OnReset()
    {
        Files = null;
    }
}