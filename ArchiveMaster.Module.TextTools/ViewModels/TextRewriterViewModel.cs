using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FzLib.Cryptography;
using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.AiAgents;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.Avalonia.Dialogs;
using Serilog;

namespace ArchiveMaster.ViewModels;

public partial class TextRewriterViewModel(ViewModelServices services)
    : AiTwoStepViewModelBase<TextRewriterService, TextRewriterConfig>(services)
{
    [ObservableProperty]
    private string result = "";

    protected override void OnConfigChanged()
    {
        base.OnConfigChanged();
        InitializeAiAgents();
    }

    private static string GetAiAgentGroupDescription(string groupName)
    {
        return groupName switch
        {
            nameof(ArchiveMaster.AiAgents.ContentTransformation) => "内容转换",
            nameof(ArchiveMaster.AiAgents.ExpressionOptimization) => "表达优化",
            nameof(ArchiveMaster.AiAgents.StructuralAdjustment) => "结构调整",
            nameof(ArchiveMaster.AiAgents.TextCorrection) => "文本纠错",
            nameof(ArchiveMaster.AiAgents.TextEvaluation) => "文本评价",
            nameof(ArchiveMaster.AiAgents.Custom) => "自定义",
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private static int GetAiAgentGroupOrder(string groupName)
    {
        return groupName switch
        {
            nameof(ArchiveMaster.AiAgents.ContentTransformation) => 3,
            nameof(ArchiveMaster.AiAgents.ExpressionOptimization) => 1,
            nameof(ArchiveMaster.AiAgents.StructuralAdjustment) => 2,
            nameof(ArchiveMaster.AiAgents.TextCorrection) => 5,
            nameof(ArchiveMaster.AiAgents.TextEvaluation) => 4,
            nameof(ArchiveMaster.AiAgents.Custom) => 6,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private void InitializeAiAgents()
    {
        AiAgentGroups.Clear();

        var configType2AiAgent = Config.AiAgents.Where(p => p != null).ToDictionary(p => p.GetType());

        var assembly = GetType().Assembly;
        var aiAgentTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.Namespace != null && t.Namespace.StartsWith("ArchiveMaster.AiAgents"))
            .Where(t => typeof(AiAgentBase).IsAssignableFrom(t))
            .ToList();
        var groups = aiAgentTypes.GroupBy(t => t.Namespace.Split('.')[^1]);

        foreach (var group in groups.OrderBy(g => GetAiAgentGroupOrder(g.Key)))
        {
            var aiAgentGroup = new AiAgentGroup(group.Key, GetAiAgentGroupDescription(group.Key));
            foreach (var type in group)
            {
                if (configType2AiAgent.TryGetValue(type, out var agent))
                {
                    aiAgentGroup.Agents.Add(agent);
                }
                else
                {
                    agent = (AiAgentBase)Activator.CreateInstance(type);
                    Config.AiAgents.Add(agent);
                    aiAgentGroup.Agents.Add(agent);
                }
            }

            AiAgentGroups.Add(aiAgentGroup);
        }
    }

    [ObservableProperty]
    private ObservableCollection<AiAgentGroup> aiAgentGroups = new ObservableCollection<AiAgentGroup>();

    [ObservableProperty]
    private AiAgentBase selectedAiAgent;

    [ObservableProperty]
    private AiAgentGroup selectedAiAgentGroup;

    public override bool EnableInitialize => false;

    public override bool EnableRepeatExecute => true;

    protected override Task OnExecutedAsync(CancellationToken ct)
    {
        Result = LlmCallerService.RemoveThink(Result);
        return base.OnExecutedAsync(ct);
    }

    protected override Task OnExecutingAsync(CancellationToken ct)
    {
        Service.AiAgent = SelectedAiAgent;
        Result = "";
        Service.TextGenerated += (sender, e) => Result += e.Value;
        return base.OnExecutingAsync(ct);
    }

    protected override void OnReset()
    {
        Result = "";
    }
}