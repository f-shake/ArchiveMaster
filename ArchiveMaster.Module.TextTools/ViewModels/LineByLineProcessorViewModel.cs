using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FzLib.Cryptography;
using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.ViewModels.FileSystem;
using Avalonia.Data.Converters;
using Avalonia.Media;
using FzLib.Avalonia.Dialogs;
using FzLib.Collections;
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

    public static IValueConverter VoteBrushConverter =>
        new FuncValueConverter<bool, IBrush>(b => b ? Brushes.Red : Brushes.Green);

    protected override Task OnInitializedAsync()
    {
        Results = Service.Items;
        return base.OnInitializedAsync();
    }

    protected override async Task OnExecutedAsync(CancellationToken ct)
    {
        int problemCount = Results.Count(p => p.VoteResultNotInconsistent);
        if (problemCount > 0)
        {
            await Services.Dialog.ShowWarningDialogAsync("投票结果不一致", $"存在{problemCount}项投票结果不一致，请检查");
        }
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
    private async Task CopyResultsAsync()
    {
        var text = string.Join(Environment.NewLine,
            Results.Select(p =>
                $"{p.Index}\t{(p.VoteResultNotInconsistent ? "不一致" : "一致")}\t{p.Input.Replace('\t', ' ')}\t{p.Output.Replace('\t', ' ')}\t{p.Message}"));
        text = $"序号\t投票\t输入\t输出\t说明{Environment.NewLine}{text}";
        await Services.Clipboard.SetTextAsync(text);
    }

    [RelayCommand]
    private async Task ImportExampleFromTextAsync()
    {
        var text = await Services.Dialog.ShowInputMultiLinesTextDialogAsync("从文本导入",
            "在此处粘贴表格文本，第一列为输入，第二列为输出，第三列（可选）为解释说明。列之前使用空格制表符或空格分隔。");
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var lines = text.SplitLines();
        Config.Examples.Clear();
        try
        {
            foreach (var line in lines)
            {
                var parts = line.Split(['\t', ' '], StringSplitOptions.RemoveEmptyEntries);

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
    private void RemoveSelectedExample(IList items)
    {
        if (items.Count > 0)
        {
            if (items.Count == 1)
            {
                Config.Examples.Remove(SelectedExample);
            }
            else
            {
                var temp = Config.Examples;
                var itemList = items.Cast<LineByLineItem>().ToList();
                Config.Examples = null;
                foreach (var item in itemList)
                {
                    temp.Remove(item);
                }

                Config.Examples = temp;
            }
        }
    }
}