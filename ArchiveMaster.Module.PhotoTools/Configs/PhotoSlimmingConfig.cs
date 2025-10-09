using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArchiveMaster.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.IO;

namespace ArchiveMaster.Configs
{
    public partial class PhotoSlimmingConfig : ConfigBase
    {
        /// <summary>
        /// 是否在处理前清空目标目录
        /// </summary>
        [ObservableProperty]
        private bool clearAllBeforeRunning = false;

        /// <summary>
        /// 需要压缩的文件的后缀名
        /// </summary>
        [ObservableProperty]
        private ObservableStringList compressExtensions = [];

        /// <summary>
        /// 直接复制的文件的后缀名
        /// </summary>
        [ObservableProperty]
        private ObservableStringList copyDirectlyExtensions =
            ["png", "gpx", "doc", "docx", "ppt", "pptx", "xls", "xlsx", "pdf"];

        /// <summary>
        /// 源目录
        /// </summary>
        [ObservableProperty]
        private string sourceDir = @"C:\源\目录";

        /// <summary>
        /// 目标目录
        /// </summary>
        [ObservableProperty]
        private string distDir = @"C:\目标\目录";

        /// <summary>
        /// 筛选
        /// </summary>
        [ObservableProperty]
        private FileFilterRule filter = new FileFilterRule();

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
        /// 质量（1-100）
        /// </summary>
        [ObservableProperty]
        private int quality = 50;

        /// <summary>
        /// 遇到已经存在的文件是否跳过（而不是覆盖）
        /// </summary>
        [ObservableProperty]
        private bool skipIfExist = true;

        /// <summary>
        /// 并行线程数（针对压缩和文件修改时间修复）
        /// </summary>
        [ObservableProperty]
        private int thread = 4;

        /// <summary>
        /// 压缩后的文件输出类型/扩展名
        /// </summary>
        [ObservableProperty]
        private string outputFormat = "jpg";

        /// <summary>
        /// 最深文件层级，例如设置为2，相对路径为D1/D2/D3/D4/File.ext，则目标相对路径将改为D1/D2/D3-D4-File.ext
        /// </summary>
        [ObservableProperty]
        private int deepestLevel = 10000;

        /// <summary>
        /// 文件名占位符
        /// </summary>
        public const string FileNamePlaceholder = "{FileName}";

        /// <summary>
        /// 文件夹名占位符
        /// </summary>
        public const string FolderNamePlaceholder = "{FolderName}";

        /// <summary>
        /// 目标文件夹名模板
        /// </summary>
        [ObservableProperty]
        private string folderNameTemplate = FolderNamePlaceholder;

        /// <summary>
        /// 目标文件名模板
        /// </summary>
        [ObservableProperty]
        private string fileNameTemplate = FileNamePlaceholder;

        public override void Check()
        {
            CheckDir(SourceDir, "源目录");
            CheckEmpty(DistDir, "目标目录");
            CheckEmpty(OutputFormat, "图片输出格式");
            CheckEmpty(FolderNameTemplate, "文件夹名模板");
            CheckEmpty(FileNameTemplate, "文件名模板");
            
            if (!FolderNameTemplate.Contains(PhotoSlimmingConfig.FolderNamePlaceholder))
            {
                throw new Exception("文件夹名模板不包含文件夹名占位符");
            }

            if (!FileNameTemplate.Contains(PhotoSlimmingConfig.FileNamePlaceholder))
            {
                throw new Exception("文件夹名模板不包含文件夹名占位符");
            }
        }
    }
}