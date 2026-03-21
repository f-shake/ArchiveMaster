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
    public int MAX_FOLDED_LENGTH = 50;
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

    public AiSystemChatMessage AddSystemMessage(string systemPrompt, bool fold, int maxLength)
    {
        var message = AiChatMessage.CreateSystemMessage(systemPrompt, fold, maxLength);
        AddMessage(message);
        return message;
    }

    public AiUserChatMessage AddUserMessage(string userPrompt, bool fold, int maxLength)
    {
        var message = AiChatMessage.CreateUserMessage(userPrompt, fold, maxLength);
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

                AddSystemMessage(systemPrompt, true, MAX_FOLDED_LENGTH);
                AddUserMessage(userPrompt, true, MAX_FOLDED_LENGTH);
            }
            else
            {
                if (!isRegenerating)
                {
                    AddUserMessage(prompt, false, MAX_FOLDED_LENGTH);
                }
            }

            var messages = Messages.ToList();
            var assistantMessage = AddAssistantMessage();

            // LastAssistantMessage.Append("<think>\n");
            // for (int i = 1; i < 20; i++)
            // {
            //     if (i % 3 == 0)
            //     {
            //         LastAssistantMessage.Append("\n");
            //     }
            //     else
            //     {
            //         LastAssistantMessage.Append("# 哈哈哈哈222");
            //     }
            //
            //     if (i == 12)
            //     {
            //         LastAssistantMessage.Append("</think>\n");
            //     }
            //
            //     await Task.Delay(100);
            // }
            //
            // return;
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
                LastAssistantMessage.FreezeAssistantMessage();
            }

            OnEndResponse();
        }
    }
}