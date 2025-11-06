using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels;

public partial class LineByLineItem : ObservableObject
{
    [ObservableProperty]
    private bool complete;
    
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
    
    /// <summary>
    /// 投票结果是否不一致
    /// </summary>
    [ObservableProperty]
    private bool voteResultNotInconsistent;

    public void Initialize(int voteCount)
    {
        if (voteCount > 0)
        {
            EachVote = new string[voteCount];
        }

        Output = "";
        VoteResultNotInconsistent = false;
        Message = "";
    }
}