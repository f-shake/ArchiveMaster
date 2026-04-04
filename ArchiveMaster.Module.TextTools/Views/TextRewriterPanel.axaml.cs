using System.ComponentModel;
using System.Reflection;
using ArchiveMaster.AiAgents;
using ArchiveMaster.Attributes;
using ArchiveMaster.Enums;
using Avalonia.Controls;
using ArchiveMaster.ViewModels;
using Avalonia.Data;
using FzLib.Avalonia.Controls;

namespace ArchiveMaster.Views
{
    public partial class TextRewriterPanel : VerticalTwoStepPanelBase
    {
        public TextRewriterPanel()
        {
            InitializeComponent();
        }


        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            if (DataContext is TextRewriterViewModel vm)
            {
                vm.PropertyChanged += ViewModelOnPropertyChanged;
            }
        }

        private void ViewModelOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(TextRewriterViewModel.SelectedAiAgent))
            {
                return;
            }

            var aiAgent = ((TextRewriterViewModel)sender).SelectedAiAgent;
            while (stkForm.Children.Count > 3)
            {
                stkForm.Children.RemoveAt(stkForm.Children.Count - 1);
            }

            if (aiAgent == null)
            {
                return;
            }

            var customProperties = aiAgent.GetType().GetProperties()
                .Where(p => p.GetCustomAttribute<AiAgentConfigAttribute>() != null);
            foreach (var propertyInfo in customProperties)
            {
                var attribute = propertyInfo.GetCustomAttribute<AiAgentConfigAttribute>();
                var formItem = new FormItem()
                {
                    Label = attribute.Name+"："
                };
                var binding = new Binding($"{nameof(TextRewriterViewModel.SelectedAiAgent)}.{propertyInfo.Name}");

                switch (attribute.Type)
                {
                    case AiAgentConfigType.Text:
                        var textBox = new TextBox
                        {
                            [!TextBox.TextProperty] = binding
                        };
                        formItem.Content = textBox;
                        break;
                    case AiAgentConfigType.TextSource:
                        var textSource = new TextSourceInput
                        {
                            [!TextSourceInput.SourceProperty] = binding
                        };
                        formItem.Content = textSource;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(attribute.Type));
                }

                stkForm.Children.Add(formItem);
            }

            if (aiAgent.CanUserSetExtraPrompt)
            {
                FormItem formItem = new FormItem()
                {
                    Label = "额外要求："
                };
                var textBox = new TextBox
                {
                    [!TextBox.TextProperty] =
                        new Binding(
                            $"{nameof(TextRewriterViewModel.SelectedAiAgent)}.{nameof(AiAgentBase.ExtraPrompt)}")
                };
                formItem.Content = textBox;
                stkForm.Children.Add(formItem);
            }
        }
    }
}