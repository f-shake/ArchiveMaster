"""解析 HEIC 的网格(grid)结构：瓦片数、瓦片尺寸、拼出来的总尺寸、以及各 item 类型统计。"""
import struct, sys, collections

def boxes(buf, start, end):
    off = start
    while off + 8 <= end:
        bs = struct.unpack(">I", buf[off:off+4])[0]; bt = buf[off+4:off+8]; hdr = 8
        if bs == 1:
            bs = struct.unpack(">Q", buf[off+8:off+16])[0]; hdr = 16
        elif bs == 0:
            bs = end - off
        if bs < hdr or off + bs > end:
            return
        yield off, bt, bs, hdr
        off += bs

def analyse(path):
    d = open(path, "rb").read()
    print("=" * 78)
    print(f"{path.split(chr(92))[-1]}   ({len(d):,} 字节)")

    ms = me = None
    for off, bt, bs, hdr in boxes(d, 0, len(d)):
        if bt == b"meta":
            ms, me = off + hdr + 4, off + bs

    types = collections.Counter()
    item_names = {}
    grid_item = None
    for off, bt, bs, hdr in boxes(d, ms, me):
        if bt != b"iinf":
            continue
        ver = d[off + hdr]
        p = off + hdr + 4 + (2 if ver == 0 else 4)
        cnt = int.from_bytes(d[off+hdr+4:off+hdr+4+(2 if ver == 0 else 4)], "big")
        for io, ib, ibs, ih in boxes(d, p, off + bs):
            if ib != b"infe":
                continue
            v2 = d[io + ih]
            if v2 >= 2:
                iid = struct.unpack(">H", d[io+ih+4:io+ih+6])[0]
                ityp = d[io+ih+8:io+ih+12].decode("latin1")
            else:
                iid = struct.unpack(">H", d[io+ih+4:io+ih+6])[0]
                ityp = ""
            types[ityp] += 1
            if ityp == "grid":
                grid_item = iid

    print("  item 类型统计:", dict(types))
    print("  网格 item id :", grid_item)

    # ipco 里的属性盒
    ispes = []
    gridbox = None
    for off, bt, bs, hdr in boxes(d, ms, me):
        if bt != b"iprp":
            continue
        for io, ib, ibs, ih in boxes(d, off + hdr, off + bs):
            if ib != b"ipco":
                continue
            for po, pb, pbs, ph in boxes(d, io + ih, io + ibs):
                if pb == b"ispe":
                    ispes.append(struct.unpack(">II", d[po+ph+4:po+ph+12]))
                elif pb == b"grid":
                    raw = d[po+ph:po+pbs]
                    ver = raw[0]
                    rows = raw[1] + 1
                    cols = raw[2] + 1
                    w = struct.unpack(">I", raw[3:7])[0] if len(raw) >= 11 else None
                    h = struct.unpack(">I", raw[7:11])[0] if len(raw) >= 11 else None
                    gridbox = (ver, rows, cols, w, h, raw.hex())

    if gridbox:
        ver, rows, cols, w, h, raw = gridbox
        print(f"  grid 盒: 版本={ver}  行={rows}  列={cols}  瓦片数={rows*cols}  输出尺寸={w}x{h}")
        print(f"  grid 盒原始字节: {raw}")
    else:
        print("  没有 grid 盒（单张图）")

    # ispe：最大的那个是整图，其余是瓦片
    uniq = sorted(set(ispes), key=lambda x: -(x[0]*x[1]))
    print(f"  ispe 共 {len(ispes)} 个，去重 {len(uniq)} 种")
    for u in uniq[:5]:
        print(f"     {u[0]}x{u[1]}   ({u[0]*u[1]/1e6:.2f} MP)  x{ispes.count(u)}")

    if uniq:
        tw, th = uniq[-1] if len(uniq) > 1 else (uniq[0][0] // max(1, int((uniq[0][0]**0.5))), 0)
        # 瓦片通常是出现次数最多的那个尺寸
        tile = max(set(ispes), key=lambda x: ispes.count(x))
        print(f"  -> 单块瓦片尺寸约 {tile[0]}x{tile[1]} ({tile[0]*tile[1]/1e6:.3f} MP)")

for p in sys.argv[1:]:
    analyse(p)
