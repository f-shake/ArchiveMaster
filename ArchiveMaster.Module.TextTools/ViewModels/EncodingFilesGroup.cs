using System.Text;
using ArchiveMaster.ViewModels.FileSystem;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels;

public partial class EncodingFilesGroup : ObservableObject
{
    [ObservableProperty]
    public Encoding encoding;

    [ObservableProperty]
    public List<EncodingFileInfo> files = new List<EncodingFileInfo>();

    [ObservableProperty]
    public bool? isChecked;

    private int checkedCount;
    
    private bool isChangingChecked = false;
    
    private int uncheckedCount;

    public EncodingFilesGroup(Encoding encoding, IEnumerable<EncodingFileInfo> files)
    {
        Encoding = encoding;
        
        //统计选中和未选中的文件数量，并订阅文件的选中状态变化事件
        foreach (var file in files)
        {
            Files.Add(file);
            if (file.IsChecked)
            {
                checkedCount++;
            }
            else
            {
                uncheckedCount++;
            }

            file.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(EncodingFileInfo.IsChecked))
                {
                    OnAnyFileIsCheckedChanged(s as EncodingFileInfo);
                }
            };
        }

        //更新IsChecked属性
        UpdateIsChecked();
    }
    
    private void OnAnyFileIsCheckedChanged(EncodingFileInfo file)
    {
        if (isChangingChecked)
        {
            return;
        }
        if (file.IsChecked)
        {
            checkedCount++;
            uncheckedCount--;
        }
        else
        {
            checkedCount--;
            uncheckedCount++;
        }

        UpdateIsChecked();
    }

    partial void OnIsCheckedChanged(bool? value)
    {
        if (value == null)
        {
            return;
        }

        isChangingChecked = true;
        foreach (var file in files)
        {
            file.IsChecked = value.Value;
        }

        isChangingChecked = false;
    }
    
    private void UpdateIsChecked()
    {
        if (checkedCount > 0)
        {
            if (uncheckedCount > 0)
            {
                IsChecked = null;
            }
            else
            {
                IsChecked = true;
            }
        }
        else
        {
            IsChecked = false;
        }
    }
}