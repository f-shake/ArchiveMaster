using System.Drawing;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.Configs
{
    public partial class TimeAssConfig : ConfigBase
    {
        [ObservableProperty]
        private int alignment;
        [ObservableProperty]
        private bool bold;
        [ObservableProperty]
        private Color borderColor;
        [ObservableProperty]
        private int borderWidth;
        [ObservableProperty]
        private string font;
        [ObservableProperty]
        private Color textColor;
        [ObservableProperty]
        private string format;
        [ObservableProperty]
        private int interval;
        [ObservableProperty]
        private bool italic;
        [ObservableProperty]
        private int margin;
        [ObservableProperty]
        private int size;
        [ObservableProperty]
        private bool underline;
        public override void Check()
        {
        }
    }
}