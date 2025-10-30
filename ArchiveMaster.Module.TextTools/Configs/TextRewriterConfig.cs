using System.ComponentModel;
using ArchiveMaster.Attributes;
using ArchiveMaster.Enums;
using ArchiveMaster.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.Configs;

public partial class TextRewriterConfig : ConfigBase
{
    [NotifyPropertyChangedFor(nameof(CurrentMode))]
    [ObservableProperty]
    private TextGenerationCategory category;

    [NotifyPropertyChangedFor(nameof(CurrentMode))]
    [ObservableProperty]
    private ContentTransformationType contentTransformationType;

    [NotifyPropertyChangedFor(nameof(CurrentMode))]
    [ObservableProperty]
    private ExpressionOptimizationType expressionOptimizationType;

    [ObservableProperty]
    private string extraAiPrompt;

    [ObservableProperty]
    private TextSource referenceSource = new TextSource();

    [ObservableProperty]
    private TextSource source = new TextSource();

    [NotifyPropertyChangedFor(nameof(CurrentMode))]
    [ObservableProperty]
    private StructuralAdjustmentType structuralAdjustmentType;

    [NotifyPropertyChangedFor(nameof(CurrentMode))]
    [ObservableProperty]
    private TextEvaluationType textEvaluationType;
    
    [NotifyPropertyChangedFor(nameof(CurrentMode))]
    [ObservableProperty]
    private CustomType customType;

    public Enum CurrentMode => GetCurrentAgent().Item;

    [ObservableProperty]
    private string extraInformation;

    public event EventHandler ModeChanged;

    public override void Check()
    {
        // switch (Category)
        // {
        //     case TextGenerationCategory.Custom:
        //         CheckEmpty(CustomPrompt, "自定义提示");
        //         break;
        //     case TextGenerationCategory.ContentTransformation
        //         when ContentTransformationType == ContentTransformationType.Translation:
        //         CheckEmpty(TranslationTargetLanguage, "翻译目标语言");
        //         break;
        //     default:
        //         break;
        // }
    }

    public (AiAgentAttribute Attribute, Enum Item) GetCurrentAgent()
    {
        Enum item = Category switch
        {
            TextGenerationCategory.ExpressionOptimization => ExpressionOptimizationType,
            TextGenerationCategory.StructuralAdjustment => StructuralAdjustmentType,
            TextGenerationCategory.ContentTransformation => ContentTransformationType,
            TextGenerationCategory.TextEvaluation => TextEvaluationType,
            TextGenerationCategory.Custom => CustomType,
            _ => throw new ArgumentOutOfRangeException()
        };
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