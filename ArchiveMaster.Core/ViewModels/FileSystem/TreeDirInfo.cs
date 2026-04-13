using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using FzLib.IO;

namespace ArchiveMaster.ViewModels.FileSystem
{
    [DebuggerDisplay("Name = {Name}, Subs Count = {Subs.Count}")]
    public partial class TreeDirInfo : TreeFileDirInfo
    {
        /// <summary>
        /// 路径分隔符
        /// </summary>
        private char[] pathSeparator = ['/', '\\'];

        /// <summary>
        /// 子目录
        /// </summary>
        private List<TreeDirInfo> subDirs = new List<TreeDirInfo>();

        /// <summary>
        /// 子目录名到子目录的字典
        /// </summary>
        private Dictionary<string, TreeDirInfo> subDirsDic = new Dictionary<string, TreeDirInfo>();

        /// <summary>
        /// 子文件
        /// </summary>
        private List<TreeFileInfo> subFiles = new List<TreeFileInfo>();

        /// <summary>
        /// 子目录和子文件
        /// </summary>
        private List<TreeFileDirInfo> subs = new List<TreeFileDirInfo>();

        public TreeDirInfo()
        {
            IsDir = true;
        }

        public TreeDirInfo(SimpleFileInfo dir, TreeDirInfo parent, int depth, int index)
            : base(dir, parent, depth, index)
        {
            IsDir = true;
        }

        public TreeDirInfo(DirectoryInfo dir, string topDir, TreeDirInfo parent, int depth, int index)
            : base(dir, topDir, parent, depth, index)
        {
            IsDir = true;
        }

        public enum TreeBuildType
        {
            /// <summary>
            /// 手动添加子级
            /// </summary>
            Manual,

            /// <summary>
            /// 通过自动枚举目录或提供文件信息，自动添加子集
            /// </summary>
            Automatic
        }

        public TreeBuildType BuildType { get; private set; }

        /// <summary>
        /// 是否已展开（UI）
        /// </summary>
        [JsonIgnore]
        public bool IsExpanded { get; set; }

        /// <summary>
        /// 子目录
        /// </summary>
        public IReadOnlyList<TreeDirInfo> SubDirs => subDirs.AsReadOnly();

        /// <summary>
        /// 子目录的数量，需手动更新
        /// </summary>
        public int SubFileCount { get; set; }

        /// <summary>
        /// 子文件
        /// </summary>
        public IReadOnlyList<TreeFileInfo> SubFiles => subFiles.AsReadOnly();

        /// <summary>
        /// 子文件的数量，需手动更新
        /// </summary>
        public int SubFolderCount { get; set; }

        /// <summary>
        /// 子文件和子目录
        /// </summary>
        [JsonIgnore]
        public IReadOnlyList<TreeFileDirInfo> Subs => subs.AsReadOnly();

        /// <summary>
        /// 增加子目录或子文件
        /// </summary>
        /// <param name="item"></param>
        /// <exception cref="ArgumentException"></exception>
        public void AddSub(TreeFileDirInfo item)
        {
            TreeDirInfo parent = this;
            switch (item)
            {
                case TreeFileInfo file:
                    subFiles.Add(file);
                    subs.Add(file);
                    while (parent != null)
                    {
                        parent.SubFileCount++;
                        parent = parent.Parent;
                    }

                    break;

                case TreeDirInfo dir:
                    subDirs.Add(dir);
                    subs.Add(dir);
                    if (!subDirsDic.TryAdd(dir.Name, dir))
                    {
                        // throw new ArgumentException($"目录名{dir.Name}已存在于当前目录{Name}下", nameof(item));
                    }

                    while (parent != null)
                    {
                        parent.SubFolderCount++;
                        parent = parent.Parent;
                    }

                    break;

                default:
                    throw new ArgumentException("未知的类");
            }

            item.Parent = this;

            //不直接UpdateCheckedStateFromChildren，不然批量添加的时间复杂度为O(n2)
            if (IsTreeItemChecked == null)
            {
                //已经是中间态了，不管加什么都是中间态
            }
            else if (IsTreeItemChecked == true)
            {
                if (item.IsTreeItemChecked == false)
                {
                    //如果新增项是未选中，且新增项为唯一一个项，那么变为未选中，否则为中间态
                    SetTreeItemChecked(Subs.Count == 1 ? false : null); //如果只有一个，那么与新增项状态一致
                    Parent?.UpdateCheckedStateFromChildren();
                }
            }
            else //IsTreeItemChecked == false
            {
                if (item.IsTreeItemChecked == true)
                {
                    //如果新增项是选中，且新增项为唯一一个项，那么变为选中，否则为中间态
                    SetTreeItemChecked(Subs.Count == 1 ? true : null); //如果只有一个，那么与新增项状态一致
                    Parent?.UpdateCheckedStateFromChildren();
                }
            }
        }

        /// <summary>
        /// 将当前节点的选中状态传播到所有子节点，适用于手动更改当前节点选中状态后调用
        /// </summary>
        internal void PropagateCheckedStateToChildren()
        {
            if (IsTreeItemChecked == null)
            {
                return;
            }

            bool isChecked = IsTreeItemChecked.Value;
            foreach (var item in Subs.Where(p => p.IsTreeItemChecked != isChecked))
            {
                //设置本身状态
                item.SetTreeItemChecked(isChecked);
                if (item is TreeDirInfo d)
                {
                    //如果是目录，继续向下传播
                    d.PropagateCheckedStateToChildren();
                }
            }
        }

        /// <summary>
        /// 更新当前节点的选中状态，并通知父节点更新状态，适用于子节点选中状态改变后调用
        /// </summary>
        internal void UpdateCheckedStateFromChildren()
        {
            bool hasHalf = Subs.Any(p => p.IsTreeItemChecked is null);
            if (hasHalf)
            {
                //有中间态，当前节点也为中间态
                SetTreeItemChecked(null);
            }
            else
            {
                bool hasChecked = Subs.Any(p => p.IsTreeItemChecked is true);
                bool hasUnchecked = Subs.Any(p => p.IsTreeItemChecked is false);
                SetTreeItemChecked(hasChecked switch
                {
                    true when hasUnchecked => null,
                    true when !hasUnchecked => true,
                    _ => false,
                });
            }

            Parent?.UpdateCheckedStateFromChildren();
        }

        #region 枚举已有文件创建

        public static TreeDirInfo BuildTree(string rootDir, FileFilterRule filter = null)
        {
            TreeDirInfo root = new TreeDirInfo(new DirectoryInfo(rootDir), rootDir, null, 0, 0);
            FileFilterHelper filterHelper = filter is { IsEnabled: true } ? new FileFilterHelper(filter) : null;
            EnumerateDirsAndFiles(root, filterHelper, CancellationToken.None);
            return root;
        }

        public static async Task<TreeDirInfo> BuildTreeAsync(string rootDir,
            FileFilterRule filter = null,
            CancellationToken cancellationToken = default)
        {
            TreeDirInfo root = new TreeDirInfo(new DirectoryInfo(rootDir), rootDir, null, 0, 0);
            FileFilterHelper filterHelper = filter is { IsEnabled: true } ? new FileFilterHelper(filter) : null;
            await Task.Run(() => EnumerateDirsAndFiles(root, filterHelper, cancellationToken), cancellationToken);
            return root;
        }

        private static int EnumerateDirs(TreeDirInfo parentDir, int initialIndex, FileFilterHelper filter,
            CancellationToken cancellationToken)
        {
            int index = initialIndex;
            int count = 0;
            foreach (var dir in (parentDir.FileSystemInfo as DirectoryInfo).EnumerateDirectories())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (filter != null && !filter.IsMatched(dir))
                {
                    continue;
                }

                var childDir = new TreeDirInfo(dir, parentDir.TopDirectory, parentDir, parentDir.Depth + 1, index++);
                parentDir.AddSub(childDir);

                try
                {
                    EnumerateDirsAndFiles(childDir, filter, cancellationToken);
                }
                catch (UnauthorizedAccessException)
                {
                    childDir.Warn("没有访问权限");
                }
                catch (Exception ex)
                {
                    childDir.Warn("枚举子文件和目录失败：" + ex.Message);
                }

                count++;
            }

            return index;
        }

        private static void EnumerateDirsAndFiles(TreeDirInfo dir, FileFilterHelper filter,
            CancellationToken cancellationToken)
        {
            int tempIndex = EnumerateDirs(dir, 0, filter, cancellationToken);
            EnumerateFiles(dir, tempIndex, filter, cancellationToken);
        }

        private static int EnumerateFiles(TreeDirInfo parentDir, int initialIndex, FileFilterHelper filter,
            CancellationToken cancellationToken)
        {
            int index = initialIndex;
            int count = 0;
            foreach (var file in (parentDir.FileSystemInfo as DirectoryInfo).EnumerateFiles())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (filter != null && !filter.IsMatched(file))
                {
                    continue;
                }

                var childFile = new TreeFileInfo(file, parentDir.TopDirectory, parentDir, parentDir.Depth + 1, index++);
                parentDir.AddSub(childFile);
                count++;
            }

            return index;
        }

        #endregion

        #region 手动添加

        /// <summary>
        /// 创建一个空实例
        /// </summary>
        /// <returns></returns>
        public static TreeDirInfo CreateEmptyTree()
        {
            return new TreeDirInfo();
        }

        /// <summary>
        /// 增加一个文件，将根据相对文件路径自动创建不存在的子目录并将文件放置到合适的子目录下
        /// </summary>
        /// <param name="file"></param>
        public void AddFile(SimpleFileInfo file)
        {
            var relativePath = file.RelativePath;
            var parts = relativePath.Split(pathSeparator, StringSplitOptions.RemoveEmptyEntries);
            Add(parts, file, 0);
        }

        public TreeDirInfo AddSubDir(string dirName)
        {
            var subDir = new TreeDirInfo()
            {
                Name = dirName,
                TopDirectory = TopDirectory,
                Path = Path == null ? dirName : System.IO.Path.Combine(Path, dirName),
                Depth = Depth + 1,
                Index = subs.Count,
                IsDir = true,
            };
            AddSub(subDir);
            return subDir;
        }

        public TreeDirInfo AddSubDirByPath(string dirPath)
        {
            var subDir = new TreeDirInfo()
            {
                Name = System.IO.Path.GetFileName(dirPath),
                TopDirectory = TopDirectory,
                Path = dirPath,
                Depth = Depth + 1,
                Index = subs.Count,
                IsDir = true,
            };
            AddSub(subDir);
            return subDir;
        }

        public TreeFileInfo AddSubFile(SimpleFileInfo file)
        {
            if (file is TreeFileInfo treeFile)
            {
                treeFile.Depth = Depth + 1;
                treeFile.Index = subs.Count;
            }
            else
            {
                treeFile = new TreeFileInfo(file, this, Depth + 1, subs.Count);
            }

            AddSub(treeFile);
            return treeFile;
        }

        /// <summary>
        /// 增加节点
        /// </summary>
        /// <param name="pathParts"></param>
        /// <param name="file"></param>
        /// <param name="depth"></param>
        private void Add(string[] pathParts, SimpleFileInfo file, int depth)
        {
            //这里的depth和this.Depth可能不同，比如在非顶级目录调用Add方法
            if (depth == pathParts.Length - 1)
            {
                if (file is TreeFileInfo treeFile)
                {
                    treeFile.Depth = Depth + 1;
                    treeFile.Index = subs.Count;
                }
                else
                {
                    treeFile = new TreeFileInfo(file, this, Depth + 1, subs.Count);
                }

                AddSub(treeFile);
                return;
            }

            string name = pathParts[depth];
            if (!subDirsDic.TryGetValue(name, out TreeDirInfo subDir))
            {
                subDir = new TreeDirInfo()
                {
                    Name = name,
                    TopDirectory = TopDirectory,
                    Path = Path == null ? name : System.IO.Path.Combine(Path, name),
                    Depth = Depth + 1,
                    Index = subs.Count,
                    IsDir = true,
                };
                AddSub(subDir);
            }

            subDir.Add(pathParts, file, depth + 1);
        }

        #endregion

        #region 操作

        /// <summary>
        /// 展开为平铺的文件列表
        /// </summary>
        /// <param name="includingDir"></param>
        /// <returns></returns>
        public IEnumerable<TreeFileDirInfo> Flatten(bool includingDir = false)
        {
            Stack<TreeDirInfo> stack = new Stack<TreeDirInfo>();
            stack.Push(this);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (includingDir)
                {
                    yield return current;
                }

                foreach (var subFile in current.SubFiles)
                {
                    yield return subFile;
                }

                foreach (var subDir in current.SubDirs.Reverse())
                {
                    stack.Push(subDir);
                }
            }
        }

        public void Reorder()
        {
            if (subDirs.Count > 0)
            {
                subDirs.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
                foreach (var dir in subDirs)
                {
                    dir.Reorder();
                }
            }

            if (subFiles.Count > 0)
            {
                subFiles.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            }

            if (subs.Count > 0)
            {
                subs.Clear();
                subs.AddRange(subDirs);
                subs.AddRange(subFiles);
            }

            for (int i = 0; i < subs.Count; i++)
            {
                subs[i].Index = i;
            }
        }


        public List<TreeFileDirInfo> Search(string fileName)
        {
            List<TreeFileDirInfo> list = new List<TreeFileDirInfo>();
            SearchInternal(fileName, list);
            return list;
        }

        private void SearchInternal(string fileName, List<TreeFileDirInfo> list)
        {
            list.AddRange(subDirs.Where(dir => dir.Name.Contains(fileName)));
            subDirs.ForEach(p => p.SearchInternal(fileName, list));
            list.AddRange(subFiles.Where(file => file.Name.Contains(fileName)));
        }

        #endregion

        #region JSON序列化

        public string ToJson()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions()
            {
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                WriteIndented = true,
                MaxDepth = 64,
            });
        }

        public static TreeDirInfo FromJson(string json)
        {
            var tree= JsonSerializer.Deserialize<TreeDirInfo>(json, new JsonSerializerOptions()
            {
                Converters = { new TreeDirInfoConverter() }
            });
            RepairParent(tree);
            return tree;
        }

        private static void RepairParent(TreeDirInfo node)
        {
            foreach (var subDir in node.SubDirs)
            {
                subDir.Parent = node;
                RepairParent(subDir);
            }
            foreach (var subFile in node.SubFiles)
            {
                subFile.Parent = node;
            }
        }

        class TreeDirInfoConverter : JsonConverter<TreeDirInfo>
        {
            public override TreeDirInfo Read(ref Utf8JsonReader reader, Type typeToConvert,
                JsonSerializerOptions options)
            {
                using JsonDocument doc = JsonDocument.ParseValue(ref reader);
                var root = doc.RootElement;

                // 反序列化其他属性
                var treeDirInfo = root.Deserialize<TreeDirInfo>(); //不提供options，否则会无限循环（Converters）

                // 获取 JSON 中的子目录和子文件信息
                treeDirInfo.subDirs =
                    JsonSerializer.Deserialize<List<TreeDirInfo>>(root.GetProperty("SubDirs").GetRawText(), options);
                treeDirInfo.subFiles =
                    JsonSerializer.Deserialize<List<TreeFileInfo>>(root.GetProperty("SubFiles").GetRawText(), options);

                // 合并 SubDirs 和 SubFiles 到 Subs 中
                treeDirInfo.subs = treeDirInfo.subDirs.Cast<TreeFileDirInfo>()
                    .Concat(treeDirInfo.subFiles.Cast<TreeFileDirInfo>()).ToList();

                // 构建subDirsDic
                treeDirInfo.subDirsDic = treeDirInfo.subDirs.ToDictionary(p => p.Name);

                return treeDirInfo;
            }

            public override void Write(Utf8JsonWriter writer, TreeDirInfo value, JsonSerializerOptions options)
            {
                JsonSerializer.Serialize(writer, value, options);
            }
        }

        #endregion
    }
}