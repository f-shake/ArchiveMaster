using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels;

public partial class LineByLineItem : ObservableObject
{
    [ObservableProperty]
    private string[] eachVote;

    /// <summary>
    /// 解释说明（用于示例）
    /// </summary>
    [ObservableProperty]
    private string explain = "";

    /// <summary>
    /// 序号（自动）
    /// </summary>
    [ObservableProperty]
    private int index;
    
    /// <summary>
    /// 输入
    /// </summary>
    [ObservableProperty]
    private string input = "";

    /// <summary>
    /// 信息
    /// </summary>
    [ObservableProperty]
    private string message;

    /// <summary>
    /// 输出
    /// </summary>
    [ObservableProperty]
    private string output = "";
}