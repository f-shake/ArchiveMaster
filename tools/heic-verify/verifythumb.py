"""检查 EXIF 里的内嵌缩略图（IFD1）结构是否完整。

用法：
    python verifythumb.py <EXIF 原始字节文件>
    python verifythumb.py <HEIC 文件>          # 自动取文件里最后一段 Exif

为什么需要它：Magick.NET 的 ExifProfile 一旦调用过 SetValue 就会重建整个 EXIF，
而它**不输出 IFD1**——实测 25,194 字节的源 EXIF 会缩到 1,104 字节、缩略图整个消失，
真机表现就是相册不再"秒出缩略图"。AppendExifThumbnail 会把缩略图补回末尾，
这个脚本用来确认补回的结构合法。

注意：IFD 里存的偏移是相对 **TIFF 头**的（不含 Exif\\0\\0 那 6 字节前缀），
读数据时要加回这 6 字节；这是实现时踩过的坑。
"""
import struct
import sys
from pathlib import Path

DIM_TAGS = {0x0100: "ImageWidth", 0x0101: "ImageLength",
            0xA002: "PixelXDimension", 0xA003: "PixelYDimension"}


def load_blob(path):
    data = path.read_bytes()
    if data[:6] == b"Exif\x00\x00":
        return data
    j = data.rfind(b"Exif\x00\x00")
    if j < 0:
        return None
    return data[j:]


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 1

    blob = load_blob(Path(sys.argv[1]))
    if blob is None:
        print("找不到 Exif\\0\\0 标记")
        return 1

    print(f"EXIF {len(blob):,} 字节，起始 {blob[:6]}")
    tiff = 6
    endian = "<" if blob[tiff:tiff + 2] == b"II" else ">"
    print(f"TIFF 头 {blob[tiff:tiff + 8].hex()}  端序={'小端' if endian == '<' else '大端'}")

    found = {}
    dims = []

    def walk(off, label, depth=0):
        if off <= 0 or tiff + off + 2 > len(blob):
            return
        count = struct.unpack(endian + "H", blob[tiff + off:tiff + off + 2])[0]
        nxt = struct.unpack(endian + "I", blob[tiff + off + 2 + count * 12:tiff + off + 6 + count * 12])[0]
        print(f"-- {label}: {count} 条, 下一个 IFD = {nxt}")
        for k in range(count):
            q = tiff + off + 2 + k * 12
            tag, typ, cnt = struct.unpack(endian + "HHI", blob[q:q + 8])
            raw = blob[q + 8:q + 12]
            if tag in DIM_TAGS:
                value = struct.unpack(endian + "H", raw[:2])[0] if typ == 3 \
                    else struct.unpack(endian + "I", raw[:4])[0]
                dims.append((DIM_TAGS[tag], value))
                print(f"     0x{tag:04x} type={typ}  {DIM_TAGS[tag]} = {value}")
            elif tag in (0x0201, 0x0202, 0x0103):
                value = struct.unpack(endian + "I", raw[:4])[0] if typ == 4 \
                    else struct.unpack(endian + "H", raw[:2])[0]
                name = {0x0201: "ThumbnailOffset", 0x0202: "ThumbnailLength",
                        0x0103: "Compression"}[tag]
                found[name] = value
                print(f"     0x{tag:04x} type={typ}  {name} = {value}")
            if tag == 0x8769 and typ == 4 and depth == 0:
                walk(struct.unpack(endian + "I", raw)[0], "Exif SubIFD", depth + 1)
        if nxt and depth < 2:
            walk(nxt, "IFD1(缩略图)", depth + 1)

    walk(struct.unpack(endian + "I", blob[tiff + 4:tiff + 8])[0], "IFD0")

    print()
    print(f"尺寸标签 {len(dims)}/4：" + (", ".join(f"{n}={v}" for n, v in dims) or "无"))

    if "ThumbnailOffset" not in found:
        print("缩略图：✗ 没有 IFD1 / 没有 0x0201")
        return 2

    offset, length = found["ThumbnailOffset"], found.get("ThumbnailLength", 0)
    start = tiff + offset          # IFD 内偏移是相对 TIFF 头的，加回前缀长度
    inside = start + length <= len(blob)
    segment = blob[start:start + length] if inside else b""
    valid = segment[:3] == b"\xff\xd8\xff" and segment[-2:] == b"\xff\xd9"
    print(f"缩略图：TIFF 内偏移={offset} 长度={length} -> 载荷偏移={start}  "
          f"落在文件内={inside}  起止标记={'有效 JPEG ✓' if valid else '✗'}")
    print(f"Compression = {found.get('Compression', '(未写，规范缺省为 1=未压缩，严格读图器可能忽略缩略图)')}")
    return 0 if valid and inside else 2


if __name__ == "__main__":
    sys.exit(main())
