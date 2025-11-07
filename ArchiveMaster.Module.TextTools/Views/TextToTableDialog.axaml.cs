using ArchiveMaster.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FzLib.Avalonia.Dialogs;
using FzLib.Text;

namespace ArchiveMaster.Views;

public partial class TextToTableDialog : DialogHost
{
    public HashSet<int> AllowedColumns { get; init; } = [];

    public TextToTableDialog()
    {
    }

    public TextToTableDialog(string message, ICollection<int> allowedColumns)
    {
        AllowedColumns = new HashSet<int>(allowedColumns);
        InitializeComponent();
        tbkMessage.Text = message;
    }

    protected override void OnCloseButtonClick()
    {
        Close();
    }

    protected override void OnPrimaryButtonClick()
    {
        var text = (txt.Text ?? "").Trim();
        if (text.Length == 0)
        {
            tbkMessage.Text = "请输入文本";
            return;
        }

        var separator = GetSeparator();
        var linesText = text.SplitLines();
        List<string[]> lines = new List<string[]>();
        foreach (var line in linesText)
        {
            var parts = line.Split(separator);
            if (AllowedColumns.Count == 0 || AllowedColumns.Contains(parts.Length))
            {
                lines.Add(parts);
            }
            else
            {
                tbkWarning.Text = $"第{lines.Count + 1}行的列数（{parts.Length}）不符合要求（{string.Join("或", AllowedColumns)}列）";
                return;
            }
        }

        Close(lines);
    }

    private char GetSeparator()
    {
        return separator.SelectedIndex switch
        {
            0 => '\t',
            1 => ' ',
            2 => ',',
            3 => '，',
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}