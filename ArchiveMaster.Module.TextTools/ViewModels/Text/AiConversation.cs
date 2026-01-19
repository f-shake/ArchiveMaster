using ArchiveMaster.Enums;
using ArchiveMaster.Services;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.AI;

namespace ArchiveMaster.ViewModels;

public partial class AiConversation : ObservableObject
{
    public AvaloniaList<AiChatMessage> Messages { get; } = new AvaloniaList<AiChatMessage>();

    [ObservableProperty]
    private bool canUserInput = false;


    public AiChatMessage AddSystemMessage(string systemPrompt)
    {
        var message = new AiChatMessage(AiChatMessageSender.System, systemPrompt);
        Messages.Add(message);
        return message;
    }

    public AiChatMessage AddUserMessage(string userPrompt)
    {
        var message = new AiChatMessage(AiChatMessageSender.User, userPrompt);
        Messages.Add(message);
        return message;
    }

    public AiChatMessage AddAssistantMessage()
    {
        var message = new AiChatMessage(AiChatMessageSender.Assistant);
        Messages.Add(message);
        return message;
    }

    public AiChatMessage LastSystemMessage => Messages.LastOrDefault(x => x.Sender == AiChatMessageSender.System);

    public AiChatMessage LastUserMessage => Messages.LastOrDefault(x => x.Sender == AiChatMessageSender.User);

    public AiChatMessage LastAssistantMessage => Messages.LastOrDefault(x => x.Sender == AiChatMessageSender.Assistant);
}