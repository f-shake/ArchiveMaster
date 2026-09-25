#!/usr/bin/env bash
# 构建照片瘦身输出 HEIC 所需的原生库：libheif + x265（编码器）+ libde265（解码器）。
#
# 与 build-heif.ps1 对应，产物放到 ArchiveMaster.UI.Desktop/native/<RID>/，
# 由 ArchiveMaster.UI.Desktop.csproj 随包复制，运行时由 ArchiveMaster.Helpers.HeifEncoder 加载。
#
# 为什么需要自己构建：Magick.NET 自带的 libheif 没有编入 HEVC 编码器（Heic 只读），
# 而 NuGet 上可自由再分发的 .NET 包都刻意不含 x265（GPL）。
#
# 用法:
#   ./build-heif.sh                # 自动识别 RID（linux-x64 / linux-arm64 / osx-x64 / osx-arm64）
#   ./build-heif.sh linux-x64
#   WORK=/tmp/heif ./build-heif.sh
#
# 依赖: git, cmake(>=3.5), ninja 或 make, gcc/clang。
# 注意: Linux 上 libheif 的依赖按 SONAME 记录，因此必须连版本化的 .so 一起随包（本脚本已处理）；
#       macOS 的 install_name 需改成 @loader_path（本脚本已处理，但**未经实机验证**）。
set -euo pipefail

# x265 按 tag 检出：Bitbucket 不允许按裸 commit 抓取，且 x265 的版本识别要读 git tag；
# 检出后用 X265_COMMIT 断言实际 commit，兼顾可复现（与 THIRD-PARTY-NOTICES.txt 一致）
X265_TAG="${X265_TAG:-4.1}"
X265_COMMIT="${X265_COMMIT:-1d117bed4747758b51bd2c124d738527e30392cb}"
LIBHEIF_REF="${LIBHEIF_REF:-5c7b41f3cc097447dd3c700cc9ec7d94fbb59eec}"   # 1.23.5（master 上的 commit）
LIBDE265_REF="${LIBDE265_REF:-78bd19905b90a95c2ddbe109b2554ea01f65acd7}" # 1.1.3（master 上的 commit）

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RID="${1:-}"
if [ -z "$RID" ]; then
    case "$(uname -s)-$(uname -m)" in
        Linux-x86_64)  RID=linux-x64 ;;
        Linux-aarch64) RID=linux-arm64 ;;
        Darwin-x86_64) RID=osx-x64 ;;
        Darwin-arm64)  RID=osx-arm64 ;;
        *) echo "无法识别的平台，请显式传入 RID" >&2; exit 1 ;;
    esac
fi

WORK="${WORK:-/tmp/archiveMaster-heif-build}"
OUT="${OUT:-$SCRIPT_DIR/../../ArchiveMaster.UI.Desktop/native/$RID}"
JOBS="${JOBS:-$( (command -v nproc >/dev/null && nproc) || sysctl -n hw.ncpu || echo 4 )}"
GENERATOR="Unix Makefiles"
command -v ninja >/dev/null && GENERATOR="Ninja"

mkdir -p "$WORK" "$OUT"
echo "RID=$RID  WORK=$WORK  OUT=$OUT  生成器=$GENERATOR"

clone() { # clone <name> <url> <ref> <expect_commit>
    local dir="$WORK/$1" head
    if [ ! -d "$dir/.git" ]; then
        # 用完整克隆而非 --depth 1：x265 的版本识别要读 git tag，
        # 而浅克隆既没有 tag，也无法按任意 commit 检出（部分服务器还不允许按裸 SHA 抓取）
        echo "[$1] 克隆 $2"
        git clone "$2" "$dir"
    fi
    # 兼容旧版本脚本留下的浅克隆：x265 的版本识别依赖 tag，浅克隆会让它算出 "unknown" 而使配置失败
    if [ "$(git -C "$dir" rev-parse --is-shallow-repository)" = "true" ]; then
        echo "[$1] 检测到浅克隆，补齐历史与 tag"
        git -C "$dir" fetch --unshallow --tags
    fi
    git -C "$dir" checkout --detach "$3"
    head="$(git -C "$dir" rev-parse HEAD)"
    if [ -n "${4:-}" ] && [ "$head" != "$4" ]; then
        echo "[$1] commit 不符：期望 $4，实际 $head（上游 tag 可能被移动，请核对后更新脚本）" >&2
        exit 1
    fi
    echo "[$1] 版本: $head"
}

# 构建目录与安装前缀都带上"变体"后缀：换了开关（是否有 nasm 从而启用汇编）就自动换一套目录，
# 避免旧 CMake 缓存/旧安装产物混进来造成难查的配置失败
if command -v nasm >/dev/null 2>&1; then VARIANT=asm; else VARIANT=noasm; fi
PREFIX="$WORK/install-$VARIANT"
B_X265="$WORK/b-x265-$VARIANT"
B_DE265="$WORK/b-libde265-$VARIANT"
B_HEIF="$WORK/b-libheif-$VARIANT"
HEIF_OUT="$WORK/out-libheif-$VARIANT"
echo "变体: $VARIANT"
if [ "$VARIANT" = "noasm" ]; then echo '  提示：未找到 nasm，编码会明显变慢；装上 nasm 后重跑即可提速'; fi

# ---------------- 1. x265（HEVC 编码器，GPL-2.0-or-later）----------------
clone x265 https://bitbucket.org/multicoreware/x265_git.git "$X265_TAG" "$X265_COMMIT"
echo '[x265] 配置与构建'
# STATIC_LINK_CRT：MSVC 下把 /MD 换成 /MT；非 MSVC 下加 -static-libgcc -static-libstdc++ 与 -static，
# 使产物不依赖 VC++ 运行库 / libstdc++（否则用户机器缺这些时 HEIC 就用不了）
cmake -S "$WORK/x265/source" -B "$B_X265" -G "$GENERATOR" \
    -DCMAKE_BUILD_TYPE=Release -DCMAKE_INSTALL_PREFIX="$PREFIX" \
    -DSTATIC_LINK_CRT=ON \
    -DENABLE_SHARED=ON -DENABLE_CLI=OFF -DHIGH_BIT_DEPTH=OFF
cmake --build "$B_X265" --target install -j "$JOBS"

# ---------------- 2. libde265（HEVC 解码器，LGPL-3.0）----------------
clone libde265 https://github.com/strukturag/libde265.git "$LIBDE265_REF" "$LIBDE265_REF"
echo '[libde265] 配置与构建'
cmake -S "$WORK/libde265" -B "$B_DE265" -G "$GENERATOR" \
    -DCMAKE_BUILD_TYPE=Release -DCMAKE_INSTALL_PREFIX="$PREFIX" \
    -DBUILD_SHARED_LIBS=ON -DENABLE_SDL=OFF
cmake --build "$B_DE265" --target install -j "$JOBS"

# ---------------- 3. libheif（HEIF 容器，LGPL-3.0）----------------
clone libheif https://github.com/strukturag/libheif.git "$LIBHEIF_REF" "$LIBHEIF_REF"
echo '[libheif] 配置与构建'
# WITH_EXAMPLES=OFF：示例程序（heif-enc）在部分工具链下有链接错误，本项目用不到
# CMAKE_INSTALL_RPATH=$ORIGIN：程序用绝对路径 dlopen 这个 libheif.so，
# 而它的依赖（libx265.so.NN、libde265.so.NN）也在同一目录，必须靠 RUNPATH 才能被找到
cmake -S "$WORK/libheif" -B "$B_HEIF" -G "$GENERATOR" \
    -DCMAKE_BUILD_TYPE=Release -DCMAKE_INSTALL_PREFIX="$HEIF_OUT" \
    -DCMAKE_PREFIX_PATH="$PREFIX" -DBUILD_SHARED_LIBS=ON \
    "-DCMAKE_INSTALL_RPATH=\$ORIGIN" \
    -DWITH_X265=ON -DWITH_LIBDE265=ON -DWITH_KVAZAAR=OFF \
    -DWITH_AOM_ENCODER=OFF -DWITH_AOM_DECODER=OFF -DWITH_EXAMPLES=OFF -DBUILD_TESTING=OFF
cmake --build "$B_HEIF" --target install -j "$JOBS"

# ---------------- 4. 收集产物 ----------------
case "$RID" in
    osx-*)
        # macOS: libheif 记录的是构建时的绝对路径，必须改成 @loader_path 才能在用户机器上找到同目录的依赖
        for lib in x265 de265; do
            old=$(otool -L "$HEIF_OUT/lib/libheif.dylib" | awk "/lib$lib.*dylib/ {print \$1}" | head -1 || true)
            [ -n "$old" ] && install_name_tool -change "$old" "@loader_path/lib$lib.dylib" "$HEIF_OUT/lib/libheif.dylib"
        done
        cp -f "$HEIF_OUT/lib/libheif.dylib" "$OUT/libheif.dylib"
        cp -fL "$PREFIX/lib/libx265"*.dylib "$OUT/"
        cp -fL "$PREFIX/lib/libde265"*.dylib "$OUT/"
        ;;
    *)
        # Linux: 依赖按 SONAME 记录（如 libx265.so.215），必须连版本化的文件名一起随包；
        # 不加 || true：缺库时让失败暴露出来，否则会"产出成功"但运行期 dlopen 失败
        cp -f "$HEIF_OUT/lib/libheif.so" "$OUT/libheif.so"
        cp -fL "$PREFIX/lib/libx265".so* "$OUT/"
        cp -fL "$PREFIX/lib/libde265".so* "$OUT/"
        ;;
esac

echo
echo "完成，产物已放入 $OUT"
ls -l "$OUT"
echo '提醒：libheif/libde265 为 LGPL-3.0、x265 为 GPL-2.0-or-later，分发时需随包提供 THIRD-PARTY-NOTICES.txt'
