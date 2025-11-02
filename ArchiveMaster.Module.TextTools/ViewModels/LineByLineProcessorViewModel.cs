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
    private void RemoveSelectedExample()
    {
        if (SelectedExample != null)
        {
            Config.Examples.Remove(SelectedExample);
        }
    }
}