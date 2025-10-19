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
    private TextGenerationCategory category;

    [ObservableProperty]
    private ExpressionOptimizationType expressionOptimizationType;

    [ObservableProperty]
    private StructuralAdjustmentType structuralAdjustmentType;

    [ObservableProperty]
    private ContentTransformationType contentTransformationType;

    [ObservableProperty]
    private TextEvaluationType textEvaluationType;

    public override void Check()
    {
        switch (Category)
        {
            case TextGenerationCategory.Custom:
                CheckEmpty(CustomPrompt, "自定义提示");
                break;
            case TextGenerationCategory.ContentTransformation
                when ContentTransformationType == ContentTransformationType.Translation:
                CheckEmpty(TranslationTargetLanguage, "翻译目标语言");
                break;
            default:
                break;
        }
    }
}