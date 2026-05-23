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

    public AiAssistantChatMessage LastAssistantMessage => Messages.OfType<AiAssistantChatMessage>().LastOrDefault();
    public AiSystemChatMessage LastSystemMessage => Messages.OfType<AiSystemChatMessage>().LastOrDefault();
    public AiUserChatMessage LastUserMessage => Messages.OfType<AiUserChatMessage>().LastOrDefault();
    public AvaloniaList<AiChatMessage> Messages { get; } = new AvaloniaList<AiChatMessage>();
    public IAiService Service { get; private set; }
    public ViewModelServices Services { get; }

    public AiAssistantChatMessage AddAssistantMessage()
    {
        var message = AiChatMessage.CreateAssistantMessage();
        AddMessage(message);
        return message;
    }

    public AiSystemChatMessage AddSystemMessage(string systemPrompt)
    {
        var message = AiChatMessage.CreateSystemMessage(systemPrompt);
        AddMessage(message);
        return message;
    }

    public AiUserChatMessage AddUserMessage(string userPrompt)
    {
        var message = AiChatMessage.CreateUserMessage(userPrompt);
        AddMessage(message);
        return message;
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


    private void AddMessage(AiChatMessage message)
    {
        Messages.Add(message);
        message.MessageAppended += (s, e) => MessageAppended?.Invoke(s, e);
        MessageAppended?.Invoke(this, EventArgs.Empty);
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

                AddSystemMessage(systemPrompt);
                AddUserMessage(userPrompt);
            }
            else
            {
                if (!isRegenerating)
                {
                    AddUserMessage(prompt);
                }
            }

            var messages = Messages.ToList();
            var assistantMessage = AddAssistantMessage();

            assistantMessage.AiAssistantEndLine += (s, e) =>
            {
                var newLine = Service.PostProcessLine(e.LineText);
                e.ReplaceLine(newLine);
            };

            await Service.CallAiWithStreamAsync(messages, assistantMessage, ct);
        }
        catch (Exception ex)
        {
            await Services.Dialog.ShowErrorDialogAsync("AI调用失败", ex);
        }
        finally
        {
            OnEndResponse();
        }
    }
}