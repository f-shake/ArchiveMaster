﻿using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Messaging;
using FzLib.Avalonia.Dialogs;
using FzLib.Avalonia.Messages;
using ArchiveMaster.Messages;
using ArchiveMaster.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.Configs;
using System.Reflection;
using Avalonia.Media.Imaging;
using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Interactivity;
using ArchiveMaster.Platforms;
using FzLib;
using Microsoft.Extensions.DependencyInjection;

namespace ArchiveMaster.Views;

public partial class MainView : UserControl
{
    private readonly IPermissionService permissionService;
    private CancellationTokenSource loadingToken = null;

    public MainView(MainViewModel viewModel, Initializer initializer, IViewPadding viewPadding = null,
        IPermissionService permissionService = null)
    {
        this.permissionService = permissionService;
        DataContext = viewModel;

        InitializeComponent();
        RegisterMessages();
        if (viewPadding != null)
        {
            Padding = new Thickness(0, viewPadding.GetTop(), 0, viewPadding.GetBottom());
        }

        initializer.ModuleInitializers.ForEach(p => p.RegisterMessages(this));
    }

    private void RegisterMessages()
    {
        this.RegisterDialogHostMessage();
        this.RegisterGetClipboardMessage();
        this.RegisterGetStorageProviderMessage();
        this.RegisterCommonDialogMessage();
        WeakReferenceMessenger.Default.Register<LoadingMessage>(this, (_, m) =>
        {
            if (m.IsVisible)
            {
                loadingToken ??= LoadingOverlay.ShowLoading(this);
            }
            else
            {
                if (loadingToken != null)
                {
                    loadingToken.Cancel();
                    loadingToken = null;
                }
            }
        });
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        permissionService?.CheckPermissions();
    }
}