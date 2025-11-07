using System.ComponentModel;

namespace ArchiveMaster.Enums;

public enum TableTextType
{
    /// <summary>
    /// 制表符（\t）
    /// </summary>
    [Description("制表符（\t）")]
    TabDelimited,

    /// <summary>
    /// 逗号（,）
    /// </summary>
    [Description("逗号（,）")]
    Csv,

    /// <summary>
    /// 空格
    /// </summary>
    [Description("空格")]
    SpaceDelimited,

    /// <summary>
    /// JSON
    /// </summary>
    [Description("JSON")]
    Json,

    /// <summary>
    /// XML
    /// </summary>
    [Description("XML")]
    Xml,

    /// <summary>
    /// HTML
    /// </summary>
    [Description("HTML")]
    Html
}