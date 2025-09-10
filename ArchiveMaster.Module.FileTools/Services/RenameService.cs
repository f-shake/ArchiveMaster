using System.Collections;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Helpers;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;
using Avalonia.Media;
using FzLib.Avalonia.Converters;
using FzLib.IO;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using RenameFileInfo = ArchiveMaster.ViewModels.FileSystem.RenameFileInfo;

namespace ArchiveMaster.Services;

public class RenameService(AppConfig appConfig)
    : TwoStepServiceBase<RenameConfig>(appConfig)
{
    private static readonly Dictionary<string, Regex> regexes = new Dictionary<string, Regex>();
    private static Dictionary<string, Script<string>> csScripts = new Dictionary<string, Script<string>>();
    private RegexOptions regexOptions;
    private StringComparison stringComparison;
    public IReadOnlyList<RenameFileInfo> Files { get; private set; }

    public override async Task ExecuteAsync(CancellationToken ct = default)
    {
        var processingFiles = Files.Where(p => p.IsMatched && p.IsChecked).ToList();
        var duplicates = processingFiles
            .Select(p => p.GetNewPath())
            .GroupBy(p => p)
            .Where(p => p.Count() > 1)
            .Select(p => p.Key);
        if (duplicates.Any())
        {
            throw new Exception("有一些文件（夹）的目标路径相同：" + string.Join('、', duplicates));
        }

        //重命名为临时文件名，避免有可能新的文件名和其他文件的旧文件名一致导致错误的问题
        //假设直接重命名文件 A -> B，但同时另一个文件正在从 B -> C，可能会导致冲突，因为文件系统在操作过程中会认为目标路径已经存在。
        //临时名称中转确保了重命名操作在一个独立的“空间”中完成，避免了名称重叠的问题。
        await TryForFilesAsync(processingFiles, (file, s) =>
        {
            NotifyMessage($"正在重命名（第一步，共二步）{s.GetFileNumberMessage()}：{file.Name}=>{file.NewName}");
            file.TempPath = Path.Combine(Path.GetDirectoryName(file.Path), Guid.NewGuid().ToString());
            if (file.IsDir)
            {
                Directory.Move(file.Path, file.TempPath);
            }
            else
            {
                File.Move(file.Path, file.TempPath);
            }
        }, ct, FilesLoopOptions.Builder().AutoApplyFileNumberProgress().Build());

        //重命名为目标文件名
        await TryForFilesAsync(processingFiles, (file, s) =>
        {
            NotifyMessage($"正在重命名（第二步，共二步）{s.GetFileNumberMessage()}：{file.Name}=>{file.NewName}");
            if (file.IsDir)
            {
                Directory.Move(file.TempPath, file.GetNewPath());
            }
            else
            {
                File.Move(file.TempPath, file.GetNewPath());
            }
        }, ct, FilesLoopOptions.Builder().AutoApplyStatus().AutoApplyFileNumberProgress().Build());
    }

    public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
    {
        return Files.Cast<RenameFileInfo>();
    }

    public override async Task InitializeAsync(CancellationToken ct = default)
    {
        regexOptions = Config.IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
        stringComparison = Config.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        NotifyProgressIndeterminate();
        NotifyMessage("正在查找文件");


        await Task.Run(async () =>
        {
            HashSet<string> usedPaths = new HashSet<string>(FileHelper.GetStringComparer());
            List<RenameFileInfo> renameFiles = null;

            if (Config.Manual)
            {
                renameFiles = ProcessManual(usedPaths);
            }
            else
            {
                renameFiles = await ProcessAutoAsync(usedPaths);
            }

            //有一种情况：三个文件，abc、abbc、abbbbc，b重命名为bb，
            //实际是无冲突的，但若直接检测会认为有冲突
            //所以采用了一些方法来规避这个问题，但不完美。
            foreach (var renameFile in renameFiles
                         .Where(p => p.IsMatched)
                         .Where(p => p.Name != p.NewName))
            {
                string desiredPath = renameFile.GetNewPath();

                string finalPath = FileNameHelper.GenerateUniquePath(desiredPath, usedPaths);
                if (finalPath != desiredPath)
                {
                    renameFile.HasUniqueNameProcessed = true;
                }

                renameFile.NewName = Path.GetFileName(finalPath);
                usedPaths.Add(finalPath);
            }

            Files = renameFiles.AsReadOnly();
        }, ct);
    }

    private Regex GetRegex(string pattern)
    {
        if (regexes.TryGetValue(pattern, out Regex r))
        {
            return r;
        }

        r = new Regex(pattern, regexOptions);
        regexes.Add(pattern, r);
        return r;
    }

    private bool IsMatched(RenameFileInfo file)
    {
        string name = Config.SearchPath ? file.Path : file.Name;

        return Config.SearchMode switch
        {
            SearchMode.Contain => name.Contains(Config.SearchPattern, stringComparison),
            SearchMode.EqualWithExtension => Path.GetExtension(name).Equals(Config.SearchPattern, stringComparison),
            SearchMode.EqualWithName => Path.GetFileNameWithoutExtension(name)
                .Equals(Config.SearchPattern, stringComparison),
            SearchMode.Equal => name.Equals(Config.SearchPattern, stringComparison),
            SearchMode.Regex => GetRegex(Config.SearchPattern).IsMatch(name),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private async Task<List<RenameFileInfo>> ProcessAutoAsync(HashSet<string> usedPaths)
    {
        FilePlaceholderReplacer placeholderReplacer = new FilePlaceholderReplacer(Config.ReplacePattern ?? "");

        // 获取所有待处理文件
        IEnumerable<FileSystemInfo> files = new DirectoryInfo(Config.Dir)
            .EnumerateFileSystemInfos("*", FileEnumerateExtension.GetEnumerationOptions(Config.IncludeSubDirs));
        List<RenameFileInfo> renameFiles = new List<RenameFileInfo>();


        foreach (var file in files)
        {
            if (file is FileInfo && Config.RenameTarget == RenameTargetType.Folder ||
                file is DirectoryInfo && Config.RenameTarget == RenameTargetType.File)
            {
                usedPaths.Add(file.FullName);
                continue;
            }

            var renameFile = new RenameFileInfo(file, Config.Dir);
            renameFile.IsMatched = IsMatched(renameFile);

            if (renameFile.IsMatched)
            {
                string originalNewName = await RenameAsync(placeholderReplacer, renameFile);
                renameFile.NewName = originalNewName; // 临时存储原始目标名称
            }
            else
            {
                usedPaths.Add(file.FullName);
            }

            renameFiles.Add(renameFile);
        }

        return renameFiles;
    }

    private List<RenameFileInfo> ProcessManual(HashSet<string> usedPaths)
    {
        List<RenameFileInfo> renameFiles = new List<RenameFileInfo>();
        HashSet<string> checkedDirs = new HashSet<string>();
        if (string.IsNullOrWhiteSpace(Config.ManualMaps))
        {
            return renameFiles;
        }

        var lines = Config.ManualMaps.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim());
        int index = 0;
        foreach (var line in lines)
        {
            index++;
            var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).GetEnumerator();
            if (!parts.MoveNext())
            {
                throw new FormatException($"第{index}行为空");
            }

            RenameFileInfo renameFile = null;

            var path = parts.Current;
            if (!parts.MoveNext())
            {
                throw new FormatException($"第{index}行（{line}）不包含分隔符");
            }

            var newName = parts.Current;

            if (parts.MoveNext())
            {
                throw new FormatException($"第{index}行（{line}）分隔符数量过多");
            }

            if (File.Exists(path))
            {
                renameFile = new RenameFileInfo(new FileInfo(path), "/")
                {
                    NewName = newName,
                    IsMatched = true
                };
            }
            else if (Directory.Exists(path))
            {
                renameFile = new RenameFileInfo(new DirectoryInfo(path), "/")
                {
                    NewName = newName,
                    IsMatched = true
                };
            }
            else
            {
                throw new FileNotFoundException($"第{index}行的文件或目录（{path}）不存在");
            }

            var dir = Path.GetDirectoryName(path);
            if (checkedDirs.Add(dir))
            {
                foreach (var file in Directory.EnumerateFileSystemEntries(dir))
                {
                    usedPaths.Add(file);
                }
            }

            renameFiles.Add(renameFile);
        }

        return renameFiles;
    }

    private async Task<string> RenameAsync(FilePlaceholderReplacer replacer, RenameFileInfo file)
    {
        string name = file.Name;
        string matched = null;
        if (Config.RenameMode is RenameMode.ReplaceMatched or RenameMode.RetainMatched
            or RenameMode.RetainMatchedExtension)
        {
            matched = Config.SearchMode == SearchMode.Regex
                ? GetRegex(Config.SearchPattern).Match(name).Value
                : Config.SearchPattern;
        }

        return Config.RenameMode switch
        {
            RenameMode.ReplaceMatched => name.Replace(matched, replacer.GetTargetName(file), stringComparison),
            RenameMode.ReplaceExtension => $"{Path.GetFileNameWithoutExtension(name)}.{replacer.GetTargetName(file)}",
            RenameMode.ReplaceName => replacer.GetTargetName(file) + Path.GetExtension(name),
            RenameMode.ReplaceAll => replacer.GetTargetName(file),
            RenameMode.RetainMatched => matched,
            RenameMode.RetainMatchedExtension => $"{matched}{Path.GetExtension(name)}",
            RenameMode.Csharp => await ReplaceCsharp(matched, name, file, replacer.Template),
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    private async Task<string> ReplaceCsharp(string matched, string name, RenameFileInfo file, string code)
    {
        try
        {
            var globals = new CustomCsharpRenameData
            {
                matched = matched,
                file = file,
            };

            if (!csScripts.TryGetValue(code, out var cs))
            {
                var options = ScriptOptions.Default
                    .AddReferences(
                        typeof(object).Assembly,
                        typeof(Enumerable).Assembly,
                        typeof(Regex).Assembly,
                        typeof(MD5).Assembly,
                        typeof(RenameFileInfo).Assembly
                    )
                    .AddImports(
                        nameof(System),
                        typeof(Path).Namespace,
                        typeof(Encoding).Namespace,
                        typeof(Enumerable).Namespace,
                        typeof(Regex).Namespace,
                        typeof(Math).Namespace,
                        typeof(MD5).Namespace,
                        typeof(RenameFileInfo).Namespace
                    );

                // 关键修改：添加 globalsType 参数
                cs = CSharpScript.Create<string>(
                    code,
                    options,
                    globalsType: typeof(CustomCsharpRenameData)); // 指定全局变量类型
                csScripts.Add(code, cs);
            }

            return (await cs.RunAsync(globals)).ReturnValue;
        }
        catch (CompilationErrorException ex)
        {
            throw new Exception($"C# 脚本编译错误: {string.Join("\n", ex.Diagnostics)}");
        }
        catch (Exception ex)
        {
            throw new Exception($"执行 C# 脚本时出错: {ex.Message}");
        }
    }

    public class CustomCsharpRenameData
    {
        public RenameFileInfo file { get; set; }
        public string matched { get; set; }
    }
}