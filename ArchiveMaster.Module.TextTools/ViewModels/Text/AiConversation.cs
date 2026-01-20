using ArchiveMaster.Enums;
using ArchiveMaster.Services;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.AI;

namespace ArchiveMaster.ViewModels;

public partial class AiConversation : ObservableObject
{
    public AiConversation()
    {
        Reset();
    }

    public AvaloniaList<AiChatMessage> Messages { get; } = new AvaloniaList<AiChatMessage>();

    [ObservableProperty]
    private bool canUserInput;

    [ObservableProperty]
    private string inputText;

    [ObservableProperty]
    private bool canUserSend;

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

    public event EventHandler SendMessageRequested;

    public void CallSendMessageRequested()
    {
        SendMessageRequested?.Invoke(this, EventArgs.Empty);
        InputText = "";
        CanUserInput = false;
        CanUserSend = false;
    }

    public void EndResponse()
    {
        CanUserInput = true;
        CanUserSend = true;
    }

    public AiChatMessage LastSystemMessage => Messages.LastOrDefault(x => x.Sender == AiChatMessageSender.System);

    public AiChatMessage LastUserMessage => Messages.LastOrDefault(x => x.Sender == AiChatMessageSender.User);

    public AiChatMessage LastAssistantMessage => Messages.LastOrDefault(x => x.Sender == AiChatMessageSender.Assistant);

    public void Reset()
    {
        Messages.Clear();
        CanUserInput = false;
        CanUserSend = true;
        InputText = "（自动生成）";
    }
}