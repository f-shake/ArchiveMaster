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
using ArchiveMaster.Enums;
using ArchiveMaster.ViewModels.FileSystem;
using Avalonia.Collections;
using FzLib.Avalonia.Dialogs;
using Serilog;

namespace ArchiveMaster.ViewModels;

public partial class TextRewriterViewModel(ViewModelServices services)
    : AiTwoStepViewModelBase<TextRewriterService, TextRewriterConfig>(services)
{
    [ObservableProperty]
    private AiConversation aiConversation;

    protected override void OnConfigChanged()
    {
        base.OnConfigChanged();
        InitializeAiAgents();
    }

    private static readonly Dictionary<string, (string Description, int Order)>
        AiAgentGroupMap = new()
        {
            [nameof(AiAgents.ContentTransformation)] = ("内容转换", 3),
            [nameof(AiAgents.ExpressionOptimization)] = ("表达优化", 1),
            [nameof(AiAgents.StructuralAdjustment)] = ("结构调整", 2),
            [nameof(AiAgents.TextCorrection)] = ("文本纠错", 5),
            [nameof(AiAgents.TextEvaluation)] = ("文本评价", 4),
            [nameof(AiAgents.Custom)] = ("自定义", 6),
        };


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

        foreach (var group in groups.OrderBy(g => AiAgentGroupMap[g.Key].Order))
        {
            var aiAgentGroup = new AiAgentGroup(group.Key, AiAgentGroupMap[group.Key].Description);
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


    protected override Task OnExecutingAsync(CancellationToken ct)
    {
        Service.AiAgent = SelectedAiAgent;
        AiConversation = new AiConversation();
        Service.BindConversation(AiConversation);
        return base.OnExecutingAsync(ct);
    }

    protected override void OnReset()
    {
        AiConversation = null;
    }
}