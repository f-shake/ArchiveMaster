using ArchiveMaster.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.Configs
{
    public partial class TwinFileCleanerConfig : ConfigBase
    {
        [ObservableProperty]
        private string dir;

        [ObservableProperty]
        private ObservableStringList masterExtensions = ["DNG","ARW","RW2"];
        
        [ObservableProperty]
        private ObservableStringList deletingPatterns = ["{Name}.JPG","{Name}(*).JPG"];
        
        public override void Check()
        {
            CheckDir(Dir,"目录");
            if (MasterExtensions == null || MasterExtensions.Count == 0)
            {
                throw new Exception("主文件后缀名为空");
            }
            if (DeletingPatterns == null || DeletingPatterns.Count == 0)
            {
                throw new Exception("待删除的附属文件模式列表为空");
            }

            foreach (var pattern in DeletingPatterns.Trimmed)
            {
                if (!pattern.Contains("{Name}"))
                {
                    throw new Exception($"附属文件模式{pattern}中不包含表示原文件名的通配符{{Name}}");
                }
            }
        }
    }
}
