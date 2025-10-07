using System.Collections.ObjectModel;
using ArchiveMaster.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels;

public partial class FullSnapshotItem : ObservableObject
{
    public FullSnapshotItem(BackupSnapshotEntity fullSnapshot)
    {
        FullSnapshot = fullSnapshot;
        Snapshots = [fullSnapshot];
    }

    [ObservableProperty]
    private BackupSnapshotEntity fullSnapshot;

    [ObservableProperty]
    private ObservableCollection<BackupSnapshotEntity> snapshots;
}