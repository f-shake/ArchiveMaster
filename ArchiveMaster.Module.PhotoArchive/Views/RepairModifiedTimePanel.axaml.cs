using Avalonia.Controls;
using ArchiveMaster.ViewModels;

namespace ArchiveMaster.Views
{
    public partial class RepairModifiedTimePanel : TwoStepPanelBase
    {
        public RepairModifiedTimePanel(RepairModifiedTimeViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}
