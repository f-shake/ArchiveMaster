using ArchiveMaster.Helpers;
using ImageMagick;

namespace ArchiveMaster.Tests.Infrastructure;

public static class ImageFactory
{
    public static string CreateJpeg(string path, int width = 64, int height = 48, DateTime? exifTime = null,
        double? latitude = null, double? longitude = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using (var image = new MagickImage(MagickColors.White, (uint)width, (uint)height))
        {
            if (exifTime.HasValue)
            {
                var exif = new ExifProfile();
                string value = exifTime.Value.ToString("yyyy:MM:dd HH:mm:ss");
                exif.SetValue(ImageMagick.ExifTag.DateTimeOriginal, value);
                exif.SetValue(ImageMagick.ExifTag.DateTimeDigitized, value);
                image.SetProfile(exif);
            }

            image.Format = MagickFormat.Jpeg;
            image.Write(path);
        }

        if (latitude.HasValue && longitude.HasValue)
        {
            ExifHelper.WriteGpsToImage(path, latitude.Value, longitude.Value);
        }

        return path;
    }

    public static string CreatePng(string path, int width = 32, int height = 32)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var image = new MagickImage(MagickColors.Red, (uint)width, (uint)height);
        image.Format = MagickFormat.Png;
        image.Write(path);
        return path;
    }
}
