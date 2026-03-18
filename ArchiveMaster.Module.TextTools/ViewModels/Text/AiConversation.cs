using System.ComponentModel;
using ArchiveMaster.Enums;
using ArchiveMaster.Events;
using ArchiveMaster.Services;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.ViewModels;

public partial class AiConversation : ObservableObject
{
    public ViewModelServices Services { get; }

    [ObservableProperty]
    private bool canUserInput;

    [ObservableProperty]
    private string inputText;

    private bool isRegenerating;

    public AiConversation(ViewModelServices services)
    {
        Services = services;
        Reset();
    }

    public event EventHandler MessageAppended;


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

    public void OnEndResponse()
    {
        CanUserInput = true;
    }

    [RelayCommand]
    public void Reset()
    {
        if (SendCommand.IsRunning)
        {
            throw new Exception("正在生成中");
        }

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

    [RelayCommand]
    private async Task RegenerateAsync(AiChatMessage message)
    {
        var index = Messages.IndexOf(message);
        if (index < 0)
        {
            throw new Exception("消息不存在");
        }

        Messages.RemoveRange(index, Messages.Count - index);
        try
        {
            isRegenerating = true;
            await SendCommand.ExecuteAsync(null);
        }
        finally
        {
            isRegenerating = false;
        }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task SendAsync(CancellationToken ct)
    {
        if (SendCommand.IsRunning)
        {
            throw new Exception("正在生成中");
        }

        var prompt = isRegenerating ? LastUserMessage.FullText : InputText;
        bool isFirst = LastSystemMessage == null;
        OnBeginResponse();
        try
        {
            if (isFirst)
            {
                var (systemPrompt, userPrompt) = await Service.GetFirstPromptAsync(ct);

                AddSystemMessage(systemPrompt).Freeze(fold: true);
                AddUserMessage(userPrompt).Freeze(fold: true);
            }
            else
            {
                if (!isRegenerating)
                {
                    AddUserMessage(prompt).Freeze(fold: false);
                }
            }

            var messages = Messages.ToList();
            var assistantMessage = AddAssistantMessage();

            await Service.CallAiWithStreamAsync(messages, assistantMessage, ct);
        }
        catch (Exception ex)
        {
            await Services.Dialog.ShowErrorDialogAsync("AI调用失败", ex);
        }
        finally
        {
            if (LastAssistantMessage?.IsFrozen == false)
            {
                LastAssistantMessage.Freeze(Service.NeedRemoveThink, false);
            }

            OnEndResponse();
        }
    }
}