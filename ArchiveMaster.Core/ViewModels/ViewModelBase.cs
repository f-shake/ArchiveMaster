using System.ComponentModel;
using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FzLib.Avalonia.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace ArchiveMaster.ViewModels;

public abstract partial class ViewModelBase(ViewModelServices services) : ObservableObject
{
    public ViewModelServices Services { get; } = services;

    public static ViewModelBase Current { get; private set; }

    [ObservableProperty]
    private bool isWorking = false;

    public event EventHandler RequestClosing;

    [RelayCommand]
    public void Exit()
    {
        RequestClosing?.Invoke(this, EventArgs.Empty);
    }

    public virtual void OnEnter()
    {
        Current = this;
    }

    public virtual Task OnExitAsync(CancelEventArgs args)
    {
        Current = null;
        return Task.CompletedTask;
    }
}