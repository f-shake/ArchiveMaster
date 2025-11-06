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

public partial class TextRewriterViewModel(ViewModelServices services)
    : AiTwoStepViewModelBase<TextRewriterService, TextRewriterConfig>(services)
{
    [ObservableProperty]
    private string result = "";

    public override bool EnableInitialize => false;

    public override bool EnableRepeatExecute => true;
    
    protected override Task OnExecutedAsync(CancellationToken ct)
    {
        Result = LlmCallerService.RemoveThink(Result);
        return base.OnExecutedAsync(ct);
    }

    protected override Task OnExecutingAsync(CancellationToken ct)
    {
        Result = "";
        Service.TextGenerated += (sender, e) => Result += e.Value;
        return base.OnExecutingAsync(ct);
    }

    protected override void OnReset()
    {
        Result = "";
    }
}