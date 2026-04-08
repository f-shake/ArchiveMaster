using ArchiveMaster.Services;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ImageMagick;

namespace ArchiveMaster.ViewModels.FileSystem;

public class ImageFileInfo : SimpleFileInfo, IDisposable
{
    public static int MaxPixels = 500_000;

    private bool imageLoaded;

    private bool isLoadingImage;

    private Bitmap thumbnailImage;

    public ImageFileInfo(FileInfo file, string topDir) : base(file, topDir)
    {
    }

    public ImageFileInfo() : base()
    {
    }

    public ImageFileInfo(string relativePath, string topDir) : base(relativePath, topDir)
    {
    }

    public Bitmap ThumbnailImage
    {
        get
        {
            if (imageLoaded)
            {
                return thumbnailImage;
            }

            if (isLoadingImage || !ExistsFile)
            {
                return null;
            }

            ThumbnailScheduler.Enqueue(this, LoadImage);
            return null;
        }
        private set => SetProperty(ref thumbnailImage, value);
    }

    private void LoadImage()
    {
        isLoadingImage = true;
        Bitmap newImage = null;
        try
        {
            if (Path == null)
            {
                return;
            }

            if (!ExistsFile)
            {
                return;
            }

            try
            {
                using var magickImage = new MagickImage(Path);
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
    }

    public void Dispose()
    {
        thumbnailImage?.Dispose();
        GC.SuppressFinalize(this);
    }

    ~ImageFileInfo()
    {
        Dispose();
    }
}