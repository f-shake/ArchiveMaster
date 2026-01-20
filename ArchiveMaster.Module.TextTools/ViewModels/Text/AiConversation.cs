using System.ComponentModel;
using ArchiveMaster.Enums;
using ArchiveMaster.Events;
using ArchiveMaster.Services;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    public event EventHandler MessageAppended;

    [ObservableProperty]
    private string inputText;


    private TaskCompletionSource sendTaskCompletionSource;

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task SendAsync(CancellationToken ct)
    {
        sendTaskCompletionSource = new TaskCompletionSource();
        OnBeginResponse(ct);
        await sendTaskCompletionSource.Task;
    }

    private AiChatMessage AddMessage(AiChatMessageSender sender, string text = "")
    {
        var message = new AiChatMessage(sender, text);
        Messages.Add(message);
        message.MessageAppended += (s, e) => MessageAppended?.Invoke(s, e);
        MessageAppended?.Invoke(this, EventArgs.Empty);
        return message;
    }

    public AiChatMessage AddSystemMessage(string systemPrompt)
    {
        return AddMessage(AiChatMessageSender.System, systemPrompt);
    }

    public AiChatMessage AddUserMessage(string userPrompt)
    {
        return AddMessage(AiChatMessageSender.User, userPrompt);
    }

    public AiChatMessage AddAssistantMessage()
    {
        return AddMessage(AiChatMessageSender.Assistant);
    }

    public event GenericEventHandler<CancellationToken> SendMessageRequested;

    public IList<ChatMessage> GetChatMessages()
    {
        return Messages.Select(x => x.ChatMessage).ToList();
    }

    private void OnBeginResponse(CancellationToken ct)
    {
        SendMessageRequested?.Invoke(this, new GenericEventArgs<CancellationToken>(ct));
        InputText = "";
        CanUserInput = false;
    }

    public void EndResponse()
    {
        CanUserInput = true;
        sendTaskCompletionSource?.SetResult();
    }

    public AiChatMessage LastSystemMessage => Messages.LastOrDefault(x => x.Sender == AiChatMessageSender.System);

    public AiChatMessage LastUserMessage => Messages.LastOrDefault(x => x.Sender == AiChatMessageSender.User);

    public AiChatMessage LastAssistantMessage => Messages.LastOrDefault(x => x.Sender == AiChatMessageSender.Assistant);

    public void Reset()
    {
        Messages.Clear();
        CanUserInput = false;
        InputText = "（自动生成）";
    }
}