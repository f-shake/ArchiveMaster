using FzLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Helpers;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.Programming;
using MatchingFileInfo = ArchiveMaster.ViewModels.FileSystem.MatchingFileInfo;

namespace ArchiveMaster.Services
{
    public class DirStructureSyncService(AppConfig appConfig)
        : TwoStepServiceBase<DirStructureSyncConfig>(appConfig)
    {
        private readonly Dictionary<long, object> length2Template = new Dictionary<long, object>();
        private readonly Dictionary<DateTime, object> modifiedTime2Template = new Dictionary<DateTime, object>();
        private readonly Dictionary<string, object> name2Template = new Dictionary<string, object>();
        public IList<MatchingFileInfo> ExecutingFiles { get; set; }
        public List<MatchingFileInfo> RightPositionFiles { get; private set; }
        public List<MatchingFileInfo> WrongPositionFiles { get; private set; }


        public override Task ExecuteAsync(CancellationToken ct)
        {
            if (ExecutingFiles == null)
            {
                throw new NullReferenceException($"{nameof(ExecutingFiles)}为空");
            }

            string copyMoveText = Config.Copy ? "复制" : "移动";
            IEnumerable<MatchingFileInfo> files = ExecutingFiles;

            if (!Config.Copy)
            {
                files = files.Where(p => !p.RightPosition);
            }

            files = files.ToList();

            return TryForFilesAsync(files, (file, s) =>
            {
                NotifyMessage($"正在{copyMoveText}{s.GetFileNumberMessage()}：{file.RelativePath}");
                string destFile = Path.Combine(Config.TargetDir, file.Template.RelativePath);
                string destFileDir = Path.GetDirectoryName(destFile);
                if (!Directory.Exists(destFileDir))
                {
                    Directory.CreateDirectory(destFileDir);
                }

                if (Config.Copy)
                {
                    File.Copy(Path.Combine(Config.SourceDir, file.RelativePath), destFile);
                }
                else
                {
                    File.Move(Path.Combine(Config.SourceDir, file.RelativePath), destFile);
                }
            }, ct, FilesLoopOptions.Builder().AutoApplyStatus().AutoApplyFileNumberProgress().Build());
        }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            return WrongPositionFiles.Concat(RightPositionFiles).Cast<SimpleFileInfo>();
        }

        public override async Task InitializeAsync(CancellationToken ct)
        {
            List<MatchingFileInfo> rightPositionFiles = new List<MatchingFileInfo>();
            List<MatchingFileInfo> wrongPositionFiles = new List<MatchingFileInfo>();

            List<SimpleFileInfo> notMatchedFiles = new List<SimpleFileInfo>();
            List<SimpleFileInfo> matchedFiles = new List<SimpleFileInfo>();

            List<MatchingFileInfo> tempFiles = new List<MatchingFileInfo>();
            await Task.Run(() =>
            {
                //分析模板目录，创建由属性指向文件的字典
                CreateDictionaries(Config.TemplateDir, Config.MaxTimeToleranceSecond, ct);

                NotifyMessage($"正在查找源文件");

                //枚举源目录
                var sourceFiles = new DirectoryInfo(Config.SourceDir)
                    .EnumerateFiles("*", FileEnumerateExtension.GetEnumerationOptions())
                    .ApplyFilter(ct, Config.Filter)
                    .Select(p => new SimpleFileInfo(p, Config.SourceDir))
                    .ToList();

                TryForFiles(sourceFiles, (sourceFile, s) =>
                {
                    NotifyMessage($"正在分析源文件{s.GetFileNumberMessage()}：{sourceFile.RelativePath}");
                    matchedFiles.Clear();
                    tempFiles.Clear();

                    //与模板文件进行匹配
                    bool notMatched = false;
                    if (Config.CompareName) GetMatchedFiles(name2Template, sourceFile.Name);
                    if (!notMatched && Config.CompareTime)
                        GetMatchedFiles(modifiedTime2Template, sourceFile.Time.TruncateToSecond());
                    if (!notMatched && Config.CompareLength) GetMatchedFiles(length2Template, sourceFile.Length);

                    if (notMatched) //无匹配，不需要继续操作
                    {
                        notMatchedFiles.Add(sourceFile);
                        return;
                    }

                    foreach (var templateFile in matchedFiles)
                    {
                        //创建模板文件数据结构
                        var template = new SimpleFileInfo(templateFile.FileSystemInfo, Config.TemplateDir); //模板文件

                        //创建模型
                        var goHomeFile =
                            new MatchingFileInfo(sourceFile.FileSystemInfo as FileInfo, Config.SourceDir) //源文件
                            {
                                MultipleMatches = matchedFiles.Count > 1,
                                Template = template,
                            };

                        goHomeFile.RightPosition = template.RelativePath == goHomeFile.RelativePath;
                        tempFiles.Add(goHomeFile);
                    }

                    if (tempFiles.Count == 1) //如果只有一个匹配的文件
                    {
                        if (tempFiles[0].RightPosition) //位置正确
                        {
                            rightPositionFiles.Add(tempFiles[0]);
                        }
                        else //位置错误
                        {
                            tempFiles[0].IsChecked = true;
                            wrongPositionFiles.Add(tempFiles[0]);
                        }
                    }
                    else //有多个匹配
                    {
                        if (tempFiles.Any(p => p.RightPosition))
                        {
                            var tempWrongPositionFiles = tempFiles.Where(p => !p.RightPosition);
                            tempWrongPositionFiles.ForEach(p => p.Warn("包含一个位置正确的匹配"));
                            wrongPositionFiles.AddRange(tempFiles.Where(p => !p.RightPosition));
                            rightPositionFiles.Add(tempFiles.First(p => p.RightPosition));
                        }
                        else
                        {
                            wrongPositionFiles.AddRange(tempFiles);
                        }
                    }


                    //在指定字典中查找符合所给的属性值的文件
                    void GetMatchedFiles<TK>(Dictionary<TK, object> dic, TK key)
                    {
                        object files = null;
                        if (!dic.TryGetValue(key, out files))
                        {
                            notMatched = true;
                            return;
                        }

                        if (matchedFiles.Count == 0) //如果notMatched==false，但matchedFiles.Count==0，说明这是第一个匹配
                        {
                            if (files is SimpleFileInfo f)
                            {
                                matchedFiles.Add(f);
                            }
                            else if (files is List<SimpleFileInfo> list)
                            {
                                matchedFiles.AddRange(list);
                            }
                            else
                            {
                                throw new Exception();
                            }
                        }
                        else //如果不是第一个匹配，已经有规则匹配了
                        {
                            if (files is SimpleFileInfo f)
                            {
                                if (!matchedFiles.Contains(f)) //单个文件，但是当前匹配不在之前匹配的文件中，说明匹配失败
                                {
                                    matchedFiles.Clear();
                                }
                                else //否则，修改为本次匹配的单个文件
                                {
                                    matchedFiles.Clear();
                                    matchedFiles.Add(f);
                                }
                            }
                            else if (files is List<SimpleFileInfo> list)
                            {
                                var oldMatchedFiles = new List<SimpleFileInfo>(matchedFiles);
                                matchedFiles.Clear();
                                matchedFiles.AddRange(oldMatchedFiles.Intersect(list));
                            }
                            else
                            {
                                throw new Exception();
                            }

                            if (matchedFiles.Count == 0) //如果经过这一轮的匹配，没有任何文件完成匹配了，说明总的匹配失败
                            {
                                notMatched = true;
                            }
                        }
                    }
                }, ct, FilesLoopOptions.Builder().AutoApplyFileNumberProgress().ThrowExceptions().Build());
            }, ct);
            RightPositionFiles = rightPositionFiles;
            WrongPositionFiles = wrongPositionFiles;
        }

        private void CreateDictionaries(string dir, int maxTimeTolerance, CancellationToken ct)
        {
            name2Template.Clear();
            length2Template.Clear();
            modifiedTime2Template.Clear();
            var fileInfos = new DirectoryInfo(dir)
                .EnumerateFiles("*", FileEnumerateExtension.GetEnumerationOptions())
                .ApplyFilter(ct)
                .Select(p => new SimpleFileInfo(p, dir))
                .ToList();

            NotifyProgressIndeterminate();

            foreach (var file in fileInfos)
            {
                ct.ThrowIfCancellationRequested();

                NotifyMessage($"正在分析模板文件：{file.RelativePath}");
                SetOrAdd(name2Template, file.Name);
                SetOrAdd(length2Template, file.Length);
                for (int i = -maxTimeTolerance; i <= maxTimeTolerance; i++)
                {
                    SetOrAdd(modifiedTime2Template, file.Time.TruncateToSecond().AddSeconds(i));
                }


                void SetOrAdd<TK>(Dictionary<TK, object> dic, TK key)
                {
                    if (dic.ContainsKey(key))
                    {
                        if (dic[key] is List<SimpleFileInfo> list)
                        {
                            list.Add(file);
                        }
                        else if (dic[key] is SimpleFileInfo f)
                        {
                            dic[key] = new List<SimpleFileInfo> { f, file };
                        }
                        else
                        {
                            throw new Exception();
                        }
                    }
                    else
                    {
                        dic.Add(key, file);
                    }
                }
            }
        }
    }
}