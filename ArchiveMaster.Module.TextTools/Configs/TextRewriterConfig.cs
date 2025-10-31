using System.ComponentModel;
using System.Text.Json.Serialization;
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
    private CustomType customType;

    [NotifyPropertyChangedFor(nameof(CurrentMode))]
    [ObservableProperty]
    private ExpressionOptimizationType expressionOptimizationType;

    [property: JsonIgnore]
    [ObservableProperty]
    private string extraAiPrompt;

    [property: JsonIgnore]
    [ObservableProperty]
    private string extraInformation;

    [property: JsonIgnore]
    [ObservableProperty]
    private TextSource referenceSource = new TextSource() { FromFile = false };

    [ObservableProperty]
    private TextSource source = new TextSource();

    [NotifyPropertyChangedFor(nameof(CurrentMode))]
    [ObservableProperty]
    private StructuralAdjustmentType structuralAdjustmentType;

    [NotifyPropertyChangedFor(nameof(CurrentMode))]
    [ObservableProperty]
    private TextEvaluationType textEvaluationType;

    public event EventHandler ModeChanged;

    public Enum CurrentMode => GetCurrentAgent().Item;

    public override void Check()
    {
        if (Source.IsEmpty())
        {
            throw new ArgumentException("文本源为空");
        }

        var current = GetCurrentAgent();
        if (current.Attribute.NeedExtraInformation)
        {
            CheckEmpty(ExtraInformation, current.Attribute.ExtraInformationLabel);
        }

        if (current.Attribute.NeedReferenceText && ReferenceSource.IsEmpty())
        {
            throw new ArgumentException("参考文本为空");
        }
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