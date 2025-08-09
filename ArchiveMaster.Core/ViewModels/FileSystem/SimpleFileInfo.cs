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
        private bool isChecked = true;

        [property: JsonIgnore]
        [ObservableProperty]
        private bool canCheck = true;

        private string message;

        private string relativePath;

        [ObservableProperty]
        private string name;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RelativePath))]
        private string path;

        [ObservableProperty]
        private DateTime time;

        [ObservableProperty]
        private bool isDir;

        [ObservableProperty]
        private long length;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RelativePath))]
        private string topDirectory;

        [property: JsonIgnore]
        [ObservableProperty]
        private FileSystemInfo fileSystemInfo;

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

        private ProcessStatus status = ProcessStatus.Ready;

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

        [JsonIgnore]
        public string Message => message;

        [JsonIgnore]
        public bool IsCompleted => status == ProcessStatus.Completed;

        [JsonIgnore]
        public ProcessStatus Status => status;

        [JsonIgnore]
        public bool Exists => File.Exists(Path);

        public void Complete()
        {
            status = ProcessStatus.Completed;
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(IsCompleted));
        }

        public void Error(Exception ex)
        {
            Error(ex.Message);
        }

        public void Error(string message)
        {
            status = ProcessStatus.Error;
            this.message = message;
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(IsCompleted));
            OnPropertyChanged(nameof(Message));
        }

        public void Warn(string msg)
        {
            status = ProcessStatus.Warn;
            message = msg;
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(IsCompleted));
            OnPropertyChanged(nameof(Message));
        }

        public void Processing()
        {
            status = ProcessStatus.Processing;
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(IsCompleted));
        }

        public void SetRelativePath(string relativePath)
        {
            this.relativePath = relativePath;
        }

        public static IEqualityComparer<SimpleFileInfo> EqualityComparer { get; }
            = EqualityComparer<SimpleFileInfo>.Create(
                (s1, s2) => s1.Path == s2.Path,
                s => s.Path.GetHashCode());

        
        public override int GetHashCode()
        {
            int hash = default;
            //FNV算法，规避字符串每次GetHashCode都不一样的问题
            unchecked
            {
                hash = (int)2166136261;
                foreach (char c in Path)
                {
                    hash = (hash ^ c) * 16777619;
                }

                return hash;
            }

            return HashCode.Combine(hash, Length, Time);
        }
    }
}