"""批量核对目录下所有 HEIC 的：像素尺寸、走的是网格还是单张、Exif 尺寸标签是否齐全。

用法：
    python recheck.py <目录>

为什么要查 Exif 尺寸标签：真机实测小米相册在这几个标签缺失时**会显示错误的分辨率**——
8192x6144 显示成 32x24、4096x3072 显示成 16x12（都恰好是把大端存的 16 位值按小端读的结果）。
带标签的文件显示正常，所以输出必须保证四个标签"存在且正确"。

四个标签：IFD0 的 ImageWidth(0x0100)/ImageLength(0x0101)，
Exif SubIFD 的 PixelXDimension(0xA002)/PixelYDimension(0xA003)。
"""
import struct
import sys
from pathlib import Path


def boxes(buf, start, end):
    off = start
    while off + 8 <= end:
        size = struct.unpack(">I", buf[off:off + 4])[0]
        btype = buf[off + 4:off + 8]
        hdr = 8
        if size == 1:
            size = struct.unpack(">Q", buf[off + 8:off + 16])[0]
            hdr = 16
        elif size == 0:
            size = end - off
        if size < hdr or off + size > end:
            return
        yield off, btype, size, hdr
        off += size


def analyse(path):
    data = path.read_bytes()
    ispes = []
    for i in range(len(data) - 16):
        if data[i:i + 4] == b"ispe":
            ispes.append(struct.unpack(">II", data[i + 8:i + 16]))
    ispes = sorted(set(ispes), key=lambda x: -(x[0] * x[1]))
    width, height = ispes[0] if ispes else (0, 0)

    is_grid = data.count(b"grid") > 0
    tiles = data.count(b"hvc1")

    tags = {}
    j = data.rfind(b"Exif\x00\x00")
    if j >= 0:
        t = j + 6
        endian = "<" if data[t:t + 2] == b"II" else ">"
        try:
            def walk(off):
                count = struct.unpack(endian + "H", data[t + off:t + off + 2])[0]
                for k in range(count):
                    q = t + off + 2 + k * 12
                    tag, typ, _ = struct.unpack(endian + "HHI", data[q:q + 8])
                    raw = data[q + 8:q + 12]
                    if tag in (0x0100, 0x0101, 0xA002, 0xA003):
                        tags[tag] = struct.unpack(endian + "H", raw[:2])[0] if typ == 3 \
                            else struct.unpack(endian + "I", raw[:4])[0]
                    if tag == 0x8769 and typ == 4:
                        walk(struct.unpack(endian + "I", raw)[0])
            walk(struct.unpack(endian + "I", data[t + 4:t + 8])[0])
        except Exception:
            pass

    return width, height, is_grid, tiles, tags


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 1

    directory = Path(sys.argv[1])
    # Windows 下 glob 不区分大小写，*.heic 与 *.HEIC 会返回同一批文件；
    # 用集合去重，顺带把 Linux 上大小写敏感的两种情况也覆盖到
    files = sorted({p for pattern in ("*.heic", "*.HEIC", "*.heif", "*.HEIF")
                    for p in directory.glob(pattern)})
    if not files:
        print(f"{directory} 下没有找到 HEIC")
        return 1

    print(f"{'文件':<38} {'像素':>13} {'路径':>14} {'Exif 尺寸标签':>30}  判定")
    print("-" * 106)
    bad = 0
    for f in files:
        width, height, is_grid, tiles, tags = analyse(f)
        where = f"网格 {tiles} 块" if is_grid else "单张"
        shown = ", ".join(
            f"{name}={tags[tag]}"
            for tag, name in ((0x0100, "W"), (0x0101, "H"), (0xA002, "xW"), (0xA003, "xH"))
            if tag in tags) or "（无）"
        want = {0x0100: width, 0x0101: height, 0xA002: width, 0xA003: height}
        ok = all(tags.get(k) == v for k, v in want.items())
        if not ok:
            bad += 1
        verdict = "✓" if ok else ("!! 缺标签" if not tags else "!! 值不符")
        print(f"{f.name:<38} {width}x{height:<9} {where:>14} {shown:>30}  {verdict}")

    print(f"\n合计 {len(files)} 个文件，{bad} 个不合格")
    return 0 if bad == 0 else 2


if __name__ == "__main__":
    sys.exit(main())
