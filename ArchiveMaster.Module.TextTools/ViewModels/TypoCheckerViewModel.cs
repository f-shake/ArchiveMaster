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
using FzLib.Avalonia.Dialogs;
using FzLib.Avalonia.Services;
using Serilog;

namespace ArchiveMaster.ViewModels;

public partial class TypoCheckerViewModel(AppConfig appConfig, IDialogService dialogService)
    : TwoStepViewModelBase<TypoCheckerService, TypoCheckerConfig>(appConfig, dialogService)
{
    public IClipboardService ClipboardService { get; }

    public ObservableCollection<TypoItem> Typos { get; } = new ObservableCollection<TypoItem>();

    private readonly ConcurrentQueue<Func<Task>> taskQueue = new ConcurrentQueue<Func<Task>>();

    public override bool EnableInitialize { get; } = false;

    public override bool EnableRepeatExecute { get; } = true;

    protected override Task OnExecutingAsync(CancellationToken ct)
    {
        Service.TypoItemGenerated += (s, e) => { Typos.Add(e.Value); };
        return base.OnExecutingAsync(ct);
    }
}