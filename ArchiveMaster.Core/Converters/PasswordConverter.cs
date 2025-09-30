// using System.Globalization;
// using ArchiveMaster.Services;
// using Avalonia.Data.Converters;
//
// namespace ArchiveMaster.Converters;
//
// public class PasswordConverter : IValueConverter
// {
//     public static string TempMainPassword = "hello world";
//     public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
//     {
//         if (value is null)
//         {
//             return null;
//         }
//
//         if (value is not string str)
//         {
//             throw new ArgumentException("参数必须是字符串");
//         }
//
//         return SecurePasswordStoreService.LoadPassword(str, TempMainPassword);
//     }
//
//     public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
//     {
//         if (value is null)
//         {
//             return null;
//         }
//
//         if (value is not string str)
//         {
//             throw new ArgumentException("参数必须是字符串");
//         }
//         
//         return SecurePasswordStoreService.SavePassword(str, TempMainPassword);
//     }
// }