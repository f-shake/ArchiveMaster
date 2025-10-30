using System.ComponentModel;
using ArchiveMaster.Attributes;
using ArchiveMaster.Enums;
using ArchiveMaster.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.Configs;

public partial class TextRewriterConfig : ConfigBase
{
    [ObservableProperty]
    private TextGenerationCategory category;

    [ObservableProperty]
    private ContentTransformationType contentTransformationType;

    [ObservableProperty]
    private string customPrompt;

    [ObservableProperty]
    private ExpressionOptimizationType expressionOptimizationType;

    [ObservableProperty]
    private string extraAiPrompt;

    [ObservableProperty]
    private TextSource referenceSource = new TextSource();

    [ObservableProperty]
    private TextSource source = new TextSource();
 
    [ObservableProperty]
    private StructuralAdjustmentType structuralAdjustmentType;

    [ObservableProperty]
    private TextEvaluationType textEvaluationType;

    [ObservableProperty]
    private string translationTargetLanguage;
    public event EventHandler ModeChanged;

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

    public (AiAgentAttribute Attribute, Enum) GetCurrentAgent()
    {
        Enum item = Category switch
        {
            TextGenerationCategory.ExpressionOptimization => ExpressionOptimizationType,
            TextGenerationCategory.StructuralAdjustment => StructuralAdjustmentType,
            TextGenerationCategory.ContentTransformation => ContentTransformationType,
            TextGenerationCategory.TextEvaluation => TextEvaluationType,
            TextGenerationCategory.Custom => TextGenerationCategory.Custom,
            _ => throw new ArgumentOutOfRangeException()
        };
        if (TextGenerationCategory.Custom.Equals(item))
        {
            return (
                new AiAgentAttribute("自定义", "自定义提示", CustomPrompt),
                TextGenerationCategory.Custom);
        }

        return (GetAiPrompt(item), item);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(Category)
            or nameof(ExpressionOptimizationType)
            or nameof(StructuralAdjustmentType)
            or nameof(ContentTransformationType)
            or nameof(TextEvaluationType))
        {
            ModeChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    private static AiAgentAttribute GetAiPrompt(Enum type)
    {
        var field = type.GetType().GetField(type.ToString());
        var attr = field?.GetCustomAttributes(typeof(AiAgentAttribute), false);
        return attr is { Length: > 0 }
            ? (AiAgentAttribute)attr[0]
            : throw new ArgumentException("未找到提示");
    }
}