using ArchiveMaster.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.IO;

namespace ArchiveMaster.Configs
{
    public abstract class ConfigBase : ObservableObject
    {
        public abstract void Check();

        protected static void CheckEmpty(object value, string name)
        {
            if (value == null || value is string s && string.IsNullOrWhiteSpace(s))
            {
                throw new Exception($"{name}为空");
            }
        }

        protected static void CheckFile(string filePath, string name, bool checkWhenNotEmptyOnly = false)
        {
            if (checkWhenNotEmptyOnly && string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            CheckEmpty(filePath, name);
            if (FileNameHelper.GetFileNames(filePath, false).Any(f => !File.Exists(f)))
            {
                throw new Exception($"{name}不存在");
            }
        }


        protected static void CheckDir(string dirPath, string name)
        {
            CheckEmpty(dirPath, name);
            foreach (var f in FileNameHelper.GetDirNames(dirPath, false))
            {
                if (!Directory.Exists(f))
                {
                    throw new Exception($"{name}不存在");
                }
            }
        }

        protected static void CheckRange<T>(T value, T min, T max, string name) where T : IComparable
        {
            if (value.CompareTo(min) > 0 || value.CompareTo(max) < 0)
            {
                throw new Exception($"{name}超出范围");
            }
        }
    }
}