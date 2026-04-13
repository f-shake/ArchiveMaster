using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Globalization;
using System;
using System.Reflection;
using System.Runtime.InteropServices.Marshalling;
using ImageMagick;

namespace ArchiveMaster.Converters
{
    public class BitmapAssetValueConverter : IValueConverter
    {
        public static BitmapAssetValueConverter Instance = new BitmapAssetValueConverter();

        public bool ReturnNullIfError { get; set; } = true;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return null;
            }

            if (value is string filePath)
            {
                var file = new FileInfo(filePath);
                if (!file.Exists)
                {
                    return ReturnNullIfError ? null : throw new FileNotFoundException(file.FullName);
                }

                if (file.Extension.Equals(".jpg", StringComparison.InvariantCultureIgnoreCase)
                    || file.Extension.Equals(".jpeg", StringComparison.InvariantCultureIgnoreCase)
                    || file.Extension.Equals(".png", StringComparison.InvariantCultureIgnoreCase)
                    || file.Extension.Equals(".gif", StringComparison.InvariantCultureIgnoreCase)
                    || file.Extension.Equals(".bmp", StringComparison.InvariantCultureIgnoreCase)
                    || file.Extension.Equals(".tiff", StringComparison.InvariantCultureIgnoreCase)
                    || file.Extension.Equals(".tif", StringComparison.InvariantCultureIgnoreCase))
                {
                    return new Bitmap(file.FullName);
                }

                try
                {
                    using var image = new MagickImage(file);
                    long targetPixels = 2_000_000;
                    long currentPixels = (long)image.Width * image.Height;

                    if (currentPixels > targetPixels)
                    {
                        double ratio = Math.Sqrt((double)targetPixels / currentPixels);
                        image.Resize(new Percentage(ratio * 100), FilterType.Lanczos);
                    }

                    image.Format = MagickFormat.Jpeg;
                    using var ms = new MemoryStream();
                    image.Write(ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    return new Bitmap(ms);
                }
                catch (Exception ex)
                {
                    return ReturnNullIfError ? null : throw new InvalidOperationException($"无法加载图片文件 {file.FullName}", ex);
                }
            }

            return ReturnNullIfError ? null : throw new ArgumentException($"无法将{value}转换为Bitmap");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}