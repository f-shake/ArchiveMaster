using System.ComponentModel;
using ArchiveMaster.Enums;
using ArchiveMaster.Events;
using ArchiveMaster.Services;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FzLib.Avalonia.Dialogs;
using Microsoft.Extensions.AI;

namespace ArchiveMaster.ViewModels;

public partial class AiConversation : ObservableObject
{
    [ObservableProperty]
    private bool canUserInput;

    [ObservableProperty]
    private string inputText;

    public AiConversation(IDialogService dialogService)
    {
        DialogService = dialogService;
        Reset();
    }

    public event EventHandler MessageAppended;

    public IDialogService DialogService { get; }
    public AiChatMessage LastAssistantMessage => Messages.LastOrDefault(x => x.Sender == AiChatMessageSender.Assistant);
    public AiChatMessage LastSystemMessage => Messages.LastOrDefault(x => x.Sender == AiChatMessageSender.System);
    public AiChatMessage LastUserMessage => Messages.LastOrDefault(x => x.Sender == AiChatMessageSender.User);
    public AvaloniaList<AiChatMessage> Messages { get; } = new AvaloniaList<AiChatMessage>();
    public IAiService Service { get; private set; }

    public AiChatMessage AddAssistantMessage()
    {
        return AddMessage(AiChatMessageSender.Assistant);
    }

    public AiChatMessage AddSystemMessage(string systemPrompt)
    {
        return AddMessage(AiChatMessageSender.System, systemPrompt);
    }

    public AiChatMessage AddUserMessage(string userPrompt)
    {
        return AddMessage(AiChatMessageSender.User, userPrompt);
    }

    public void BindService(IAiService service)
    {
        Service = service;
    }
    public IList<ChatMessage> GetChatMessages()
    {
        return Messages.Select(x => x.ChatMessage).ToList();
    }

    public void OnEndResponse()
    {
        CanUserInput = true;
    }

    public void Reset()
    {
        Messages.Clear();
        CanUserInput = false;
        InputText = "（自动生成）";
    }

    private AiChatMessage AddMessage(AiChatMessageSender sender, string text = "")
    {
        var message = new AiChatMessage(sender, text);
        Messages.Add(message);
        message.MessageAppended += (s, e) => MessageAppended?.Invoke(s, e);
        MessageAppended?.Invoke(this, EventArgs.Empty);
        return message;
    }

    private void OnBeginResponse()
    {
        InputText = "";
        CanUserInput = false;
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task SendAsync(CancellationToken ct)
    {
        var prompt = InputText;
        OnBeginResponse();
        if (LastSystemMessage == null) //第一次
        {
            var (systemPrompt, userPrompt) = await Service.GetFirstPromptAsync(ct);

            AddSystemMessage(systemPrompt).Freeze(true);
            AddUserMessage(userPrompt).Freeze(true);
        }
        else
        {
            AddUserMessage(prompt).Freeze(true);
        }

        string result = "";
        var messages = GetChatMessages();
        var assistantMessage = AddAssistantMessage();
        try
        {
            result = await Service.CallAiWithStreamAsync(messages,assistantMessage, ct);
        }
        catch (Exception ex)
        {
            await DialogService.ShowErrorDialogAsync("AI调用失败", ex);
        }
        
        LastAssistantMessage.ReplaceWithFinalResponse(result);
        OnEndResponse();
    }
}