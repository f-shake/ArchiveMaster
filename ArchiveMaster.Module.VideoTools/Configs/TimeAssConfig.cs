using System.Drawing;
using ArchiveMaster.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.Configs
{
    public partial class TimeAssConfig : ConfigBase
    {
        [ObservableProperty]
        private TimeAssFormat format = new TimeAssFormat();

        [ObservableProperty]
        private string files;

        public override void Check()
        {
        }
    }
}