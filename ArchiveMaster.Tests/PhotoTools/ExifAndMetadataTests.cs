using System.Text;
using ArchiveMaster.Helpers;
using ArchiveMaster.Tests.Infrastructure;

namespace ArchiveMaster.Tests.PhotoTools;

public class ExifAndMetadataTests : IDisposable
{
    private readonly TempWorkspace workspace = new();

    public void Dispose()
    {
        workspace.Dispose();
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(12.5, 12, 30, 0)]
    [InlineData(-45.25, 45, 15, 0)]
    public void ConvertToDmsTuple_ConvertsDecimalDegrees(double input, int degrees, int minutes, double seconds)
    {
        var result = ExifHelper.ConvertToDmsTuple(input);

        Assert.Equal(degrees, result.degrees);
        Assert.Equal(minutes, result.minutes);
        Assert.Equal(seconds, result.seconds, 3);
    }

    [Fact]
    public void ConvertToDmsTuple_CarriesSixtySeconds()
    {
        var result = ExifHelper.ConvertToDmsTuple(12.9999999);

        Assert.Equal(13, result.degrees);
        Assert.Equal(0, result.minutes);
        Assert.Equal(0, result.seconds, 3);
    }

    [Fact]
    public void ExifTime_RoundTrips()
    {
        var time = new DateTime(2020, 1, 2, 3, 4, 5);
        string path = ImageFactory.CreateJpeg(workspace.PathOf("time.jpg"), exifTime: time);

        DateTime? actual = ExifHelper.FindExifTime(path);

        Assert.NotNull(actual);
        Assert.Equal(time, actual.Value);
    }

    [Fact]
    public void Gps_RoundTrips()
    {
        string path = ImageFactory.CreateJpeg(workspace.PathOf("gps.jpg"), latitude: 31.2304, longitude: 121.4737);

        var actual = ExifHelper.FindGps(path);

        Assert.NotNull(actual);
        Assert.Equal(31.2304, actual.Value.lat, 3);
        Assert.Equal(121.4737, actual.Value.lon, 3);
    }

    [Fact]
    public void SyncXmpDimensions_UpdatesCroppedAndFullPanoAndFormat()
    {
        string xmpText = """
                         <x:xmpmeta xmlns:x="adobe:ns:meta/">
                           <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
                             <rdf:Description xmlns:GPano="http://ns.google.com/photos/1.0/panorama/"
                               xmlns:dc="http://purl.org/dc/elements/1.1/"
                               GPano:CroppedAreaImageWidthPixels="100"
                               GPano:CroppedAreaImageHeightPixels="50"
                               GPano:FullPanoWidthPixels="200"
                               GPano:FullPanoHeightPixels="100"
                               GPano:CroppedAreaLeftPixels="10"
                               GPano:CroppedAreaTopPixels="5"
                               dc:format="image/jpeg" />
                           </rdf:RDF>
                         </x:xmpmeta>
                         """;
        byte[] input = Encoding.UTF8.GetBytes(xmpText);

        byte[] result = ImageMetadataHelper.SyncXmpDimensions(input, 400, 200, "image/heic");
        string text = Encoding.UTF8.GetString(result);

        Assert.Contains("GPano:CroppedAreaImageWidthPixels=\"400\"", text);
        Assert.Contains("GPano:CroppedAreaImageHeightPixels=\"200\"", text);
        Assert.Contains("GPano:FullPanoWidthPixels=\"800\"", text);
        Assert.Contains("GPano:FullPanoHeightPixels=\"400\"", text);
        Assert.Contains("dc:format=\"image/heic\"", text);
    }

    [Fact]
    public void SyncXmpDimensions_WithoutPano_ReturnsSameInstance()
    {
        byte[] input = Encoding.UTF8.GetBytes("<xmp><dc:format>image/jpeg</dc:format></xmp>");

        byte[] result = ImageMetadataHelper.SyncXmpDimensions(input, 100, 100, null);

        Assert.Same(input, result);
    }

    [Fact]
    public void SyncXmpDimensions_InvalidUtf8_ReturnsSameInstance()
    {
        byte[] input = [0xFF, 0xFE, 0x00];

        byte[] result = ImageMetadataHelper.SyncXmpDimensions(input, 100, 100, "image/heic");

        Assert.Same(input, result);
    }
}
