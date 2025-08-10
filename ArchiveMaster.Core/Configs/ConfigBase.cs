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
    }
}