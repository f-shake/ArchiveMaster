using ArchiveMaster.Helpers;
using ImageMagick;

namespace ArchiveMaster.Tests.PhotoTools;

public class HeifEncoderTests
{
    [Theory]
    [InlineData(100u, 50u, 96u, 50u)]
    [InlineData(10u, 10u, 16u, 10u)]
    [InlineData(4000u, 4000u, 4000u, 4000u)]
    [InlineData(4000u, 4001u, 4000u, 4001u)]
    public void GetTargetSize_AlignsSingleImageWidthAndKeepsGridSize(uint width, uint height,
        uint expectedWidth, uint expectedHeight)
    {
        var result = HeifEncoder.GetTargetSize(width, height);

        Assert.Equal(expectedWidth, result.Width);
        Assert.Equal(expectedHeight, result.Height);
    }

    [Theory]
    [InlineData("veryfast", "fast")]
    [InlineData("faster", "fast")]
    [InlineData("slow", "slow")]
    [InlineData("unknown", "unknown")]
    [InlineData(null, null)]
    public void NormalizePreset_MapsAliases(string input, string expected)
    {
        Assert.Equal(expected, HeifEncoder.NormalizePreset(input));
    }

    [Theory]
    [InlineData(MagickFormat.Heic, true)]
    [InlineData(MagickFormat.Heif, true)]
    [InlineData(MagickFormat.Jpg, false)]
    public void IsHeifFormat_RecognizesHeicAndHeif(MagickFormat format, bool expected)
    {
        Assert.Equal(expected, HeifEncoder.IsHeifFormat(format));
    }
}
