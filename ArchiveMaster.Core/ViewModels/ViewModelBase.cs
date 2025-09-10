using System.ComponentModel;
using ArchiveMaster.Configs;
using ArchiveMaster.Messages;
using ArchiveMaster.Services;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FzLib.Avalonia.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace ArchiveMaster.ViewModels;

public abstract partial class ViewModelBase(IDialogService dialogService) : ObservableObject
{
    public IDialogService DialogService { get; } = dialogService;

    public static ViewModelBase Current { get; private set; }

    [ObservableProperty]
    private bool isWorking = false;

    // public event EventHandler RequestClosing;
    //
    // [RelayCommand]
    // public void Exit()
    // {
    //     RequestClosing?.Invoke(this, EventArgs.Empty);
    // }

    public virtual void OnEnter()
    {
        Current = this;
    }

    public virtual Task OnExitAsync(CancelEventArgs args)
    {
        Current = null;
        return Task.CompletedTask;
    }

    protected async Task<bool> TryDoAsync(string workName, Func<Task> task)
    {
        WeakReferenceMessenger.Default.Send(new LoadingMessage(true));
        await Task.Delay(100);
        try
        {
            await task();
            WeakReferenceMessenger.Default.Send(new LoadingMessage(false));
            return true;
        }
        catch (Exception ex)
        {
            WeakReferenceMessenger.Default.Send(new LoadingMessage(false));
            await DialogService.ShowErrorDialogAsync($"{workName}失败", ex);
            return false;
        }
    }
}