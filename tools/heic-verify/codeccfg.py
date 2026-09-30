"""正确解析 HEIC 里的 HEVC 编码配置：hvcC 头部 + SPS 的真实尺寸/level，并检测是否用了网格(tiled)编码。"""
import struct, sys, glob, os

def boxes(buf, start, end):
    off = start
    while off + 8 <= end:
        bs = struct.unpack(">I", buf[off:off+4])[0]; bt = buf[off+4:off+8]; hdr = 8
        if bs == 1:
            bs = struct.unpack(">Q", buf[off+8:off+16])[0]; hdr = 16
        elif bs == 0:
            bs = end - off
        if bs < hdr or off + bs > end: return
        yield off, bt, bs, hdr
        off += bs

class BR:
    def __init__(self, data): self.d = data; self.p = 0
    def bits(self, n):
        v = 0
        for _ in range(n):
            b = self.d[self.p >> 3]
            v = (v << 1) | ((b >> (7 - (self.p & 7))) & 1)
            self.p += 1
        return v
    def ue(self):
        z = 0
        while self.bits(1) == 0: z += 1
        return (1 << z) - 1 + (self.bits(z) if z else 0)

def unescape(data):
    out = bytearray(); i = 0
    while i < len(data):
        if i + 2 < len(data) and data[i] == 0 and data[i+1] == 0 and data[i+2] == 3:
            out += data[i:i+2]; i += 3
        else:
            out.append(data[i]); i += 1
    return bytes(out)

def parse_sps(nal):
    rbsp = unescape(nal[2:])          # 跳过 2 字节 NAL 头
    r = BR(rbsp)
    r.bits(4)                          # sps_video_parameter_set_id
    max_sub = r.bits(3)
    r.bits(1)                          # sps_temporal_id_nesting_flag
    # --- profile_tier_level ---
    r.bits(2); tier = r.bits(1); pidc = r.bits(5)
    r.bits(32); r.bits(48)
    level_idc = r.bits(8)
    out = {"tier": tier, "profile_idc": pidc, "sps_level_idc": level_idc}
    if max_sub > 0:
        for _ in range(max_sub):
            r.bits(2 + 1 + 5 + 32 + 48)
        for _ in range(max_sub, 8):
            r.bits(2)
    r.ue()                             # sps_seq_parameter_set_id
    chroma = r.ue()
    out["chroma_format_idc"] = chroma
    if chroma == 3: r.bits(1)
    out["width"] = r.ue()
    out["height"] = r.ue()
    if r.bits(1):                      # conformance_window_flag
        out["conf"] = [r.ue() for _ in range(4)]
    return out

def analyse(path):
    d = open(path, "rb").read()
    name = os.path.basename(path)
    print("=" * 100)
    print(f"{name}   ({len(d):,} bytes)")
    # meta box
    ms = me = None
    for off, bt, bs, hdr in boxes(d, 0, len(d)):
        if bt == b"meta": ms, me = off + hdr + 4, off + bs
    # 主图 hvcC + ispe
    i = d.find(b"hvcC")
    if i < 0:
        print("  无 hvcC"); return
    P = i + 4                          # hvcC 负载起点
    cfg = d[P]; pb = d[P+1]
    level = d[P+12]
    minss = struct.unpack(">H", d[P+13:P+15])[0] & 0xFFF
    par = d[P+15] & 3
    chf = d[P+16] & 3
    bdl = (d[P+17] & 7) + 8
    bdc = (d[P+18] & 7) + 8
    numarr = d[P+22]
    print(f"  hvcC: version={cfg} profile_space={pb>>6} tier={pb>>5&1} profile_idc={pb&0x1f}"
          f" level_idc={level} (={level/30:.1f})")
    compat = struct.unpack(">I", d[P+2:P+6])[0]
    constraints = int.from_bytes(d[P+6:P+12], "big")
    print(f"        兼容标志=0x{compat:08x}  约束=0x{constraints:048b}")
    print(f"        minSpatialSeg={minss} parallelism={par} chromaFormat={chf}"
          f" bitDepth={bdl}/{bdc} numArrays={numarr}")

    # 遍历 hvcC 里的 NAL 数组，找 SPS(type 33)
    q = P + 23
    types = []
    for _ in range(numarr):
        nal_type = d[q] & 0x3F
        num = struct.unpack(">H", d[q+1:q+3])[0]
        q += 3
        for _n in range(num):
            ln = struct.unpack(">H", d[q:q+2])[0]
            nal = d[q+2:q+2+ln]
            types.append(nal_type)
            if nal_type == 33:
                try:
                    s = parse_sps(nal)
                    print(f"  SPS:  真实尺寸 {s['width']}x{s['height']}"
                          f"  level_idc={s['sps_level_idc']} ({s['sps_level_idc']/30:.1f})"
                          f"  profile={s['profile_idc']} tier={s['tier']}"
                          f"  chroma={s['chroma_format_idc']}")
                    if "conf" in s:
                        c = s["conf"]
                        sub_w = s["width"] - c[1] - c[2]
                        sub_h = s["height"] - c[0] - c[3]
                        print(f"         conformance 裁剪后 {sub_w}x{sub_h}")
                except Exception as e:
                    print("  SPS 解析失败:", e)
            q += 2 + ln
    print(f"  NAL 数组类型: {types}  (32=VPS 33=SPS 34=PPS)")

    i = d.find(b"ispe")
    if i > 0:
        print(f"  ispe: {struct.unpack('>II', d[i+8:i+16])[0]}x{struct.unpack('>II', d[i+8:i+16])[1]}")
    # 网格/瓦片检测
    for kw in (b"grid", b"hvc1", b"hvc2"):
        print(f"  含 {kw.decode()}: {d.count(kw)} 次")

for f in sys.argv[1:]:
    analyse(f)
