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
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.Avalonia.Dialogs;
using FzLib.Text;
using Serilog;

namespace ArchiveMaster.ViewModels;

public partial class LineByLineProcessorViewModel(ViewModelServices services)
    : AiTwoStepViewModelBase<LineByLineProcessorService, LineByLineProcessorConfig>(services)
{
    [ObservableProperty]
    private ObservableCollection<LineByLineItem> results;

    [ObservableProperty]
    private LineByLineItem selectedExample;

    public override bool EnableRepeatExecute => true;

    protected override Task OnInitializedAsync()
    {
        Results = Service.Items;
        return base.OnInitializedAsync();
    }

    protected override void OnReset()
    {
        Results = null;
    }

    [RelayCommand]
    private void AddExample()
    {
        var item = new LineByLineItem();
        Config.Examples.Add(item);
        SelectedExample = item;
    }

    [RelayCommand]
    private async Task ImportExampleFromTextAsync()
    {
        var text = await Services.Dialog.ShowInputMultiLinesTextDialogAsync("从文本导入",
            "在此处粘贴表格文本，第一列为输入，第二列为输出，第三列（可选）为解释说明。列之前使用空格制表符或空格分隔。");
        var lines = text.SplitLines();
        Config.Examples.Clear();
        try
        {
            foreach (var line in lines)
            {
                var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1)
                {
                    parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                }

                if (parts.Length is not (2 or 3))
                {
                    throw new Exception("列的数量应当为2或3");
                }

                LineByLineItem item = new LineByLineItem();
                item.Input = parts[0];
                item.Output = parts[1];
                if (parts.Length == 3)
                {
                    item.Explain = parts[2];
                }
                Config.Examples.Add(item);
            }
        }
        catch (Exception ex)
        {
            await Services.Dialog.ShowErrorDialogAsync("导入失败", ex);
        }
    }

    [RelayCommand]
    private void RemoveSelectedExample()
    {
        if (SelectedExample != null)
        {
            Config.Examples.Remove(SelectedExample);
        }
    }
}