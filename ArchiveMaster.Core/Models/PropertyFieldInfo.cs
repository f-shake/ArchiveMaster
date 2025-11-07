using System.Reflection;

namespace ArchiveMaster.Models;

public class PropertyFieldInfo
{
    public PropertyFieldInfo()
    {
    }

    public PropertyFieldInfo(string propertyName, string fieldName, Func<object, string> converter = null)
    {
        PropertyName = propertyName;
        FieldName = fieldName;
        Converter = converter;
    }

    public PropertyFieldInfo(string propertyName, Func<object, string> converter = null)
    {
        PropertyName = propertyName;
        Converter = converter;
    }

    public string PropertyName { get; init; }
    public string FieldName { get; init; }
    public Func<object, string> Converter { get; init; }
    internal PropertyInfo PropertyInfo { get; set; }
}