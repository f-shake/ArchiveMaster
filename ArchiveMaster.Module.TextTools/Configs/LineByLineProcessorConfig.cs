using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Serialization;
using ArchiveMaster.Attributes;
using ArchiveMaster.Enums;
using ArchiveMaster.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.Configs;

public partial class LineByLineProcessorConfig : ConfigBase
{
    [ObservableProperty]
    private ObservableCollection<LineByLineItem> examples =
        new ObservableCollection<LineByLineItem>();

    [ObservableProperty]
    private int maxLineEachCall = 200;

    [ObservableProperty]
    private string prompt;

    [ObservableProperty]
    private bool skipEmptyLine = true;

    [ObservableProperty]
    private TextSource source = new TextSource();
    public override void Check()
    {
        CheckEmpty(Prompt, "要求");
        if (Source.IsEmpty())
        {
            throw new ArgumentException("文本源为空");
        }

        if (Examples == null || Examples.Count < 2)
        {
            throw new ArgumentException("至少需要提供2个示例");
        }

        for (int i = 0; i < Examples.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(Examples[i].Input))
            {
                 throw new ArgumentException($"示例{i + 1}的输入为空");
            }
            if (string.IsNullOrWhiteSpace(Examples[i].Output))
            {
                 throw new ArgumentException($"示例{i + 1}的输出为空");
            }
        }
    }
}