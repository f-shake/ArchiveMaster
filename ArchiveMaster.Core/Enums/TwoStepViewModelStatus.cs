namespace ArchiveMaster.Enums;

public enum TwoStepViewModelStatus
{
    /// <summary>
    /// 就绪
    /// </summary>
    Ready,

    /// <summary>
    /// 初始化中
    /// </summary>
    Initializing,

    /// <summary>
    /// 完成初始化
    /// </summary>
    Initialized,

    /// <summary>
    /// 正在执行
    /// </summary>
    Executing,
    
    /// <summary>
    /// 执行完成
    /// </summary>
    Executed,


    /// <summary>
    /// 正在取消
    /// </summary>
    Cancelling,
    
    /// <summary>
    /// 非正常结束，可能是初始化出错、执行出错或已取消
    /// </summary>
    Canceled,
}