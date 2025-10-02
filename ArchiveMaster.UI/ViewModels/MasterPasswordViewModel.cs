using System;
using System.Linq;
using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.ViewModels;

public partial class MasterPasswordViewModel(IDialogService dialogService, AppConfig appConfig) : ObservableObject
{
    [ObservableProperty]
    private bool hasPasswordVarified;

    [ObservableProperty]
    private string newPassword1;

    [ObservableProperty]
    private string newPassword2;

    [ObservableProperty]
    private string oldPassword;

    [ObservableProperty]
    private string primaryButtonContent = "验证";

    public event EventHandler Close;

    [RelayCommand]
    private void ClearPasswords()
    {
        appConfig.ClearAllPassword();
        PassVerification();
    }

    [RelayCommand]
    private void Initialize()
    {
        //如果没有密码，则直接跳过验证；如果解析失败，则提示错误
        try
        {
            string masterPassword =
                SecurePasswordStoreService.DecryptMasterPassword(GlobalConfigs.Instance.MasterPassword);
            if (masterPassword == GlobalConfigs.DefaultPassword)
            {
                PassVerification();
            }
        }
        catch (Exception ex)
        {
            dialogService.ShowErrorDialogAsync("密码解析失败", "已存储的密码解析失败，请重新设置密码");
            appConfig.ClearAllPassword();
            PassVerification();
        }
    }

    [RelayCommand]
    private void OnPrimaryButtonClick()
    {
        if (!HasPasswordVarified) //验证密码
        {
            if (SecurePasswordStoreService.VerifyMasterPassword(OldPassword, GlobalConfigs.Instance.MasterPassword))
            {
                PassVerification();
            }
            else
            {
                dialogService.ShowErrorDialogAsync("密码错误", "输入的密码与当前密码不一致");
            }
        }
        else //设置密码
        {
            if (NewPassword1 != NewPassword2) //两次输入的密码不一致
            {
                dialogService.ShowErrorDialogAsync("密码不一致", "两次输入的密码不一致");
                return;
            }
            else if (string.IsNullOrWhiteSpace(NewPassword1) //密码为空或强度不够
                     || NewPassword1.Length < 8
                     || !NewPassword1.Any(char.IsUpper)
                     || !NewPassword1.Any(char.IsLower)
                     || !NewPassword1.Any(char.IsNumber))
            {
                dialogService.ShowErrorDialogAsync("密码强度不足", "密码长度至少为8位，且包含大小写字母、数字");
            }
            else
            {
                GlobalConfigs.Instance.MasterPassword = SecurePasswordStoreService.EncryptMasterPassword(NewPassword1);
                appConfig.Save();
                Close?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void PassVerification()
    {
        PrimaryButtonContent = "保存";
        HasPasswordVarified = true;
    }
}