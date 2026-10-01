# HEIC 输出验证工具

照片瘦身输出的 HEIC 走的是随包的 libheif（`build_scripts/native/build-heif.ps1` 构建），
**不是 Magick.NET**。这套脚本用于验证那条链路产出的文件是否正确——尤其是"对不对"，
而不是只看"能不能打开"。

## 为什么需要专门的工具

**ffmpeg 验证不了网格 HEIC。** 我们对超过 1600 万像素的图改用 HEIF 网格（多块 HEVC 小图拼成一张大图，
详见 `ArchiveMaster.Module.PhotoTools/Helpers/HeifEncoder.cs` 里 `EncodeTiledCore` 的注释）。ffmpeg 不支持网格拼接，对这种文件它**只会解出单块 512×512 瓦片，
而且不报任何错**。拿它当验证手段会得到"无报错"的假通过——实测手机拍的原片也是同样表现。

所以验证必须用 libheif 自己的解码器，也就是这里的 `heicdec.cs`。

同理，网格文件里主图的行列数、瓦片尺寸写在网格 item 自身的数据里（`idat` 盒），
普通工具看不到，要用 `gridparse.py`。

## 前置条件

原生库在 `Publish/win-x64/`（`libheif.dll` + `libx265.dll` + `libde265.dll`）。
`.cs` 脚本通过 `dotnet run` 执行，运行前把该目录加进 `PATH`：

```powershell
$env:PATH = "<仓库根>\Publish\win-x64;$env:PATH"
dotnet run tools\heic-verify\heicdec.cs -- <输入.heic> <输出.raw>
```

Python 脚本会自己定位仓库根并设置 `PATH`，直接跑即可。需要 `ffmpeg` 在 `PATH` 里（只用它做缩放/解码参考图）。

## 工具清单

### 解码与结构

| 脚本 | 用途 |
|---|---|
| `heicdec.cs <输入.heic> <输出.raw>` | **用 libheif 解码**，网格会正确拼回整图。验证像素正确性的唯一入口 |
| `gridparse.py <文件.heic>...` | 解析 item 类型统计、ispe、网格 struct（行列数、瓦片尺寸、输出宽高） |
| `codeccfg.py <文件.heic>...` | 解析 hvcC 与 SPS：profile / **level** / 色度格式 / 位深 / 真实编码尺寸 |
| `verifythumb.py <文件.heic 或 EXIF 文件>` | 检查 EXIF 内嵌缩略图（IFD1）结构是否完整 |

### 编码（复现产物，用于 A/B 对比）

| 脚本 | 用途 |
|---|---|
| `heicgrid.cs <rgb.raw> <宽> <高> <质量> <瓦片边长> <exif\|-> <xmp\|-> <输出.heic> [icc\|-]` | 网格编码 |
| `heicenc.cs <rgb.raw> <宽> <高> <质量> <exif\|-> <xmp\|-> <icc\|-> <输出.heic> [tight]` | 单张编码 |

两个脚本都复刻 `HeifEncoder` 的调用序列，用来在改动前后做对照。

### 比对

| 脚本 | 用途 |
|---|---|
| `pixelcheck.py <被测.heic> <参考图>` | 解码后与参考图**逐像素比对**，看平均差异与色度 |
| `recheck.py <目录>` | 批量核对每个 HEIC 的像素尺寸、网格/单张、**Exif 四个尺寸标签是否齐全** |

## 判定阈值

- `pixelcheck.py`：平均差异 < 6 且色度不低于参考值的 85% = 正常。
  色度明显偏低（例如 17 → 11）配差异 25+ 就是**色度损坏**——libheif 在图像宽度不是 16 的倍数时会这样。
- `recheck.py`：四个尺寸标签必须都存在且等于实际像素尺寸。缺标签时小米相册会显示错误分辨率
  （8192×6144 显示成 32×24，恰好是把大端存的 16 位值按小端读）。

## 改动 HEIC 编码路径前请先读这几条

都是实测踩出来的，写在这里免得再踩一遍：

1. **所有瓦片必须同尺寸，并且要"盖住"整图**——ISO/IEC 23008-12 §6.6.2.3.1 的判据是
   `tile_width*columns ≥ output_width` 且 `tile_height*rows ≥ output_height`（是 **≥，不是等号**），
   多出来的部分解码端按声明尺寸在右/底裁掉。**所以声明尺寸不必是瓦片边长的整数倍**：行列数向上取整、
   末列/末行用边缘像素复制补满即可（`HeifEncoder` 就是这么做的，2026-10-01 真机实测含奇数尺寸都正常）。
   但瓦片**大小必须一致**——把末行/末列做成小瓦片会报 `Grid tiles have different sizes`（写出时同样不报错）。
   注意：以前这里写的是"宽高必须是瓦片边长的整数倍"，那是拿小瓦片试验得出的错误结论。
2. **单张编码的图像宽度必须是 16 的倍数**——否则 libheif 输出的色度是坏的（画面发灰、撕裂）；
   高度不需要对齐。这条只管"单张"：网格里每块瓦片都是 512 宽，所以网格的声明尺寸可以是任意值（含奇数）。
3. **ICC 色彩配置只挂第 0 块瓦片**——libheif 仅在 `tile_x==0 && tile_y==0` 时把 `colr` 收集到网格 item 上。
4. **网格接口的参数语义容易搞反**：`heif_context_add_grid_image` 收的是**列数/行数**（不是瓦片宽高），
   `heif_context_add_image_tile` 收的是**瓦片序号**（不是像素坐标）。签名见 libheif 的 `heif_tiling.h`。
5. **EXIF 里存的偏移是相对 TIFF 头**的，不含 `Exif\0\0` 那 6 字节前缀。自己拼 IFD 时容易差这 6 字节。
6. **Magick.NET 的 `ExifProfile` 一旦 `SetValue` 就会重建整个 EXIF，且不输出 IFD1（内嵌缩略图）**——
   实测 25,194 字节的源 EXIF 会缩到 1,104 字节、缩略图消失，真机表现是相册不再秒出缩略图。
   `ImageMetadataHelper.AppendExifThumbnail` 负责把它补回去。

## 已知限制

- `ffmpeg` 不能用于验证网格 HEIC（见上）。
- `gridparse.py` / `recheck.py` 直接按字节扫描定位 item 与 Exif，**不解析 iloc**。
  要读某个 item 的载荷（例如网格 item 的 `idat` 数据）需要按 iloc 正确解析——
  注意 iloc v1 的 `item_ID` 是 **16 位**（只有 v2 才是 32 位），且 ID 之后还有
  `reserved(12)+construction_method(4)` 两个字节，以及 `construction_method=1` 时偏移是相对 `idat` 的。
