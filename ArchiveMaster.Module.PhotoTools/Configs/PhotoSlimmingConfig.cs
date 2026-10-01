using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArchiveMaster.Helpers;
using ArchiveMaster.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.IO;
using FzLib.Text;
using ImageMagick;

namespace ArchiveMaster.Configs
{
    public partial class PhotoSlimmingConfig : ConfigBase
    {
        /// <summary>
        /// 文件名占位符
        /// </summary>
        public const string FileNamePlaceholder = "{FileName}";

        /// <summary>
        /// 文件夹名占位符
        /// </summary>
        public const string FolderNamePlaceholder = "{FolderName}";

        /// <summary>
        /// 是否在处理前清空目标目录
        /// </summary>
        [ObservableProperty]
        private bool clearAllBeforeRunning = false;

        /// <summary>
        /// 需要压缩的文件的后缀名
        /// </summary>
        [ObservableProperty]
        private FileFilterRule compressFilter = new FileFilterRule()
        {
            IncludeFiles = string.Join(Environment.NewLine, "*.jpg", "*.jpeg", "*.heic", "*.heif", "*.bmp", "*.png",
                "*.gif", "*.tiff", "*.tif")
        };

        /// <summary>
        /// 压缩后的文件输出类型/扩展名
        /// </summary>
        [ObservableProperty]
        private MagickFormat compressImageFormat = MagickFormat.Jpg;

        /// <summary>
        /// 直接复制的文件的后缀名
        /// </summary>
        [ObservableProperty]
        private FileFilterRule copyDirectlyFilter = new FileFilterRule()
        {
            IncludeFiles = string.Join(Environment.NewLine, "*.gpx", "*.doc", "*.docx", "*.ppt", "*.pptx", "*.xls",
                "*.xlsx", "*.pdf")
        };

        /// <summary>
        /// 最深文件层级，例如设置为2，相对路径为D1/D2/D3/D4/File.ext，则目标相对路径将改为D1/D2/D3-D4-File.ext
        /// </summary>
        [ObservableProperty]
        private int deepestLevel = 10000;

        /// <summary>
        /// 目标目录
        /// </summary>
        [ObservableProperty]
        private string distDir = @"C:\目标\目录";

        /// <summary>
        /// 目标文件名模板
        /// </summary>
        [ObservableProperty]
        private string fileNameTemplate = FileNamePlaceholder;

        /// <summary>
        /// 目标文件夹名模板
        /// </summary>
        [ObservableProperty]
        private string folderNameTemplate = FolderNamePlaceholder;

        /// <summary>
        /// 修复文件修改时间时，最大可接受的Exif和修改时间的时间差（秒）
        /// </summary>
        [ObservableProperty]
        private double maxDurationTolerance = 60;

        /// <summary>
        /// 最大长边像素（大于则进行缩放）
        /// </summary>
        [ObservableProperty]
        private int maxLongSize = 10000;

        /// <summary>
        /// 最大短边像素（大于则进行缩放）
        /// </summary>
        [ObservableProperty]
        private int maxShortSize = 5000;

        /// <summary>
        /// 质量（0-100；界面可调范围为 10-100）
        /// </summary>
        [ObservableProperty]
        private int quality = 50;

        /// <summary>
        /// HEIC 输出的 x265 编码档位（仅输出格式为 HEIC/HEIF 时生效），取值见 <see cref="HeifEncoder.Presets"/>。
        /// </summary>
        /// <remarks>
        /// 默认不是"不设置"：libheif 的 x265 插件在没设置时用的是 <c>slow</c>（最慢档），实测换成
        /// <c>fast</c> 后体积几乎不变、墙钟快 2.4 倍，详见 <see cref="HeifEncoder.Encode"/> 的 remarks。
        /// </remarks>
        [ObservableProperty]
        private string heifPreset = HeifEncoder.DefaultPreset;

        /// <summary>
        /// 旧配置里的等价档位名（<c>veryfast</c>/<c>faster</c>）在这里迁到规范名。
        /// </summary>
        /// <remarks>
        /// 不迁的话会自相矛盾：校验认这些名字（见 <see cref="HeifEncoder.NormalizePreset"/>），但下拉框的候选里
        /// 没有它们，界面会显示成空，甚至把值回写成 null 而反过来被校验挡住。
        /// </remarks>
        partial void OnHeifPresetChanged(string value)
        {
            string canonical = HeifEncoder.NormalizePreset(value);
            if (canonical != value)
            {
                HeifPreset = canonical;
            }
        }

        /// <summary>
        /// HEIC 网格的瓦片边长（仅输出格式为 HEIC/HEIF 且图片超过单张上限时生效），
        /// 取值见 <see cref="HeifEncoder.TileSizes"/>。
        /// </summary>
        /// <remarks>
        /// 512 与手机相机原片一致；512 与 2048 已确认可在手机上正常打开，1024 未验证。
        /// 调大可以显著提升单张图的并行度（实测并行度 512/1024/2048 依次为 2.4x/4.6x/8.3x）。
        /// </remarks>
        [ObservableProperty]
        private int heifTileSize = HeifEncoder.DefaultTileSize;

        /// <summary>
        /// 遇到已经存在的文件是否跳过（而不是覆盖）
        /// </summary>
        [ObservableProperty]
        private bool skipIfExist = true;

        /// <summary>
        /// 源目录
        /// </summary>
        [ObservableProperty]
        private string sourceDir = @"C:\源\目录";
        /// <summary>
        /// 并行线程数（针对压缩和文件修改时间修复）
        /// </summary>
        [ObservableProperty]
        private int thread = 4;
        public override void Check()
        {
            CheckDir(SourceDir, "源目录");
            CheckEmpty(DistDir, "目标目录");
            CheckEmpty(FolderNameTemplate, "文件夹名模板");
            CheckEmpty(FileNameTemplate, "文件名模板");
            // 线程数现在是 HEIC 编码并发的唯一闸门（见 HeifEncoder.Encode 的 remarks），必须校验：
            // 界面滑块是 1-8，但旧版本的滑块允许到 16，配置文件里可能还留着越界值
            CheckRange(Thread, 1, 8, "处理线程数");
            if (CopyDirectlyFilter.IsEnabled == false && CompressFilter.IsEnabled == false)
            {
                throw new Exception("欲复制的文件和欲压缩的文件至少选择一项");
            }

            if (!FolderNameTemplate.Contains(PhotoSlimmingConfig.FolderNamePlaceholder))
            {
                throw new Exception("文件夹名模板不包含文件夹名占位符");
            }

            if (!FileNameTemplate.Contains(PhotoSlimmingConfig.FileNamePlaceholder))
            {
                throw new Exception("文件夹名模板不包含文件夹名占位符");
            }

            // HEIC 专有设置只在输出 HEIC 时才校验：配置里可能留着上一次的取值，
            // 不该因此挡住一次 JPEG 的输出
            if (HeifEncoder.IsHeifFormat(CompressImageFormat))
            {
                if (!HeifEncoder.Presets.Contains(HeifPreset))
                {
                    throw new Exception($"不支持的 HEIC 编码档位：{HeifPreset}（可选 {string.Join("/", HeifEncoder.Presets)}）");
                }

                if (!HeifEncoder.TileSizes.Contains(HeifTileSize))
                {
                    throw new Exception(
                        $"HEIC 网格的瓦片边长只能是 {string.Join("/", HeifEncoder.TileSizes)} 之一（当前 {HeifTileSize}）");
                }
            }
        }
    }
}