using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ArchiveMaster.Helpers;

/// <summary>
/// 缩放后把 XMP 里记录的尺寸同步成实际像素尺寸（EXIF 的同类同步在
/// <c>PhotoSlimmingService.SyncDimensionMetadata</c> 里用 Magick.NET 的 ExifProfile 做）。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ImageMagick.MagickImage.AdaptiveResize"/> 只改像素，不会改 XMP 里 GPano 的全景尺寸；
/// 而这个 profile 是原样透传给编码器的。不同步就会出现"元数据写着 17304x8652、像素实际 15000x7500"
/// 这类错位，360 全景播放器会按错误的满景尺寸换算经度；XMP 里的 <c>dc:format</c> 也会还停在源图的格式上。
/// </para>
/// <para>
/// 只做原地改写、不重建结构；找不到对应字段或解析出任何问题，都原样返回传入的字节，
/// 绝不因为元数据把输出图片搞坏。
/// </para>
/// </remarks>
public static class ImageMetadataHelper
{
    /// <summary>XMP 必须是合法 UTF-8；用会抛异常的编码器，这样非法内容会走"原样返回"而不是被替换成 U+FFFD。</summary>
    private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

    /// <summary>
    /// 把 XMP 里 GPano 的全景尺寸同步到 <paramref name="width"/> × <paramref name="height"/>，
    /// 并把 <c>dc:format</c> 改成 <paramref name="mimeType"/>。
    /// </summary>
    /// <returns>改写后的字节；没有任何字段需要改时返回传入的同一个数组实例。</returns>
    public static byte[] SyncXmpDimensions(byte[] xmp, uint width, uint height, string mimeType)
    {
        if (xmp is not { Length: > 0 })
        {
            return xmp;
        }

        try
        {
            string text = StrictUtf8.GetString(xmp);
            string original = text;

            // 关键区分：CroppedAreaImage*Pixels 是"这张图覆盖了多少像素"，FullPano*Pixels 是"整张全景有多少像素"，
            // 两者只在完整 360° 全景时相等（实测那张全景 17304==17304）。部分全景（裁切过的）里
            // FullPano > CroppedArea，播放器靠它们的比值算视野角——直接写成图片尺寸会把这台部分全景
            // 当成完整 360° 渲染，画面被横向拉伸。
            // 所以 FullPano 和裁切偏移都要按"新尺寸/原裁切区尺寸"同比例缩放，不能直接赋值。
            uint? oldCroppedWidth = ReadXmpValue(text, "GPano:CroppedAreaImageWidthPixels");
            uint? oldCroppedHeight = ReadXmpValue(text, "GPano:CroppedAreaImageHeightPixels");

            // 只有拿到旧的裁切区尺寸，才能按比例推算其余字段。拿不到（缺失或为 0）时整块 GPano 都不动：
            // 比率无从推算，若只把裁切区改成新尺寸、把 FullPano 和裁切偏移留在旧尺度，
            // 就会制造出字段间自相矛盾的元数据——正是本方法要消除的那类问题
            if (oldCroppedWidth is > 0 && oldCroppedHeight is > 0)
            {
                text = ScaleXmpValue(text, "GPano:FullPanoWidthPixels", width, oldCroppedWidth.Value);
                text = ScaleXmpValue(text, "GPano:FullPanoHeightPixels", height, oldCroppedHeight.Value);
                text = ScaleXmpValue(text, "GPano:CroppedAreaLeftPixels", width, oldCroppedWidth.Value);
                text = ScaleXmpValue(text, "GPano:CroppedAreaTopPixels", height, oldCroppedHeight.Value);

                // 裁切区尺寸就等于输出图片自身的尺寸
                text = ReplaceXmpValue(text, "GPano:CroppedAreaImageWidthPixels", width);
                text = ReplaceXmpValue(text, "GPano:CroppedAreaImageHeightPixels", height);
            }

            // dc:format 记的是源图的格式（实测 HEIC 里写着 image/jpeg，就是源 JPEG 原样带过来的）
            if (!string.IsNullOrEmpty(mimeType))
            {
                text = Regex.Replace(text, "(<dc:format>)[^<]*(</dc:format>)", "${1}" + mimeType + "${2}");
                text = Regex.Replace(text, "(dc:format\\s*=\\s*\")([^\"]*)(\")", "${1}" + mimeType + "${3}");
            }

            return text == original ? xmp : StrictUtf8.GetBytes(text);
        }
        catch
        {
            return xmp;
        }
    }

    /// <summary>
    /// 把一个内嵌缩略图（JPEG 字节）追加到 EXIF 末尾，并让 IFD0 的"下一个 IFD"指针指向它。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 为什么需要它：<see cref="ImageMagick.ExifProfile"/> 一旦调用过 SetValue 就会重建整个 EXIF，
    /// 而它的序列化<b>不输出 IFD1（内嵌缩略图）</b>；实测 25,194 字节的源 EXIF 会缩到 1,104 字节、
    /// 缩略图 JPEG 完全消失。改之前 EXIF 是原始字节透传，缩略图是保留的，所以这是重建带来的数据丢失。
    /// Magick.NET 只提供 RemoveThumbnail、没有 SetThumbnail，所以只能自己补回去。
    /// </para>
    /// <para>
    /// 做法是把缩略图跟一个最小的 IFD1（0x0103 Compression=6、0x0201 ThumbnailOffset、0x0202 ThumbnailLength）
    /// <b>追加到末尾</b>：追加不会移动任何已有字节，因此不需要修正任何现有偏移，只改 IFD0 里那个
    /// "下一个 IFD"指针即可。
    /// </para>
    /// </remarks>
    /// <param name="exif">重建后的 EXIF 字节（可带 Exif\0\0 前缀）。</param>
    /// <param name="thumbnail">要补回去的缩略图 JPEG 字节。</param>
    /// <returns>补好缩略图的字节；输入不合法时原样返回 <paramref name="exif"/>。</returns>
    public static byte[] AppendExifThumbnail(byte[] exif, byte[] thumbnail)
    {
        if (exif is not { Length: > 8 } || thumbnail is not { Length: > 0 })
        {
            return exif;
        }

        try
        {
            // 先把长度判断做完再索引，避免"先读后判"的写法
            bool hasPrefix = exif.Length >= 6
                             && exif[0] == (byte)'E' && exif[1] == (byte)'x' && exif[2] == (byte)'i'
                             && exif[3] == (byte)'f' && exif[4] == 0 && exif[5] == 0;
            int tiff = hasPrefix ? 6 : 0;
            if (exif.Length < tiff + 8)
            {
                return exif;
            }

            bool bigEndian = exif[tiff] == (byte)'M' && exif[tiff + 1] == (byte)'M';
            bool littleEndian = exif[tiff] == (byte)'I' && exif[tiff + 1] == (byte)'I';
            if (!bigEndian && !littleEndian)
            {
                return exif;
            }

            int ifd0 = tiff + (int)ReadUInt32(exif, tiff + 4, bigEndian);
            if (ifd0 + 2 > exif.Length)
            {
                return exif;
            }

            int entryCount = ReadUInt16(exif, ifd0, bigEndian);
            // IFD0 的"下一个 IFD"指针紧跟在条目数组之后
            int nextPointer = ifd0 + 2 + entryCount * 12;
            if (nextPointer + 4 > exif.Length)
            {
                return exif;
            }

            int thumbnailOffset = exif.Length;
            int ifd1Offset = exif.Length + thumbnail.Length;

            // 最小 IFD1：3 个条目 + 结束指针 = 2 + 36 + 4 = 42 字节。
            // 除缩略图偏移/长度，还要写 Compression=6（JPEG）——按规范它缺省是 1（未压缩），
            // 不写的话严格的第三方读图器可能据此忽略缩略图。
            byte[] result = new byte[exif.Length + thumbnail.Length + 42];
            exif.CopyTo(result, 0);
            thumbnail.CopyTo(result, exif.Length);

            // 注意 IFD 里存的偏移是相对 **TIFF 头**的（不含 Exif\0\0 那 6 字节前缀），所以要减掉 tiff
            int p = ifd1Offset;
            WriteUInt16(result, p, 3, bigEndian); p += 2;
            p = WriteExifShortEntry(result, p, 0x0103, 6, bigEndian);
            p = WriteExifLongEntry(result, p, 0x0201, (uint)(thumbnailOffset - tiff), bigEndian);
            p = WriteExifLongEntry(result, p, 0x0202, (uint)thumbnail.Length, bigEndian);
            WriteUInt32(result, p, 0, bigEndian);

            WriteUInt32(result, nextPointer, (uint)(ifd1Offset - tiff), bigEndian);
            return result;
        }
        catch
        {
            return exif;
        }
    }

    /// <summary>写一个 SHORT 类型的单值 IFD 条目，返回下一个条目的偏移。</summary>
    private static int WriteExifShortEntry(byte[] buffer, int offset, ushort tag, ushort value, bool bigEndian)
    {
        WriteUInt16(buffer, offset, tag, bigEndian);
        WriteUInt16(buffer, offset + 2, 3, bigEndian);   // 类型 3 = SHORT
        WriteUInt32(buffer, offset + 4, 1, bigEndian);   // 数量 1
        WriteUInt16(buffer, offset + 8, value, bigEndian);
        buffer[offset + 10] = 0;                         // 值域是 4 字节，SHORT 占前 2 字节，剩下的填充位清零
        buffer[offset + 11] = 0;
        return offset + 12;
    }

    /// <summary>写一个 LONG 类型的单值 IFD 条目，返回下一个条目的偏移。</summary>
    private static int WriteExifLongEntry(byte[] buffer, int offset, ushort tag, uint value, bool bigEndian)
    {
        WriteUInt16(buffer, offset, tag, bigEndian);
        WriteUInt16(buffer, offset + 2, 4, bigEndian);   // 类型 4 = LONG
        WriteUInt32(buffer, offset + 4, 1, bigEndian);   // 数量 1
        WriteUInt32(buffer, offset + 8, value, bigEndian);
        return offset + 12;
    }

    private static ushort ReadUInt16(byte[] buffer, int offset, bool bigEndian)
    {
        return bigEndian
            ? (ushort)((buffer[offset] << 8) | buffer[offset + 1])
            : (ushort)((buffer[offset + 1] << 8) | buffer[offset]);
    }

    private static uint ReadUInt32(byte[] buffer, int offset, bool bigEndian)
    {
        return bigEndian
            ? ((uint)buffer[offset] << 24) | ((uint)buffer[offset + 1] << 16) | ((uint)buffer[offset + 2] << 8) | buffer[offset + 3]
            : ((uint)buffer[offset + 3] << 24) | ((uint)buffer[offset + 2] << 16) | ((uint)buffer[offset + 1] << 8) | buffer[offset];
    }

    private static void WriteUInt16(byte[] buffer, int offset, ushort value, bool bigEndian)
    {
        if (bigEndian)
        {
            buffer[offset] = (byte)(value >> 8);
            buffer[offset + 1] = (byte)value;
        }
        else
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
        }
    }

    private static void WriteUInt32(byte[] buffer, int offset, uint value, bool bigEndian)
    {
        if (bigEndian)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }
        else
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }
    }

    /// <summary>读取 XMP 中一个数值字段，属性写法和元素写法都支持；不是数字或不存在则返回 null。</summary>
    private static uint? ReadXmpValue(string text, string name)
    {
        string escaped = Regex.Escape(name);
        Match match = Regex.Match(text, "(" + escaped + "\\s*=\\s*\")([^\"]*)(\")");
        if (!match.Success)
        {
            match = Regex.Match(text, "(<" + escaped + ">)([^<]*)(</" + escaped + ">)");
        }

        return match.Success
               && uint.TryParse(match.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint value)
            ? value
            : null;
    }

    /// <summary>
    /// 按 <paramref name="newBase"/> / <paramref name="oldBase"/> 的比例缩放 XMP 里一个数值字段。
    /// 用于 GPano 的满景尺寸与裁切偏移——它们必须和裁切区尺寸同比例变化，否则视野角会错。
    /// </summary>
    private static string ScaleXmpValue(string text, string name, uint newBase, uint oldBase)
    {
        if (ReadXmpValue(text, name) is not { } oldValue)
        {
            return text;
        }

        long scaled = (long)Math.Round((double)oldValue * newBase / oldBase);
        return ReplaceXmpValue(text, name, (uint)Math.Clamp(scaled, 0L, uint.MaxValue));
    }

    /// <summary>改写 XMP 中一个字段的值，属性写法（name="值"）和元素写法（&lt;name&gt;值&lt;/name&gt;）都支持。</summary>
    private static string ReplaceXmpValue(string text, string name, uint value)
    {
        string literal = value.ToString(CultureInfo.InvariantCulture);
        string escaped = Regex.Escape(name);
        text = Regex.Replace(text, "(" + escaped + "\\s*=\\s*\")([^\"]*)(\")", "${1}" + literal + "${3}");
        return Regex.Replace(text, "(<" + escaped + ">)[^<]*(</" + escaped + ">)", "${1}" + literal + "${2}");
    }
}
