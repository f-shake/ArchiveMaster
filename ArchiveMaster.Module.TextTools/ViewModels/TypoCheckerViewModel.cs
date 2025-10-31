using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FzLib.Cryptography;
using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels.FileSystem;
using Avalonia.Threading;
using FzLib.Avalonia.Dialogs;
using FzLib.Avalonia.Services;
using Serilog;

namespace ArchiveMaster.ViewModels;

public partial class TypoCheckerViewModel(
    ViewModelServices services)
    : AiTwoStepViewModelBase<TypoCheckerService, TypoCheckerConfig>(services)
{
    public override bool EnableInitialize { get; } = false;
    public override bool EnableRepeatExecute { get; } = true;
    public ObservableCollection<TypoItem> Typos { get; } = new ObservableCollection<TypoItem>();

    protected override Task OnExecutingAsync(CancellationToken ct)
    {
        Typos.Clear();
        Service.TypoItemGenerated += (_, e) => Dispatcher.UIThread.Invoke(() => Typos.Add(e.Value));
        return base.OnExecutingAsync(ct);
    }

    protected override void OnReset()
    {
        Typos.Clear();
    }
}