using ArchiveMaster.Enums;
using ArchiveMaster.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.Configs;

public partial class TextRewriterConfig : ConfigBase
{
    [ObservableProperty]
    private string customPrompt;

    [ObservableProperty]
    private string extraAiPrompt;

    [ObservableProperty]
    private TextSource source = new TextSource();

    [ObservableProperty]
    private string translationTargetLanguage;

    [ObservableProperty]
    private TextRewriterType type;
    public override void Check()
    {
        switch (Type)
        {
            case TextRewriterType.Custom:
                CheckEmpty(CustomPrompt, "自定义提示");
                break;
            case TextRewriterType.Translation:
                CheckEmpty(TranslationTargetLanguage, "翻译目标语言");
                break;
            default:
                break;
        }
    }
}