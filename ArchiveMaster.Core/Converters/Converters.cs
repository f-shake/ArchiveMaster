using FzLib.Avalonia.Converters;

namespace ArchiveMaster.Converters;

public static class Converters
{
    public static readonly NumberToAlignmentConverter Alignment = FzLib.Avalonia.Converters.Converters.Alignment;

    public static readonly BoolLogicConverter AndLogic = FzLib.Avalonia.Converters.Converters.AndLogic;

    public static readonly BoolToFontWeightConverter
        BoldFontWeight = FzLib.Avalonia.Converters.Converters.BoldFontWeight;

    public static readonly BoolToOpacityStyleConverter BoldOpacity = FzLib.Avalonia.Converters.Converters.BoldOpacity;

    public static readonly CountToBoolConverter CountGreaterThanZero =
        FzLib.Avalonia.Converters.Converters.CountGreaterThanZero;

    public static readonly CountToBoolConverter CountIsZero = FzLib.Avalonia.Converters.Converters.CountIsZero;

    public static readonly DescriptionConverter Description = FzLib.Avalonia.Converters.Converters.Description;

    public static readonly EqualToBoolConverter EqualWithParameter =
        FzLib.Avalonia.Converters.Converters.EqualWithParameter;

    public static readonly FileLengthConverter FileLength = FzLib.Avalonia.Converters.Converters.FileLength;
    
    public static readonly TransferSpeedConverter TransferSpeed = FzLib.Avalonia.Converters.Converters.TransferSpeed;

    public static readonly FilePickerFilterConverter FilePickerFilter =
        FzLib.Avalonia.Converters.Converters.FilePickerFilter;

    public static readonly InverseBoolConverter InverseBool = FzLib.Avalonia.Converters.Converters.InverseBool;

    public static readonly NullToBoolConverter IsNotNull = FzLib.Avalonia.Converters.Converters.IsNotNull;

    public static readonly NullToBoolConverter IsNull = FzLib.Avalonia.Converters.Converters.IsNull;

    public static readonly BoolToFontStyleConverter ItalicFontStyle =
        FzLib.Avalonia.Converters.Converters.ItalicFontStyle;

    public static readonly BoolToFontWeightConverter LightFontWeight =
        FzLib.Avalonia.Converters.Converters.LightFontWeight;

    public static readonly EqualToBoolConverter NotEqualWithParameter =
        FzLib.Avalonia.Converters.Converters.NotEqualWithParameter;

    public static readonly BoolLogicConverter OrLogic = FzLib.Avalonia.Converters.Converters.OrLogic;

    public static readonly StringListConverter StringList = FzLib.Avalonia.Converters.Converters.StringList;

    public static readonly BoolToTextWrappingConverter TextWrapping = FzLib.Avalonia.Converters.Converters.TextWrapping;

    public static readonly NumberToThicknessConverter Thickness = FzLib.Avalonia.Converters.Converters.Thickness;

    public static readonly TimeSpanConverter TimeSpan = FzLib.Avalonia.Converters.Converters.TimeSpan;

    public static readonly TimeSpanNumberConverter TimeSpanNumber = FzLib.Avalonia.Converters.Converters.TimeSpanNumber;

    public static readonly BoolToTextDecorationConverter UnderlineTextDecoration =
        FzLib.Avalonia.Converters.Converters.UnderlineTextDecoration;

    public static readonly BoolToTextDecorationConverter OverlineTextDecoration =
        FzLib.Avalonia.Converters.Converters.OverlineTextDecoration;

    public static readonly BitmapAssetValueConverter BitmapAssetValue = new();

    public static readonly DateTimeConverter DateTime = new();

    public static readonly ValueMappingConverter LogMap = new()
    {
        Map = new Dictionary<string, string>()
        {
            { "Trace", "细节" },
            { "Debug", "调试" },
            { "Information", "信息" },
            { "Warning", "警告" },
            { "Error", "错误" },
            { "Critical", "关键" },
            { "None", "全部" }
        }
    };

    public static readonly TupleConverter Tuple = new();

    public static readonly FileFilterDescriptionConverter FileFilter = new();
    
    // public static readonly  PasswordConverter Password = new();
}