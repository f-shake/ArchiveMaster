using System.Diagnostics;
using System.Text.Json.Serialization;
using ArchiveMaster.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels.FileSystem
{
    [DebuggerDisplay("Name = {Name}, Path = {Path}")]
    public partial class SimpleFileInfo : ObservableObject
    {
        [property: JsonIgnore]
        [ObservableProperty]
        private bool canCheck = true;

        [property: JsonIgnore]
        [ObservableProperty]
        private FileSystemInfo fileSystemInfo;

        [property: JsonIgnore]
        [ObservableProperty]
        private bool isChecked = true;

        [ObservableProperty]
        private bool isDir;

        [ObservableProperty]
        private long length;

        private string message;

        [ObservableProperty]
        private string name;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RelativePath))]
        private string path;

        private string relativePath;
        private ProcessStatus status = ProcessStatus.Ready;

        [ObservableProperty]
        private DateTime time;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RelativePath))]
        private string topDirectory;
        public SimpleFileInfo()
        {
        }

        public SimpleFileInfo(SimpleFileInfo template)
        {
            Name = template.Name;
            Path = template.Path;
            TopDirectory = template.TopDirectory;
            Time = template.Time;
            Length = template.Length;
        }

        public SimpleFileInfo(FileSystemInfo file, string topDir)
        {
            ArgumentNullException.ThrowIfNull(file);
            FileSystemInfo = file;
            Name = file.Name;
            Path = file.FullName;
            if (System.IO.Path.IsPathRooted(topDir))
            {
                TopDirectory = topDir;
            }
            else
            {
                throw new ArgumentException($"提供的{nameof(topDir)}不是{nameof(file)}的父级");
            }

            Time = file.LastWriteTime;
            IsDir = file.Attributes.HasFlag(FileAttributes.Directory);
            if (!IsDir && file is FileInfo f)
            {
                Length = f.Length;
            }
        }

        public static IEqualityComparer<SimpleFileInfo> EqualityComparer { get; }
            = EqualityComparer<SimpleFileInfo>.Create(
                (s1, s2) => s1.Path == s2.Path,
                s => s.Path.GetHashCode());

        [JsonIgnore]
        public bool Exists => File.Exists(Path);

        [JsonIgnore]
        public string Message => message;

        [JsonIgnore]
        public string RelativePath
        {
            get
            {
                if (relativePath != null)
                {
                    return relativePath;
                }

                if (string.IsNullOrEmpty(TopDirectory))
                {
                    return Path;
                }


                // if (Path.StartsWith(TopDirectory))
                // {
                //     return Path[TopDirectory.Length..].TrimStart([System.IO.Path.DirectorySeparatorChar,System.IO.Path.AltDirectorySeparatorChar]);
                // }
                //下面这个效率太低了，所以如果上面的可以就用上面的
                //更新：上面的代码，潜在问题太多了，比如如果 TopDirectory 是 C:\Foo，而 Path 是 C:\Foo\Bar\file.txt，还是用下面的
                return System.IO.Path.GetRelativePath(TopDirectory, Path);
            }
        }

        public void SetRelativePath(string relativePath)
        {
            this.relativePath = relativePath;
        }


        #region 进度
        [JsonIgnore]
        public ProcessStatus Status => status;

        public void Success(string message)
        {
            Success();
            this.message = message;
            NotifyStatusProperties();
        }

        public void Success()
        {
            status = ProcessStatus.Success;
            NotifyStatusProperties();
        }

        public void Skip()
        {
            message = "文件已存在，跳过";
            status = ProcessStatus.Skip;
            NotifyStatusProperties();
        }

        public void Error(Exception ex)
        {
            Error(ex.Message);
        }

        public void Error(string message)
        {
            status = ProcessStatus.Error;
            this.message = message;
        }

        private void NotifyStatusProperties()
        {
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(Message));
        }

        public void Warn(string msg)
        {
            status = ProcessStatus.Warn;
            message = msg;
            NotifyStatusProperties();
        }

        public void Processing()
        {
            status = ProcessStatus.Processing;
            NotifyStatusProperties();
        }
        #endregion

        public override int GetHashCode()
        {
            int hash = default;
            //FNV算法，规避字符串每次GetHashCode都不一样的问题
            unchecked
            {
                hash = (int)2166136261;
                foreach (char c in Path ?? RelativePath)
                {
                    hash = (hash ^ c) * 16777619;
                }

                return hash;
            }
        }

    }
}