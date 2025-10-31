using ExifLibrary;
using ImageMagick;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Serilog;
using ExifTag = ExifLibrary.ExifTag;

namespace ArchiveMaster.Helpers;

public static class ExifHelper
{
    public static (int degrees, int minutes, double seconds) ConvertToDmsTuple(double decimalDegrees)
    {
        // 取绝对值（方向由外部处理）
        decimalDegrees = Math.Abs(decimalDegrees);

        // 计算度、分、秒
        int degrees = (int)decimalDegrees;
        double remainingMinutes = (decimalDegrees - degrees) * 60;
        int minutes = (int)remainingMinutes;
        double seconds = (remainingMinutes - minutes) * 60;

        // 处理浮点误差（如 59.9999 秒进位）
        if (seconds >= 59.999)
        {
            seconds = 0;
            minutes++;
            if (minutes >= 60)
            {
                minutes = 0;
                degrees++;
            }
        }

        return (degrees, minutes, seconds);
    }

    public static DateTime? FindExifTime(string file)
    {
        IReadOnlyList<MetadataExtractor.Directory> directories = ImageMetadataReader.ReadMetadata(file);

        foreach (var dir in directories.Where(p => p.Name == "Exif SubIFD"))
        {
            if (dir.TryGetDateTime(36867, out DateTime time1))
            {
                return time1;
            }

            if (dir.TryGetDateTime(36868, out DateTime time2))
            {
                return time2;
            }
        }

        MetadataExtractor.Directory dir2 = null;
        if ((dir2 = directories.FirstOrDefault(p => p.Name == "Exif IFD0")) != null)
        {
            if (dir2.TryGetDateTime(306, out DateTime time))
            {
                return time;
            }
        }

        return null;
    }

    /// <summary>
    /// 从图片文件中读取 GPS 坐标（纬度、经度）
    /// </summary>
    /// <param name="file">图片文件路径</param>
    /// <returns>返回 (纬度, 经度) 元组，如果未找到则返回 null</returns>
    public static (double lat, double lon)? FindGps(string file)
    {
        try
        {
            // 1. 使用 MetadataExtractor 读取 GPS 信息
            var directories = ImageMetadataReader.ReadMetadata(file);
            var gpsDir = directories.OfType<GpsDirectory>().FirstOrDefault();

            if (gpsDir != null && gpsDir.TryGetGeoLocation(out var gps) && !gps.IsZero)
            {
                return (gps.Latitude, gps.Longitude);
            }

            //2. 使用更全面的 ImageMagisk 读取，速度慢很多
            using var image = new MagickImage(file);
            var profile = image.GetExifProfile();
            if (profile == null)
            {
                return null;
            }

            // 读取GPS纬度
            var latitude = profile.GetValue(ImageMagick.ExifTag.GPSLatitude);
            var latitudeRef = profile.GetValue(ImageMagick.ExifTag.GPSLatitudeRef);

            // 读取GPS经度
            var longitude = profile.GetValue(ImageMagick.ExifTag.GPSLongitude);
            var longitudeRef = profile.GetValue(ImageMagick.ExifTag.GPSLongitudeRef);

            if (latitude != null && longitude != null)
            {
                double lat = ConvertRationalToDouble(latitude.Value, latitudeRef?.Value == "S");
                double lon = ConvertRationalToDouble(longitude.Value, longitudeRef?.Value == "W");
                return (lat, lon);
            }

            return null;
        }
        catch (Exception ex)
        {
            Log.Logger.Warning("读取 {File} 的 GPS 信息失败: {ExMessage}", file, ex.Message);
            return null;
        }
    }

    public static void WriteGpsToImage(string filePath, double lat, double lon)
    {
        var imageFile = ImageFile.FromFile(filePath);

        // 写入纬度（需转换为 EXIF 格式：度/分/秒）
        var (d, m, s) = ConvertToDmsTuple(lat);
        imageFile.Properties.Set(ExifTag.GPSLatitude, d, m, (float)s);
        imageFile.Properties.Set(ExifTag.GPSLatitudeRef, lat >= 0 ? "N" : "S");

        // 写入经度
        (d, m, s) = ConvertToDmsTuple(lon);
        imageFile.Properties.Set(ExifTag.GPSLongitude, d, m, (float)s);
        imageFile.Properties.Set(ExifTag.GPSLongitudeRef, lon >= 0 ? "E" : "W");

        // 保存文件
        imageFile.Save(filePath);
    }

    /// <summary>
    /// 将度分秒格式转换为十进制坐标
    /// </summary>
    private static double ConvertDmsToDecimal(double degrees, double minutes, double seconds, bool isPositive)
    {
        double decimalDegrees = degrees + (minutes / 60) + (seconds / 3600);
        return isPositive ? decimalDegrees : -decimalDegrees;
    }

    private static double ConvertRationalToDouble(ImageMagick.Rational[] rationals, bool isNegative)
    {
        if (rationals is not { Length: 3 })
        {
            return 0;
        }

        double degrees = rationals[0].ToDouble();
        double minutes = rationals[1].ToDouble();
        double seconds = rationals[2].ToDouble();

        double result = degrees + (minutes / 60) + (seconds / 3600);
        return isNegative ? -result : result;
    }
}