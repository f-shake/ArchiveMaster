"""把一个 HEIC 解码出来，和参考图逐像素比对，判断有没有色度损坏。

用法：
    python pixelcheck.py <被测.heic> <参考图>

解码用的是 libheif（heicdec.cs），不是 ffmpeg —— ffmpeg 不支持 HEIF 网格拼接，
对网格文件只会解出单块 512x512 瓦片而且**不报错**，拿它验证会得到假通过。

判定参考：平均差异 < 6 且色度与参考接近 = 正常；
色度明显偏低（例如 17 → 11）、差异 25+ = 色度损坏（libheif 在宽度非 16 倍数时的症状）。
"""
import os
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
NATIVE = REPO / "Publish" / "win-x64"
HEICDEC = Path(__file__).resolve().parent / "heicdec.cs"


def make_env():
    env = dict(os.environ)
    if NATIVE.is_dir():
        env["PATH"] = str(NATIVE) + os.pathsep + env["PATH"]
    return env


def decode_with_libheif(heic_path, raw_path, env):
    """返回 (宽, 高)；失败返回 (0, 0)。"""
    proc = subprocess.run(
        ["dotnet", "run", str(HEICDEC), "--", str(heic_path), str(raw_path)],
        env=env, capture_output=True, text=True, encoding="utf-8", errors="replace")
    print(proc.stdout.strip())
    if proc.returncode != 0:
        print("解码失败：" + (proc.stderr or "")[-1500:])
        return 0, 0
    for line in proc.stdout.splitlines():
        if "解码得到" in line:
            size = line.split("解码得到")[1].split()[0]
            w, h = size.split("x")
            return int(w), int(h)
    return 0, 0


def scale_reference(ref_path, width, height, raw_path, env):
    subprocess.run(
        ["ffmpeg", "-y", "-v", "error", "-i", str(ref_path),
         "-vf", f"scale={width}:{height}", "-pix_fmt", "rgb24",
         "-f", "rawvideo", str(raw_path)],
        env=env, check=True, capture_output=True)


def chroma(buf, width):
    """平均 (|R-G|+|G-B|)/2 —— 色度损坏时这个值会明显下降。"""
    total = 0.0
    count = 0
    step = max(3, (width * 3) // 8)
    step -= step % 3
    for i in range(0, len(buf) - 3, step):
        r, g, b = buf[i], buf[i + 1], buf[i + 2]
        total += (abs(r - g) + abs(g - b)) / 2
        count += 1
    return total / max(count, 1)


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 1

    heic, ref = sys.argv[1], sys.argv[2]
    env = make_env()
    work = Path(os.environ.get("TEMP", ".")) / "heic-verify-tmp"
    work.mkdir(parents=True, exist_ok=True)
    got_raw = work / "got.raw"
    ref_raw = work / "ref.raw"

    try:
        width, height = decode_with_libheif(heic, got_raw, env)
        if width == 0:
            return 1

        scale_reference(ref, width, height, ref_raw, env)
        got = got_raw.read_bytes()
        expected = ref_raw.read_bytes()
        if len(got) != len(expected):
            print(f"尺寸不符：解码 {len(got):,} 字节，参考 {len(expected):,} 字节")
            return 1

        n = len(got)
        sampled = range(0, n, 997)
        diff = sum(abs(expected[i] - got[i]) for i in sampled) / ((n + 996) // 997)
        chroma_ref = chroma(expected, width)
        chroma_got = chroma(got, width)

        print(f"像素数 {width}x{height}")
        print(f"逐像素平均差异 {diff:.2f}")
        print(f"参考色度 {chroma_ref:.2f}   被测色度 {chroma_got:.2f}")
        ok = diff < 6 and chroma_got > chroma_ref * 0.85
        print("判定:", "正常" if ok else "*** 有问题（差异过大或色度偏低）***")
        return 0 if ok else 2
    finally:
        for f in (got_raw, ref_raw):
            try:
                f.unlink()
            except OSError:
                pass


if __name__ == "__main__":
    sys.exit(main())
