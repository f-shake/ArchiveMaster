using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ImageMagick;

namespace ArchiveMaster.ViewModels.FileSystem;

public class ImageFileInfo : SimpleFileInfo
{
    public static int MaxPixels = 500_000;
    
    private bool imageLoaded;

    private bool isLoadingImage;

    public ImageFileInfo(FileInfo file, string topDir) : base(file, topDir)
    {
    }

    public ImageFileInfo() : base()
    {
    }

    public Bitmap ThumbnailImage
    {
        get
        {
            if (imageLoaded)
            {
                return field;
            }

            if (isLoadingImage)
            {
                return null;
            }

            _ = LoadImageAsync();
            return null;
        }
        private set => SetProperty(ref field, value);
    }

    private async Task LoadImageAsync()
    {
        isLoadingImage = true;
        Bitmap newImage = null;
        await Task.Run(() =>
        {
            try
            {
                if (Path == null)
                {
                    return;
                }

                var file = new FileInfo(Path);
                if (!file.Exists)
                {
                    return;
                }

                try
                {
                    using var magickImage = new MagickImage(file);
                    long currentPixels = (long)magickImage.Width * magickImage.Height;

                    if (currentPixels > MaxPixels)
                    {
                        double ratio = Math.Sqrt((double)MaxPixels / currentPixels);
                        magickImage.Resize(new Percentage(ratio * 100), FilterType.Lanczos);
                    }

                    magickImage.Format = MagickFormat.Jpeg;
                    using var ms = new MemoryStream();
                    magickImage.Write(ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    newImage = new Bitmap(ms);
                }
                catch (Exception ex)
                {
                    return;
                }
            }
            finally
            {
                isLoadingImage = false;
                imageLoaded = true;
                if (newImage != null)
                {
                    Dispatcher.UIThread.Invoke(() => { ThumbnailImage = newImage; });
                }
            }
        });
    }
}